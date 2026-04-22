using AxonVoiceAI.AgentConfig.Services;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Security;
using FluentAssertions;

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class TokenServiceTests
{
    [Fact]
    public void IssueToken_ConsoleIdentity_EmitsConsoleScopedClaims()
    {
        var service = new ConsoleTokenService("0123456789abcdef0123456789abcdef", "https://platform.axonvoice.test");

        var response = service.IssueToken(
            new ConsoleAuthenticationResult(
                Guid.Parse("1f96539d-1f64-4cc0-8470-3f8ccff674c8"),
                "Axon Bistro",
                Guid.Parse("1bc80ed5-98e2-47d7-8d25-74d03bac6f83"),
                "owner@axonvoice.ai",
                TenantUserRoles.Owner));

        var principal = service.ValidateToken(response.AccessToken);

        principal.FindFirst(PlatformTokenClaims.TokenUse)!.Value.Should().Be(PlatformTokenUses.Console);
        principal.FindFirst(PlatformTokenClaims.TenantName)!.Value.Should().Be("Axon Bistro");
        principal.FindFirst(PlatformTokenClaims.Email)!.Value.Should().Be("owner@axonvoice.ai");
        response.ExpiresAt.Should().BeAfter(DateTimeOffset.UtcNow.AddHours(7));
    }

    [Fact]
    public void IssueSessionToken_ValidInput_EmitsSessionScopedClaims()
    {
        var service = new SessionTokenService("0123456789abcdef0123456789abcdef", "https://platform.axonvoice.test");

        var response = service.IssueSessionToken(
            Guid.Parse("1f96539d-1f64-4cc0-8470-3f8ccff674c8"),
            Guid.Parse("c4df35af-d507-4ae4-a8e0-bf0a6a9635eb"),
            Guid.Parse("443b510c-0efc-4cac-b66d-9e10cb3651b6"));

        var principal = service.ValidateToken(response.Token);

        principal.FindFirst(PlatformTokenClaims.TokenUse)!.Value.Should().Be(PlatformTokenUses.Session);
        principal.FindFirst(PlatformTokenClaims.AgentId)!.Value.Should().Be("c4df35af-d507-4ae4-a8e0-bf0a6a9635eb");
        response.WsUrl.Should().Be("wss://platform.axonvoice.test/ws/session");
    }
}