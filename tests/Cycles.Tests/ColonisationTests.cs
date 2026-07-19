using Cycles.Core;

namespace Cycles.Tests;

public sealed class ColonisationTests
{
    [Fact]
    public void Colonisation_order_spends_population_and_establishes_outpost_on_tick()
    {
        var state = CreateColonisationState();
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var resources = Assert.Single(state.EmpireResources);
        resources.Population = 150m;

        var order = OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);
        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(FleetOrderStatus.Processed, state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId).Status);
        var outpost = Assert.Single(state.ColonialOutposts);
        Assert.Equal(fleet.EmpireId, outpost.EmpireId);
        Assert.Equal(fleet.CurrentSystemId, outpost.SystemId);
        Assert.Equal(1, outpost.EstablishedTick);
        Assert.Equal(70m, state.EmpireResources.Single().Population);
        Assert.Equal(OrderService.ColonisationPopulationCost, state.EmpireResources.Single().LastSpentPopulation);
        Assert.Contains(state.Events, item => item.EventType == EventType.ColonialOutpostEstablished);
    }

    [Fact]
    public void Colonial_outpost_adds_presence_only_while_owner_has_active_local_fleet()
    {
        var state = CreateColonisationState();
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var priority = Assert.Single(state.EmpirePriorities);
        priority.ExpansionWeight = 0;
        priority.MilitaryWeight = 100;
        state.ColonialOutposts.Add(new ColonialOutpost
        {
            CycleId = cycle.CycleId,
            EmpireId = fleet.EmpireId,
            SystemId = fleet.CurrentSystemId,
            EstablishedTick = 0,
            CreatedAt = TestState.Now
        });

        var activePresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, fleet.CurrentSystemId);
        fleet.Status = FleetStatus.InTransit;
        var absentPresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, fleet.CurrentSystemId);

        Assert.Equal(fleet.ShipCount + InfluenceCalculator.ColonialOutpostPresence, activePresence[fleet.EmpireId]);
        Assert.Empty(absentPresence);
    }

    [Fact]
    public void Colonisation_order_is_rejected_if_population_is_spent_before_processing()
    {
        var state = CreateColonisationState();
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var resources = Assert.Single(state.EmpireResources);
        resources.Population = 100m;
        var order = OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);
        resources.Population = 0m;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var processed = state.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);
        Assert.Equal(FleetOrderStatus.Rejected, processed.Status);
        Assert.Contains("requires 100 population", processed.RejectionReason, StringComparison.Ordinal);
        Assert.Empty(state.ColonialOutposts);
        Assert.Equal(20m, state.EmpireResources.Single().Population);
    }

    [Fact]
    public void Colonisation_requires_strictly_leading_presence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 40, defenderShips: 40);
        foreach (var empire in state.Empires)
        {
            empire.HomeSystemId = Guid.NewGuid();
        }
        state.EmpireResources[0].Population = 100m;
        var fleet = state.Fleets.Single(item => item.EmpireId == state.Empires[0].EmpireId);

        var error = Assert.Throws<InvalidOperationException>(
            () => OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now));

        Assert.Contains("leading influence", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Duplicate_pending_colonisation_order_is_idempotent()
    {
        var state = CreateColonisationState();
        var fleet = Assert.Single(state.Fleets);
        state.EmpireResources.Single().Population = 200m;
        var pending = OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);

        var duplicate = OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now.AddSeconds(1));

        Assert.Same(pending, duplicate);
        Assert.Single(state.FleetOrders);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 0, 0)]
    [InlineData(100, 100, 2)]
    public void Command_closure_reserves_population_for_the_whole_eligible_colonisation_set(
        int populationBeforeIncome,
        int currentTurnPopulationIncome,
        int expectedColonisations)
    {
        var state = TestState.CreateColonisationContentionState(currentTurnPopulationIncome);
        var cycle = state.GetActiveCycle()!;
        var resources = Assert.Single(state.EmpireResources);
        var orderIds = SubmitAllColonisationOrders(state).Select(order => order.FleetOrderId).ToArray();
        resources.Population = populationBeforeIncome;

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));
        var coloniseOrders = state.FleetOrders.Where(order => orderIds.Contains(order.FleetOrderId)).ToArray();
        var committedResources = Assert.Single(state.EmpireResources);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(expectedColonisations, state.ColonialOutposts.Count);
        Assert.Equal(
            expectedColonisations,
            coloniseOrders.Count(order => order.Status == FleetOrderStatus.Processed));

        if (expectedColonisations == orderIds.Length)
        {
            Assert.All(coloniseOrders, order => Assert.Equal(1, order.SealedTick));
            Assert.Equal(200m, committedResources.LastSpentPopulation);
            Assert.DoesNotContain(state.FleetOrders, order => order.OrderType == FleetOrderType.Hold);
            return;
        }

        Assert.All(coloniseOrders, order =>
        {
            Assert.Equal(FleetOrderStatus.Rejected, order.Status);
            Assert.Null(order.SealedTick);
            Assert.Equal(1, order.ProcessedTick);
            Assert.Contains("whole 2-order set was rejected", order.RejectionReason, StringComparison.Ordinal);
        });
        Assert.Equal(2, state.FleetOrders.Count(order =>
            order.OrderType == FleetOrderType.Hold
            && order.Status == FleetOrderStatus.Processed
            && order.SealedTick == 1));
        Assert.Equal(populationBeforeIncome + currentTurnPopulationIncome, committedResources.Population);
        Assert.Equal(2, state.Events.Count(item => item.EventType == EventType.OrderRejected));
    }

    [Fact]
    public void Cancelling_one_colonisation_before_closure_changes_the_reserved_set()
    {
        var state = TestState.CreateColonisationContentionState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var orders = SubmitAllColonisationOrders(state);
        var cancelledOrderId = orders[0].FleetOrderId;
        var retainedOrderId = orders[1].FleetOrderId;
        var retainedTargetSystemId = orders[1].TargetSystemId;

        OrderService.CancelFleetOrder(state, cancelledOrderId, empire.EmpireId, TestState.Now.AddMinutes(1));
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(FleetOrderStatus.Cancelled, state.FleetOrders.Single(item => item.FleetOrderId == cancelledOrderId).Status);
        Assert.Equal(FleetOrderStatus.Processed, state.FleetOrders.Single(item => item.FleetOrderId == retainedOrderId).Status);
        Assert.Single(state.ColonialOutposts, item => item.SystemId == retainedTargetSystemId);
        Assert.Equal(0m, state.EmpireResources.Single().Population);
    }

    [Fact]
    public void Replacing_one_colonisation_before_closure_changes_the_reserved_set()
    {
        var state = TestState.CreateColonisationContentionState();
        var cycle = state.GetActiveCycle()!;
        var orders = SubmitAllColonisationOrders(state);
        var supersededOrderId = orders[0].FleetOrderId;
        var retainedOrderId = orders[1].FleetOrderId;
        var retainedTargetSystemId = orders[1].TargetSystemId;

        var replacement = OrderService.SubmitHoldOrder(
            state,
            orders[0].FleetId,
            TestState.Now.AddMinutes(1),
            supersededOrderId);
        var replacementOrderId = replacement.FleetOrderId;
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(FleetOrderStatus.Superseded, state.FleetOrders.Single(item => item.FleetOrderId == supersededOrderId).Status);
        Assert.Equal(FleetOrderStatus.Processed, state.FleetOrders.Single(item => item.FleetOrderId == replacementOrderId).Status);
        Assert.Equal(FleetOrderStatus.Processed, state.FleetOrders.Single(item => item.FleetOrderId == retainedOrderId).Status);
        Assert.Single(state.ColonialOutposts, item => item.SystemId == retainedTargetSystemId);
        Assert.Equal(0m, state.EmpireResources.Single().Population);
    }

    [Fact]
    public void Oversubscribed_colonisation_orders_do_not_revive_when_later_population_is_available()
    {
        var state = TestState.CreateColonisationContentionState();
        var cycle = state.GetActiveCycle()!;
        var orderIds = SubmitAllColonisationOrders(state).Select(order => order.FleetOrderId).ToArray();

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));
        state.EmpireResources.Single().Population = 500m;
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(2));

        Assert.All(
            state.FleetOrders.Where(order => orderIds.Contains(order.FleetOrderId)),
            order => Assert.Equal(FleetOrderStatus.Rejected, order.Status));
        Assert.Empty(state.ColonialOutposts);
    }

    [Fact]
    public void Deep_clone_preserves_colonial_outposts()
    {
        var state = CreateColonisationState();
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        state.ColonialOutposts.Add(new ColonialOutpost
        {
            CycleId = cycle.CycleId,
            EmpireId = fleet.EmpireId,
            SystemId = fleet.CurrentSystemId,
            EstablishedTick = 3,
            CreatedAt = TestState.Now
        });

        var clone = state.DeepClone();

        var outpost = Assert.Single(clone.ColonialOutposts);
        Assert.Equal(3, outpost.EstablishedTick);
        Assert.NotSame(state.ColonialOutposts[0], outpost);
    }

    private static GameState CreateColonisationState()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var fleet = Assert.Single(state.Fleets);
        fleet.CurrentSystemId = destination.SystemId;
        return state;
    }

    private static FleetOrder[] SubmitAllColonisationOrders(GameState state) =>
        state.Fleets
            .OrderBy(fleet => fleet.FleetId)
            .Select((fleet, index) => OrderService.SubmitColoniseOrder(
                state,
                fleet.FleetId,
                TestState.Now.AddMinutes(index)))
            .ToArray();
}
