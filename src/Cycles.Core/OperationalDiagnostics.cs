namespace Cycles.Core;

public sealed record OperationalDiagnostics(
    Guid? CycleId,
    string? CycleName,
    CycleStatus? CycleStatus,
    int? CurrentTickNumber,
    int? TickLengthMinutes,
    DateTimeOffset? LastCompletedAt,
    DateTimeOffset? NextDueAt,
    bool IsTickDue,
    bool RequiresRecovery,
    int CompletedTickLogs,
    int FailedTickLogs,
    int RunningTickLogs,
    int PendingOrders,
    int OrdersDueNextTick,
    int QueuedShipConstructions,
    int ConstructionsDueNextTick);

public static class OperationalDiagnosticsService
{
    public static OperationalDiagnostics Create(GameState state, DateTimeOffset now)
    {
        var cycle = state.Cycles
            .OrderBy(cycle => cycle.Status == CycleStatus.Active ? 0 : cycle.Status == CycleStatus.RecoveryRequired ? 1 : 2)
            .ThenByDescending(cycle => cycle.StartAt)
            .FirstOrDefault();
        if (cycle is null)
        {
            return new OperationalDiagnostics(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                false,
                0,
                0,
                0,
                0,
                0,
                0,
                0);
        }

        var tickLogs = state.TickLogs.Where(log => log.CycleId == cycle.CycleId).ToArray();
        var lastCompletedAt = tickLogs
            .Where(log => log.Status == TickLogStatus.Completed && log.CompletedAt.HasValue)
            .Max(log => log.CompletedAt);
        var nextDueAt = lastCompletedAt.HasValue
            ? lastCompletedAt.Value.AddMinutes(Math.Max(1, cycle.TickLengthMinutes))
            : cycle.StartAt;
        var nextTick = cycle.CurrentTickNumber + 1;
        var pendingOrders = state.FleetOrders
            .Where(order => order.CycleId == cycle.CycleId && order.Status == FleetOrderStatus.Pending)
            .ToArray();
        var queuedConstructions = state.ShipConstructions
            .Where(construction => construction.CycleId == cycle.CycleId
                                   && construction.Status == ShipConstructionStatus.Queued)
            .ToArray();

        return new OperationalDiagnostics(
            cycle.CycleId,
            cycle.Name,
            cycle.Status,
            cycle.CurrentTickNumber,
            cycle.TickLengthMinutes,
            lastCompletedAt,
            nextDueAt,
            cycle.Status == CycleStatus.Active && now >= nextDueAt,
            cycle.Status == CycleStatus.RecoveryRequired,
            tickLogs.Count(log => log.Status == TickLogStatus.Completed),
            tickLogs.Count(log => log.Status == TickLogStatus.Failed),
            tickLogs.Count(log => log.Status == TickLogStatus.Running),
            pendingOrders.Length,
            pendingOrders.Count(order => order.ExecuteAfterTick <= nextTick),
            queuedConstructions.Length,
            queuedConstructions.Count(construction => construction.CompleteAfterTick <= nextTick));
    }
}
