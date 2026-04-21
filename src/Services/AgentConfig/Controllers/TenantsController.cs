using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("tenants")]
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
        var encrypted = _encryption.Encrypt(request.GeminiApiKey);
        var hint = ApiKeyEncryptionService.ExtractHint(request.GeminiApiKey);

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

        tenant.ApiKeyEncrypted = _encryption.Encrypt(request.GeminiApiKey);
        tenant.ApiKeyHint = ApiKeyEncryptionService.ExtractHint(request.GeminiApiKey);
        tenant.UpdatedAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst("tenant_id")?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record CreateTenantRequest(string Name, string GeminiApiKey, string? DefaultLanguage, string? WebhookUrl);
public record UpdateApiKeyRequest(string GeminiApiKey);
public record TenantResponse(Guid Id, string Name, string? ApiKeyHint, string DefaultLanguage, int RateLimitDaily, int RateLimitConcurrent, bool IsActive);
