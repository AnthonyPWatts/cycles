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
        priority.IndustryWeight = 50;
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
    public void Duplicate_pending_colonisation_order_is_rejected()
    {
        var state = CreateColonisationState();
        var fleet = Assert.Single(state.Fleets);
        state.EmpireResources.Single().Population = 200m;
        OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);

        var error = Assert.Throws<InvalidOperationException>(
            () => OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now));

        Assert.Contains("pending colonisation order", error.Message, StringComparison.Ordinal);
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
}
