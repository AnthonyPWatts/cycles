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
    public void FailedTickDoesNotPartiallyCommitWorkingState()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        state.EmpireResources.Clear();

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, result.Status);
        Assert.Equal(CycleStatus.RecoveryRequired, cycle.Status);
        Assert.Equal(0, cycle.CurrentTickNumber);
        Assert.Single(state.TickLogs);
        Assert.DoesNotContain(state.Events, item => item.TickNumber == 1);
    }
}
