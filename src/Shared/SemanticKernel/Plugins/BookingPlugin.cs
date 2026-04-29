using System.ComponentModel;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.Shared.SemanticKernel.Plugins;

public sealed class BookingPlugin
{
    private readonly IBookingRepository _bookingRepository;

    public BookingPlugin(IBookingRepository bookingRepository)
    {
        _bookingRepository = bookingRepository;
    }

    [KernelFunction("create_pending_booking")]
    [Description("Create a pending booking hold after collecting and confirming all required details with the customer")]
    public async Task<BookingResult> CreatePendingBookingAsync(
        [Description("Customer full name as spoken")] string customerName,
        [Description("Customer phone number")] string customerPhone,
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Time in HH:MM format (24h)")] string time,
        [Description("Number of guests")] int partySize,
        [Description("The agent ID for this session")] string agentId,
        [Description("The tenant ID for this session")] string tenantId,
        [Description("The session ID")] string sessionId,
        [Description("Detected session language code: si, ta, or en")] string detectedLanguage,
        [Description("Any special requests from the customer")] string? specialRequests = null,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            throw new ArgumentException($"Invalid date format '{date}' — use YYYY-MM-DD");

        if (!TimeOnly.TryParse(time, out var parsedTime))
            throw new ArgumentException($"Invalid time format '{time}' — use HH:MM");

        if (!Guid.TryParse(agentId, out var parsedAgentId))
            throw new ArgumentException($"Invalid agent ID '{agentId}'");

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            throw new ArgumentException($"Invalid tenant ID '{tenantId}'");

        if (!Guid.TryParse(sessionId, out var parsedSessionId))
            throw new ArgumentException($"Invalid session ID '{sessionId}'");

        var requestedDatetime = new DateTimeOffset(
            parsedDate.ToDateTime(parsedTime),
            TimeSpan.Zero);

        var request = new CreatePendingBookingRequest(
            AgentId: parsedAgentId,
            TenantId: parsedTenantId,
            SessionId: parsedSessionId,
            CustomerName: customerName,
            CustomerPhone: customerPhone,
            RequestedDatetime: requestedDatetime,
            PartySize: partySize,
            DetectedLanguage: detectedLanguage,
            SpecialRequests: specialRequests);

        var result = await _bookingRepository.CreatePendingBookingAsync(request, cancellationToken);

        return new BookingResult(result.BookingId, result.ConfirmationCode, result.ExpiresAt);
    }
}
