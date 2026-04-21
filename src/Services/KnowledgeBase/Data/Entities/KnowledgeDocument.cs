namespace AxonVoiceAI.KnowledgeBase.Data.Entities;

public class KnowledgeDocument
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public string Filename { get; set; } = string.Empty;
    public long? FileSizeBytes { get; set; }
    public string? MimeType { get; set; }
    public string Status { get; set; } = "uploading";
    public string? ErrorMessage { get; set; }
    public int? ChunkCount { get; set; }
    public string? Language { get; set; }
    public string? ContentHash { get; set; }
    public DateTimeOffset UploadedAt { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
}
