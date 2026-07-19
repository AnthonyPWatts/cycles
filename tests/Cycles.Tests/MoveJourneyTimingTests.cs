using Cycles.Core;

namespace Cycles.Tests;

public sealed class MoveJourneyTimingTests
{
    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(4, 4)]
    public void Direct_route_projection_uses_inclusive_dispatch_and_arrival_ticks(
        int travelTicks,
        int expectedArrivalTick)
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: travelTicks);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");

        var projection = MoveJourneyTiming.TryProject(
            state,
            cycle.CycleId,
            fleet.CurrentSystemId,
            destination.SystemId,
            dispatchTickNumber: 1);

        Assert.NotNull(projection);
        Assert.Equal(travelTicks, projection.TravelTicks);
        Assert.Equal(1, projection.DispatchTickNumber);
        Assert.Equal(expectedArrivalTick, projection.ArrivalTickNumber);
    }

    [Fact]
    public void Unlinked_destination_has_no_journey_projection()
    {
        var state = TestState.CreateMovementState(linkSystems: false);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");

        var projection = MoveJourneyTiming.TryProject(
            state,
            cycle.CycleId,
            fleet.CurrentSystemId,
            destination.SystemId,
            dispatchTickNumber: 1);

        Assert.Null(projection);
    }

    [Fact]
    public void Resolution_uses_changed_authoritative_route_timing()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        var queuedProjection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);
        Assert.NotNull(queuedProjection);
        Assert.Equal(2, queuedProjection.ArrivalTickNumber);

        Assert.Single(state.SystemLinks).TravelTicks = 4;

        var refreshedProjection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);
        Assert.NotNull(refreshedProjection);
        Assert.Equal(4, refreshedProjection.TravelTicks);
        Assert.Equal(4, refreshedProjection.ArrivalTickNumber);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddMinutes(1));

        var dispatchedFleet = Assert.Single(state.Fleets);
        var processedOrder = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);
        Assert.Equal(FleetStatus.InTransit, dispatchedFleet.Status);
        Assert.Equal(1, dispatchedFleet.DepartureTickNumber);
        Assert.Equal(4, dispatchedFleet.ArrivalTickNumber);
        Assert.Equal(FleetOrderStatus.Processed, processedOrder.Status);
    }

    [Fact]
    public void Removed_route_makes_the_projection_unavailable_and_resolution_rejects_the_move()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var originSystemId = fleet.CurrentSystemId;
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        state.SystemLinks.Clear();

        var projection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);
        Assert.NotNull(projection);
        Assert.False(projection.RouteAvailable);
        Assert.Equal(1, projection.ActivationTickNumber);
        Assert.Null(projection.DispatchTickNumber);
        Assert.Null(projection.ArrivalTickNumber);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddMinutes(1));

        var rejectedOrder = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);
        var retainedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        Assert.Equal(FleetOrderStatus.Rejected, rejectedOrder.Status);
        Assert.Equal(originSystemId, retainedFleet.CurrentSystemId);
        Assert.Equal(FleetStatus.Active, retainedFleet.Status);
        Assert.DoesNotContain(state.Events, item => item.EventType == EventType.FleetMoved);
    }
}
