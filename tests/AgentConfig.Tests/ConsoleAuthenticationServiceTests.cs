using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.AgentConfig.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Tests;

public sealed class ConsoleAuthenticationServiceTests
{
    [Fact]
    public async Task RegisterOwnerAsync_NewEmail_CreatesTenantAndOwnerAccount()
    {
        await using var db = CreateDbContext();
        var service = new ConsoleAuthenticationService(db, CreatePasswordHasher());

        var result = await service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Axon Bistro", "owner@axonvoice.ai", "StrongerPass123"),
            CancellationToken.None);

        result.TenantName.Should().Be("Axon Bistro");
        result.Email.Should().Be("owner@axonvoice.ai");
        result.Role.Should().Be(TenantUserRoles.Owner);

        var user = await db.TenantUsers.Include(candidate => candidate.Tenant).SingleAsync();
        user.TenantId.Should().Be(result.TenantId);
        user.NormalizedEmail.Should().Be("OWNER@AXONVOICE.AI");
        user.PasswordHash.Should().NotBe("StrongerPass123");
        user.Tenant.ApiKeyEncrypted.Should().BeEmpty();
    }

    [Fact]
    public async Task RegisterOwnerAsync_DuplicateEmail_ThrowsConflict()
    {
        await using var db = CreateDbContext();
        var service = new ConsoleAuthenticationService(db, CreatePasswordHasher());

        await service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Axon Bistro", "owner@axonvoice.ai", "StrongerPass123"),
            CancellationToken.None);

        var action = () => service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Another Bistro", "owner@axonvoice.ai", "AnotherPass123"),
            CancellationToken.None);

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("An account with that email already exists.");
    }

    [Fact]
    public async Task AuthenticateAsync_ValidCredentials_ReturnsTenantIdentity()
    {
        await using var db = CreateDbContext();
        var passwordHasher = CreatePasswordHasher();
        var service = new ConsoleAuthenticationService(db, passwordHasher);

        var registration = await service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Axon Bistro", "owner@axonvoice.ai", "StrongerPass123"),
            CancellationToken.None);

        var result = await service.AuthenticateAsync("owner@axonvoice.ai", "StrongerPass123", CancellationToken.None);

        result.Should().NotBeNull();
        result!.TenantId.Should().Be(registration.TenantId);
        result.UserId.Should().Be(registration.UserId);

        var storedUser = await db.TenantUsers.SingleAsync();
        storedUser.LastLoginAt.Should().NotBeNull();
    }

    [Fact]
    public async Task AuthenticateAsync_WrongPassword_ReturnsNull()
    {
        await using var db = CreateDbContext();
        var service = new ConsoleAuthenticationService(db, CreatePasswordHasher());

        await service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Axon Bistro", "owner@axonvoice.ai", "StrongerPass123"),
            CancellationToken.None);

        var result = await service.AuthenticateAsync("owner@axonvoice.ai", "WrongPass123", CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RegisterOwnerAsync_WeakPassword_RejectsRegistration()
    {
        await using var db = CreateDbContext();
        var service = new ConsoleAuthenticationService(db, CreatePasswordHasher());

        var action = () => service.RegisterOwnerAsync(
            new RegisterConsoleOwnerCommand("Axon Bistro", "owner@axonvoice.ai", "weakpass"),
            CancellationToken.None);

        await action.Should().ThrowAsync<ArgumentException>()
            .WithMessage("Password must be at least 12 characters long.*");
    }

    private static AgentConfigDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentConfigDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new AgentConfigDbContext(options);
    }

    private static IPasswordHasher<TenantUser> CreatePasswordHasher() =>
        new PasswordHasher<TenantUser>();
}