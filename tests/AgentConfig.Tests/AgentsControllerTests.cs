using System.Security.Claims;
using AxonVoiceAI.AgentConfig.Controllers;
using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared.Security;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using StackExchange.Redis;

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class AgentsControllerTests
{
    [Fact]
    public async Task CreateAgentAsync_UnsupportedLanguage_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();

        await using var db = CreateDbContext();
        db.Tenants.Add(CreateTenant(tenantId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);

        var result = await controller.CreateAgentAsync(
            new CreateAgentRequest(
                Name: "dining-room",
                DisplayName: "Dining Room Host",
                PersonaPrompt: "Welcome guests and guide them through booking questions.",
                PrimaryLanguage: "en",
                SupportedLanguages: ["en", "fr"],
                VoiceName: "Aoede",
                ToolsEnabled: ["check_availability", "create_pending_booking"],
                SessionTimeoutSeconds: 600,
                SilenceTimeoutSeconds: 90,
                IsActive: true),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Supported languages must be one of: si, ta, en." });
    }

    [Fact]
    public async Task CreateAgentAsync_PrimaryLanguageMissingFromSupportedLanguages_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();

        await using var db = CreateDbContext();
        db.Tenants.Add(CreateTenant(tenantId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);

        var result = await controller.CreateAgentAsync(
            new CreateAgentRequest(
                Name: "dining-room",
                DisplayName: "Dining Room Host",
                PersonaPrompt: "Welcome guests and guide them through booking questions.",
                PrimaryLanguage: "ta",
                SupportedLanguages: ["si", "en"],
                VoiceName: "Aoede",
                ToolsEnabled: ["check_availability", "create_pending_booking"],
                SessionTimeoutSeconds: 600,
                SilenceTimeoutSeconds: 90,
                IsActive: true),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Supported languages must include the primary language." });
    }

    [Fact]
    public async Task CreateAgentAsync_UnsupportedVoice_ReturnsBadRequest()
    {
        var tenantId = Guid.NewGuid();

        await using var db = CreateDbContext();
        db.Tenants.Add(CreateTenant(tenantId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);

        var result = await controller.CreateAgentAsync(
            new CreateAgentRequest(
                Name: "dining-room",
                DisplayName: "Dining Room Host",
                PersonaPrompt: "Welcome guests and guide them through booking questions.",
                PrimaryLanguage: "en",
                SupportedLanguages: ["en", "si"],
                VoiceName: "NotARealVoice",
                ToolsEnabled: ["check_availability", "create_pending_booking"],
                SessionTimeoutSeconds: 600,
                SilenceTimeoutSeconds: 90,
                IsActive: true),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Voice name must be one of the supported Gemini prebuilt voices." });
    }

    [Fact]
    public async Task CreateAgentAsync_ValidRequest_PersistsAgentAndReturnsCreatedResponse()
    {
        var tenantId = Guid.NewGuid();

        await using var db = CreateDbContext();
        db.Tenants.Add(CreateTenant(tenantId));
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);

        var result = await controller.CreateAgentAsync(
            new CreateAgentRequest(
                Name: "mathara-bath-kade",
                DisplayName: "Mathara Bath Kade",
                PersonaPrompt: "Greets callers warmly and captures booking details with short follow-up questions.",
                PrimaryLanguage: "si",
                SupportedLanguages: ["si", "ta", "en"],
                VoiceName: "puck",
                ToolsEnabled: ["check_availability", "create_pending_booking"],
                SessionTimeoutSeconds: 600,
                SilenceTimeoutSeconds: 90,
                IsActive: true),
            CancellationToken.None);

        var created = result.Should().BeOfType<CreatedResult>().Subject;
        created.Location.Should().StartWith("/agents/");

        var payload = created.Value.Should().BeOfType<AgentDetailResponse>().Subject;
        payload.DisplayName.Should().Be("Mathara Bath Kade");
        payload.PrimaryLanguage.Should().Be("si");
        payload.VoiceName.Should().Be("Puck");
        payload.ToolsEnabled.Should().Equal("check_availability", "create_pending_booking");

        var savedAgent = await db.Agents.SingleAsync();
        savedAgent.Name.Should().Be("mathara-bath-kade");
        savedAgent.DisplayName.Should().Be("Mathara Bath Kade");
        savedAgent.VoiceName.Should().Be("Puck");
        savedAgent.GeminiModel.Should().Be("gemini-2.5-flash-native-audio-preview-12-2025");
        created.Location.Should().Be($"/agents/{savedAgent.Id:D}");
        payload.Id.Should().Be(savedAgent.Id);
    }

    [Fact]
    public async Task UpdateAgentAsync_ValidMutation_UpdatesEditableFields()
    {
        var tenantId = Guid.NewGuid();
        var agentId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-15);

        await using var db = CreateDbContext();
        db.Tenants.Add(CreateTenant(tenantId));
        db.Agents.Add(new Agent
        {
            Id = agentId,
            TenantId = tenantId,
            Name = "front-desk",
            DisplayName = "Front Desk",
            PersonaPrompt = "Help customers with reservations.",
            SupportedLanguages = ["si", "ta", "en"],
            PrimaryLanguage = "si",
            VoiceName = "Aoede",
            SessionTimeoutSeconds = 600,
            SilenceTimeoutSeconds = 90,
            ToolsEnabled = ["check_availability", "create_pending_booking"],
            IsActive = true,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);

        var result = await controller.UpdateAgentAsync(
            agentId,
            new UpdateAgentRequest(
                Name: "front-desk-premium",
                DisplayName: "Premium Front Desk",
                PersonaPrompt: "Welcome guests, confirm availability, and capture booking details clearly.",
                PrimaryLanguage: "en",
                SupportedLanguages: ["en", "si"],
                VoiceName: "Charon",
                ToolsEnabled: ["check_availability"],
                SessionTimeoutSeconds: 900,
                SilenceTimeoutSeconds: 120,
                IsActive: false),
            CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var agent = await db.Agents.SingleAsync();
        agent.Name.Should().Be("front-desk-premium");
        agent.DisplayName.Should().Be("Premium Front Desk");
        agent.PersonaPrompt.Should().Be("Welcome guests, confirm availability, and capture booking details clearly.");
        agent.PrimaryLanguage.Should().Be("en");
        agent.SupportedLanguages.Should().Equal("en", "si");
        agent.VoiceName.Should().Be("Charon");
        agent.ToolsEnabled.Should().Equal("check_availability");
        agent.SessionTimeoutSeconds.Should().Be(900);
        agent.SilenceTimeoutSeconds.Should().Be(120);
        agent.IsActive.Should().BeFalse();
        agent.UpdatedAt.Should().BeAfter(createdAt);
    }

    private static AgentsController CreateController(AgentConfigDbContext db, Guid tenantId)
    {
        var controller = new AgentsController(
            db,
            new SessionTokenService("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef", "http://localhost:8080"),
            CreateRedisMock().Object,
            new ApiKeyEncryptionService(Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray())));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = CreatePrincipal(tenantId),
            },
        };

        return controller;
    }

    private static Tenant CreateTenant(Guid tenantId) =>
        new()
        {
            Id = tenantId,
            Name = "Axon Bistro",
            ApiKeyEncrypted = "ciphertext",
            DefaultLanguage = "si",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

    private static ClaimsPrincipal CreatePrincipal(Guid tenantId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(PlatformTokenClaims.TenantId, tenantId.ToString()),
        ],
        "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static Mock<IConnectionMultiplexer> CreateRedisMock()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis.Setup(connection => connection.GetDatabase(It.IsAny<int>(), It.IsAny<object?>())).Returns(database.Object);
        return redis;
    }

    private static AgentConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentConfigDbContext(options);
    }
}