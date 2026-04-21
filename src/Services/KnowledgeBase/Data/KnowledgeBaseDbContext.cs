using AxonVoiceAI.KnowledgeBase.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.KnowledgeBase.Data;

public class KnowledgeBaseDbContext : DbContext
{
    public KnowledgeBaseDbContext(DbContextOptions<KnowledgeBaseDbContext> options) : base(options) { }

    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<KnowledgeDocument>(e =>
        {
            e.ToTable("knowledge_documents");
            e.HasKey(d => d.Id);
            e.Property(d => d.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(d => d.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(d => d.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(d => d.Filename).HasColumnName("filename").HasMaxLength(500).IsRequired();
            e.Property(d => d.FileSizeBytes).HasColumnName("file_size_bytes");
            e.Property(d => d.MimeType).HasColumnName("mime_type").HasMaxLength(100);
            e.Property(d => d.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("uploading");
            e.Property(d => d.ErrorMessage).HasColumnName("error_message");
            e.Property(d => d.ChunkCount).HasColumnName("chunk_count");
            e.Property(d => d.Language).HasColumnName("language").HasMaxLength(10);
            e.Property(d => d.ContentHash).HasColumnName("content_hash").HasMaxLength(64);
            e.Property(d => d.UploadedAt).HasColumnName("uploaded_at").HasDefaultValueSql("NOW()");
            e.Property(d => d.ProcessedAt).HasColumnName("processed_at");
            e.HasIndex(d => new { d.AgentId, d.TenantId });
            e.HasIndex(d => d.ContentHash);
        });
    }
}
