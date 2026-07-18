using System.Text.Json;

namespace Cycles.Core;

public static class RecoveryService
{
    public static EventRecord MarkTickAbandoned(
        GameState state,
        Guid tickLogId,
        string operatorName,
        string reason,
        DateTimeOffset now,
        TimeSpan? runningTickSuspicionThreshold = null)
    {
        ValidateOperatorAndReason(operatorName, reason, "Tick abandonment");
        var suspicionThreshold = runningTickSuspicionThreshold
            ?? OperationalDiagnosticsService.DefaultRunningTickSuspicionThreshold;
        if (suspicionThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(runningTickSuspicionThreshold), "Running-tick suspicion threshold must be positive.");
        }

        var tickLog = state.TickLogs.SingleOrDefault(log => log.TickLogId == tickLogId)
            ?? throw new InvalidOperationException("Tick attempt was not found.");
        if (tickLog.Status != TickLogStatus.Running)
        {
            throw new InvalidOperationException("Only a running tick attempt can be marked abandoned.");
        }

        var age = now - tickLog.StartedAt;
        if (age < suspicionThreshold)
        {
            throw new InvalidOperationException($"The running tick attempt is {age.TotalMinutes:0.##} minute(s) old and is not yet suspicious under the {suspicionThreshold.TotalMinutes:0.##}-minute threshold.");
        }

        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == tickLog.CycleId)
            ?? throw new InvalidOperationException("The tick attempt's Cycle was not found.");

        tickLog.Status = TickLogStatus.Failed;
        tickLog.CompletedAt = now;
        var abandonmentDiagnostic = $"Marked abandoned by {operatorName} at {now:u}. Reason: {reason}";
        tickLog.DiagnosticLog = string.IsNullOrWhiteSpace(tickLog.DiagnosticLog)
            ? abandonmentDiagnostic
            : $"{tickLog.DiagnosticLog.TrimEnd()}{Environment.NewLine}{abandonmentDiagnostic}";
        cycle.Status = CycleStatus.RecoveryRequired;

        var abandonmentEvent = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = tickLog.TickNumber,
            EventType = EventType.TickAbandoned,
            Severity = EventSeverity.High,
            DisplayText = $"Tick {tickLog.TickNumber} was marked abandoned by {operatorName} and requires recovery.",
            FactJson = JsonSerializer.Serialize(new
            {
                cycleId = cycle.CycleId,
                tickLogId,
                tickLog.TickNumber,
                operatorName,
                reason,
                startedAt = tickLog.StartedAt,
                abandonedAt = now,
                ageSeconds = Math.Max(0, (long)age.TotalSeconds)
            }, GameStateJson.Options),
            CreatedAt = now
        };

        state.Events.Add(abandonmentEvent);
        return abandonmentEvent;
    }

    public static EventRecord ClearRecovery(
        GameState state,
        Guid cycleId,
        string operatorName,
        string reason,
        DateTimeOffset now)
    {
        ValidateOperatorAndReason(operatorName, reason, "Recovery clear");

        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == cycleId)
            ?? throw new InvalidOperationException("Cycle was not found.");

        if (cycle.Status != CycleStatus.RecoveryRequired)
        {
            throw new InvalidOperationException("Only recovery-required cycles can be cleared.");
        }

        var runningLogs = state.TickLogs
            .Where(log => log.CycleId == cycleId && log.Status == TickLogStatus.Running)
            .OrderBy(log => log.StartedAt)
            .ToArray();

        if (runningLogs.Length > 0)
        {
            throw new InvalidOperationException("Recovery cannot be cleared while the cycle has unfinished running tick logs.");
        }

        var failedTickNumbers = state.TickLogs
            .Where(log => log.CycleId == cycleId && log.Status == TickLogStatus.Failed)
            .OrderBy(log => log.TickNumber)
            .Select(log => log.TickNumber)
            .Distinct()
            .ToArray();

        cycle.Status = CycleStatus.Active;
        cycle.TurnStage = TurnResolutionStage.CommandOpen;

        var recoveryEvent = new EventRecord
        {
            CycleId = cycleId,
            TickNumber = cycle.CurrentTickNumber,
            EventType = EventType.RecoveryCleared,
            Severity = EventSeverity.High,
            DisplayText = $"Recovery was cleared for {cycle.Name} by {operatorName}.",
            FactJson = JsonSerializer.Serialize(new
            {
                cycleId,
                operatorName,
                reason,
                failedTickNumbers
            }, GameStateJson.Options),
            CreatedAt = now
        };

        state.Events.Add(recoveryEvent);
        return recoveryEvent;
    }

    private static void ValidateOperatorAndReason(string operatorName, string reason, string operation)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new InvalidOperationException($"{operation} requires an operator.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException($"{operation} requires a reason.");
        }
    }
}
