using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[Route("agents/{agentId:guid}/business-hours")]
[Authorize(Policy = PlatformAuthorizationPolicyNames.ConsoleAccess)]
public sealed class BusinessHoursController : ControllerBase
{
    private readonly AgentConfigDbContext _db;

    public BusinessHoursController(AgentConfigDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetBusinessHoursAsync(Guid agentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agentBelongsToTenant = await _db.Agents
            .AnyAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);

        if (!agentBelongsToTenant) return NotFound();

        var hours = await _db.BusinessHours
            .Where(bh => bh.AgentId == agentId)
            .OrderBy(bh => bh.DayOfWeek)
            .Select(bh => new BusinessHoursResponse(
                bh.Id, bh.DayOfWeek, bh.OpenTime, bh.CloseTime,
                bh.SlotDurationMinutes, bh.MaxCapacityPerSlot, bh.IsActive))
            .ToListAsync(ct);

        return Ok(hours);
    }

    [HttpPut]
    public async Task<IActionResult> SetBusinessHoursAsync(
        Guid agentId,
        SetBusinessHoursRequest request,
        CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agent = await _db.Agents
            .Where(a => a.Id == agentId && a.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (agent is null) return NotFound();

        // Replace the entire schedule — simpler than partial updates and consistent.
        var existing = await _db.BusinessHours.Where(bh => bh.AgentId == agentId).ToListAsync(ct);
        _db.BusinessHours.RemoveRange(existing);

        foreach (var slot in request.Schedule)
        {
            _db.BusinessHours.Add(new BusinessHours
            {
                AgentId = agentId,
                TenantId = tenantId,
                DayOfWeek = slot.DayOfWeek,
                OpenTime = slot.OpenTime,
                CloseTime = slot.CloseTime,
                SlotDurationMinutes = slot.SlotDurationMinutes,
                MaxCapacityPerSlot = slot.MaxCapacityPerSlot,
                IsActive = true,
            });
        }

        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("closed-dates")]
    public async Task<IActionResult> GetClosedDatesAsync(Guid agentId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agentBelongsToTenant = await _db.Agents
            .AnyAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);

        if (!agentBelongsToTenant) return NotFound();

        var closedDates = await _db.ClosedDates
            .Where(cd => cd.AgentId == agentId)
            .OrderBy(cd => cd.Date)
            .Select(cd => new ClosedDateResponse(cd.Id, cd.Date, cd.Reason))
            .ToListAsync(ct);

        return Ok(closedDates);
    }

    [HttpPost("closed-dates")]
    public async Task<IActionResult> AddClosedDateAsync(Guid agentId, AddClosedDateRequest request, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var agentBelongsToTenant = await _db.Agents
            .AnyAsync(a => a.Id == agentId && a.TenantId == tenantId, ct);

        if (!agentBelongsToTenant) return NotFound();

        var closedDate = new ClosedDate
        {
            AgentId = agentId,
            TenantId = tenantId,
            Date = request.Date,
            Reason = request.Reason,
        };

        _db.ClosedDates.Add(closedDate);
        await _db.SaveChangesAsync(ct);

        return Created($"/agents/{agentId:D}/business-hours/closed-dates/{closedDate.Id:D}",
            new ClosedDateResponse(closedDate.Id, closedDate.Date, closedDate.Reason));
    }

    [HttpDelete("closed-dates/{dateId:guid}")]
    public async Task<IActionResult> RemoveClosedDateAsync(Guid agentId, Guid dateId, CancellationToken ct)
    {
        var tenantId = ResolveTenantId();
        var closedDate = await _db.ClosedDates
            .Where(cd => cd.Id == dateId && cd.AgentId == agentId && cd.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (closedDate is null) return NotFound();

        _db.ClosedDates.Remove(closedDate);
        await _db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Guid ResolveTenantId()
    {
        var claim = User.FindFirst(PlatformTokenClaims.TenantId)?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : Guid.Empty;
    }
}

public record BusinessHoursResponse(Guid Id, short DayOfWeek, TimeOnly OpenTime, TimeOnly CloseTime,
    int SlotDurationMinutes, int MaxCapacityPerSlot, bool IsActive);
public record BusinessHoursSlot(short DayOfWeek, TimeOnly OpenTime, TimeOnly CloseTime,
    int SlotDurationMinutes, int MaxCapacityPerSlot);
public record SetBusinessHoursRequest(IReadOnlyList<BusinessHoursSlot> Schedule);
public record ClosedDateResponse(Guid Id, DateOnly Date, string? Reason);
public record AddClosedDateRequest(DateOnly Date, string? Reason);
