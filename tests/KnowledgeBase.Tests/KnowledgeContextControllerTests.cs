using AxonVoiceAI.KnowledgeBase.Controllers;
using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Data.Entities;
using AxonVoiceAI.KnowledgeBase.Retrieval;
using AxonVoiceAI.Shared.DTOs;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AxonVoiceAI.KnowledgeBase.Tests;

public sealed class KnowledgeContextControllerTests
{
    [Fact]
    public async Task GetKnowledgeContextAsync_NoReadyDocuments_ReturnsEmptyArray()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        dbContext.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            Filename = "menu.pdf",
            Status = "extracting",
            UploadedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var retriever = new Mock<IKnowledgeRetriever>(MockBehavior.Strict);
        var controller = new KnowledgeContextController(dbContext, retriever.Object);

        var result = await controller.GetKnowledgeContextAsync(agentId, tenantId, "menu prices", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(Array.Empty<KnowledgeChunkDto>());
        retriever.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetKnowledgeContextAsync_ReadyDocumentsExist_UsesTenantScopedRetriever()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var expectedChunks = new[]
        {
            new KnowledgeChunkDto("Dinner menu", "menu.pdf", 0, 0.93f)
        };

        await using var dbContext = CreateDbContext();
        dbContext.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AgentId = agentId,
            Filename = "menu.pdf",
            Status = "ready",
            UploadedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        dbContext.KnowledgeDocuments.Add(new KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            AgentId = agentId,
            Filename = "other-tenant.pdf",
            Status = "ready",
            UploadedAt = DateTimeOffset.UtcNow,
            ProcessedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var retriever = new Mock<IKnowledgeRetriever>();
        retriever
            .Setup(service => service.RetrieveRelevantChunksAsync(tenantId, agentId, "menu prices", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedChunks)
            .Verifiable();

        var controller = new KnowledgeContextController(dbContext, retriever.Object);

        var result = await controller.GetKnowledgeContextAsync(agentId, tenantId, "menu prices", CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().BeEquivalentTo(expectedChunks);
        retriever.Verify();
    }

    private static KnowledgeBaseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeBaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KnowledgeBaseDbContext(options);
    }
}