using System.Text.Json;

namespace Cycles.Core;

public static class RecoveryService
{
    public static EventRecord ClearRecovery(
        GameState state,
        Guid cycleId,
        string operatorName,
        string reason,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(operatorName))
        {
            throw new InvalidOperationException("Recovery clear requires an operator.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Recovery clear requires a reason.");
        }

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
}
