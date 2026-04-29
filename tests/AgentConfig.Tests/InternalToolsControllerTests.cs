using AxonVoiceAI.AgentConfig.Controllers;
using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Contracts;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class InternalToolsControllerTests
{
    [Fact]
    public async Task CheckAvailabilityAsync_KnownAgent_ReturnsRepositoryResult()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var requestedDatetime = new DateTimeOffset(2026, 5, 2, 19, 0, 0, TimeSpan.Zero);
        var expected = new AvailabilityQueryResult(true, 14, []);

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var availabilityRepository = new Mock<IAvailabilityRepository>(MockBehavior.Strict);
        availabilityRepository
            .Setup(repository => repository.QueryAvailabilityAsync(agent.Id, requestedDatetime, 4, CancellationToken.None))
            .ReturnsAsync(expected);

        var bookingRepository = new Mock<IBookingRepository>(MockBehavior.Strict);
        var orderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);

        var controller = new InternalToolsController(db, availabilityRepository.Object, bookingRepository.Object, orderRepository.Object);

        var result = await controller.CheckAvailabilityAsync(agent.Id, requestedDatetime, 4, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
        availabilityRepository.VerifyAll();
    }

    [Fact]
    public async Task CreatePendingBookingAsync_ValidRequest_NormalizesAgentIdAndReturnsRepositoryResult()
    {
        var tenant = CreateTenant();
        var agent = CreateAgent(tenant);
        var request = new CreatePendingBookingRequest(
            Guid.Empty,
            tenant.Id,
            Guid.NewGuid(),
            "Ada Lovelace",
            "+94110000000",
            new DateTimeOffset(2026, 5, 2, 19, 0, 0, TimeSpan.Zero),
            4,
            "en",
            "Window seat");
        var expected = new PendingBookingCreationResult(Guid.NewGuid(), "AB12CD34", DateTimeOffset.UtcNow.AddMinutes(30));

        await using var db = CreateDbContext();
        db.Tenants.Add(tenant);
        db.Agents.Add(agent);
        await db.SaveChangesAsync();

        var availabilityRepository = new Mock<IAvailabilityRepository>(MockBehavior.Strict);
        var bookingRepository = new Mock<IBookingRepository>(MockBehavior.Strict);
        bookingRepository
            .Setup(repository => repository.CreatePendingBookingAsync(
                It.Is<CreatePendingBookingRequest>(value =>
                    value.AgentId == agent.Id
                    && value.TenantId == tenant.Id
                    && value.SessionId == request.SessionId
                    && value.CustomerName == request.CustomerName
                    && value.CustomerPhone == request.CustomerPhone
                    && value.RequestedDatetime == request.RequestedDatetime
                    && value.PartySize == request.PartySize
                    && value.DetectedLanguage == request.DetectedLanguage
                    && value.SpecialRequests == request.SpecialRequests),
                CancellationToken.None))
            .ReturnsAsync(expected);

        var orderRepository = new Mock<IOrderRepository>(MockBehavior.Strict);

        var controller = new InternalToolsController(db, availabilityRepository.Object, bookingRepository.Object, orderRepository.Object);

        var result = await controller.CreatePendingBookingAsync(agent.Id, request, CancellationToken.None);

        var ok = result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(expected);
        bookingRepository.VerifyAll();
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

    private static AgentConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentConfigDbContext(options);
    }
}