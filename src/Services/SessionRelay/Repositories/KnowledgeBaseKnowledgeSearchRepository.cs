using System.Net.Http.Json;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;

namespace AxonVoiceAI.SessionRelay.Repositories;

public sealed class KnowledgeBaseKnowledgeSearchRepository : IKnowledgeSearchRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    public KnowledgeBaseKnowledgeSearchRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<KnowledgeChunkDto>> SearchKnowledgeAsync(
        Guid tenantId,
        Guid agentId,
        string query,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("knowledge-base");
        var requestUri = $"/internal/agents/{agentId:D}/knowledge/context?tenantId={tenantId:D}&query={Uri.EscapeDataString(query)}";

        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<IReadOnlyList<KnowledgeChunkDto>>(cancellationToken: ct)
            ?? [];
    }
}