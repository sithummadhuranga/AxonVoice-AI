using AxonVoiceAI.Shared.DTOs;

namespace AxonVoiceAI.KnowledgeBase.Retrieval;

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<KnowledgeChunkDto>> RetrieveRelevantChunksAsync(
        Guid tenantId,
        Guid agentId,
        string query,
        CancellationToken ct);
}