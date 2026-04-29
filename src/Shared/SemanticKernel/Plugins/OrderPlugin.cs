using System.ComponentModel;
using System.Text.Json;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.Shared.SemanticKernel.Plugins;

/// <summary>
/// Handles order-taking workflows for agents configured for food ordering, product sales, etc.
/// The order flow is distinct from booking: no time slot, no capacity check, immediate confirmation.
/// agentId, tenantId, sessionId, and detectedLanguage are injected server-side by
/// FunctionCallInterceptor and are never exposed in the Gemini tool declaration.
/// </summary>
public sealed class OrderPlugin
{
    private readonly IOrderRepository _orderRepository;

    public OrderPlugin(IOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    [KernelFunction("place_order")]
    [Description("Record a verbally confirmed customer order after all items and the total have been agreed")]
    public async Task<OrderResult> PlaceOrderAsync(
        [Description("JSON array of order items e.g. [{\"item\":\"Kottu\",\"qty\":2,\"price\":450}]")] string itemsJson,
        [Description("Customer full name as spoken")] string customerName,
        [Description("Customer phone number")] string customerPhone,
        [Description("Agent ID — injected by platform, do not ask the caller")] string agentId,
        [Description("Tenant ID — injected by platform, do not ask the caller")] string tenantId,
        [Description("Session ID — injected by platform, do not ask the caller")] string sessionId,
        [Description("Detected language code — injected by platform")] string detectedLanguage,
        [Description("'delivery' or 'pickup'")] string orderType,
        [Description("Delivery address — required when orderType is 'delivery'")] string? deliveryAddress = null,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(agentId, out var parsedAgentId))
            throw new ArgumentException($"Invalid agent ID '{agentId}'.");

        if (!Guid.TryParse(tenantId, out var parsedTenantId))
            throw new ArgumentException($"Invalid tenant ID '{tenantId}'.");

        if (!Guid.TryParse(sessionId, out var parsedSessionId))
            throw new ArgumentException($"Invalid session ID '{sessionId}'.");

        var normalizedOrderType = string.Equals(orderType, "delivery", StringComparison.OrdinalIgnoreCase)
            ? "delivery"
            : "pickup";

        var totalAmount = ComputeTotalFromItemsJson(itemsJson);

        var request = new CreateOrderRequest(
            AgentId: parsedAgentId,
            TenantId: parsedTenantId,
            SessionId: parsedSessionId,
            CustomerName: customerName,
            CustomerPhone: customerPhone,
            DetectedLanguage: detectedLanguage,
            ItemsJson: itemsJson,
            OrderType: normalizedOrderType,
            DeliveryAddress: normalizedOrderType == "delivery" ? deliveryAddress : null,
            TotalAmount: totalAmount);

        var result = await _orderRepository.CreateOrderAsync(request, cancellationToken);

        return new OrderResult(
            result.OrderId,
            result.ConfirmationCode,
            result.TotalAmount,
            result.EstimatedReadyTime.ToString("HH:mm"));
    }

    private static decimal ComputeTotalFromItemsJson(string itemsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(itemsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return 0m;

            var total = 0m;
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var qty = item.TryGetProperty("qty", out var qtyEl) && qtyEl.TryGetDecimal(out var q) ? q : 0m;
                var price = item.TryGetProperty("price", out var priceEl) && priceEl.TryGetDecimal(out var p) ? p : 0m;
                total += qty * price;
            }

            return total;
        }
        catch (JsonException)
        {
            return 0m;
        }
    }
}

public record OrderResult(
    Guid OrderId,
    string ConfirmationCode,
    decimal TotalAmount,
    string EstimatedReadyTime);

