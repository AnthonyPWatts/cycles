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
}
