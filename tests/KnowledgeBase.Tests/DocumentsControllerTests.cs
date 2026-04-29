using System.Security.Claims;
using AxonVoiceAI.KnowledgeBase.Controllers;
using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Ingestion;
using AxonVoiceAI.KnowledgeBase.Providers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Security.Cryptography;

namespace AxonVoiceAI.KnowledgeBase.Tests;

public sealed class DocumentsControllerTests
{
    [Fact]
    public async Task UploadDocumentAsync_ValidFile_PersistsDocumentAndReturnsAcceptedResponse()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();

        await using var dbContext = CreateDbContext();
        var ingestionJob = new DocumentIngestionJob(Mock.Of<IServiceScopeFactory>(), NullLogger<DocumentIngestionJob>.Instance);
        var controller = CreateController(dbContext, ingestionJob, tenantId);

        var fileBytes = "Menu items and opening hours"u8.ToArray();
        await using var fileStream = new MemoryStream(fileBytes);
        var file = new FormFile(fileStream, 0, fileBytes.Length, "file", "mathara_bath_kade_kb.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        var result = await controller.UploadDocumentAsync(agentId, file, "si", CancellationToken.None);

        var accepted = result.Should().BeOfType<AcceptedResult>().Subject;
        accepted.Location.Should().StartWith($"/agents/{agentId:D}/documents/");

        var payload = accepted.Value.Should().BeOfType<DocumentSummary>().Subject;
        payload.Filename.Should().Be("mathara_bath_kade_kb.pdf");
        payload.Status.Should().Be("uploading");

        var savedDocument = await dbContext.KnowledgeDocuments.SingleAsync();
        savedDocument.AgentId.Should().Be(agentId);
        savedDocument.TenantId.Should().Be(tenantId);
        savedDocument.Filename.Should().Be("mathara_bath_kade_kb.pdf");
        savedDocument.Language.Should().Be("si");
        accepted.Location.Should().Be($"/agents/{agentId:D}/documents/{savedDocument.Id:D}");
        payload.Id.Should().Be(savedDocument.Id);
    }

    [Fact]
    public async Task UploadDocumentAsync_DuplicateContent_ReturnsConflictWithFriendlyMessage()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var fileBytes = "Duplicate knowledge base content"u8.ToArray();
        var contentHash = Convert.ToHexStringLower(SHA256.HashData(fileBytes));

        await using var dbContext = CreateDbContext();
        dbContext.KnowledgeDocuments.Add(new Data.Entities.KnowledgeDocument
        {
            Id = Guid.NewGuid(),
            AgentId = agentId,
            TenantId = tenantId,
            Filename = "existing.pdf",
            MimeType = "application/pdf",
            ContentHash = contentHash,
            Status = "ready",
            UploadedAt = DateTimeOffset.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        var ingestionJob = new DocumentIngestionJob(Mock.Of<IServiceScopeFactory>(), NullLogger<DocumentIngestionJob>.Instance);
        var controller = CreateController(dbContext, ingestionJob, tenantId);

        await using var fileStream = new MemoryStream(fileBytes);
        var file = new FormFile(fileStream, 0, fileBytes.Length, "file", "duplicate.pdf")
        {
            Headers = new HeaderDictionary(),
            ContentType = "application/pdf",
        };

        var result = await controller.UploadDocumentAsync(agentId, file, null, CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeEquivalentTo(new
        {
            error = "A document with this content already exists.",
            existingId = dbContext.KnowledgeDocuments.Single().Id,
        });
    }

    private static DocumentsController CreateController(
        KnowledgeBaseDbContext dbContext,
        DocumentIngestionJob ingestionJob,
        Guid tenantId)
    {
        var vectorStore = new QdrantVectorStore(QdrantClientFactory.Create("http://127.0.0.1:6334"));
        var controller = new DocumentsController(dbContext, ingestionJob, vectorStore);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(tenantId),
            },
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid tenantId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim("tenant_id", tenantId.ToString()),
        ],
        "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static KnowledgeBaseDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<KnowledgeBaseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new KnowledgeBaseDbContext(options);
    }
}