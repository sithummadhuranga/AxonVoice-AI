namespace AxonVoiceAI.ConversationStore.Data.Entities;

public class ConversationSession
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public string CallerIdentifier { get; set; } = string.Empty;
    public string Language { get; set; } = "en";
    public string Status { get; set; } = "active";
    public string? Summary { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public int? DurationSeconds { get; set; }

    public ICollection<SessionFunctionCall> FunctionCalls { get; set; } = [];
    public ICollection<WebhookDelivery> WebhookDeliveries { get; set; } = [];
}

public class SessionFunctionCall
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public string FunctionName { get; set; } = string.Empty;
    public string? ArgumentsJson { get; set; }
    public string? ResultJson { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTimeOffset CalledAt { get; set; }
    public int DurationMs { get; set; }

    public ConversationSession Session { get; set; } = null!;
}

public class WebhookDelivery
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid TenantId { get; set; }
    public string WebhookUrl { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public int AttemptCount { get; set; }
    public string Status { get; set; } = "pending";
    public int? LastHttpStatus { get; set; }
    public string? LastErrorMessage { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? DeliveredAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }

    public ConversationSession Session { get; set; } = null!;
}
