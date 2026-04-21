using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("agents")]
public sealed class AgentsController : ControllerBase
{
    private readonly AgentConfigDbContext _db;
    private readonly SessionTokenService _sessionTokens;
    private readonly IConnectionMultiplexer _redis;
    private readonly ApiKeyEncryptionService _encryption;

    public AgentsController(
        AgentConfigDbContext db,
        SessionTokenService sessionTokens,
        IConnectionMultiplexer redis,
        ApiKeyEncryptionService encryption)
    {
        _db = db;
        _sessionTokens = sessionTokens;
        _redis = redis;
        _encryption = encryption;
    }

    [HttpGet]
    public async Task<IActionResult> ListAgentsAsync(CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agents = await _db.Agents
            .Where(a => a.TenantId == tenantId)
            .Select(a => new AgentSummaryResponse(a.Id, a.Name, a.DisplayName, a.PrimaryLanguage, a.IsActive))
            .ToListAsync(ct);

        return Ok(agents);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAgentAsync(Guid id, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agent = await _db.Agents
            .Where(a => a.Id == id && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        return Ok(MapToResponse(agent));
    }

    [HttpPost]
    public async Task<IActionResult> CreateAgentAsync(CreateAgentRequest request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var tenantExists = await _db.Tenants.AnyAsync(t => t.Id == tenantId, ct);
        if (!tenantExists) return Forbid();

        var agent = new Agent
        {
            TenantId = tenantId,
            Name = request.Name,
            DisplayName = request.DisplayName,
            PersonaPrompt = request.PersonaPrompt,
            PrimaryLanguage = request.PrimaryLanguage ?? "si",
            SupportedLanguages = request.SupportedLanguages ?? ["si", "ta", "en"],
            VoiceName = request.VoiceName ?? "Aoede",
            ToolsEnabled = request.ToolsEnabled ?? ["check_availability", "create_pending_booking"],
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAgentAsync), new { id = agent.Id }, MapToResponse(agent));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAgentAsync(Guid id, UpdateAgentRequest request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agent = await _db.Agents
            .Where(a => a.Id == id && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        if (request.DisplayName is not null) agent.DisplayName = request.DisplayName;
        if (request.PersonaPrompt is not null) agent.PersonaPrompt = request.PersonaPrompt;
        if (request.PrimaryLanguage is not null) agent.PrimaryLanguage = request.PrimaryLanguage;
        if (request.SupportedLanguages is not null) agent.SupportedLanguages = request.SupportedLanguages;
        if (request.VoiceName is not null) agent.VoiceName = request.VoiceName;
        if (request.ToolsEnabled is not null) agent.ToolsEnabled = request.ToolsEnabled;
        if (request.SessionTimeoutSeconds.HasValue) agent.SessionTimeoutSeconds = request.SessionTimeoutSeconds.Value;
        agent.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return Ok(MapToResponse(agent));
    }

    [HttpPost("{id:guid}/session-token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueSessionTokenAsync(
        Guid id,
        SessionTokenRequest request,
        CancellationToken ct)
    {
        if (request.AgentId != Guid.Empty && request.AgentId != id)
            return BadRequest(new { error = "Route agent id must match the request payload." });

        if (string.IsNullOrWhiteSpace(request.Channel))
            return BadRequest(new { error = "Channel is required." });

        var agent = await _db.Agents
            .Include(a => a.Tenant)
            .Where(a => a.Id == id && a.IsActive && a.Tenant.IsActive)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        var tenantId = agent.TenantId;
        var cache = _redis.GetDatabase();
        var today = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd");
        var dailyKey = $"rate:{tenantId}:daily:{today}";
        var concurrentKey = $"rate:{tenantId}:concurrent";

        var dailyCount = (long?)await cache.StringGetAsync(dailyKey) ?? 0;
        var concurrentCount = (long?)await cache.StringGetAsync(concurrentKey) ?? 0;

        if (dailyCount >= agent.Tenant.RateLimitDaily)
            return StatusCode(429, new { error = "Daily session limit reached.", retryAfter = "tomorrow" });

        if (concurrentCount >= agent.Tenant.RateLimitConcurrent)
            return StatusCode(429, new { error = "Concurrent session limit reached. Try again shortly." });

        var sessionId = Guid.NewGuid();
        var tokenResponse = _sessionTokens.IssueSessionToken(tenantId, agent.Id, sessionId);

        await cache.StringIncrementAsync(dailyKey);
        await cache.KeyExpireAsync(dailyKey, TimeSpan.FromDays(1));

        return Ok(tokenResponse);
    }

    /// <summary>
    /// Internal endpoint for service-to-service calls (e.g., SessionRelay).
    /// Returns the full agent config including the decrypted Gemini API key.
    /// This route is NOT exposed through the public gateway.
    /// </summary>
    [HttpGet("{id:guid}/config")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAgentConfigAsync(Guid id, [FromQuery] Guid tenantId, CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        var agent = await _db.Agents
            .Include(a => a.Tenant)
            .Where(a => a.Id == id && a.TenantId == tenantId && a.IsActive && a.Tenant.IsActive)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        var decryptedKey = _encryption.Decrypt(agent.Tenant.ApiKeyEncrypted);
        var bookingEnabled = agent.ToolsEnabled.Contains("check_availability")
            || agent.ToolsEnabled.Contains("create_pending_booking");

        var config = new AgentConfigDto(
            AgentId: agent.Id,
            TenantId: agent.TenantId,
            AgentName: agent.DisplayName,
            BusinessName: agent.Tenant.Name,
            Persona: agent.PersonaPrompt,
            Language: agent.PrimaryLanguage,
            SupportedLanguages: agent.SupportedLanguages,
            VoiceName: agent.VoiceName,
            GeminiModel: agent.GeminiModel,
            GeminiApiKey: decryptedKey,
            BookingEnabled: bookingEnabled,
            SessionTimeoutSeconds: agent.SessionTimeoutSeconds,
            SilenceTimeoutSeconds: agent.SilenceTimeoutSeconds);

        return Ok(config);
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static AgentDetailResponse MapToResponse(Agent a) =>
        new(a.Id, a.TenantId, a.Name, a.DisplayName, a.PersonaPrompt,
            a.SupportedLanguages, a.PrimaryLanguage, a.VoiceName, a.GeminiModel,
            a.SessionTimeoutSeconds, a.SilenceTimeoutSeconds, a.ToolsEnabled, a.IsActive);
}

public record AgentSummaryResponse(Guid Id, string Name, string DisplayName, string PrimaryLanguage, bool IsActive);
public record AgentDetailResponse(Guid Id, Guid TenantId, string Name, string DisplayName, string PersonaPrompt,
    string[] SupportedLanguages, string PrimaryLanguage, string VoiceName, string GeminiModel,
    int SessionTimeoutSeconds, int SilenceTimeoutSeconds, string[] ToolsEnabled, bool IsActive);
public record CreateAgentRequest(string Name, string DisplayName, string PersonaPrompt,
    string? PrimaryLanguage, string[]? SupportedLanguages, string? VoiceName, string[]? ToolsEnabled);
public record UpdateAgentRequest(string? DisplayName, string? PersonaPrompt, string? PrimaryLanguage,
    string[]? SupportedLanguages, string? VoiceName, string[]? ToolsEnabled, int? SessionTimeoutSeconds);
