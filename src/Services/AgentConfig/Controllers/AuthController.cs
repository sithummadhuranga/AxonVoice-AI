using AxonVoiceAI.AgentConfig.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly ConsoleAuthenticationService _authentication;
    private readonly ConsoleTokenService _tokens;

    public AuthController(
        ConsoleAuthenticationService authentication,
        ConsoleTokenService tokens)
    {
        _authentication = authentication;
        _tokens = tokens;
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync(RegisterConsoleOwnerRequest request, CancellationToken ct)
    {
        try
        {
            var identity = await _authentication.RegisterOwnerAsync(
                new RegisterConsoleOwnerCommand(request.BusinessName, request.Email, request.Password),
                ct);

            return Ok(BuildResponse(identity));
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Conflict(new { error = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync(LoginConsoleRequest request, CancellationToken ct)
    {
        var identity = await _authentication.AuthenticateAsync(request.Email, request.Password, ct);
        if (identity is null)
        {
            return Unauthorized(new { error = "Email or password is incorrect." });
        }

        return Ok(BuildResponse(identity));
    }

    private ConsoleAuthResponse BuildResponse(ConsoleAuthenticationResult identity)
    {
        var token = _tokens.IssueToken(identity);

        return new ConsoleAuthResponse(
            token.AccessToken,
            token.ExpiresAt,
            new ConsoleSessionResponse(
                identity.TenantId,
                identity.TenantName,
                identity.UserId,
                identity.Email,
                identity.Role));
    }
}

public sealed record RegisterConsoleOwnerRequest(string BusinessName, string Email, string Password);

public sealed record LoginConsoleRequest(string Email, string Password);

public sealed record ConsoleAuthResponse(
    string AccessToken,
    DateTimeOffset ExpiresAt,
    ConsoleSessionResponse Session);

public sealed record ConsoleSessionResponse(
    Guid TenantId,
    string TenantName,
    Guid UserId,
    string Email,
    string Role);