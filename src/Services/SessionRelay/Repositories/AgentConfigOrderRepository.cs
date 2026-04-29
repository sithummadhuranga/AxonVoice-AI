using System.Net.Http.Json;
using AxonVoiceAI.Shared.Contracts;

namespace AxonVoiceAI.SessionRelay.Repositories;

public sealed class AgentConfigOrderRepository : IOrderRepository
{
    private readonly IHttpClientFactory _httpClientFactory;

    public AgentConfigOrderRepository(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<OrderCreationResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var client = _httpClientFactory.CreateClient("agent-config");
        using var response = await client.PostAsJsonAsync(
            $"/internal/agents/{request.AgentId:D}/tools/orders",
            request,
            cancellationToken: ct);

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<OrderCreationResult>(cancellationToken: ct)
            ?? throw new InvalidOperationException($"Order response was empty for agent {request.AgentId}.");
    }
}
