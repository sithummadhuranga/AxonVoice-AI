using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("tenants")]
[Authorize(Policy = PlatformAuthorizationPolicyNames.ConsoleAccess)]
public sealed class TenantsController : ControllerBase
{
    private readonly AgentConfigDbContext _db;
    private readonly ApiKeyEncryptionService _encryption;

    public TenantsController(AgentConfigDbContext db, ApiKeyEncryptionService encryption)
    {
        _db = db;
        _encryption = encryption;
    }

    [HttpPost]
    public async Task<IActionResult> CreateTenantAsync(
        CreateTenantRequest request,
        CancellationToken ct)
    {
        var apiKeyError = ValidateGeminiApiKey(request.GeminiApiKey);
        if (apiKeyError is not null)
        {
            return BadRequest(new { error = apiKeyError });
        }

        var geminiApiKey = request.GeminiApiKey.Trim();
        var encrypted = _encryption.Encrypt(geminiApiKey);
        var hint = ApiKeyEncryptionService.ExtractHint(geminiApiKey);

        var tenant = new Tenant
        {
            Name = request.Name,
            ApiKeyEncrypted = encrypted,
            ApiKeyHint = hint,
            DefaultLanguage = request.DefaultLanguage ?? "si",
            WebhookUrl = request.WebhookUrl,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetTenantAsync), new { id = tenant.Id }, new TenantResponse(
            tenant.Id, tenant.Name, tenant.ApiKeyHint, tenant.DefaultLanguage,
            tenant.RateLimitDaily, tenant.RateLimitConcurrent, tenant.IsActive));
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTenantAsync(Guid id, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var tenant = await _db.Tenants
            .Where(t => t.Id == id && t.Id == tenantId)
            .FirstOrDefaultAsync(ct);

        if (tenant is null) return NotFound();

        return Ok(new TenantResponse(
            tenant.Id, tenant.Name, tenant.ApiKeyHint, tenant.DefaultLanguage,
            tenant.RateLimitDaily, tenant.RateLimitConcurrent, tenant.IsActive));
    }

    [HttpPut("{id:guid}/api-key")]
    public async Task<IActionResult> UpdateApiKeyAsync(Guid id, UpdateApiKeyRequest request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var tenant = await _db.Tenants
            .Where(t => t.Id == id && t.Id == tenantId)
            .FirstOrDefaultAsync(ct);

        if (tenant is null) return NotFound();

        var apiKeyError = ValidateGeminiApiKey(request.GeminiApiKey);
        if (apiKeyError is not null)
        {
            return BadRequest(new { error = apiKeyError });
        }

        var geminiApiKey = request.GeminiApiKey.Trim();
        tenant.ApiKeyEncrypted = _encryption.Encrypt(geminiApiKey);
        tenant.ApiKeyHint = ApiKeyEncryptionService.ExtractHint(geminiApiKey);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static string? ValidateGeminiApiKey(string geminiApiKey)
    {
        var trimmedApiKey = geminiApiKey.Trim();
        if (trimmedApiKey.Length == 0)
        {
            return "Gemini API key is required.";
        }

        if (trimmedApiKey.Length > 512)
        {
            return "Gemini API key must be 512 characters or fewer.";
        }

        if (trimmedApiKey.Any(char.IsWhiteSpace))
        {
            return "Gemini API key must not contain spaces or line breaks.";
        }

        return null;
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst(PlatformTokenClaims.TenantId)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record CreateTenantRequest(string Name, string GeminiApiKey, string? DefaultLanguage, string? WebhookUrl);
public record UpdateApiKeyRequest(string GeminiApiKey);
public record TenantResponse(Guid Id, string Name, string? ApiKeyHint, string DefaultLanguage, int RateLimitDaily, int RateLimitConcurrent, bool IsActive);
