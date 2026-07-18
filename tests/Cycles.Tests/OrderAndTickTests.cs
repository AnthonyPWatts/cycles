using Cycles.Core;

namespace Cycles.Tests;

public sealed class OrderAndTickTests
{
    [Fact]
    public void EmptyTickAdvancesOnceAndCreatesCompletedTickLog()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var committedCycle = state.GetActiveCycle()!;

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(1, result.TickNumber);
        Assert.Equal(1, committedCycle.CurrentTickNumber);
        var tickLog = Assert.Single(state.TickLogs);
        Assert.Equal(TickLogStatus.Completed, tickLog.Status);
    }

    [Fact]
    public void RunningTickPreventsDuplicateProcessing()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            Status = TickLogStatus.Running
        });

        Assert.Throws<InvalidOperationException>(() => new TickEngine().RunTick(state, cycle.CycleId, TestState.Now));
    }

    [Fact]
    public void FutureOrdersAreNotProcessedEarly()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        order.ExecuteAfterTick = 3;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var committedOrder = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);
        var committedCycle = state.GetActiveCycle()!;

        Assert.Equal(FleetOrderStatus.Pending, committedOrder.Status);
        Assert.Equal(0, committedOrder.SubmitTick);
        Assert.Null(committedOrder.ProcessedTick);
        Assert.Equal(1, committedCycle.CurrentTickNumber);
    }

    [Fact]
    public void FleetOrderReplacementRequiresTheCurrentPendingOrderId()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var pending = OrderService.SubmitHoldOrder(state, fleet.FleetId, TestState.Now);

        var error = Assert.Throws<FleetOrderReplacementConflictException>(
            () => OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now));

        Assert.Contains("Confirm its replacement", error.Message, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Pending, pending.Status);
        Assert.Single(state.FleetOrders);
    }

    [Fact]
    public void ConfirmedFleetOrderReplacementSupersedesHistoryAndOnlyExecutesTheNewIntention()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var pending = OrderService.SubmitHoldOrder(state, fleet.FleetId, TestState.Now);

        var replacement = OrderService.SubmitMoveOrder(
            state,
            fleet.FleetId,
            destination.SystemId,
            TestState.Now.AddSeconds(1),
            pending.FleetOrderId);

        Assert.Equal(FleetOrderStatus.Superseded, pending.Status);
        Assert.Equal(cycle.CurrentTickNumber, pending.ProcessedTick);
        Assert.Equal(replacement.FleetOrderId, pending.SupersededByOrderId);
        Assert.Equal(FleetOrderStatus.Pending, replacement.Status);
        Assert.Single(state.FleetOrders, order => order.Status == FleetOrderStatus.Pending);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddMinutes(1));

        var committedReplacement = state.FleetOrders.Single(order => order.FleetOrderId == replacement.FleetOrderId);
        var committedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        Assert.Equal(FleetOrderStatus.Processed, committedReplacement.Status);
        Assert.Equal(destination.SystemId, committedFleet.CurrentSystemId);
        Assert.DoesNotContain(state.Events, item => item.EventType == EventType.FleetHeld);
    }

    [Fact]
    public void IdenticalFleetOrderSubmissionIsIdempotent()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var pending = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        var duplicate = OrderService.SubmitMoveOrder(
            state,
            fleet.FleetId,
            destination.SystemId,
            TestState.Now.AddSeconds(1));

        Assert.Same(pending, duplicate);
        Assert.Single(state.FleetOrders);
    }

    [Fact]
    public void StaleFleetOrderReplacementDoesNotChangeThePendingIntention()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var pending = OrderService.SubmitHoldOrder(state, fleet.FleetId, TestState.Now);

        Assert.Throws<FleetOrderReplacementConflictException>(() => OrderService.SubmitMoveOrder(
            state,
            fleet.FleetId,
            destination.SystemId,
            TestState.Now.AddSeconds(1),
            Guid.NewGuid()));

        Assert.Equal(FleetOrderStatus.Pending, pending.Status);
        Assert.Null(pending.SupersededByOrderId);
        Assert.Single(state.FleetOrders);
    }

    [Fact]
    public void MoveOrdersCanOnlyTargetLinkedSystems()
    {
        var state = TestState.CreateMovementState(linkSystems: false);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");

        Assert.Throws<InvalidOperationException>(
            () => OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now));
    }

    [Fact]
    public void OneTickMoveOrderMovesFleetDuringProcessingTick()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");

        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var movedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        Assert.Equal(destination.SystemId, movedFleet.CurrentSystemId);
        Assert.Equal(FleetStatus.Active, movedFleet.Status);
        Assert.Contains(state.Events, item => item.EventType == EventType.FleetMoved);
    }

    [Fact]
    public void MultiTickMoveOrderArrivesOnTheExpectedTick()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");

        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var inTransitFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        Assert.Equal(FleetStatus.InTransit, inTransitFleet.Status);
        Assert.Equal(2, inTransitFleet.ArrivalTickNumber);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var arrivedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        Assert.Equal(destination.SystemId, arrivedFleet.CurrentSystemId);
        Assert.Equal(FleetStatus.Active, arrivedFleet.Status);
        Assert.Null(arrivedFleet.ArrivalTickNumber);
        Assert.Contains(state.Events, item => item.EventType == EventType.FleetArrived);
    }

    [Fact]
    public void ProcessingTimeRejectedOrdersAreNotProcessedAgain()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var order = new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = FleetOrderType.MoveFleet,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            Status = FleetOrderStatus.Pending,
            CreatedAt = TestState.Now
        };
        state.FleetOrders.Add(order);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var committedOrder = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);

        Assert.Equal(FleetOrderStatus.Rejected, committedOrder.Status);
        Assert.Equal(1, committedOrder.ProcessedTick);
        Assert.Single(state.Events, item => item.EventType == EventType.OrderRejected);
    }

    [Fact]
    public void PendingOrdersCanBeCancelledBeforeExecutionTick()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        var cancelled = OrderService.CancelFleetOrder(state, order.FleetOrderId, empire.EmpireId, TestState.Now);

        Assert.Equal(order.FleetOrderId, cancelled.FleetOrderId);
        Assert.Equal(FleetOrderStatus.Cancelled, order.Status);
        Assert.Equal(0, order.ProcessedTick);
        Assert.Single(state.Events, item => item.EventType == EventType.OrderCancelled);
    }

    [Fact]
    public void CancelledOrdersAreNotProcessedByTick()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var originSystemId = fleet.CurrentSystemId;
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        OrderService.CancelFleetOrder(state, order.FleetOrderId, empire.EmpireId, TestState.Now);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var committedOrder = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);
        var committedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(FleetOrderStatus.Cancelled, committedOrder.Status);
        Assert.Equal(originSystemId, committedFleet.CurrentSystemId);
        Assert.DoesNotContain(state.Events, item => item.EventType == EventType.FleetMoved);
    }

    [Fact]
    public void OrdersCanOnlyBeCancelledByOwningEmpire()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 40, defenderShips: 30);
        var attackerFleet = state.Fleets.First();
        var attackerEmpire = state.Empires.Single(empire => empire.EmpireId == attackerFleet.EmpireId);
        var defenderEmpire = state.Empires.Single(empire => empire.EmpireId != attackerEmpire.EmpireId);
        var order = OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, defenderEmpire.EmpireId, TestState.Now);

        var ex = Assert.Throws<InvalidOperationException>(
            () => OrderService.CancelFleetOrder(state, order.FleetOrderId, defenderEmpire.EmpireId, TestState.Now));

        Assert.Contains("owning empire", ex.Message, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Pending, order.Status);
    }

    [Fact]
    public void DueOrdersCannotBeCancelled()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        cycle.CurrentTickNumber = order.ExecuteAfterTick;

        var ex = Assert.Throws<InvalidOperationException>(
            () => OrderService.CancelFleetOrder(state, order.FleetOrderId, empire.EmpireId, TestState.Now));

        Assert.Contains("before their execution tick", ex.Message, StringComparison.Ordinal);
        Assert.Equal(FleetOrderStatus.Pending, order.Status);
    }

    [Fact]
    public void FailedTickDoesNotPartiallyCommitWorkingState()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        state.EmpireResources.Clear();

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, result.Status);
        Assert.Equal(CycleStatus.RecoveryRequired, cycle.Status);
        Assert.Equal(0, cycle.CurrentTickNumber);
        Assert.Equal(TurnResolutionStage.Resolving, cycle.TurnStage);
        Assert.Empty(state.FleetOrders);
        Assert.Single(state.TickLogs);
        Assert.DoesNotContain(state.Events, item => item.TickNumber == 1);
    }

    [Fact]
    public void FailedTickRollsBackFactsAppendedBeforeLateFailure()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var resourcesBefore = Assert.Single(state.EmpireResources).Industry;
        var eventsBefore = state.Events.Count;
        state.EmpirePriorities.Clear();

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, result.Status);
        Assert.Equal(resourcesBefore, Assert.Single(state.EmpireResources).Industry);
        Assert.Equal(eventsBefore, state.Events.Count);
        Assert.Single(state.TickLogs, item => item.Status == TickLogStatus.Failed);
        Assert.Empty(state.BattleRecords);
        Assert.Empty(state.ChronicleEntries);
    }

    [Fact]
    public void FocusedWorkingCopyMatchesFullCloneTickOutcome()
    {
        var source = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 55, strategicValue: 35);
        var cycle = source.GetActiveCycle()!;
        var attacker = source.Empires.Single(item => item.EmpireName == "First");
        var defender = source.Empires.Single(item => item.EmpireName == "Second");
        var attackerFleet = source.Fleets.Single(item => item.EmpireId == attacker.EmpireId);
        DiplomacyService.SetState(
            source,
            cycle.CycleId,
            attacker.EmpireId,
            defender.EmpireId,
            DiplomaticRelationshipState.Alliance,
            tickNumber: 0,
            TestState.Now);
        OrderService.SubmitAttackOrder(source, attackerFleet.FleetId, defender.EmpireId, TestState.Now);
        var focusedState = source.DeepClone();
        var referenceState = source.DeepClone();

        var focusedResult = new TickEngine().RunTick(focusedState, cycle.CycleId, TestState.Now);
        var referenceResult = new TickEngine(
            static (state, _, _) => state.DeepClone(),
            rollbackSharedAppends: false).RunTick(referenceState, cycle.CycleId, TestState.Now);

        Assert.Equal(referenceResult, focusedResult);
        AssertEquivalentTickState(referenceState, focusedState, cycle.CycleId);
    }

    [Fact]
    public void SustainedConflictScenarioCompletesConfiguredFullCycle()
    {
        var result = BalanceScenarioRunner.Run(new BalanceScenarioOptions(
            TickCount: 2_160,
            SystemCount: 24,
            EmpireCount: 4,
            Seed: 71421,
            RetainedRecordLimit: 150_000));

        Assert.Equal(2_160, result.CompletedTicks);
        Assert.Null(result.StopReason);
        Assert.True(result.RetainedRecords > 15_000);
        Assert.True(result.Battles > 1_000);
    }

    [Fact]
    public void RecoveryRequiredCycleCannotProcessAnotherTick()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.Status = CycleStatus.RecoveryRequired;

        Assert.Throws<InvalidOperationException>(() => new TickEngine().RunTick(state, cycle.CycleId, TestState.Now));
    }

    [Fact]
    public void RecoveryClearRequiresOperatorAndReason()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.Status = CycleStatus.RecoveryRequired;

        Assert.Throws<InvalidOperationException>(() => RecoveryService.ClearRecovery(state, cycle.CycleId, "", "fixed", TestState.Now));
        Assert.Throws<InvalidOperationException>(() => RecoveryService.ClearRecovery(state, cycle.CycleId, "admin", "", TestState.Now));
    }

    [Fact]
    public void RecoveryClearMarksCycleActiveAndWritesAuditEvent()
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
            DiagnosticLog = "boom"
        });

        var recoveryEvent = RecoveryService.ClearRecovery(state, cycle.CycleId, "admin", "restored missing resources", TestState.Now);

        Assert.Equal(CycleStatus.Active, cycle.Status);
        Assert.Equal(EventType.RecoveryCleared, recoveryEvent.EventType);
        Assert.Equal(EventSeverity.High, recoveryEvent.Severity);
        Assert.Contains("admin", recoveryEvent.FactJson, StringComparison.Ordinal);
        Assert.Contains("restored missing resources", recoveryEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void RecoveryClearRefusesUnfinishedRunningTickLogs()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.Status = CycleStatus.RecoveryRequired;
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            Status = TickLogStatus.Running
        });

        Assert.Throws<InvalidOperationException>(() => RecoveryService.ClearRecovery(state, cycle.CycleId, "admin", "manual repair", TestState.Now));
    }

    [Fact]
    public void ClearedRecoveryCanRetryFailedTickNumber()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var resources = Assert.Single(state.EmpireResources);
        state.EmpireResources.Clear();

        var failedResult = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, failedResult.Status);
        Assert.Equal(CycleStatus.RecoveryRequired, cycle.Status);

        state.EmpireResources.Add(resources);
        RecoveryService.ClearRecovery(state, cycle.CycleId, "admin", "restored missing resources", TestState.Now);
        var retryResult = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        var committedCycle = state.Cycles.Single(item => item.CycleId == cycle.CycleId);

        Assert.Equal(TickLogStatus.Completed, retryResult.Status);
        Assert.Equal(1, retryResult.TickNumber);
        Assert.Equal(1, committedCycle.CurrentTickNumber);
        Assert.Contains(state.TickLogs, log => log.TickNumber == 1 && log.Status == TickLogStatus.Failed);
        Assert.Contains(state.TickLogs, log => log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
    }

    private static void AssertEquivalentTickState(GameState expected, GameState actual, Guid cycleId)
    {
        Assert.Equal(
            expected.Cycles.Where(item => item.CycleId == cycleId).Select(item => (item.CurrentTickNumber, item.Status)),
            actual.Cycles.Where(item => item.CycleId == cycleId).Select(item => (item.CurrentTickNumber, item.Status)));
        Assert.Equal(
            expected.EmpireResources.OrderBy(item => item.EmpireId).Select(item =>
                (item.EmpireId, item.Industry, item.Research, item.Population, item.LastGeneratedIndustry, item.LastSpentIndustry)),
            actual.EmpireResources.OrderBy(item => item.EmpireId).Select(item =>
                (item.EmpireId, item.Industry, item.Research, item.Population, item.LastGeneratedIndustry, item.LastSpentIndustry)));
        Assert.Equal(
            expected.Fleets.OrderBy(item => item.FleetId).Select(item =>
                (item.FleetId, item.CurrentSystemId, item.DestinationSystemId, item.ArrivalTickNumber, item.ShipCount, item.Status)),
            actual.Fleets.OrderBy(item => item.FleetId).Select(item =>
                (item.FleetId, item.CurrentSystemId, item.DestinationSystemId, item.ArrivalTickNumber, item.ShipCount, item.Status)));
        Assert.Equal(
            expected.FleetOrders.OrderBy(item => item.FleetOrderId).Select(item =>
                (item.FleetOrderId, item.Status, item.CommandSource, item.SealedTick, item.ProcessedTick, item.RejectionReason)),
            actual.FleetOrders.OrderBy(item => item.FleetOrderId).Select(item =>
                (item.FleetOrderId, item.Status, item.CommandSource, item.SealedTick, item.ProcessedTick, item.RejectionReason)));
        Assert.Equal(
            expected.DiplomaticRelationships.OrderBy(item => item.DiplomaticRelationshipId).Select(item =>
                (item.FirstEmpireId, item.SecondEmpireId, item.State, item.UpdatedTick)),
            actual.DiplomaticRelationships.OrderBy(item => item.DiplomaticRelationshipId).Select(item =>
                (item.FirstEmpireId, item.SecondEmpireId, item.State, item.UpdatedTick)));
        Assert.Equal(
            expected.Events.Where(item => item.TickNumber == 1).Select(item =>
                (item.EventType, item.SystemId, item.EmpireId, item.Severity, item.DisplayText)),
            actual.Events.Where(item => item.TickNumber == 1).Select(item =>
                (item.EventType, item.SystemId, item.EmpireId, item.Severity, item.DisplayText)));
        Assert.Equal(
            expected.BattleRecords.Select(item =>
                (item.TickNumber, item.AttackerEmpireId, item.DefenderEmpireId, item.AttackerLosses, item.DefenderLosses, item.Outcome)),
            actual.BattleRecords.Select(item =>
                (item.TickNumber, item.AttackerEmpireId, item.DefenderEmpireId, item.AttackerLosses, item.DefenderLosses, item.Outcome)));
        Assert.Equal(
            expected.ChronicleEntries.Select(item => (item.Title, item.ImportanceScore, item.FactualSummary, item.NarrativeText)),
            actual.ChronicleEntries.Select(item => (item.Title, item.ImportanceScore, item.FactualSummary, item.NarrativeText)));
        Assert.Equal(
            expected.EmpireMetrics.OrderBy(item => item.EmpireId).Select(item =>
                (item.EmpireId, item.TickNumber, item.Rank, item.MapControlPercent, item.TotalEffectivePresence, item.ActiveShipCount)),
            actual.EmpireMetrics.OrderBy(item => item.EmpireId).Select(item =>
                (item.EmpireId, item.TickNumber, item.Rank, item.MapControlPercent, item.TotalEffectivePresence, item.ActiveShipCount)));
    }
}
