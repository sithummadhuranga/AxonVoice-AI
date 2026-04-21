namespace AxonVoiceAI.Shared.Contracts;

public interface IAvailabilityRepository
{
    Task<AvailabilityQueryResult> QueryAvailabilityAsync(
        Guid agentId,
        DateTimeOffset requestedDatetime,
        int partySize,
        CancellationToken ct);
}

public record AvailabilityQueryResult(
    bool Available,
    int RemainingCapacity,
    IReadOnlyList<DateTimeOffset> ClosestAlternatives);
