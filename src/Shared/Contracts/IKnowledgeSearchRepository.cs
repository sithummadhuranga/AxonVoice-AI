using AxonVoiceAI.Shared.DTOs;

namespace AxonVoiceAI.Shared.Contracts;

public interface IKnowledgeSearchRepository
{
    Task<IReadOnlyList<KnowledgeChunkDto>> SearchKnowledgeAsync(
        Guid tenantId,
        Guid agentId,
        string query,
        CancellationToken ct);
}