using System.Net.Http.Json;
using AxonVoiceAI.Shared.Contracts;

namespace AxonVoiceAI.SessionRelay.Repositories;

public sealed class AgentConfigBookingRepository : IBookingRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentConfigBookingRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<PendingBookingCreationResult> CreatePendingBookingAsync(
        CreatePendingBookingRequest request,
        CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("agent-config");
        using var response = await client.PostAsJsonAsync(
            $"/internal/agents/{request.AgentId:D}/tools/pending-bookings",
            request,
            cancellationToken: ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<PendingBookingCreationResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Pending booking response was empty for agent {request.AgentId}.");
    }
}