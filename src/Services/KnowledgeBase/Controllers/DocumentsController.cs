using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Data.Entities;
using AxonVoiceAI.KnowledgeBase.Ingestion;
using AxonVoiceAI.KnowledgeBase.Providers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace AxonVoiceAI.KnowledgeBase.Controllers;

[ApiController]
[Route("agents/{agentId:guid}/documents")]
public sealed class DocumentsController : ControllerBase
{
    private readonly KnowledgeBaseDbContext _db;
    private readonly DocumentIngestionJob _ingestionQueue;
    private readonly QdrantVectorStore _vectorStore;

    private static readonly string[] AllowedMimeTypes =
    [
        "application/pdf",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "text/plain",
    ];

    public DocumentsController(
        KnowledgeBaseDbContext db,
        DocumentIngestionJob ingestionQueue,
        QdrantVectorStore vectorStore)
    {
        _db = db;
        _ingestionQueue = ingestionQueue;
        _vectorStore = vectorStore;
    }

    [HttpGet]
    public async Task<IActionResult> ListDocumentsAsync(Guid agentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var documents = await _db.KnowledgeDocuments
            .Where(d => d.AgentId == agentId && d.TenantId == tenantId)
            .OrderByDescending(d => d.UploadedAt)
            .Select(d => new DocumentSummary(d.Id, d.Filename, d.Status, d.ChunkCount, d.UploadedAt))
            .ToListAsync(ct);

        return Ok(documents);
    }

    [HttpGet("{documentId:guid}")]
    public async Task<IActionResult> GetDocumentStatusAsync(Guid agentId, Guid documentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var document = await _db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.AgentId == agentId && d.TenantId == tenantId, ct);

        if (document is null) return NotFound();

        return Ok(new DocumentDetail(
            document.Id, document.Filename, document.Status,
            document.ChunkCount, document.ErrorMessage, document.UploadedAt, document.ProcessedAt));
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadDocumentAsync(
        Guid agentId,
        IFormFile file,
        [FromQuery] string? language,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();

        if (file.Length == 0)
            return BadRequest("File is empty.");

        if (!AllowedMimeTypes.Contains(file.ContentType))
            return BadRequest($"Unsupported file type: {file.ContentType}");

        // Compute content hash to enable deduplication.
        using var hashStream = file.OpenReadStream();
        var hashBytes = await SHA256.HashDataAsync(hashStream, ct);
        var contentHash = Convert.ToHexStringLower(hashBytes);

        var existing = await _db.KnowledgeDocuments.FirstOrDefaultAsync(
            d => d.AgentId == agentId && d.ContentHash == contentHash, ct);

        if (existing is not null)
            return Conflict(new { Message = "A document with this content already exists.", ExistingId = existing.Id });

        var document = new KnowledgeDocument
        {
            AgentId = agentId,
            TenantId = tenantId,
            Filename = file.FileName,
            FileSizeBytes = file.Length,
            MimeType = file.ContentType,
            Language = language,
            ContentHash = contentHash,
            Status = "uploading",
            UploadedAt = DateTimeOffset.UtcNow,
        };

        _db.KnowledgeDocuments.Add(document);
        await _db.SaveChangesAsync(ct);

        // Hand off a copy of the stream to the background ingestion worker.
        var fileStream = new MemoryStream();
        await using (var sourceStream = file.OpenReadStream())
        {
            await sourceStream.CopyToAsync(fileStream, ct);
        }
        fileStream.Position = 0;

        _ingestionQueue.Enqueue(new IngestionRequest(document.Id, fileStream, file.ContentType));

        return AcceptedAtAction(nameof(GetDocumentStatusAsync), new { agentId, documentId = document.Id },
            new DocumentSummary(document.Id, document.Filename, document.Status, null, document.UploadedAt));
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> DeleteDocumentAsync(Guid agentId, Guid documentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var document = await _db.KnowledgeDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId && d.AgentId == agentId && d.TenantId == tenantId, ct);

        if (document is null) return NotFound();

        await _vectorStore.DeleteChunksByDocumentAsync(documentId, ct);
        _db.KnowledgeDocuments.Remove(document);
        await _db.SaveChangesAsync(ct);

        return NoContent();
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record DocumentSummary(Guid Id, string Filename, string Status, int? ChunkCount, DateTimeOffset UploadedAt);
public record DocumentDetail(Guid Id, string Filename, string Status, int? ChunkCount,
    string? ErrorMessage, DateTimeOffset UploadedAt, DateTimeOffset? ProcessedAt);
