using AxonVoiceAI.KnowledgeBase.Providers;
using AxonVoiceAI.Shared.DTOs;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AxonVoiceAI.KnowledgeBase.Providers;

public sealed class QdrantVectorStore
{
    private readonly QdrantClient _client;
    private const string CollectionName = "knowledge_base";
    private const uint VectorDimension = 768; // nomic-embed-text output dimension

    public QdrantVectorStore(QdrantClient client)
    {
        _client = client;
    }

    public async Task EnsureCollectionExistsAsync(CancellationToken ct)
    {
        var collections = await _client.ListCollectionsAsync(ct);
        if (!collections.Any(c => c == CollectionName))
        {
            await _client.CreateCollectionAsync(
                CollectionName,
                new VectorParams
                {
                    Size = VectorDimension,
                    Distance = Distance.Cosine,
                },
                cancellationToken: ct);
        }
    }

    public async Task UpsertChunksAsync(
        IReadOnlyList<KnowledgeChunkPoint> chunks,
        CancellationToken ct)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        var points = chunks.Select(c => new PointStruct
        {
            Id = new PointId { Uuid = c.PointId.ToString() },
            Vectors = c.Vector,
            Payload =
            {
                ["tenant_id"] = c.TenantId.ToString(),
                ["agent_id"] = c.AgentId.ToString(),
                ["document_id"] = c.DocumentId.ToString(),
                ["chunk_index"] = c.ChunkIndex,
                ["text"] = c.Text,
                ["language"] = c.Language ?? "",
                ["source_filename"] = c.SourceFilename,
            },
        }).ToList();

        await _client.UpsertAsync(CollectionName, points, cancellationToken: ct);
    }

    public async Task<IReadOnlyList<KnowledgeChunkDto>> SearchAsync(
        Guid tenantId,
        Guid agentId,
        float[] queryVector,
        int topK,
        float scoreThreshold,
        CancellationToken ct)
    {
        var filter = new Filter
        {
            Must =
            {
                CreateTextMatchCondition("tenant_id", tenantId.ToString()),
                CreateTextMatchCondition("agent_id", agentId.ToString())
            }
        };

        var results = await _client.SearchAsync(
            CollectionName,
            queryVector,
            filter: filter,
            limit: (ulong)topK,
            scoreThreshold: scoreThreshold,
            cancellationToken: ct);

        return results
            .Select(r => new KnowledgeChunkDto(
                r.Payload["text"].StringValue,
                r.Payload["source_filename"].StringValue,
                (int)r.Payload["chunk_index"].IntegerValue,
                r.Score))
            .ToList()
            .AsReadOnly();
    }

    public async Task DeleteChunksByDocumentAsync(Guid documentId, CancellationToken ct)
    {
        var filter = new Filter
        {
            Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key = "document_id",
                        Match = new Match { Text = documentId.ToString() }
                    }
                }
            }
        };

        await _client.DeleteAsync(CollectionName, filter, cancellationToken: ct);
    }

    private static Condition CreateTextMatchCondition(string key, string value)
    {
        return new Condition
        {
            Field = new FieldCondition
            {
                Key = key,
                Match = new Match { Text = value }
            }
        };
    }
}

public record KnowledgeChunkPoint(
    Guid PointId,
    Guid TenantId,
    Guid AgentId,
    Guid DocumentId,
    int ChunkIndex,
    string Text,
    string? Language,
    string SourceFilename,
    float[] Vector);
