using AxonVoiceAI.ConversationStore.Controllers;
using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Data.Entities;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.ConversationStore.Tests;

public sealed class InternalSessionsControllerTests
{
    [Fact]
    public async Task RecordSessionStartAsync_NewSession_PersistsConversationSession()
    {
        await using var db = CreateDbContext();
        var controller = new InternalSessionsController(db);
        var request = new InternalRecordSessionStartRequest(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "WebSocket",
            "en");

        var result = await controller.RecordSessionStartAsync(request, CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        var session = await db.Sessions.SingleAsync();
        session.Id.Should().Be(request.SessionId);
        session.AgentId.Should().Be(request.AgentId);
        session.TenantId.Should().Be(request.TenantId);
        session.CallerIdentifier.Should().Be("WebSocket");
        session.Language.Should().Be("en");
        session.Status.Should().Be("active");
    }

    [Fact]
    public async Task RecordFunctionCallAsync_MatchingTenant_PersistsFunctionCall()
    {
        var session = CreateSession();

        await using var db = CreateDbContext();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new InternalSessionsController(db);
        var request = new InternalRecordFunctionCallRequest(
            session.TenantId,
            "check_availability",
            "{\"party_size\":4}",
            "{\"available\":true}",
            true,
            null,
            42);

        var result = await controller.RecordFunctionCallAsync(session.Id, request, CancellationToken.None);

        result.Should().BeOfType<CreatedResult>();
        var functionCall = await db.FunctionCalls.SingleAsync();
        functionCall.SessionId.Should().Be(session.Id);
        functionCall.TenantId.Should().Be(session.TenantId);
        functionCall.FunctionName.Should().Be("check_availability");
        functionCall.DurationMs.Should().Be(42);
        functionCall.Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task CloseSessionAsync_OpenSession_RecordsEndState()
    {
        var session = CreateSession();

        await using var db = CreateDbContext();
        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        var controller = new InternalSessionsController(db);
        var request = new InternalCloseSessionRequest(session.TenantId, 93);

        var result = await controller.CloseSessionAsync(session.Id, request, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
        var persistedSession = await db.Sessions.SingleAsync();
        persistedSession.Status.Should().Be("closed");
        persistedSession.DurationSeconds.Should().Be(93);
        persistedSession.EndedAt.Should().NotBeNull();
    }

    private static ConversationSession CreateSession() =>
        new()
        {
            Id = Guid.NewGuid(),
            AgentId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            CallerIdentifier = "WebSocket",
            Language = "en",
            Status = "active",
            StartedAt = DateTimeOffset.UtcNow.AddMinutes(-3),
        };

    private static ConversationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ConversationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ConversationDbContext(options);
    }
}