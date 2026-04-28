using System.Security.Claims;
using AxonVoiceAI.AgentConfig.Controllers;
using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class BookingsControllerTests
{
    [Fact]
    public async Task GetBookingsAsync_KnownAgent_ReturnsPendingAndConfirmedBookings()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var expiredPendingBooking = CreatePendingBooking(agent, status: "pending", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));
        var activePendingBooking = CreatePendingBooking(agent, status: "pending", expiresAt: DateTimeOffset.UtcNow.AddMinutes(20));
        var confirmedBooking = CreateConfirmedBooking(agent, activePendingBooking);

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        db.PendingBookings.AddRange(expiredPendingBooking, activePendingBooking);
        db.ConfirmedBookings.Add(confirmedBooking);
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenant.Id);

        var result = await controller.GetBookingsAsync(agent.Id, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<AgentBookingsResponse>().Subject;

        response.PendingBookings.Should().HaveCount(2);
        response.PendingBookings.Should().Contain(booking => booking.Id == expiredPendingBooking.Id && booking.Status == "expired");
        response.PendingBookings.Should().Contain(booking => booking.Id == activePendingBooking.Id && booking.Status == "pending");
        response.ConfirmedBookings.Should().ContainSingle(booking => booking.Id == confirmedBooking.Id);
    }

    [Fact]
    public async Task ConfirmPendingBookingAsync_ValidPendingBooking_PromotesBooking()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var pendingBooking = CreatePendingBooking(agent, status: "pending", expiresAt: DateTimeOffset.UtcNow.AddMinutes(25));

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        db.PendingBookings.Add(pendingBooking);
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenant.Id);

        var result = await controller.ConfirmPendingBookingAsync(
            agent.Id,
            pendingBooking.Id,
            new ConfirmPendingBookingRequest("Seat near the entrance"),
            CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        var response = ok.Value.Should().BeOfType<ConfirmedBookingConsoleResponse>().Subject;

        response.PromotedFromPendingId.Should().Be(pendingBooking.Id);
        response.InternalNotes.Should().Be("Seat near the entrance");
        response.ConfirmedBy.Should().Be("owner@axonvoice.test");

        var savedPendingBooking = await db.PendingBookings.SingleAsync();
        savedPendingBooking.Status.Should().Be("confirmed");

        var savedConfirmedBooking = await db.ConfirmedBookings.SingleAsync();
        savedConfirmedBooking.PromotedFromPendingId.Should().Be(pendingBooking.Id);
        savedConfirmedBooking.InternalNotes.Should().Be("Seat near the entrance");
    }

    [Fact]
    public async Task ConfirmPendingBookingAsync_ExpiredPendingBooking_ReturnsConflict()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var pendingBooking = CreatePendingBooking(agent, status: "pending", expiresAt: DateTimeOffset.UtcNow.AddMinutes(-1));

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        db.PendingBookings.Add(pendingBooking);
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenant.Id);

        var result = await controller.ConfirmPendingBookingAsync(
            agent.Id,
            pendingBooking.Id,
            new ConfirmPendingBookingRequest(null),
            CancellationToken.None);

        var conflict = result.Should().BeOfType<ConflictObjectResult>().Subject;
        conflict.Value.Should().BeEquivalentTo(new { error = "This pending booking has expired and can no longer be confirmed." });
        db.ConfirmedBookings.Should().BeEmpty();
    }

    [Fact]
    public async Task ExpirePendingBookingAsync_PendingBooking_UpdatesStatus()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var pendingBooking = CreatePendingBooking(agent, status: "pending", expiresAt: DateTimeOffset.UtcNow.AddMinutes(10));

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        db.PendingBookings.Add(pendingBooking);
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenant.Id);

        var result = await controller.ExpirePendingBookingAsync(agent.Id, pendingBooking.Id, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var savedPendingBooking = await db.PendingBookings.SingleAsync();
        savedPendingBooking.Status.Should().Be("expired");
    }

    private static BookingsController CreateController(AgentConfigDbContext db, Guid tenantId)
    {
        var controller = new BookingsController(db);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(tenantId),
            },
        };

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid tenantId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(PlatformTokenClaims.TenantId, tenantId.ToString()),
            new Claim(PlatformTokenClaims.Email, "owner@axonvoice.test"),
        ],
        "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static Tenant CreateTenant() =>
        new()
        {
            Id = Guid.NewGuid(),
            Name = "Axon Bistro",
            ApiKeyEncrypted = "ciphertext",
            DefaultLanguage = "si",
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static Agent CreateAgent(Tenant tenant) =>
        new()
        {
            Id = Guid.NewGuid(),
            TenantId = tenant.Id,
            Tenant = tenant,
            Name = "front-desk",
            DisplayName = "Front Desk",
            PersonaPrompt = "Handle bookings clearly.",
            SupportedLanguages = ["si", "ta", "en"],
            PrimaryLanguage = "si",
            VoiceName = "Aoede",
            GeminiModel = "gemini-2.5-flash-native-audio-preview-12-2025",
            SessionTimeoutSeconds = 600,
            SilenceTimeoutSeconds = 90,
            ToolsEnabled = ["check_availability", "create_pending_booking"],
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static PendingBooking CreatePendingBooking(Agent agent, string status, DateTimeOffset expiresAt) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            TenantId = agent.TenantId,
            SessionId = Guid.NewGuid(),
            CustomerName = "Ada Lovelace",
            CustomerPhone = "+94110000000",
            CustomerLanguage = "en",
            PartySize = 4,
            RequestedDatetime = new DateTimeOffset(2026, 5, 2, 19, 0, 0, TimeSpan.Zero),
            SpecialRequests = "Window seat",
            Status = status,
            ExpiresAt = expiresAt,
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        };

    private static ConfirmedBooking CreateConfirmedBooking(Agent agent, PendingBooking pendingBooking) =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = agent.Id,
            TenantId = agent.TenantId,
            PromotedFromPendingId = pendingBooking.Id,
            CustomerName = pendingBooking.CustomerName,
            CustomerPhone = pendingBooking.CustomerPhone,
            CustomerLanguage = pendingBooking.CustomerLanguage,
            PartySize = pendingBooking.PartySize,
            BookingDatetime = pendingBooking.RequestedDatetime,
            SpecialRequests = pendingBooking.SpecialRequests,
            Status = "confirmed",
            InternalNotes = "Confirmed by operator",
            ConfirmedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
            ConfirmedBy = "owner@axonvoice.test",
            UpdatedAt = DateTimeOffset.UtcNow.AddMinutes(-2),
        };

    private static AgentConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentConfigDbContext(options);
    }
}