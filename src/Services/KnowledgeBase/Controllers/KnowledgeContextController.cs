using AxonVoiceAI.KnowledgeBase.Data;
using AxonVoiceAI.KnowledgeBase.Retrieval;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.KnowledgeBase.Controllers;

[ApiController]
[ApiExplorerSettings(IgnoreApi = true)]
[Route("internal/agents/{agentId:guid}/knowledge")]
public sealed class KnowledgeContextController : ControllerBase
{
    private readonly KnowledgeBaseDbContext _db;
    private readonly IKnowledgeRetriever _knowledgeRetriever;

    public KnowledgeContextController(
        KnowledgeBaseDbContext db,
        IKnowledgeRetriever knowledgeRetriever)
    {
        _db = db;
        _knowledgeRetriever = knowledgeRetriever;
    }

    [HttpGet("context")]
    [AllowAnonymous]
    public async Task<IActionResult> GetKnowledgeContextAsync(
        Guid agentId,
        [FromQuery] Guid tenantId,
        [FromQuery] string query,
        CancellationToken ct)
    {
        if (tenantId == Guid.Empty)
            return BadRequest(new { error = "tenantId is required." });

        if (string.IsNullOrWhiteSpace(query))
            return BadRequest(new { error = "query is required." });

        var hasReadyDocuments = await _db.KnowledgeDocuments.AnyAsync(
            document => document.AgentId == agentId
                && document.TenantId == tenantId
                && document.Status == "ready",
            ct);

        if (!hasReadyDocuments)
            return Ok(Array.Empty<KnowledgeChunkDto>());

        var chunks = await _knowledgeRetriever.RetrieveRelevantChunksAsync(
            tenantId,
            agentId,
            query,
            ct);

        return Ok(chunks);
    }
}