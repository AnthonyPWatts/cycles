using Cycles.Core;

namespace Cycles.Tests;

public sealed class MoveJourneyPresentationContractTests
{
    [Theory]
    [InlineData(1, 6)]
    [InlineData(2, 7)]
    [InlineData(4, 9)]
    public void Legal_destinations_expose_duration_dispatch_and_arrival(
        int travelTicks,
        int expectedArrivalTick)
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: travelTicks);
        var cycle = state.GetActiveCycle()!;
        cycle.CurrentTickNumber = 5;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");

        var result = Assert.Single(
            MoveJourneyPresentationContract.CreateLegalDestinations(state, cycle, fleet));

        Assert.Equal(destination.SystemId, result.SystemId);
        Assert.Equal(destination.SystemName, result.SystemName);
        Assert.Equal(travelTicks, result.TravelTicks);
        Assert.Equal(6, result.ProjectedDispatchTickNumber);
        Assert.Equal(expectedArrivalTick, result.ProjectedArrivalTickNumber);
    }

    [Fact]
    public void Pending_move_projection_repeats_the_current_legal_destination_timing()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var legalDestination = Assert.Single(
            MoveJourneyPresentationContract.CreateLegalDestinations(state, cycle, fleet));
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        var queuedProjection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);

        Assert.NotNull(queuedProjection);
        Assert.True(queuedProjection.RouteAvailable);
        Assert.Equal(legalDestination.TravelTicks, queuedProjection.TravelTicks);
        Assert.Equal(legalDestination.ProjectedDispatchTickNumber, queuedProjection.DispatchTickNumber);
        Assert.Equal(legalDestination.ProjectedArrivalTickNumber, queuedProjection.ArrivalTickNumber);
    }

    [Fact]
    public void Pending_move_projection_refreshes_when_authoritative_route_timing_changes()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        Assert.Single(state.SystemLinks).TravelTicks = 4;

        var queuedProjection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);

        Assert.NotNull(queuedProjection);
        Assert.True(queuedProjection.RouteAvailable);
        Assert.Equal(4, queuedProjection.TravelTicks);
        Assert.Equal(1, queuedProjection.DispatchTickNumber);
        Assert.Equal(4, queuedProjection.ArrivalTickNumber);
    }

    [Fact]
    public void Pending_move_projection_reports_an_unavailable_removed_route_without_fabricated_timing()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var order = OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        state.SystemLinks.Clear();

        var queuedProjection = MoveJourneyPresentationContract.CreateOrderProjection(state, order);

        Assert.NotNull(queuedProjection);
        Assert.False(queuedProjection.RouteAvailable);
        Assert.Equal(1, queuedProjection.ActivationTickNumber);
        Assert.Null(queuedProjection.TravelTicks);
        Assert.Null(queuedProjection.DispatchTickNumber);
        Assert.Null(queuedProjection.ArrivalTickNumber);
    }

    [Fact]
    public void In_transit_fleet_has_no_legal_move_destinations()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddMinutes(1));

        Assert.Empty(MoveJourneyPresentationContract.CreateLegalDestinations(
            state,
            state.GetActiveCycle()!,
            state.Fleets.Single(item => item.FleetId == fleet.FleetId)));
    }
}
