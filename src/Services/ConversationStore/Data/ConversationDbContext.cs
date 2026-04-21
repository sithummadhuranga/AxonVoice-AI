using AxonVoiceAI.ConversationStore.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.ConversationStore.Data;

public class ConversationDbContext : DbContext
{
    public ConversationDbContext(DbContextOptions<ConversationDbContext> options) : base(options) { }

    public DbSet<ConversationSession> Sessions => Set<ConversationSession>();
    public DbSet<SessionFunctionCall> FunctionCalls => Set<SessionFunctionCall>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ConversationSession>(e =>
        {
            e.ToTable("conversation_sessions");
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(s => s.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(s => s.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(s => s.CallerIdentifier).HasColumnName("caller_identifier").HasMaxLength(200).IsRequired();
            e.Property(s => s.Language).HasColumnName("language").HasMaxLength(10).HasDefaultValue("en");
            e.Property(s => s.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("active");
            e.Property(s => s.Summary).HasColumnName("summary");
            e.Property(s => s.StartedAt).HasColumnName("started_at").HasDefaultValueSql("NOW()");
            e.Property(s => s.EndedAt).HasColumnName("ended_at");
            e.Property(s => s.DurationSeconds).HasColumnName("duration_seconds");
            e.HasIndex(s => new { s.TenantId, s.AgentId });
            e.HasIndex(s => s.StartedAt);
        });

        modelBuilder.Entity<SessionFunctionCall>(e =>
        {
            e.ToTable("session_function_calls");
            e.HasKey(f => f.Id);
            e.Property(f => f.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(f => f.SessionId).HasColumnName("session_id").IsRequired();
            e.Property(f => f.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(f => f.FunctionName).HasColumnName("function_name").HasMaxLength(100).IsRequired();
            e.Property(f => f.ArgumentsJson).HasColumnName("arguments_json");
            e.Property(f => f.ResultJson).HasColumnName("result_json");
            e.Property(f => f.Succeeded).HasColumnName("succeeded");
            e.Property(f => f.ErrorMessage).HasColumnName("error_message");
            e.Property(f => f.CalledAt).HasColumnName("called_at").HasDefaultValueSql("NOW()");
            e.Property(f => f.DurationMs).HasColumnName("duration_ms");
            e.HasOne(f => f.Session)
                .WithMany(s => s.FunctionCalls)
                .HasForeignKey(f => f.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(f => f.SessionId);
        });

        modelBuilder.Entity<WebhookDelivery>(e =>
        {
            e.ToTable("webhook_deliveries");
            e.HasKey(w => w.Id);
            e.Property(w => w.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(w => w.SessionId).HasColumnName("session_id").IsRequired();
            e.Property(w => w.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(w => w.WebhookUrl).HasColumnName("webhook_url").HasMaxLength(2000).IsRequired();
            e.Property(w => w.EventType).HasColumnName("event_type").HasMaxLength(100).IsRequired();
            e.Property(w => w.PayloadJson).HasColumnName("payload_json").IsRequired();
            e.Property(w => w.AttemptCount).HasColumnName("attempt_count").HasDefaultValue(0);
            e.Property(w => w.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            e.Property(w => w.LastHttpStatus).HasColumnName("last_http_status");
            e.Property(w => w.LastErrorMessage).HasColumnName("last_error_message");
            e.Property(w => w.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(w => w.DeliveredAt).HasColumnName("delivered_at");
            e.Property(w => w.NextRetryAt).HasColumnName("next_retry_at");
            e.HasOne(w => w.Session)
                .WithMany(s => s.WebhookDeliveries)
                .HasForeignKey(w => w.SessionId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(w => new { w.Status, w.NextRetryAt });
        });
    }
}
