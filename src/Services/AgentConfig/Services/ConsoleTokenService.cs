using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AxonVoiceAI.Shared.Security;
using Microsoft.IdentityModel.Tokens;

namespace AxonVoiceAI.AgentConfig.Services;

public sealed class ConsoleTokenService
{
    private readonly string _jwtSigningKey;
    private readonly string _platformBaseUrl;

    public ConsoleTokenService(string jwtSigningKey, string platformBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(jwtSigningKey))
            throw new ArgumentException("JWT_SIGNING_KEY must not be empty.", nameof(jwtSigningKey));
        if (string.IsNullOrWhiteSpace(platformBaseUrl))
            throw new ArgumentException("PLATFORM_BASE_URL must not be empty.", nameof(platformBaseUrl));

        _jwtSigningKey = jwtSigningKey;
        _platformBaseUrl = platformBaseUrl.TrimEnd('/');
    }

    public ConsoleAccessTokenResponse IssueToken(ConsoleAuthenticationResult identity)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddHours(8);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, identity.UserId.ToString()),
            new Claim(PlatformTokenClaims.UserId, identity.UserId.ToString()),
            new Claim(PlatformTokenClaims.TenantId, identity.TenantId.ToString()),
            new Claim(PlatformTokenClaims.TenantName, identity.TenantName),
            new Claim(PlatformTokenClaims.Email, identity.Email),
            new Claim(PlatformTokenClaims.Role, identity.Role),
            new Claim(PlatformTokenClaims.TokenUse, PlatformTokenUses.Console),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _platformBaseUrl,
            audience: "console",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new ConsoleAccessTokenResponse(
            new JwtSecurityTokenHandler().WriteToken(token),
            expiresAt);
    }

    public ClaimsPrincipal ValidateToken(string token)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSigningKey)),
            ValidateIssuer = true,
            ValidIssuer = _platformBaseUrl,
            ValidateAudience = true,
            ValidAudience = "console",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false,
        };

        var principal = handler.ValidateToken(token, validationParameters, out _);
        var tokenUse = principal.FindFirstValue(PlatformTokenClaims.TokenUse);

        if (!string.Equals(tokenUse, PlatformTokenUses.Console, StringComparison.Ordinal))
        {
            throw new SecurityTokenValidationException("Token is not valid for console access.");
        }

        return principal;
    }
}

public sealed record ConsoleAccessTokenResponse(string AccessToken, DateTimeOffset ExpiresAt);