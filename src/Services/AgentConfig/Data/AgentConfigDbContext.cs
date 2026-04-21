using AxonVoiceAI.AgentConfig.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Data;

public class AgentConfigDbContext : DbContext
{
    public AgentConfigDbContext(DbContextOptions<AgentConfigDbContext> options) : base(options) { }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<BusinessHours> BusinessHours => Set<BusinessHours>();
    public DbSet<ClosedDate> ClosedDates => Set<ClosedDate>();
    public DbSet<PendingBooking> PendingBookings => Set<PendingBooking>();
    public DbSet<ConfirmedBooking> ConfirmedBookings => Set<ConfirmedBooking>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Tenant>(e =>
        {
            e.ToTable("tenants");
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(t => t.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(t => t.ApiKeyEncrypted).HasColumnName("api_key_encrypted").IsRequired();
            e.Property(t => t.ApiKeyHint).HasColumnName("api_key_hint").HasMaxLength(8);
            e.Property(t => t.DefaultLanguage).HasColumnName("default_language").HasMaxLength(10).HasDefaultValue("si");
            e.Property(t => t.WebhookUrl).HasColumnName("webhook_url");
            e.Property(t => t.WebhookSecret).HasColumnName("webhook_secret");
            e.Property(t => t.RateLimitDaily).HasColumnName("rate_limit_daily").HasDefaultValue(1000);
            e.Property(t => t.RateLimitConcurrent).HasColumnName("rate_limit_concurrent").HasDefaultValue(20);
            e.Property(t => t.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(t => t.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(t => t.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
        });

        modelBuilder.Entity<Agent>(e =>
        {
            e.ToTable("agents");
            e.HasKey(a => a.Id);
            e.Property(a => a.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(a => a.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(a => a.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            e.Property(a => a.DisplayName).HasColumnName("display_name").HasMaxLength(255).IsRequired();
            e.Property(a => a.PersonaPrompt).HasColumnName("persona_prompt").IsRequired();
            e.Property(a => a.SupportedLanguages).HasColumnName("supported_languages").HasColumnType("text[]");
            e.Property(a => a.PrimaryLanguage).HasColumnName("primary_language").HasMaxLength(10).HasDefaultValue("si");
            e.Property(a => a.VoiceName).HasColumnName("voice_name").HasMaxLength(100).HasDefaultValue("Aoede");
            e.Property(a => a.GeminiModel).HasColumnName("gemini_model").HasMaxLength(100).HasDefaultValue("gemini-2.0-flash-live-001");
            e.Property(a => a.SessionTimeoutSeconds).HasColumnName("session_timeout_sec").HasDefaultValue(600);
            e.Property(a => a.SilenceTimeoutSeconds).HasColumnName("silence_timeout_sec").HasDefaultValue(90);
            e.Property(a => a.ToolsEnabled).HasColumnName("tools_enabled").HasColumnType("text[]");
            e.Property(a => a.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.Property(a => a.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.Property(a => a.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            e.HasOne(a => a.Tenant).WithMany(t => t.Agents).HasForeignKey(a => a.TenantId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BusinessHours>(e =>
        {
            e.ToTable("business_hours");
            e.HasKey(b => b.Id);
            e.Property(b => b.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(b => b.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(b => b.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(b => b.DayOfWeek).HasColumnName("day_of_week");
            e.Property(b => b.OpenTime).HasColumnName("open_time");
            e.Property(b => b.CloseTime).HasColumnName("close_time");
            e.Property(b => b.SlotDurationMinutes).HasColumnName("slot_duration_minutes").HasDefaultValue(90);
            e.Property(b => b.MaxCapacityPerSlot).HasColumnName("max_capacity_per_slot").HasDefaultValue(30);
            e.Property(b => b.IsActive).HasColumnName("is_active").HasDefaultValue(true);
            e.HasOne(b => b.Agent).WithMany(a => a.BusinessHours).HasForeignKey(b => b.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(b => new { b.AgentId, b.DayOfWeek }).IsUnique();
        });

        modelBuilder.Entity<ClosedDate>(e =>
        {
            e.ToTable("closed_dates");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(c => c.Date).HasColumnName("closed_date");
            e.Property(c => c.Reason).HasColumnName("reason").HasMaxLength(255);
            e.HasOne(c => c.Agent).WithMany(a => a.ClosedDates).HasForeignKey(c => c.AgentId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(c => new { c.AgentId, c.Date }).IsUnique();
        });

        modelBuilder.Entity<PendingBooking>(e =>
        {
            e.ToTable("pending_bookings");
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(p => p.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(p => p.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(p => p.SessionId).HasColumnName("session_id").IsRequired();
            e.Property(p => p.CustomerName).HasColumnName("customer_name").HasMaxLength(255).IsRequired();
            e.Property(p => p.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(50).IsRequired();
            e.Property(p => p.CustomerLanguage).HasColumnName("customer_language").HasMaxLength(10).IsRequired();
            e.Property(p => p.PartySize).HasColumnName("party_size").IsRequired();
            e.Property(p => p.RequestedDatetime).HasColumnName("requested_datetime").IsRequired();
            e.Property(p => p.SpecialRequests).HasColumnName("special_requests");
            e.Property(p => p.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("pending");
            e.Property(p => p.ExpiresAt).HasColumnName("expires_at").IsRequired();
            e.Property(p => p.CreatedAt).HasColumnName("created_at").HasDefaultValueSql("NOW()");
            e.HasOne(p => p.Agent).WithMany(a => a.PendingBookings).HasForeignKey(p => p.AgentId);
            e.HasIndex(p => new { p.AgentId, p.RequestedDatetime, p.Status, p.ExpiresAt })
             .HasDatabaseName("idx_pending_bookings_slot");
        });

        modelBuilder.Entity<ConfirmedBooking>(e =>
        {
            e.ToTable("confirmed_bookings");
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnName("id").HasDefaultValueSql("gen_random_uuid()");
            e.Property(c => c.AgentId).HasColumnName("agent_id").IsRequired();
            e.Property(c => c.TenantId).HasColumnName("tenant_id").IsRequired();
            e.Property(c => c.PromotedFromPendingId).HasColumnName("promoted_from_pending");
            e.Property(c => c.CustomerName).HasColumnName("customer_name").HasMaxLength(255).IsRequired();
            e.Property(c => c.CustomerPhone).HasColumnName("customer_phone").HasMaxLength(50).IsRequired();
            e.Property(c => c.CustomerLanguage).HasColumnName("customer_language").HasMaxLength(10).IsRequired();
            e.Property(c => c.PartySize).HasColumnName("party_size").IsRequired();
            e.Property(c => c.BookingDatetime).HasColumnName("booking_datetime").IsRequired();
            e.Property(c => c.SpecialRequests).HasColumnName("special_requests");
            e.Property(c => c.Status).HasColumnName("status").HasMaxLength(20).HasDefaultValue("confirmed");
            e.Property(c => c.InternalNotes).HasColumnName("internal_notes");
            e.Property(c => c.ConfirmedAt).HasColumnName("confirmed_at").HasDefaultValueSql("NOW()");
            e.Property(c => c.ConfirmedBy).HasColumnName("confirmed_by").HasMaxLength(255);
            e.Property(c => c.UpdatedAt).HasColumnName("updated_at").HasDefaultValueSql("NOW()");
            e.HasOne(c => c.Agent).WithMany(a => a.ConfirmedBookings).HasForeignKey(c => c.AgentId);
            e.HasOne(c => c.PromotedFromPending).WithMany().HasForeignKey(c => c.PromotedFromPendingId);
            e.HasIndex(c => new { c.AgentId, c.BookingDatetime, c.Status })
             .HasDatabaseName("idx_confirmed_bookings_slot");
        });
    }
}
