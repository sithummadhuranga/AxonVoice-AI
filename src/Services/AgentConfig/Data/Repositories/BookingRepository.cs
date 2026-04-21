using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Contracts;

namespace AxonVoiceAI.AgentConfig.Data.Repositories;

public sealed class BookingRepository : IBookingRepository
{
    private readonly AgentConfigDbContext _db;

    public BookingRepository(AgentConfigDbContext db)
    {
        _db = db;
    }

    public async Task<PendingBookingCreationResult> CreatePendingBookingAsync(
        CreatePendingBookingRequest request,
        CancellationToken ct)
    {
        var tenantId = request.TenantId != Guid.Empty
            ? request.TenantId
            : await ResolveTenantIdFromAgentAsync(request.AgentId, ct);

        var booking = new PendingBooking
        {
            AgentId = request.AgentId,
            TenantId = tenantId,
            SessionId = request.SessionId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerLanguage = request.DetectedLanguage,
            PartySize = (short)request.PartySize,
            RequestedDatetime = request.RequestedDatetime,
            SpecialRequests = request.SpecialRequests,
            Status = "pending",
            ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(30),
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.PendingBookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        // Confirmation code is the first 8 characters of the booking ID (uppercase) for easy
        // verbal readback. Not a security token — just a human-friendly reference number.
        var confirmationCode = booking.Id.ToString("N")[..8].ToUpperInvariant();

        return new PendingBookingCreationResult(booking.Id, confirmationCode, booking.ExpiresAt);
    }

    private async Task<Guid> ResolveTenantIdFromAgentAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await _db.Agents.FindAsync([agentId], ct)
            ?? throw new InvalidOperationException($"Agent {agentId} not found.");
        return agent.TenantId;
    }
}
