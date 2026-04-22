using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Services;

public sealed class ConsoleAuthenticationService
{
    private readonly AgentConfigDbContext _db;
    private readonly IPasswordHasher<TenantUser> _passwordHasher;

    public ConsoleAuthenticationService(
        AgentConfigDbContext db,
        IPasswordHasher<TenantUser> passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<ConsoleAuthenticationResult> RegisterOwnerAsync(
        RegisterConsoleOwnerCommand command,
        CancellationToken ct)
    {
        var businessName = ValidateBusinessName(command.BusinessName);
        var email = ValidateEmail(command.Email);
        var normalizedEmail = NormalizeEmail(email);
        ValidatePassword(command.Password);

        var emailExists = await _db.TenantUsers
            .AnyAsync(user => user.NormalizedEmail == normalizedEmail, ct);

        if (emailExists)
        {
            throw new InvalidOperationException("An account with that email already exists.");
        }

        var now = DateTimeOffset.UtcNow;
        var tenant = new Tenant
        {
            Name = businessName,
            ApiKeyEncrypted = string.Empty,
            DefaultLanguage = "si",
            CreatedAt = now,
            UpdatedAt = now,
        };

        var user = new TenantUser
        {
            Tenant = tenant,
            Email = email,
            NormalizedEmail = normalizedEmail,
            Role = TenantUserRoles.Owner,
            CreatedAt = now,
            UpdatedAt = now,
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, command.Password);

        _db.TenantUsers.Add(user);
        await _db.SaveChangesAsync(ct);

        return new ConsoleAuthenticationResult(tenant.Id, tenant.Name, user.Id, user.Email, user.Role);
    }

    public async Task<ConsoleAuthenticationResult?> AuthenticateAsync(
        string email,
        string password,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        var normalizedEmail = NormalizeEmail(email);
        var user = await _db.TenantUsers
            .Include(candidate => candidate.Tenant)
            .FirstOrDefaultAsync(candidate => candidate.NormalizedEmail == normalizedEmail, ct);

        if (user is null || !user.IsActive || !user.Tenant.IsActive)
        {
            return null;
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (verification == PasswordVerificationResult.Failed)
        {
            return null;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = _passwordHasher.HashPassword(user, password);
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = user.LastLoginAt.Value;
        await _db.SaveChangesAsync(ct);

        return new ConsoleAuthenticationResult(
            user.TenantId,
            user.Tenant.Name,
            user.Id,
            user.Email,
            user.Role);
    }

    internal static string NormalizeEmail(string email) =>
        email.Trim().ToUpperInvariant();

    private static string ValidateBusinessName(string businessName)
    {
        var trimmedName = businessName.Trim();
        if (trimmedName.Length == 0)
        {
            throw new ArgumentException("Business name is required.", nameof(businessName));
        }

        if (trimmedName.Length > 255)
        {
            throw new ArgumentException("Business name must be 255 characters or fewer.", nameof(businessName));
        }

        return trimmedName;
    }

    private static string ValidateEmail(string email)
    {
        var trimmedEmail = email.Trim();
        if (trimmedEmail.Length == 0)
        {
            throw new ArgumentException("Email is required.", nameof(email));
        }

        if (!trimmedEmail.Contains('@', StringComparison.Ordinal))
        {
            throw new ArgumentException("Email must be a valid address.", nameof(email));
        }

        if (trimmedEmail.Length > 320)
        {
            throw new ArgumentException("Email must be 320 characters or fewer.", nameof(email));
        }

        return trimmedEmail;
    }

    private static void ValidatePassword(string password)
    {
        if (password.Length < 12)
        {
            throw new ArgumentException("Password must be at least 12 characters long.", nameof(password));
        }

        if (!password.Any(char.IsUpper))
        {
            throw new ArgumentException("Password must contain at least one uppercase letter.", nameof(password));
        }

        if (!password.Any(char.IsLower))
        {
            throw new ArgumentException("Password must contain at least one lowercase letter.", nameof(password));
        }

        if (!password.Any(char.IsDigit))
        {
            throw new ArgumentException("Password must contain at least one number.", nameof(password));
        }
    }
}

public sealed record RegisterConsoleOwnerCommand(string BusinessName, string Email, string Password);

public sealed record ConsoleAuthenticationResult(
    Guid TenantId,
    string TenantName,
    Guid UserId,
    string Email,
    string Role);