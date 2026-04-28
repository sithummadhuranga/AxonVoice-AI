using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("agents/{agentId:guid}/bookings")]
[Authorize(Policy = PlatformAuthorizationPolicyNames.ConsoleAccess)]
public sealed class BookingsController : ControllerBase
{
    private readonly AgentConfigDbContext _db;

    public BookingsController(AgentConfigDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetBookingsAsync(Guid agentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agentExists = await _db.Agents
            .AnyAsync(agent => agent.Id == agentId && agent.TenantId == tenantId, ct);

        if (!agentExists)
            return NotFound();

        var now = DateTimeOffset.UtcNow;

        var pendingBookings = await _db.PendingBookings
            .Where(booking => booking.AgentId == agentId && booking.TenantId == tenantId && booking.Status != "confirmed")
            .OrderBy(booking => booking.RequestedDatetime)
            .ThenBy(booking => booking.CreatedAt)
            .Select(booking => new PendingBookingConsoleResponse(
                booking.Id,
                booking.CustomerName,
                booking.CustomerPhone,
                booking.CustomerLanguage,
                booking.PartySize,
                booking.RequestedDatetime,
                booking.SpecialRequests,
                booking.Status == "pending" && booking.ExpiresAt <= now ? "expired" : booking.Status,
                booking.ExpiresAt,
                booking.CreatedAt,
                BuildConfirmationCode(booking.Id)))
            .ToListAsync(ct);

        var confirmedBookings = await _db.ConfirmedBookings
            .Where(booking => booking.AgentId == agentId && booking.TenantId == tenantId)
            .OrderByDescending(booking => booking.BookingDatetime)
            .ThenByDescending(booking => booking.ConfirmedAt)
            .Select(booking => new ConfirmedBookingConsoleResponse(
                booking.Id,
                booking.PromotedFromPendingId,
                booking.CustomerName,
                booking.CustomerPhone,
                booking.CustomerLanguage,
                booking.PartySize,
                booking.BookingDatetime,
                booking.SpecialRequests,
                booking.Status,
                booking.InternalNotes,
                booking.ConfirmedAt,
                booking.ConfirmedBy,
                booking.UpdatedAt,
                BuildConfirmationCode(booking.PromotedFromPendingId ?? booking.Id)))
            .ToListAsync(ct);

        return Ok(new AgentBookingsResponse(pendingBookings, confirmedBookings));
    }

    [HttpPost("pending/{bookingId:guid}/confirm")]
    public async Task<IActionResult> ConfirmPendingBookingAsync(
        Guid agentId,
        Guid bookingId,
        ConfirmPendingBookingRequest request,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var pendingBooking = await _db.PendingBookings
            .Where(booking => booking.Id == bookingId && booking.AgentId == agentId && booking.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (pendingBooking is null)
            return NotFound();

        if (!string.Equals(pendingBooking.Status, "pending", StringComparison.Ordinal))
            return Conflict(new { error = "Only pending bookings can be confirmed." });

        if (pendingBooking.ExpiresAt <= DateTimeOffset.UtcNow)
            return Conflict(new { error = "This pending booking has expired and can no longer be confirmed." });

        var confirmedBooking = new ConfirmedBooking
        {
            AgentId = pendingBooking.AgentId,
            TenantId = pendingBooking.TenantId,
            PromotedFromPendingId = pendingBooking.Id,
            CustomerName = pendingBooking.CustomerName,
            CustomerPhone = pendingBooking.CustomerPhone,
            CustomerLanguage = pendingBooking.CustomerLanguage,
            PartySize = pendingBooking.PartySize,
            BookingDatetime = pendingBooking.RequestedDatetime,
            SpecialRequests = pendingBooking.SpecialRequests,
            Status = "confirmed",
            InternalNotes = NormalizeOptionalText(request.InternalNotes),
            ConfirmedAt = DateTimeOffset.UtcNow,
            ConfirmedBy = ResolveOperatorIdentifier(),
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        pendingBooking.Status = "confirmed";

        _db.ConfirmedBookings.Add(confirmedBooking);
        await _db.SaveChangesAsync(ct);

        return Ok(new ConfirmedBookingConsoleResponse(
            confirmedBooking.Id,
            confirmedBooking.PromotedFromPendingId,
            confirmedBooking.CustomerName,
            confirmedBooking.CustomerPhone,
            confirmedBooking.CustomerLanguage,
            confirmedBooking.PartySize,
            confirmedBooking.BookingDatetime,
            confirmedBooking.SpecialRequests,
            confirmedBooking.Status,
            confirmedBooking.InternalNotes,
            confirmedBooking.ConfirmedAt,
            confirmedBooking.ConfirmedBy,
            confirmedBooking.UpdatedAt,
            BuildConfirmationCode(pendingBooking.Id)));
    }

    [HttpPost("pending/{bookingId:guid}/expire")]
    public async Task<IActionResult> ExpirePendingBookingAsync(Guid agentId, Guid bookingId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var pendingBooking = await _db.PendingBookings
            .Where(booking => booking.Id == bookingId && booking.AgentId == agentId && booking.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (pendingBooking is null)
            return NotFound();

        if (string.Equals(pendingBooking.Status, "expired", StringComparison.Ordinal))
            return NoContent();

        if (!string.Equals(pendingBooking.Status, "pending", StringComparison.Ordinal))
            return Conflict(new { error = "Only pending bookings can be expired." });

        pendingBooking.Status = "expired";
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst(PlatformTokenClaims.TenantId)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private string ResolveOperatorIdentifier()
    {
        return User.FindFirst(PlatformTokenClaims.Email)?.Value
            ?? User.FindFirst(PlatformTokenClaims.UserId)?.Value
            ?? "operator";
    }

    private static string? NormalizeOptionalText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string BuildConfirmationCode(Guid bookingId)
    {
        return bookingId.ToString("N")[..8].ToUpperInvariant();
    }
}

public record AgentBookingsResponse(
    IReadOnlyList<PendingBookingConsoleResponse> PendingBookings,
    IReadOnlyList<ConfirmedBookingConsoleResponse> ConfirmedBookings);

public record PendingBookingConsoleResponse(
    Guid Id,
    string CustomerName,
    string CustomerPhone,
    string CustomerLanguage,
    short PartySize,
    DateTimeOffset RequestedDatetime,
    string? SpecialRequests,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    string ConfirmationCode);

public record ConfirmedBookingConsoleResponse(
    Guid Id,
    Guid? PromotedFromPendingId,
    string CustomerName,
    string CustomerPhone,
    string CustomerLanguage,
    short PartySize,
    DateTimeOffset BookingDatetime,
    string? SpecialRequests,
    string Status,
    string? InternalNotes,
    DateTimeOffset ConfirmedAt,
    string? ConfirmedBy,
    DateTimeOffset UpdatedAt,
    string ConfirmationCode);

public record ConfirmPendingBookingRequest(string? InternalNotes);