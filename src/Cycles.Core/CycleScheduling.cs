namespace Cycles.Core;

public static class CycleScheduling
{
    public static DateTimeOffset NextAfterCompletion(Cycle cycle, DateTimeOffset completedAt) =>
        completedAt.AddMinutes(Math.Max(1, cycle.TickLengthMinutes));

    public static void NormalizePersistedSchedule(
        GameState state,
        bool upgradeLegacyFormat = false)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (upgradeLegacyFormat)
        {
            foreach (var configuration in state.CycleConfigurations)
            {
                configuration.SchedulingMode = CycleSchedulingMode.Scheduled;
            }
        }

        foreach (var cycle in state.Cycles)
        {
            if (upgradeLegacyFormat)
            {
                cycle.SchedulingMode = CycleSchedulingMode.Scheduled;
                cycle.NextTickAt = null;
            }

            if (cycle.Status != CycleStatus.Active
                || cycle.SchedulingMode != CycleSchedulingMode.Scheduled)
            {
                cycle.NextTickAt = null;
                continue;
            }

            if (cycle.NextTickAt.HasValue)
            {
                continue;
            }

            var lastCompletedAt = state.TickLogs
                .Where(log => log.CycleId == cycle.CycleId
                              && log.Status == TickLogStatus.Completed
                              && log.CompletedAt.HasValue)
                .Max(log => log.CompletedAt);
            cycle.NextTickAt = lastCompletedAt.HasValue
                ? NextAfterCompletion(cycle, lastCompletedAt.Value)
                : cycle.StartAt;
        }
    }
}
