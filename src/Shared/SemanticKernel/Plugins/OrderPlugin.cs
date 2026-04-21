using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.Shared.SemanticKernel.Plugins;

/// <summary>
/// Handles order-taking workflows for agents configured for food ordering, product sales, etc.
/// The order flow is distinct from booking: no time slot, no capacity check, immediate confirmation.
/// </summary>
public sealed class OrderPlugin
{
    [KernelFunction("place_order")]
    [Description("Record an order collected verbally from the customer after confirming all items and the total")]
    public Task<OrderResult> PlaceOrderAsync(
        [Description("JSON array of order items e.g. [{\"item\":\"Kottu\",\"qty\":2,\"price\":450}]")] string itemsJson,
        [Description("Customer name")] string customerName,
        [Description("Customer phone number")] string customerPhone,
        [Description("The agent ID for this session")] string agentId,
        [Description("The session ID")] string sessionId,
        [Description("Detected session language code")] string detectedLanguage,
        [Description("Delivery or pickup")] string orderType,
        [Description("Delivery address if applicable")] string? deliveryAddress = null,
        CancellationToken cancellationToken = default)
    {
        // Implementation will be added when order-taking agents are configured.
        // The plugin skeleton is here to allow Semantic Kernel to discover the function declaration
        // and generate the tool definition for the Gemini system prompt.
        throw new NotImplementedException("Order plugin is pending implementation in a future phase.");
    }
}

public record OrderResult(
    Guid OrderId,
    string ConfirmationCode,
    decimal TotalAmount,
    string EstimatedReadyTime);
