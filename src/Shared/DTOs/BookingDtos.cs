namespace AxonVoiceAI.Shared.DTOs;

public record AvailabilityResult(
    bool Available,
    int RemainingCapacity,
    IReadOnlyList<string> ClosestAlternatives);

public record BookingResult(
    Guid BookingId,
    string ConfirmationCode,
    DateTimeOffset ExpiresAt);

public record CreatePendingBookingDto(
    string CustomerName,
    string CustomerPhone,
    string Date,
    string Time,
    int PartySize,
    string? SpecialRequests,
    Guid AgentId,
    Guid TenantId,
    Guid SessionId,
    string DetectedLanguage);
