using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Contracts;

namespace AxonVoiceAI.AgentConfig.Data.Repositories;

public sealed class OrderRepository : IOrderRepository
{
    private readonly AgentConfigDbContext _db;

    public OrderRepository(AgentConfigDbContext db)
    {
        _db = db;
    }

    public async Task<OrderCreationResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct)
    {
        var tenantId = request.TenantId != Guid.Empty
            ? request.TenantId
            : await ResolveTenantIdFromAgentAsync(request.AgentId, ct);

        var orderId = Guid.NewGuid();
        var confirmationCode = orderId.ToString("N")[..8].ToUpperInvariant();
        var estimatedReadyTime = DateTimeOffset.UtcNow.AddMinutes(
            string.Equals(request.OrderType, "delivery", StringComparison.Ordinal) ? 45 : 20);

        var order = new Order
        {
            Id = orderId,
            AgentId = request.AgentId,
            TenantId = tenantId,
            SessionId = request.SessionId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            CustomerLanguage = request.DetectedLanguage,
            ItemsJson = request.ItemsJson,
            OrderType = request.OrderType,
            DeliveryAddress = request.DeliveryAddress,
            TotalAmount = request.TotalAmount,
            ConfirmationCode = confirmationCode,
            Status = "received",
            CreatedAt = DateTimeOffset.UtcNow,
        };

        _db.Orders.Add(order);
        await _db.SaveChangesAsync(ct);

        return new OrderCreationResult(orderId, confirmationCode, order.TotalAmount, estimatedReadyTime);
    }

    private async Task<Guid> ResolveTenantIdFromAgentAsync(Guid agentId, CancellationToken ct)
    {
        var agent = await _db.Agents.FindAsync([agentId], ct)
            ?? throw new InvalidOperationException($"Agent {agentId} not found.");
        return agent.TenantId;
    }
}
