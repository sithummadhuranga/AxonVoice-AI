using System.Net.Http.Json;
using AxonVoiceAI.Shared.Contracts;

namespace AxonVoiceAI.SessionRelay.Repositories;

public sealed class AgentConfigAvailabilityRepository : IAvailabilityRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentConfigAvailabilityRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<AvailabilityQueryResult> QueryAvailabilityAsync(
        Guid agentId,
        DateTimeOffset requestedDatetime,
        int partySize,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("agent-config");
        var requestUri =
            $"/internal/agents/{agentId:D}/tools/availability?requestedDatetime={Uri.EscapeDataString(requestedDatetime.ToString("O"))}&partySize={partySize}";

        using var response = await client.GetAsync(requestUri, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<AvailabilityQueryResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Availability response was empty for agent {agentId}.");
    }
}