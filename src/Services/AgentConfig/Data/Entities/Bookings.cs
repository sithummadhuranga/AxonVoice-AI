namespace AxonVoiceAI.AgentConfig.Data.Entities;

public class PendingBooking
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerLanguage { get; set; } = string.Empty;
    public short PartySize { get; set; }
    public DateTimeOffset RequestedDatetime { get; set; }
    public string? SpecialRequests { get; set; }
    public string Status { get; set; } = "pending";
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public Agent Agent { get; set; } = null!;
}

public class ConfirmedBooking
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PromotedFromPendingId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerLanguage { get; set; } = string.Empty;
    public short PartySize { get; set; }
    public DateTimeOffset BookingDatetime { get; set; }
    public string? SpecialRequests { get; set; }
    public string Status { get; set; } = "confirmed";
    public string? InternalNotes { get; set; }
    public DateTimeOffset ConfirmedAt { get; set; }
    public string? ConfirmedBy { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public Agent Agent { get; set; } = null!;
    public PendingBooking? PromotedFromPending { get; set; }
}
