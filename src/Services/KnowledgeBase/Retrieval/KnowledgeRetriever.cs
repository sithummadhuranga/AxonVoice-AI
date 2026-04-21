using AxonVoiceAI.KnowledgeBase.Providers;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.Extensions.Logging;

namespace AxonVoiceAI.KnowledgeBase.Retrieval;

public sealed class KnowledgeRetriever
{
    private readonly IEmbeddingProvider _embeddingProvider;
    private readonly QdrantVectorStore _vectorStore;
    private readonly ILogger<KnowledgeRetriever> _logger;

    // Architecture specifies top-4 results with similarity threshold ≥ 0.5.
    private const int TopK = 4;
    private const float ScoreThreshold = 0.5f;

    public KnowledgeRetriever(
        IEmbeddingProvider embeddingProvider,
        QdrantVectorStore vectorStore,
        ILogger<KnowledgeRetriever> logger)
    {
        _embeddingProvider = embeddingProvider;
        _vectorStore = vectorStore;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KnowledgeChunkDto>> RetrieveRelevantChunksAsync(
        Guid agentId,
        string query,
        CancellationToken ct)
    {
        var queryVector = await _embeddingProvider.EmbedAsync(query, ct);

        var results = await _vectorStore.SearchAsync(agentId, queryVector, TopK, ScoreThreshold, ct);

        _logger.LogDebug(
            "Retrieved {Count} knowledge chunks for agent {AgentId}.",
            results.Count, agentId);

        return results;
    }
}
