using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/agents/{agentId:guid}/tools")]
public sealed class InternalToolsController : ControllerBase
{
    private readonly AgentConfigDbContext _db;
    private readonly IAvailabilityRepository _availabilityRepository;
    private readonly IBookingRepository _bookingRepository;
    private readonly IOrderRepository _orderRepository;

    public InternalToolsController(
        AgentConfigDbContext db,
        IAvailabilityRepository availabilityRepository,
        IBookingRepository bookingRepository,
        IOrderRepository orderRepository)
    {
        _db = db;
        _availabilityRepository = availabilityRepository;
        _bookingRepository = bookingRepository;
        _orderRepository = orderRepository;
    }

    [HttpGet("availability")]
    [AllowAnonymous]
    public async Task<IActionResult> CheckAvailabilityAsync(
        Guid agentId,
        [FromQuery] DateTimeOffset requestedDatetime,
        [FromQuery] int partySize,
        CancellationToken ct)
    {
        if (partySize <= 0)
            return BadRequest(new { error = "partySize must be greater than zero." });

        var agentExists = await _db.Agents
            .AnyAsync(agent => agent.Id == agentId && agent.IsActive, ct);

        if (!agentExists)
            return NotFound();

        var result = await _availabilityRepository.QueryAvailabilityAsync(
            agentId,
            requestedDatetime,
            partySize,
            ct);

        return Ok(result);
    }

    [HttpPost("pending-bookings")]
    [AllowAnonymous]
    public async Task<IActionResult> CreatePendingBookingAsync(
        Guid agentId,
        CreatePendingBookingRequest request,
        CancellationToken ct)
    {
        if (request.AgentId != Guid.Empty && request.AgentId != agentId)
            return BadRequest(new { error = "Route agent id must match the request body." });

        if (request.TenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        if (request.SessionId == Guid.Empty)
            return BadRequest(new { error = "sessionId is required." });

        if (request.PartySize <= 0)
            return BadRequest(new { error = "partySize must be greater than zero." });

        var agentExists = await _db.Agents
            .AnyAsync(agent => agent.Id == agentId && agent.TenantId == request.TenantId && agent.IsActive, ct);

        if (!agentExists)
            return NotFound();

        var normalizedRequest = request with { AgentId = agentId };
        var result = await _bookingRepository.CreatePendingBookingAsync(normalizedRequest, ct);

        return Ok(result);
    }

    [HttpPost("orders")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateOrderAsync(
        Guid agentId,
        [FromBody] CreateOrderRequest request,
        CancellationToken ct)
    {
        if (request.AgentId != Guid.Empty && request.AgentId != agentId)
            return BadRequest(new { error = "Route agent id must match the request body." });

        if (request.TenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        if (request.SessionId == Guid.Empty)
            return BadRequest(new { error = "sessionId is required." });

        var agentExists = await _db.Agents
            .AnyAsync(agent => agent.Id == agentId && agent.TenantId == request.TenantId && agent.IsActive, ct);

        if (!agentExists)
            return NotFound();

        var normalizedRequest = request with { AgentId = agentId };
        var result = await _orderRepository.CreateOrderAsync(normalizedRequest, ct);

        return Ok(result);
    }
}