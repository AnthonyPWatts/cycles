using Cycles.Core;

namespace Cycles.Tests;

public sealed class OperationalDiagnosticsTests
{
    [Fact]
    public void ActiveCycleReportsNextDueWorkAndLogHealth()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.CurrentTickNumber = 4;
        cycle.TickLengthMinutes = 60;
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 4,
            StartedAt = TestState.Now.AddHours(-1),
            CompletedAt = TestState.Now.AddHours(-1),
            Status = TickLogStatus.Completed
        });
        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = state.Fleets.Single().FleetId,
            OrderType = FleetOrderType.Hold,
            SubmitTick = 4,
            ExecuteAfterTick = 5,
            Status = FleetOrderStatus.Pending,
            CreatedAt = TestState.Now
        });
        state.ShipConstructions.Add(new ShipConstruction
        {
            CycleId = cycle.CycleId,
            EmpireId = state.Empires.Single().EmpireId,
            ShipCount = 1,
            IndustrySpent = EconomyProcessor.ShipIndustryCost,
            StartedTick = 2,
            CompleteAfterTick = 5,
            Status = ShipConstructionStatus.Queued,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        });

        var result = OperationalDiagnosticsService.Create(state, TestState.Now);

        Assert.Equal(cycle.CycleId, result.CycleId);
        Assert.True(result.IsTickDue);
        Assert.False(result.RequiresRecovery);
        Assert.Equal(TestState.Now, result.NextDueAt);
        Assert.Equal(1, result.CompletedTickLogs);
        Assert.Equal(1, result.PendingOrders);
        Assert.Equal(1, result.OrdersDueNextTick);
        Assert.Equal(1, result.QueuedShipConstructions);
        Assert.Equal(1, result.ConstructionsDueNextTick);
    }

    [Fact]
    public void RecoveryCycleReportsBlockedTickAndRequiredAction()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.Status = CycleStatus.RecoveryRequired;
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            CompletedAt = TestState.Now,
            Status = TickLogStatus.Failed,
            DiagnosticLog = "test failure"
        });

        var result = OperationalDiagnosticsService.Create(state, TestState.Now.AddHours(1));

        Assert.Equal(CycleStatus.RecoveryRequired, result.CycleStatus);
        Assert.True(result.RequiresRecovery);
        Assert.False(result.IsTickDue);
        Assert.Equal(1, result.FailedTickLogs);
    }

    [Fact]
    public void EmptyStateReportsNoCycle()
    {
        var result = OperationalDiagnosticsService.Create(new GameState(), TestState.Now);

        Assert.Null(result.CycleId);
        Assert.Null(result.NextDueAt);
        Assert.False(result.IsTickDue);
    }

    [Fact]
    public void OldRunningAttemptIsReportedWithoutChangingState()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var tickLog = new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-6),
            Status = TickLogStatus.Running,
            DiagnosticLog = "worker host app-01\nprivate detail not shown in the summary"
        };
        state.TickLogs.Add(tickLog);

        var result = OperationalDiagnosticsService.Create(state, TestState.Now);

        var suspicious = Assert.Single(result.SuspiciousRunningTicks);
        Assert.Equal(tickLog.TickLogId, suspicious.TickLogId);
        Assert.Equal(TimeSpan.FromMinutes(6), suspicious.Elapsed);
        Assert.Equal("worker host app-01", suspicious.DiagnosticContext);
        Assert.True(suspicious.IsSuspicious);
        Assert.Equal(TickLogStatus.Running, tickLog.Status);
        Assert.Null(tickLog.CompletedAt);
        Assert.Equal(CycleStatus.Active, cycle.Status);
    }

    [Fact]
    public void RunningAttemptBelowConfiguredThresholdIsNotSuspicious()
    {
        var state = TestState.CreateSingleEmpireState();
        state.TickLogs.Add(new TickLog
        {
            CycleId = state.GetActiveCycle()!.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-6),
            Status = TickLogStatus.Running
        });

        var result = OperationalDiagnosticsService.Create(state, TestState.Now, TimeSpan.FromMinutes(10));

        Assert.Equal(TimeSpan.FromMinutes(10), result.RunningTickSuspicionThreshold);
        Assert.Empty(result.SuspiciousRunningTicks);
    }

    [Fact]
    public void FinishedAttemptReportsEndToEndDuration()
    {
        var state = TestState.CreateSingleEmpireState();
        state.TickLogs.Add(new TickLog
        {
            CycleId = state.GetActiveCycle()!.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddSeconds(-12),
            CompletedAt = TestState.Now.AddSeconds(-2),
            Status = TickLogStatus.Completed
        });

        var result = OperationalDiagnosticsService.Create(state, TestState.Now);

        Assert.Equal(TimeSpan.FromSeconds(10), Assert.Single(result.RecentFinishedTicks).Elapsed);
    }
}
