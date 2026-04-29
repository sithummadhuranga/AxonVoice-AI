namespace AxonVoiceAI.AgentConfig.Data.Entities;

public class Order
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public Guid SessionId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string CustomerLanguage { get; set; } = string.Empty;
    public string ItemsJson { get; set; } = "[]";
    public string OrderType { get; set; } = "pickup";
    public string? DeliveryAddress { get; set; }
    public decimal TotalAmount { get; set; }
    public string ConfirmationCode { get; set; } = string.Empty;
    public string Status { get; set; } = "received";
    public DateTimeOffset CreatedAt { get; set; }

    public Agent Agent { get; set; } = null!;
}
