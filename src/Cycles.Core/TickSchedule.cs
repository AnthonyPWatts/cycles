namespace Cycles.Core;

public static class TickSchedule
{
    public static bool IsDue(GameState state, DateTimeOffset now)
    {
        var cycle = state.GetActiveCycle();
        if (cycle is null)
        {
            return false;
        }

        var lastCompletedAt = state.TickLogs
            .Where(item => item.CycleId == cycle.CycleId
                           && item.Status == TickLogStatus.Completed
                           && item.CompletedAt.HasValue)
            .Max(item => item.CompletedAt);

        return IsDue(cycle, lastCompletedAt, now);
    }

    public static bool IsDue(Cycle cycle, DateTimeOffset? lastCompletedAt, DateTimeOffset now)
    {
        if (cycle.Status != CycleStatus.Active
            || cycle.SchedulingMode != CycleSchedulingMode.Scheduled)
        {
            return false;
        }

        var cadence = TimeSpan.FromMinutes(Math.Max(1, cycle.TickLengthMinutes));
        var nextDueAt = cycle.NextTickAt
            ?? (lastCompletedAt.HasValue
                ? lastCompletedAt.Value + cadence
                : cycle.StartAt);

        return now >= nextDueAt;
    }
}
