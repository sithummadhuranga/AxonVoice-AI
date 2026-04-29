using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AxonVoiceAI.Shared.DTOs;
using AxonVoiceAI.Shared.Security;
using Microsoft.IdentityModel.Tokens;

namespace AxonVoiceAI.AgentConfig.Services;

public sealed class SessionTokenService
{
    private readonly string _jwtSigningKey;
    private readonly string _platformBaseUrl;

    public SessionTokenService(string jwtSigningKey, string platformBaseUrl)
    {
        if (string.IsNullOrWhiteSpace(jwtSigningKey))
            throw new ArgumentException("JWT_SIGNING_KEY must not be empty.", nameof(jwtSigningKey));
        if (string.IsNullOrWhiteSpace(platformBaseUrl))
            throw new ArgumentException("PLATFORM_BASE_URL must not be empty.", nameof(platformBaseUrl));

        _jwtSigningKey = jwtSigningKey;
        _platformBaseUrl = platformBaseUrl.TrimEnd('/');
    }

    public SessionTokenResponse IssueSessionToken(Guid tenantId, Guid agentId, Guid sessionId)
    {
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(15);

        var claims = new[]
        {
            new Claim(PlatformTokenClaims.TenantId, tenantId.ToString()),
            new Claim(PlatformTokenClaims.AgentId, agentId.ToString()),
            new Claim(PlatformTokenClaims.SessionId, sessionId.ToString()),
            new Claim(PlatformTokenClaims.TokenUse, PlatformTokenUses.Session),
            new Claim(JwtRegisteredClaimNames.Iat, now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _platformBaseUrl,
            audience: "relay",
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var wsUrl = $"{_platformBaseUrl.Replace("https://", "wss://").Replace("http://", "ws://")}/ws/session";

        return new SessionTokenResponse(tokenString, wsUrl, expiresAt);
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
            ValidAudience = "relay",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        var handler = new JwtSecurityTokenHandler
        {
            MapInboundClaims = false,
        };

        var principal = handler.ValidateToken(token, validationParameters, out _);
        var tokenUse = principal.FindFirstValue(PlatformTokenClaims.TokenUse);

        if (!string.Equals(tokenUse, PlatformTokenUses.Session, StringComparison.Ordinal))
        {
            throw new SecurityTokenValidationException("Token is not valid for session access.");
        }

        return principal;
    }
}
