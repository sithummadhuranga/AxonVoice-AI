using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.Shared.SemanticKernel.Plugins;
using FluentAssertions;
using Moq;

namespace AxonVoiceAI.Shared.Tests;

public sealed class KnowledgeSearchPluginTests
{
    [Fact]
    public async Task SearchKnowledgeBaseAsync_ValidContext_ReturnsRepositoryMatches()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var expectedMatches = new[]
        {
            new KnowledgeChunkDto("Chicken kottu costs LKR 1800.", "menu.pdf", 4, 0.94f)
        };
        var repository = new Mock<IKnowledgeSearchRepository>();
        repository
            .Setup(repo => repo.SearchKnowledgeAsync(tenantId, agentId, "chicken kottu price", It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedMatches);
        var plugin = new KnowledgeSearchPlugin(repository.Object);

        var result = await plugin.SearchKnowledgeBaseAsync(
            "  chicken kottu price  ",
            agentId.ToString(),
            tenantId.ToString(),
            CancellationToken.None);

        result.Query.Should().Be("chicken kottu price");
        result.Matches.Should().BeEquivalentTo(expectedMatches);
        repository.Verify(
            repo => repo.SearchKnowledgeAsync(tenantId, agentId, "chicken kottu price", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SearchKnowledgeBaseAsync_BlankQuery_ReturnsEmptyWithoutRepositoryCall()
    {
        var repository = new Mock<IKnowledgeSearchRepository>();
        var plugin = new KnowledgeSearchPlugin(repository.Object);

        var result = await plugin.SearchKnowledgeBaseAsync(
            "   ",
            Guid.NewGuid().ToString(),
            Guid.NewGuid().ToString(),
            CancellationToken.None);

        result.Query.Should().BeEmpty();
        result.Matches.Should().BeEmpty();
        repository.VerifyNoOtherCalls();
    }
}