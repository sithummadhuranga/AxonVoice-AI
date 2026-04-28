using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.ConversationStore.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/sessions")]
public sealed class InternalSessionsController : ControllerBase
{
    private readonly ConversationDbContext _db;

    public InternalSessionsController(ConversationDbContext db)
    {
        _db = db;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> RecordSessionStartAsync(
        InternalRecordSessionStartRequest request,
        CancellationToken ct)
    {
        var existingSession = await _db.Sessions
            .AnyAsync(session => session.Id == request.SessionId && session.TenantId == request.TenantId, ct);

        if (existingSession)
            return Ok();

        var session = new ConversationSession
        {
            Id = request.SessionId,
            AgentId = request.AgentId,
            TenantId = request.TenantId,
            CallerIdentifier = request.CallerIdentifier,
            Language = request.Language,
            Status = "active",
            StartedAt = DateTimeOffset.UtcNow,
        };

        _db.Sessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return Created($"/internal/sessions/{session.Id}", null);
    }

    [HttpPost("{sessionId:guid}/function-calls")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordFunctionCallAsync(
        Guid sessionId,
        InternalRecordFunctionCallRequest request,
        CancellationToken ct)
    {
        var sessionExists = await _db.Sessions
            .AnyAsync(session => session.Id == sessionId && session.TenantId == request.TenantId, ct);

        if (!sessionExists)
            return NotFound();

        _db.FunctionCalls.Add(new SessionFunctionCall
        {
            SessionId = sessionId,
            TenantId = request.TenantId,
            FunctionName = request.FunctionName,
            ArgumentsJson = request.ArgumentsJson,
            ResultJson = request.ResultJson,
            Succeeded = request.Succeeded,
            ErrorMessage = request.ErrorMessage,
            CalledAt = DateTimeOffset.UtcNow,
            DurationMs = request.DurationMs,
        });

        await _db.SaveChangesAsync(ct);

        return Created($"/internal/sessions/{sessionId}/function-calls", null);
    }

    [HttpPost("{sessionId:guid}/close")]
    [AllowAnonymous]
    public async Task<IActionResult> CloseSessionAsync(
        Guid sessionId,
        InternalCloseSessionRequest request,
        CancellationToken ct)
    {
        var session = await _db.Sessions
            .FirstOrDefaultAsync(existingSession => existingSession.Id == sessionId && existingSession.TenantId == request.TenantId, ct);

        if (session is null)
            return NotFound();

        if (string.Equals(session.Status, "closed", StringComparison.Ordinal))
            return NoContent();

        session.Status = "closed";
        session.EndedAt = DateTimeOffset.UtcNow;
        session.DurationSeconds = request.DurationSeconds;

        await _db.SaveChangesAsync(ct);

        return NoContent();
    }
}

public sealed record InternalRecordSessionStartRequest(
    Guid SessionId,
    Guid AgentId,
    Guid TenantId,
    string CallerIdentifier,
    string Language);

public sealed record InternalRecordFunctionCallRequest(
    Guid TenantId,
    string FunctionName,
    string? ArgumentsJson,
    string? ResultJson,
    bool Succeeded,
    string? ErrorMessage,
    int DurationMs);

public sealed record InternalCloseSessionRequest(
    Guid TenantId,
    int? DurationSeconds);