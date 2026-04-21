using System.ComponentModel;
using AxonVoiceAI.Shared.Contracts;
using AxonVoiceAI.Shared.DTOs;
using Microsoft.SemanticKernel;

namespace AxonVoiceAI.Shared.SemanticKernel.Plugins;

public sealed class AvailabilityPlugin
{
    private readonly IAvailabilityRepository _availabilityRepository;

    public AvailabilityPlugin(IAvailabilityRepository availabilityRepository)
    {
        _availabilityRepository = availabilityRepository;
    }

    [KernelFunction("check_availability")]
    [Description("Check if a time slot is available for a given party size")]
    public async Task<AvailabilityResult> CheckAvailabilityAsync(
        [Description("Date in YYYY-MM-DD format")] string date,
        [Description("Time in HH:MM format (24h)")] string time,
        [Description("Number of guests")] int partySize,
        [Description("The agent ID for this session")] string agentId,
        CancellationToken cancellationToken = default)
    {
        if (!DateOnly.TryParse(date, out var parsedDate))
            return new AvailabilityResult(false, 0, ["Invalid date format — use YYYY-MM-DD"]);

        if (!TimeOnly.TryParse(time, out var parsedTime))
            return new AvailabilityResult(false, 0, ["Invalid time format — use HH:MM"]);

        if (!Guid.TryParse(agentId, out var parsedAgentId))
            return new AvailabilityResult(false, 0, []);

        var requestedDatetime = new DateTimeOffset(
            parsedDate.ToDateTime(parsedTime),
            TimeSpan.Zero);

        var result = await _availabilityRepository.QueryAvailabilityAsync(
            parsedAgentId, requestedDatetime, partySize, cancellationToken);

        var alternatives = result.ClosestAlternatives
            .Select(dt => dt.ToString("yyyy-MM-dd HH:mm"))
            .ToArray();

        return new AvailabilityResult(result.Available, result.RemainingCapacity, alternatives);
    }
}
