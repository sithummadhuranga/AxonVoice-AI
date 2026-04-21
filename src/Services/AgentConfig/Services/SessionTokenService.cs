using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using AxonVoiceAI.Shared.DTOs;
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
            new Claim("tenant_id", tenantId.ToString()),
            new Claim("agent_id", agentId.ToString()),
            new Claim("session_id", sessionId.ToString()),
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

        return new JwtSecurityTokenHandler().ValidateToken(token, validationParameters, out _);
    }
}
