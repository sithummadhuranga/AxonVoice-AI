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

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class TenantsControllerTests
{
    [Fact]
    public async Task CreateTenantAsync_BlankApiKey_ReturnsBadRequest()
    {
        await using var db = CreateDbContext();
        var controller = CreateController(db);

        var result = await controller.CreateTenantAsync(
            new CreateTenantRequest("Axon Bistro", "   ", "si", null),
            CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeEquivalentTo(new { error = "Gemini API key is required." });
    }

    [Fact]
    public async Task UpdateApiKeyAsync_ValidKey_EncryptsValueAndStoresHint()
    {
        var tenantId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-5);

        await using var db = CreateDbContext();
        db.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Name = "Axon Bistro",
            ApiKeyEncrypted = "existing-ciphertext",
            ApiKeyHint = "1234",
            DefaultLanguage = "si",
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
        });
        await db.SaveChangesAsync();

        var controller = CreateController(db, tenantId);
        const string geminiApiKey = "AIzaSyAxonVoiceRuntimeKey1234567890";

        var result = await controller.UpdateApiKeyAsync(
            tenantId,
            new UpdateApiKeyRequest(geminiApiKey),
            CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();

        var tenant = await db.Tenants.SingleAsync();
        tenant.ApiKeyEncrypted.Should().NotBe(geminiApiKey);
        tenant.ApiKeyHint.Should().Be(ApiKeyEncryptionService.ExtractHint(geminiApiKey));
        tenant.UpdatedAt.Should().BeAfter(createdAt);
    }

    private static TenantsController CreateController(AgentConfigDbContext db, Guid? tenantId = null)
    {
        var controller = new TenantsController(db, CreateEncryptionService());

        if (tenantId.HasValue)
        {
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = CreatePrincipal(tenantId.Value),
                },
            };
        }

        return controller;
    }

    private static ClaimsPrincipal CreatePrincipal(Guid tenantId)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(PlatformTokenClaims.TenantId, tenantId.ToString()),
        ],
        "TestAuth");

        return new ClaimsPrincipal(identity);
    }

    private static ApiKeyEncryptionService CreateEncryptionService()
    {
        var key = Convert.ToBase64String(Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        return new ApiKeyEncryptionService(key);
    }

    private static AgentConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentConfigDbContext(options);
    }
}