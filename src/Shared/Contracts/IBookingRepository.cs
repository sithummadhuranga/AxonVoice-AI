namespace AxonVoiceAI.Shared.Contracts;

public interface IBookingRepository
{
    Task<PendingBookingCreationResult> CreatePendingBookingAsync(
        CreatePendingBookingRequest request,
        CancellationToken ct);
}

public record CreatePendingBookingRequest(
    Guid AgentId,
    Guid TenantId,
    Guid SessionId,
    string CustomerName,
    string CustomerPhone,
    DateTimeOffset RequestedDatetime,
    int PartySize,
    string DetectedLanguage,
    string? SpecialRequests);

public record PendingBookingCreationResult(
    Guid BookingId,
    string ConfirmationCode,
    DateTimeOffset ExpiresAt);
