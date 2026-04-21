namespace AxonVoiceAI.AgentConfig.Data.Entities;

public class Agent
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PersonaPrompt { get; set; } = string.Empty;
    public string[] SupportedLanguages { get; set; } = ["si", "ta", "en"];
    public string PrimaryLanguage { get; set; } = "si";
    public string VoiceName { get; set; } = "Aoede";
    public string GeminiModel { get; set; } = "gemini-2.0-flash-live-001";
    public int SessionTimeoutSeconds { get; set; } = 600;
    public int SilenceTimeoutSeconds { get; set; } = 90;
    public string[] ToolsEnabled { get; set; } = ["check_availability", "create_pending_booking"];
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Tenant Tenant { get; set; } = null!;
    public ICollection<BusinessHours> BusinessHours { get; set; } = [];
    public ICollection<ClosedDate> ClosedDates { get; set; } = [];
    public ICollection<PendingBooking> PendingBookings { get; set; } = [];
    public ICollection<ConfirmedBooking> ConfirmedBookings { get; set; } = [];
}
