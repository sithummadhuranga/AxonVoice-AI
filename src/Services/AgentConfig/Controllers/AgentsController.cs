using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using StackExchange.Redis;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("agents")]
[Authorize(Policy = PlatformAuthorizationPolicyNames.ConsoleAccess)]
public sealed class AgentsController : ControllerBase
{
    private static readonly string[] DefaultSupportedLanguages = ["si", "ta", "en"];
    private static readonly string[] DefaultTools = [];
    private static readonly HashSet<string> SupportedLanguageCodes = ["si", "ta", "en"];
    private static readonly HashSet<string> SupportedToolNames = ["check_availability", "create_pending_booking", "place_order"];

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

        var validationError = ValidateCreateRequest(request);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        var primaryLanguage = NormalizePrimaryLanguage(request.PrimaryLanguage) ?? "si";
        var supportedLanguages = NormalizeSupportedLanguages(request.SupportedLanguages, primaryLanguage);
        var toolsEnabled = NormalizeTools(request.ToolsEnabled) ?? DefaultTools;

        var agent = new Agent
        {
            TenantId = tenantId,
            Name = request.Name.Trim(),
            DisplayName = request.DisplayName.Trim(),
            PersonaPrompt = request.PersonaPrompt.Trim(),
            PrimaryLanguage = primaryLanguage,
            SupportedLanguages = supportedLanguages,
            VoiceName = NormalizeVoiceName(request.VoiceName) ?? GeminiVoiceCatalog.DefaultVoiceName,
            SessionTimeoutSeconds = request.SessionTimeoutSeconds ?? 600,
            SilenceTimeoutSeconds = request.SilenceTimeoutSeconds ?? 90,
            ToolsEnabled = toolsEnabled,
            IsActive = request.IsActive ?? true,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Agents.Add(agent);
        await _db.SaveChangesAsync(ct);

        return Created($"/agents/{agent.Id:D}", MapToResponse(agent));
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAgentAsync(Guid id, UpdateAgentRequest request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agent = await _db.Agents
            .Where(a => a.Id == id && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        var validationError = ValidateUpdateRequest(request, agent);
        if (validationError is not null)
        {
            return BadRequest(new { error = validationError });
        }

        if (request.Name is not null) agent.Name = request.Name.Trim();
        if (request.DisplayName is not null) agent.DisplayName = request.DisplayName.Trim();
        if (request.PersonaPrompt is not null) agent.PersonaPrompt = request.PersonaPrompt.Trim();

        var primaryLanguage = NormalizePrimaryLanguage(request.PrimaryLanguage) ?? agent.PrimaryLanguage;
        agent.PrimaryLanguage = primaryLanguage;

        if (request.SupportedLanguages is not null || request.PrimaryLanguage is not null)
        {
            agent.SupportedLanguages = NormalizeSupportedLanguages(request.SupportedLanguages ?? agent.SupportedLanguages, primaryLanguage);
        }

        if (request.VoiceName is not null) agent.VoiceName = NormalizeVoiceName(request.VoiceName)!;
        if (request.ToolsEnabled is not null) agent.ToolsEnabled = NormalizeTools(request.ToolsEnabled) ?? [];
        if (request.SessionTimeoutSeconds.HasValue) agent.SessionTimeoutSeconds = request.SessionTimeoutSeconds.Value;
        if (request.SilenceTimeoutSeconds.HasValue) agent.SilenceTimeoutSeconds = request.SilenceTimeoutSeconds.Value;
        if (request.IsActive.HasValue) agent.IsActive = request.IsActive.Value;
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
        var scheduleConfigured = await _db.BusinessHours
            .AnyAsync(bh => bh.AgentId == agent.Id && bh.TenantId == tenantId && bh.IsActive, ct);
        var bookingEnabled = scheduleConfigured && (agent.ToolsEnabled.Contains("check_availability")
            || agent.ToolsEnabled.Contains("create_pending_booking"));
        var orderingEnabled = agent.ToolsEnabled.Contains("place_order");

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
            OrderingEnabled: orderingEnabled,
            SessionTimeoutSeconds: agent.SessionTimeoutSeconds,
            SilenceTimeoutSeconds: agent.SilenceTimeoutSeconds);

        return Ok(config);
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst(PlatformTokenClaims.TenantId)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }

    private static string? ValidateCreateRequest(CreateAgentRequest request)
    {
        var nameError = ValidateRequiredText(request.Name, "Agent name", 255);
        if (nameError is not null)
        {
            return nameError;
        }

        var displayNameError = ValidateRequiredText(request.DisplayName, "Display name", 255);
        if (displayNameError is not null)
        {
            return displayNameError;
        }

        var personaError = ValidateRequiredText(request.PersonaPrompt, "Persona prompt", null);
        if (personaError is not null)
        {
            return personaError;
        }

        var primaryLanguage = NormalizePrimaryLanguage(request.PrimaryLanguage) ?? "si";
        var supportedLanguages = request.SupportedLanguages ?? DefaultSupportedLanguages;

        return ValidateAgentConfiguration(
            primaryLanguage,
            supportedLanguages,
            request.VoiceName,
            request.ToolsEnabled,
            request.SessionTimeoutSeconds ?? 600,
            request.SilenceTimeoutSeconds ?? 90);
    }

    private static string? ValidateUpdateRequest(UpdateAgentRequest request, Agent agent)
    {
        if (request.Name is not null)
        {
            var nameError = ValidateRequiredText(request.Name, "Agent name", 255);
            if (nameError is not null)
            {
                return nameError;
            }
        }

        if (request.DisplayName is not null)
        {
            var displayNameError = ValidateRequiredText(request.DisplayName, "Display name", 255);
            if (displayNameError is not null)
            {
                return displayNameError;
            }
        }

        if (request.PersonaPrompt is not null)
        {
            var personaError = ValidateRequiredText(request.PersonaPrompt, "Persona prompt", null);
            if (personaError is not null)
            {
                return personaError;
            }
        }

        var primaryLanguage = NormalizePrimaryLanguage(request.PrimaryLanguage) ?? agent.PrimaryLanguage;
        var supportedLanguages = request.SupportedLanguages ?? agent.SupportedLanguages;

        return ValidateAgentConfiguration(
            primaryLanguage,
            supportedLanguages,
            request.VoiceName ?? agent.VoiceName,
            request.ToolsEnabled ?? agent.ToolsEnabled,
            request.SessionTimeoutSeconds ?? agent.SessionTimeoutSeconds,
            request.SilenceTimeoutSeconds ?? agent.SilenceTimeoutSeconds);
    }

    private static string? ValidateAgentConfiguration(
        string primaryLanguage,
        string[] supportedLanguages,
        string? voiceName,
        string[]? toolsEnabled,
        int sessionTimeoutSeconds,
        int silenceTimeoutSeconds)
    {
        if (!SupportedLanguageCodes.Contains(primaryLanguage))
        {
            return "Primary language must be one of: si, ta, en.";
        }

        var languageError = ValidateSupportedLanguages(supportedLanguages, primaryLanguage);
        if (languageError is not null)
        {
            return languageError;
        }

        var voiceError = ValidateOptionalText(voiceName, "Voice name", 100);
        if (voiceError is not null)
        {
            return voiceError;
        }

        if (voiceName is not null && !GeminiVoiceCatalog.IsSupported(voiceName))
        {
            return "Voice name must be one of the supported Gemini prebuilt voices.";
        }

        var toolsError = ValidateTools(toolsEnabled);
        if (toolsError is not null)
        {
            return toolsError;
        }

        if (sessionTimeoutSeconds <= 0)
        {
            return "Session timeout must be greater than zero.";
        }

        if (silenceTimeoutSeconds <= 0)
        {
            return "Silence timeout must be greater than zero.";
        }

        if (silenceTimeoutSeconds >= sessionTimeoutSeconds)
        {
            return "Silence timeout must be shorter than the session timeout.";
        }

        return null;
    }

    private static string? ValidateRequiredText(string value, string fieldName, int? maxLength)
    {
        var trimmedValue = value.Trim();
        if (trimmedValue.Length == 0)
        {
            return $"{fieldName} is required.";
        }

        if (maxLength.HasValue && trimmedValue.Length > maxLength.Value)
        {
            return $"{fieldName} must be {maxLength.Value} characters or fewer.";
        }

        return null;
    }

    private static string? ValidateOptionalText(string? value, string fieldName, int maxLength)
    {
        if (value is null)
        {
            return null;
        }

        var trimmedValue = value.Trim();
        if (trimmedValue.Length == 0)
        {
            return $"{fieldName} is required.";
        }

        if (trimmedValue.Length > maxLength)
        {
            return $"{fieldName} must be {maxLength} characters or fewer.";
        }

        return null;
    }

    private static string? ValidateSupportedLanguages(string[] supportedLanguages, string primaryLanguage)
    {
        var normalizedLanguages = (supportedLanguages ?? DefaultSupportedLanguages)
            .Select(language => language.Trim().ToLowerInvariant())
            .Where(language => language.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedLanguages.Length == 0)
        {
            return "Supported languages must contain at least one language.";
        }

        if (normalizedLanguages.Any(language => !SupportedLanguageCodes.Contains(language)))
        {
            return "Supported languages must be one of: si, ta, en.";
        }

        if (!normalizedLanguages.Contains(primaryLanguage, StringComparer.Ordinal))
        {
            return "Supported languages must include the primary language.";
        }

        return null;
    }

    private static string? ValidateTools(string[]? toolsEnabled)
    {
        if (toolsEnabled is null)
        {
            return null;
        }

        var normalizedTools = toolsEnabled
            .Select(tool => tool.Trim())
            .Where(tool => tool.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedTools.Any(tool => !SupportedToolNames.Contains(tool)))
        {
            return "Tools enabled must contain only supported tool names.";
        }

        if (normalizedTools.Contains("create_pending_booking", StringComparer.Ordinal)
            && !normalizedTools.Contains("check_availability", StringComparer.Ordinal))
        {
            return "Booking creation requires availability checking to stay enabled.";
        }

        return null;
    }

    private static string NormalizePrimaryLanguage(string? value) =>
        value?.Trim().ToLowerInvariant() ?? string.Empty;

    private static string[] NormalizeSupportedLanguages(string[]? values, string primaryLanguage)
    {
        var normalizedLanguages = (values ?? DefaultSupportedLanguages)
            .Select(language => language.Trim().ToLowerInvariant())
            .Where(language => language.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedLanguages.Any(language => !SupportedLanguageCodes.Contains(language)))
        {
            throw new InvalidOperationException("Supported languages must be one of: si, ta, en.");
        }

        return normalizedLanguages;
    }

    private static string? NormalizeVoiceName(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return GeminiVoiceCatalog.Normalize(value);
    }

    private static string[]? NormalizeTools(string[]? values)
    {
        if (values is null)
        {
            return null;
        }

        var normalizedTools = values
            .Select(tool => tool.Trim())
            .Where(tool => tool.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedTools.Any(tool => !SupportedToolNames.Contains(tool)))
        {
            throw new InvalidOperationException("Tools enabled must contain only supported tool names.");
        }

        return normalizedTools;
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
    string? PrimaryLanguage, string[]? SupportedLanguages, string? VoiceName, string[]? ToolsEnabled,
    int? SessionTimeoutSeconds, int? SilenceTimeoutSeconds, bool? IsActive);
public record UpdateAgentRequest(string? Name, string? DisplayName, string? PersonaPrompt, string? PrimaryLanguage,
    string[]? SupportedLanguages, string? VoiceName, string[]? ToolsEnabled, int? SessionTimeoutSeconds,
    int? SilenceTimeoutSeconds, bool? IsActive);
