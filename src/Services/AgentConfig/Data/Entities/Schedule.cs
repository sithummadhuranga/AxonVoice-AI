namespace AxonVoiceAI.AgentConfig.Data.Entities;

public class BusinessHours
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    /// <summary>Day of week: 0 = Sunday, 6 = Saturday — matches PostgreSQL EXTRACT(DOW).</summary>
    public short DayOfWeek { get; set; }
    public TimeOnly OpenTime { get; set; }
    public TimeOnly CloseTime { get; set; }
    public int SlotDurationMinutes { get; set; } = 90;
    public int MaxCapacityPerSlot { get; set; } = 30;
    public bool IsActive { get; set; } = true;

    public Agent Agent { get; set; } = null!;
}

public class ClosedDate
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly Date { get; set; }
    public string? Reason { get; set; }

    public Agent Agent { get; set; } = null!;
}
