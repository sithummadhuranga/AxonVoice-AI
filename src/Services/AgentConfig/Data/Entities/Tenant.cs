namespace AxonVoiceAI.AgentConfig.Data.Entities;

public class Tenant
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string? ApiKeyHint { get; set; }
    public string DefaultLanguage { get; set; } = "si";
    public string? WebhookUrl { get; set; }
    public string? WebhookSecret { get; set; }
    public int RateLimitDaily { get; set; } = 1000;
    public int RateLimitConcurrent { get; set; } = 20;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<Agent> Agents { get; set; } = [];
}
