namespace Cycles.Core;

public sealed record TickAttemptDiagnostics(
    Guid TickLogId,
    Guid CycleId,
    string CycleName,
    int TickNumber,
    TickLogStatus Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    TimeSpan Elapsed,
    string? DiagnosticContext,
    bool IsSuspicious);

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
    int ConstructionsDueNextTick,
    TimeSpan RunningTickSuspicionThreshold,
    IReadOnlyList<TickAttemptDiagnostics> SuspiciousRunningTicks,
    IReadOnlyList<TickAttemptDiagnostics> RecentFinishedTicks);

public static class OperationalDiagnosticsService
{
    public static readonly TimeSpan DefaultRunningTickSuspicionThreshold = TimeSpan.FromMinutes(5);

    public static OperationalDiagnostics Create(
        GameState state,
        DateTimeOffset now,
        TimeSpan? runningTickSuspicionThreshold = null)
    {
        var suspicionThreshold = runningTickSuspicionThreshold ?? DefaultRunningTickSuspicionThreshold;
        if (suspicionThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(runningTickSuspicionThreshold), "Running-tick suspicion threshold must be positive.");
        }

        var cyclesById = state.Cycles.ToDictionary(cycle => cycle.CycleId);
        var suspiciousRunningTicks = state.TickLogs
            .Where(log => log.Status == TickLogStatus.Running && now - log.StartedAt >= suspicionThreshold)
            .OrderByDescending(log => now - log.StartedAt)
            .Select(log => ToAttemptDiagnostics(log, cyclesById, now, isSuspicious: true))
            .ToArray();
        var recentFinishedTicks = state.TickLogs
            .Where(log => log.Status is TickLogStatus.Completed or TickLogStatus.Failed && log.CompletedAt.HasValue)
            .OrderByDescending(log => log.CompletedAt)
            .Take(10)
            .Select(log => ToAttemptDiagnostics(log, cyclesById, now, isSuspicious: false))
            .ToArray();

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
                0,
                suspicionThreshold,
                suspiciousRunningTicks,
                recentFinishedTicks);
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
            queuedConstructions.Count(construction => construction.CompleteAfterTick <= nextTick),
            suspicionThreshold,
            suspiciousRunningTicks,
            recentFinishedTicks);
    }

    private static TickAttemptDiagnostics ToAttemptDiagnostics(
        TickLog log,
        IReadOnlyDictionary<Guid, Cycle> cyclesById,
        DateTimeOffset now,
        bool isSuspicious)
    {
        var finishedAt = log.CompletedAt ?? now;
        cyclesById.TryGetValue(log.CycleId, out var cycle);
        return new TickAttemptDiagnostics(
            log.TickLogId,
            log.CycleId,
            cycle?.Name ?? "Unknown cycle",
            log.TickNumber,
            log.Status,
            log.StartedAt,
            log.CompletedAt,
            finishedAt >= log.StartedAt ? finishedAt - log.StartedAt : TimeSpan.Zero,
            SummariseDiagnostic(log.DiagnosticLog),
            isSuspicious);
    }

    private static string? SummariseDiagnostic(string diagnosticLog)
    {
        if (string.IsNullOrWhiteSpace(diagnosticLog))
        {
            return null;
        }

        var firstLine = diagnosticLog
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();
        return firstLine is null
            ? null
            : firstLine[..Math.Min(firstLine.Length, 256)];
    }
}
