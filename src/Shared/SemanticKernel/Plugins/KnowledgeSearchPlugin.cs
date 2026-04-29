using System.ComponentModel;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.Shared.SemanticKernel.Plugins;

public sealed class KnowledgeSearchPlugin
{
    private readonly IKnowledgeSearchRepository _knowledgeSearchRepository;

    public KnowledgeSearchPlugin(IKnowledgeSearchRepository knowledgeSearchRepository)
    {
        _knowledgeSearchRepository = knowledgeSearchRepository;
    }

    [KernelFunction("search_knowledge_base")]
    [Description("Search the agent's knowledge base for factual business information such as menu items, prices, opening hours, location, delivery details, and policies")]
    public async Task<KnowledgeSearchResult> SearchKnowledgeBaseAsync(
        [Description("A focused natural-language query describing exactly what the caller wants to know")] string query,
        [Description("The agent ID for this session")] string agentId,
        [Description("The tenant ID for this session")] string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new KnowledgeSearchResult(string.Empty, []);
        }

        if (!Guid.TryParse(agentId, out var parsedAgentId))
        {
            throw new ArgumentException($"Invalid agent ID '{agentId}'", nameof(agentId));
        }

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
        {
            throw new ArgumentException($"Invalid tenant ID '{tenantId}'", nameof(tenantId));
        }

        var matches = await _knowledgeSearchRepository.SearchKnowledgeAsync(
            parsedTenantId,
            parsedAgentId,
            query.Trim(),
            cancellationToken);

        return new KnowledgeSearchResult(query.Trim(), matches);
    }
}

public sealed record KnowledgeSearchResult(
    string Query,
    IReadOnlyList<KnowledgeChunkDto> Matches);