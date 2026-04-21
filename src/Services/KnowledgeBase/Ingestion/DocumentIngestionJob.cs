using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Providers;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace AxonVoiceAI.KnowledgeBase.Ingestion;

/// <summary>
/// Background service that processes pending document ingestion jobs from the in-memory queue.
/// Uses a serial processing model — one document at a time — to prevent overloading Ollama.
/// </summary>
public sealed class DocumentIngestionJob : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DocumentIngestionJob> _logger;
    private readonly ConcurrentQueue<IngestionRequest> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);

    public DocumentIngestionJob(IServiceScopeFactory scopeFactory, ILogger<DocumentIngestionJob> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Enqueue(IngestionRequest request)
    {
        _queue.Enqueue(request);
        _signal.Release();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(stoppingToken);

            if (!_queue.TryDequeue(out var request))
                continue;

            await ProcessIngestionJobAsync(request, stoppingToken);
        }
    }

    private async Task ProcessIngestionJobAsync(IngestionRequest request, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeBaseDbContext>();
        var embeddingProvider = scope.ServiceProvider.GetRequiredService<IEmbeddingProvider>();
        var vectorStore = scope.ServiceProvider.GetRequiredService<QdrantVectorStore>();
        var pdfExtractor = scope.ServiceProvider.GetRequiredService<PdfExtractor>();
        var docxExtractor = scope.ServiceProvider.GetRequiredService<DocxExtractor>();

        _logger.LogInformation("Starting ingestion for document {DocumentId}", request.DocumentId);

        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(d => d.Id == request.DocumentId, ct);
        if (document is null)
        {
            _logger.LogWarning("Document {DocumentId} not found — skipping ingestion.", request.DocumentId);
            return;
        }

        try
        {
            document.Status = "processing";
            await db.SaveChangesAsync(ct);

            var rawText = request.MimeType switch
            {
                "application/pdf" => pdfExtractor.ExtractText(request.FileStream),
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                    => docxExtractor.ExtractText(request.FileStream),
                "text/plain" => await new StreamReader(request.FileStream).ReadToEndAsync(ct),
                _ => throw new NotSupportedException($"Unsupported MIME type: {request.MimeType}"),
            };

            var chunks = TextChunker.Chunk(rawText);
            var embeddings = await embeddingProvider.EmbedBatchAsync(chunks, ct);

            var points = chunks
                .Select((chunk, i) => new KnowledgeChunkPoint(
                    PointId: Guid.NewGuid(),
                    TenantId: document.TenantId,
                    AgentId: document.AgentId,
                    DocumentId: document.Id,
                    ChunkIndex: i,
                    Text: chunk,
                    Language: document.Language,
                    SourceFilename: document.Filename,
                    Vector: embeddings[i]))
                .ToList();

            await vectorStore.EnsureCollectionExistsAsync(ct);
            await vectorStore.UpsertChunksAsync(points, ct);

            document.Status = "ready";
            document.ChunkCount = chunks.Count;
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Ingestion complete for document {DocumentId}: {ChunkCount} chunks.",
                document.Id, chunks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ingestion failed for document {DocumentId}.", request.DocumentId);
            document.Status = "error";
            document.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            await request.FileStream.DisposeAsync();
        }
    }
}

public record IngestionRequest(Guid DocumentId, Stream FileStream, string MimeType);
