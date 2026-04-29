using AxonVoiceAI.ConversationStore.Data;
using AxonVoiceAI.ConversationStore.Data.Entities;
using AxonVoiceAI.ConversationStore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.ConversationStore.Controllers;

[ApiController]
[Route("sessions")]
public sealed class SessionsController : ControllerBase
{
    private readonly ConversationDbContext _db;
    private readonly SummaryService _summaryService;

    public SessionsController(ConversationDbContext db, SummaryService summaryService)
    {
        _db = db;
        _summaryService = summaryService;
    }

    [HttpPost]
    public async Task<IActionResult> RecordSessionStartAsync(
        RecordSessionStartRequest request,
        CancellationToken ct)
    {
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
        return Created($"/sessions/{session.Id}", null);
    }

    [HttpPost("{sessionId:guid}/close")]
    public async Task<IActionResult> CloseSessionAsync(
        Guid sessionId,
        CloseSessionRequest request,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var session = await _db.Sessions
            .Include(s => s.FunctionCalls)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, ct);

        if (session is null) return NotFound();
        if (session.Status == "closed") return Conflict(new { error = "Session is already closed." });

        session.Status = "closed";
        session.EndedAt = DateTimeOffset.UtcNow;
        session.DurationSeconds = request.DurationSeconds;

        if (!string.IsNullOrWhiteSpace(request.Transcript))
        {
            session.Summary = await _summaryService.SummarizeSessionAsync(
                session, session.FunctionCalls.ToList(), request.Transcript, ct);
        }

        if (!string.IsNullOrWhiteSpace(request.WebhookUrl))
        {
            _db.WebhookDeliveries.Add(new WebhookDelivery
            {
                SessionId = sessionId,
                TenantId = tenantId,
                WebhookUrl = request.WebhookUrl,
                EventType = "session.closed",
                PayloadJson = System.Text.Json.JsonSerializer.Serialize(new
                {
                    sessionId,
                    agentId = session.AgentId,
                    durationSeconds = request.DurationSeconds,
                    summary = session.Summary,
                    endedAt = session.EndedAt,
                }),
                Status = "pending",
                CreatedAt = DateTimeOffset.UtcNow,
            });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpPost("{sessionId:guid}/function-calls")]
    public async Task<IActionResult> RecordFunctionCallAsync(
        Guid sessionId,
        RecordFunctionCallRequest request,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var sessionExists = await _db.Sessions
            .AnyAsync(s => s.Id == sessionId && s.TenantId == tenantId, ct);

        if (!sessionExists) return NotFound();

        _db.FunctionCalls.Add(new SessionFunctionCall
        {
            SessionId = sessionId,
            TenantId = tenantId,
            FunctionName = request.FunctionName,
            ArgumentsJson = request.ArgumentsJson,
            ResultJson = request.ResultJson,
            Succeeded = request.Succeeded,
            ErrorMessage = request.ErrorMessage,
            CalledAt = DateTimeOffset.UtcNow,
            DurationMs = request.DurationMs,
        });

        await _db.SaveChangesAsync(ct);
        return Created();
    }

    [HttpGet("{sessionId:guid}")]
    public async Task<IActionResult> GetSessionAsync(Guid sessionId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var session = await _db.Sessions
            .Include(s => s.FunctionCalls)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.TenantId == tenantId, ct);

        if (session is null) return NotFound();
        return Ok(session);
    }

    [HttpGet]
    public async Task<IActionResult> ListSessionsAsync(
        [FromQuery] Guid? agentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var tenantId = ResolveTenantId();
        var query = _db.Sessions.Where(s => s.TenantId == tenantId);

        if (agentId.HasValue)
            query = query.Where(s => s.AgentId == agentId.Value);

        var total = await query.CountAsync(ct);
        var sessions = await query
            .OrderByDescending(s => s.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new SessionSummary(s.Id, s.AgentId, s.Language, s.Status, s.StartedAt, s.DurationSeconds))
            .ToListAsync(ct);

        return Ok(new { Total = total, Page = page, PageSize = pageSize, Sessions = sessions });
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst(AxonVoiceAI.Shared.Security.PlatformTokenClaims.TenantId)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record RecordSessionStartRequest(Guid SessionId, Guid AgentId, Guid TenantId, string CallerIdentifier, string Language);
public record CloseSessionRequest(int? DurationSeconds, string? Transcript, string? WebhookUrl);
public record RecordFunctionCallRequest(string FunctionName, string? ArgumentsJson, string? ResultJson, bool Succeeded, string? ErrorMessage, int DurationMs);
public record SessionSummary(Guid Id, Guid AgentId, string Language, string Status, DateTimeOffset StartedAt, int? DurationSeconds);
