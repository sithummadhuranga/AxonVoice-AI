using AxonVoiceAI.AgentConfig.Data;
using AxonVoiceAI.AgentConfig.Data.Entities;
using AxonVoiceAI.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace AxonVoiceAI.AgentConfig.Data.Repositories;

public sealed class AvailabilityRepository : IAvailabilityRepository
{
    private readonly AgentConfigDbContext _db;

    public AvailabilityRepository(AgentConfigDbContext db)
    {
        _db = db;
    }

    public async Task<AvailabilityQueryResult> QueryAvailabilityAsync(
        Guid agentId,
        DateTimeOffset requestedDatetime,
        int partySize,
        CancellationToken ct)
    {
        // This query mirrors the dual-table availability SQL from ARCHITECTURE.md Section 4.5.
        // It must execute at the database level to ensure atomicity — do not rewrite as application logic.
        var sql = """
            WITH slot_capacity AS (
              SELECT bh.max_capacity_per_slot
              FROM business_hours bh
              WHERE bh.agent_id = {0}
                AND bh.day_of_week = EXTRACT(DOW FROM {1}::TIMESTAMPTZ)
                AND bh.open_time <= {1}::TIME
                AND bh.close_time > {1}::TIME
                AND bh.is_active = TRUE
                AND NOT EXISTS (
                  SELECT 1 FROM closed_dates cd
                  WHERE cd.agent_id = {0}
                    AND cd.closed_date = {1}::DATE
                )
            ),
            booked AS (
              SELECT COALESCE(SUM(party_size), 0) AS confirmed_total
              FROM confirmed_bookings
              WHERE agent_id = {0}
                AND booking_datetime = {1}
                AND status = 'confirmed'
            ),
            held AS (
              SELECT COALESCE(SUM(party_size), 0) AS pending_total
              FROM pending_bookings
              WHERE agent_id = {0}
                AND requested_datetime = {1}
                AND status = 'pending'
                AND expires_at > NOW()
            )
            SELECT
              sc.max_capacity_per_slot - b.confirmed_total - h.pending_total AS remaining_capacity,
              (sc.max_capacity_per_slot - b.confirmed_total - h.pending_total) >= {2} AS available
            FROM slot_capacity sc
            CROSS JOIN booked b
            CROSS JOIN held h;
            """;

        var rows = await _db.Database
            .SqlQueryRaw<AvailabilityRow>(sql, agentId, requestedDatetime, partySize)
            .ToListAsync(ct);

        if (rows.Count == 0)
        {
            // No matching business_hours row — the requested slot is outside business hours.
            var alternatives = await FindClosestAlternativeSlotsAsync(agentId, requestedDatetime, partySize, ct);
            return new AvailabilityQueryResult(false, 0, alternatives);
        }

        var row = rows[0];
        var closestAlternatives = row.Available
            ? []
            : await FindClosestAlternativeSlotsAsync(agentId, requestedDatetime, partySize, ct);

        return new AvailabilityQueryResult(row.Available, row.RemainingCapacity, closestAlternatives);
    }

    private async Task<IReadOnlyList<DateTimeOffset>> FindClosestAlternativeSlotsAsync(
        Guid agentId,
        DateTimeOffset requestedDatetime,
        int partySize,
        CancellationToken ct)
    {
        // Find up to 3 available slots on the same day or the next available business day.
        var businessHours = await _db.BusinessHours
            .Where(bh => bh.AgentId == agentId && bh.IsActive)
            .OrderBy(bh => bh.DayOfWeek)
            .ToListAsync(ct);

        var alternatives = new List<DateTimeOffset>();
        var checkDate = requestedDatetime.Date;

        for (var daysChecked = 0; daysChecked < 14 && alternatives.Count < 3; daysChecked++)
        {
            var dayOfWeek = (short)checkDate.DayOfWeek;
            var hoursForDay = businessHours.FirstOrDefault(bh => bh.DayOfWeek == dayOfWeek);

            if (hoursForDay is not null)
            {
                var isClosed = await _db.ClosedDates
                    .AnyAsync(cd => cd.AgentId == agentId && cd.Date == DateOnly.FromDateTime(checkDate), ct);

                if (!isClosed)
                {
                    var slotTime = checkDate.Add(hoursForDay.OpenTime.ToTimeSpan());
                    while (slotTime.TimeOfDay < hoursForDay.CloseTime.ToTimeSpan() && alternatives.Count < 3)
                    {
                        var slotOffset = new DateTimeOffset(slotTime, TimeSpan.Zero);
                        var result = await QueryAvailabilityAsync(agentId, slotOffset, partySize, ct);
                        if (result.Available && slotOffset != requestedDatetime)
                            alternatives.Add(slotOffset);

                        slotTime = slotTime.AddMinutes(hoursForDay.SlotDurationMinutes);
                    }
                }
            }

            checkDate = checkDate.AddDays(1);
        }

        return alternatives.AsReadOnly();
    }

    // Projection type for raw SQL query result.
    private sealed class AvailabilityRow
    {
        public int RemainingCapacity { get; set; }
        public bool Available { get; set; }
    }
}
