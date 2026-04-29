namespace AxonVoiceAI.Shared.Contracts;

public interface IOrderRepository
{
    Task<OrderCreationResult> CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
}

public record CreateOrderRequest(
    Guid AgentId,
    Guid TenantId,
    Guid SessionId,
    string CustomerName,
    string CustomerPhone,
    string DetectedLanguage,
    string ItemsJson,
    string OrderType,
    string? DeliveryAddress,
    decimal TotalAmount);

public record OrderCreationResult(
    Guid OrderId,
    string ConfirmationCode,
    decimal TotalAmount,
    DateTimeOffset EstimatedReadyTime);
