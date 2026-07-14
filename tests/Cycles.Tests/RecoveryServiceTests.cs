using Cycles.Core;

namespace Cycles.Tests;

public sealed class RecoveryServiceTests
{
    [Fact]
    public void SuspiciousRunningAttemptCanBeMarkedAbandonedAndAudited()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var tickLog = new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-10),
            Status = TickLogStatus.Running,
            DiagnosticLog = "host stopped responding"
        };
        state.TickLogs.Add(tickLog);

        var auditEvent = RecoveryService.MarkTickAbandoned(
            state,
            tickLog.TickLogId,
            "operator-1",
            "confirmed the worker process no longer exists",
            TestState.Now);

        Assert.Equal(TickLogStatus.Failed, tickLog.Status);
        Assert.Equal(TestState.Now, tickLog.CompletedAt);
        Assert.Contains("host stopped responding", tickLog.DiagnosticLog);
        Assert.Contains("operator-1", tickLog.DiagnosticLog);
        Assert.Equal(CycleStatus.RecoveryRequired, cycle.Status);
        Assert.Equal(EventType.TickAbandoned, auditEvent.EventType);
        Assert.Equal(EventSeverity.High, auditEvent.Severity);
        Assert.Contains("confirmed the worker process", auditEvent.FactJson);
        Assert.Throws<InvalidOperationException>(() => new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddMinutes(1)));
    }

    [Fact]
    public void AbandonmentRefusesUnsafeTransitions()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var recentRunning = new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-2),
            Status = TickLogStatus.Running
        };
        var completed = new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 0,
            StartedAt = TestState.Now.AddMinutes(-20),
            CompletedAt = TestState.Now.AddMinutes(-19),
            Status = TickLogStatus.Completed
        };
        state.TickLogs.AddRange([recentRunning, completed]);

        Assert.Throws<InvalidOperationException>(() => RecoveryService.MarkTickAbandoned(
            state, Guid.NewGuid(), "operator", "reason", TestState.Now));
        Assert.Throws<InvalidOperationException>(() => RecoveryService.MarkTickAbandoned(
            state, completed.TickLogId, "operator", "reason", TestState.Now));
        Assert.Throws<InvalidOperationException>(() => RecoveryService.MarkTickAbandoned(
            state, recentRunning.TickLogId, "operator", "reason", TestState.Now));
        Assert.Throws<InvalidOperationException>(() => RecoveryService.MarkTickAbandoned(
            state, recentRunning.TickLogId, "", "reason", TestState.Now));

        Assert.Equal(TickLogStatus.Running, recentRunning.Status);
        Assert.Equal(CycleStatus.Active, cycle.Status);
        Assert.DoesNotContain(state.Events, item => item.EventType == EventType.TickAbandoned);
    }
}
