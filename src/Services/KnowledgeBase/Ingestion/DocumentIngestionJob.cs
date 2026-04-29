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
        await RecoverOrphanedDocumentsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(stoppingToken);

            if (!_queue.TryDequeue(out var request))
                continue;

            await ProcessIngestionJobAsync(request, stoppingToken);
        }
    }

    /// <summary>
    /// On startup, any document left in 'uploading' or 'processing' state is an orphan whose
    /// in-memory file stream was lost when the service was last stopped. Mark them as errors so
    /// the user knows to re-upload rather than waiting forever.
    /// </summary>
    private async Task RecoverOrphanedDocumentsAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeBaseDbContext>();

        var orphans = await db.KnowledgeDocuments
            .Where(d => d.Status == "uploading" || d.Status == "processing")
            .ToListAsync(ct);

        if (orphans.Count == 0)
            return;

        foreach (var doc in orphans)
        {
            var previousStatus = doc.Status;
            doc.Status = "error";
            doc.ErrorMessage = "Ingestion was interrupted by a service restart. Please delete this document and upload it again.";
            doc.ProcessedAt = DateTimeOffset.UtcNow;

            _logger.LogWarning(
                "Orphaned document {DocumentId} ({Filename}) found in '{PreviousStatus}' state on startup — marked as error.",
                doc.Id,
                doc.Filename,
                previousStatus);
        }

        await db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Startup recovery complete: {OrphanCount} orphaned document(s) marked as error.",
            orphans.Count);
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
            document.ErrorMessage = null;
            document.ChunkCount = null;
            document.ProcessedAt = null;
            await db.SaveChangesAsync(ct);

            _logger.LogInformation(
                "Extracting text for document {DocumentId} ({Filename}, {MimeType}).",
                document.Id,
                document.Filename,
                request.MimeType);

            string rawText;
            if (request.MimeType == "text/plain")
            {
                rawText = await new StreamReader(request.FileStream).ReadToEndAsync(ct);
            }
            else
            {
                // PDF and DOCX extractors are synchronous and have no cancellation support.
                // Run them on a thread-pool thread so the ingestion loop remains responsive,
                // and enforce a hard 30-second wall-clock limit. Image-only or corrupt files
                // can cause PdfPig / DocumentFormat.OpenXml to spin indefinitely otherwise.
                var extractTask = Task.Run(() => request.MimeType switch
                {
                    "application/pdf" => pdfExtractor.ExtractText(request.FileStream),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document"
                        => docxExtractor.ExtractText(request.FileStream),
                    _ => throw new NotSupportedException($"Unsupported MIME type: {request.MimeType}"),
                }, CancellationToken.None);

                if (await Task.WhenAny(extractTask, Task.Delay(TimeSpan.FromSeconds(30), ct)) != extractTask)
                {
                    if (ct.IsCancellationRequested)
                        throw new OperationCanceledException(ct);

                    throw new TimeoutException(
                        "Text extraction timed out after 30 seconds. The document may be image-only, corrupt, or too large to process. Upload a text-based PDF, DOCX, or plain text file.");
                }

                rawText = await extractTask;
            }

            var chunks = TextChunker.Chunk(rawText);

            _logger.LogInformation(
                "Chunked document {DocumentId} into {ChunkCount} chunks from {CharacterCount} extracted characters.",
                document.Id,
                chunks.Count,
                rawText.Length);

            if (chunks.Count == 0)
            {
                document.Status = "error";
                document.ErrorMessage = "No extractable text was found in the document. Upload a text-based PDF, DOCX, or plain text file.";
                document.ProcessedAt = DateTimeOffset.UtcNow;
                await db.SaveChangesAsync(ct);

                _logger.LogWarning(
                    "Document {DocumentId} produced no extractable text and was marked as error.",
                    document.Id);

                return;
            }

            _logger.LogInformation(
                "Requesting embeddings for {ChunkCount} chunks for document {DocumentId}.",
                chunks.Count,
                document.Id);

            var embeddings = await embeddingProvider.EmbedBatchAsync(chunks, ct);

            _logger.LogInformation(
                "Received {EmbeddingCount} embeddings for document {DocumentId}.",
                embeddings.Count,
                document.Id);

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

            _logger.LogInformation(
                "Upserting {PointCount} vectors for document {DocumentId} into Qdrant.",
                points.Count,
                document.Id);

            await vectorStore.EnsureCollectionExistsAsync(ct);
            await vectorStore.UpsertChunksAsync(points, ct);

            document.Status = "ready";
            document.ErrorMessage = null;
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
            document.ProcessedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
        }
        finally
        {
            await request.FileStream.DisposeAsync();
        }
    }
}

public record IngestionRequest(Guid DocumentId, Stream FileStream, string MimeType);
