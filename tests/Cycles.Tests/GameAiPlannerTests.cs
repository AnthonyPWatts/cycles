using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameAiPlannerTests
{
    [Fact]
    public void Curated_opening_produces_attack_colonise_and_move_intentions()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = GetAiEmpire(state, cycle.CycleId);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var aiOrders = GetAiOrders(state, aiEmpire.EmpireId);
        Assert.Equal(3, aiOrders.Length);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.Attack);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.Colonise);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.MoveFleet);
        Assert.All(aiOrders, order =>
        {
            Assert.Equal(FleetOrderCommandSource.GameAiPlanner, order.CommandSource);
            Assert.Equal(1, order.SealedTick);
            Assert.Equal(FleetOrderStatus.Processed, order.Status);
        });
        Assert.Contains(state.ColonialOutposts, outpost => outpost.EmpireId == aiEmpire.EmpireId);
        Assert.Contains(state.BattleRecords, battle => battle.AttackerEmpireId == aiEmpire.EmpireId);
    }

    [Fact]
    public void Clearly_weaker_visible_faction_is_attacked()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 10);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeEmpireAi(state, "First");

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.Attack, order.OrderType);
        Assert.Equal(state.GetEmpireFaction(state.Empires.Single(item => item.EmpireName == "Second").EmpireId).FactionId, order.TargetFactionId);
        Assert.Single(state.BattleRecords);
    }

    [Fact]
    public void Fleet_holds_against_a_local_threat_without_a_clear_advantage()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 12, defenderShips: 12);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeEmpireAi(state, "First");

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.Hold, order.OrderType);
        Assert.Empty(state.BattleRecords);
    }

    [Theory]
    [InlineData(DiplomaticRelationshipState.NonAggressionPact)]
    [InlineData(DiplomaticRelationshipState.Alliance)]
    public void Planner_does_not_initiate_an_attack_through_a_treaty(
        DiplomaticRelationshipState relationshipState)
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 30, defenderShips: 5);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeEmpireAi(state, "First");
        var otherEmpire = state.Empires.Single(item => item.EmpireName == "Second");
        DiplomacyService.SetState(
            state,
            cycle.CycleId,
            aiEmpire.EmpireId,
            otherEmpire.EmpireId,
            relationshipState,
            tickNumber: 0,
            TestState.Now);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.Hold, order.OrderType);
        Assert.Empty(state.BattleRecords);
        Assert.Equal(
            relationshipState,
            DiplomacyService.GetState(state, cycle.CycleId, aiEmpire.EmpireId, otherEmpire.EmpireId));
    }

    [Fact]
    public void No_useful_legal_action_becomes_a_game_ai_hold()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeOnlyEmpireAi(state);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.Hold, order.OrderType);
        Assert.Equal(FleetOrderCommandSource.GameAiPlanner, order.CommandSource);
    }

    [Fact]
    public void Fleet_establishes_an_affordable_outpost_before_moving_on()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeOnlyEmpireAi(state);
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        fleet.CurrentSystemId = destination.SystemId;
        state.EmpireResources.Single().Population = OrderService.ColonisationPopulationCost;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.Colonise, order.OrderType);
        Assert.Equal(destination.SystemId, order.TargetSystemId);
        Assert.Contains(state.ColonialOutposts, outpost =>
            outpost.EmpireId == aiEmpire.EmpireId && outpost.SystemId == destination.SystemId);
    }

    [Fact]
    public void Fleet_moves_towards_the_highest_value_reachable_expansion_system()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeOnlyEmpireAi(state);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        destination.StrategicValue = 50;
        destination.IndustryOutput = 100;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var order = Assert.Single(GetAiOrders(state, aiEmpire.EmpireId));
        Assert.Equal(FleetOrderType.MoveFleet, order.OrderType);
        Assert.Equal(destination.SystemId, order.TargetSystemId);
        Assert.Equal(destination.SystemId, Assert.Single(state.Fleets).CurrentSystemId);
    }

    [Fact]
    public void Equal_value_routes_use_stable_identifiers_as_a_replay_tie_breaker()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        MakeOnlyEmpireAi(state);
        var origin = state.Systems.Single(system => system.SystemName == "Origin");
        var existingDestination = state.Systems.Single(system => system.SystemName == "Destination");
        var firstTargetId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var secondTargetId = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var originalDestinationId = existingDestination.SystemId;
        existingDestination.SystemId = secondTargetId;
        existingDestination.StrategicValue = 30;
        state.SystemLinks.Single().SystemBId = secondTargetId;
        var tiedDestination = new GalaxySystem
        {
            SystemId = firstTargetId,
            CycleId = cycle.CycleId,
            SystemName = "Tied destination",
            IndustryOutput = existingDestination.IndustryOutput,
            ResearchOutput = existingDestination.ResearchOutput,
            PopulationOutput = existingDestination.PopulationOutput,
            StrategicValue = existingDestination.StrategicValue,
            HistoricalSignificance = existingDestination.HistoricalSignificance,
            CreatedAt = TestState.Now
        };
        state.Systems.Add(tiedDestination);
        state.SystemLinks.Add(new SystemLink
        {
            CycleId = cycle.CycleId,
            SystemAId = origin.SystemId,
            SystemBId = tiedDestination.SystemId,
            Distance = 1,
            TravelTicks = 1
        });
        Assert.DoesNotContain(state.Systems, system => system.SystemId == originalDestinationId);
        var replay = state.DeepClone();
        var fleetId = Assert.Single(state.Fleets).FleetId;

        var firstPlan = Assert.Single(GameAiPlanner.Plan(
            state,
            cycle.CycleId,
            tickNumber: 1,
            TestState.Now,
            [fleetId]));
        var replayPlan = Assert.Single(GameAiPlanner.Plan(
            replay,
            cycle.CycleId,
            tickNumber: 1,
            TestState.Now.AddHours(1),
            [fleetId]));

        Assert.Equal(firstTargetId, firstPlan.TargetSystemId);
        Assert.Equal(firstPlan.FleetOrderId, replayPlan.FleetOrderId);
        Assert.Equal(firstPlan.OrderType, replayPlan.OrderType);
        Assert.Equal(firstPlan.TargetSystemId, replayPlan.TargetSystemId);
    }

    [Fact]
    public void Remote_enemy_fleets_and_hidden_human_commands_do_not_change_the_plan()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 20, defenderShips: 5);
        var cycle = state.GetActiveCycle()!;
        var aiEmpire = MakeEmpireAi(state, "First");
        var humanEmpire = state.Empires.Single(item => item.EmpireName == "Second");
        var origin = Assert.Single(state.Systems);
        var valuable = AddLinkedSystem(state, cycle.CycleId, origin.SystemId, "Valuable", 50, 100);
        var alternative = AddLinkedSystem(state, cycle.CycleId, origin.SystemId, "Alternative", 10, 10);
        var humanFleet = state.Fleets.Single(fleet => fleet.EmpireId == humanEmpire.EmpireId);
        humanFleet.CurrentSystemId = valuable.SystemId;
        humanEmpire.HomeSystemId = valuable.SystemId;
        var replay = state.DeepClone();
        var replayHumanFleet = replay.Fleets.Single(fleet => fleet.FleetId == humanFleet.FleetId);
        replayHumanFleet.CurrentSystemId = alternative.SystemId;
        OrderService.SubmitMoveOrder(replay, replayHumanFleet.FleetId, origin.SystemId, TestState.Now);
        var aiFleetId = state.Fleets.Single(fleet => fleet.EmpireId == aiEmpire.EmpireId).FleetId;

        var firstPlan = Assert.Single(GameAiPlanner.Plan(
            state,
            cycle.CycleId,
            tickNumber: 1,
            TestState.Now,
            [aiFleetId]));
        var replayPlan = Assert.Single(GameAiPlanner.Plan(
            replay,
            cycle.CycleId,
            tickNumber: 1,
            TestState.Now,
            [aiFleetId]));

        Assert.Equal(FleetOrderType.MoveFleet, firstPlan.OrderType);
        Assert.Equal(valuable.SystemId, firstPlan.TargetSystemId);
        Assert.Equal(firstPlan.FleetOrderId, replayPlan.FleetOrderId);
        Assert.Equal(firstPlan.TargetSystemId, replayPlan.TargetSystemId);
    }

    [Fact]
    public void Neutral_fleets_remain_positional_hold_obstacles()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var neutralOrders = state.FleetOrders
            .Where(order => order.SealedTick == 1 && order.CommandSource == FleetOrderCommandSource.NeutralPlanner)
            .ToArray();
        Assert.Equal(6, neutralOrders.Length);
        Assert.All(neutralOrders, order => Assert.Equal(FleetOrderType.Hold, order.OrderType));
    }

    [Fact]
    public void Unattended_curated_match_changes_the_map_over_eight_ticks()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycleId = state.GetActiveCycle()!.CycleId;

        for (var tick = 1; tick <= 8; tick++)
        {
            var now = TestState.Now.AddHours(tick);
            Assert.Equal(TickLogStatus.Completed, new TickEngine().RunTick(state, cycleId, now).Status);
        }

        var aiEmpire = GetAiEmpire(state, cycleId);
        var aiOrders = GetAiOrders(state, aiEmpire.EmpireId);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.MoveFleet);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.Colonise);
        Assert.Contains(aiOrders, order => order.OrderType == FleetOrderType.Attack);
        Assert.Contains(state.ColonialOutposts, outpost => outpost.EmpireId == aiEmpire.EmpireId);
    }

    private static GalaxySystem AddLinkedSystem(
        GameState state,
        Guid cycleId,
        Guid originSystemId,
        string name,
        int strategicValue,
        decimal industryOutput)
    {
        var system = new GalaxySystem
        {
            CycleId = cycleId,
            SystemName = name,
            IndustryOutput = industryOutput,
            ResearchOutput = 10,
            PopulationOutput = 10,
            StrategicValue = strategicValue,
            CreatedAt = TestState.Now
        };
        state.Systems.Add(system);
        state.SystemLinks.Add(new SystemLink
        {
            CycleId = cycleId,
            SystemAId = originSystemId,
            SystemBId = system.SystemId,
            Distance = 1,
            TravelTicks = 1
        });
        return system;
    }

    private static Empire GetAiEmpire(GameState state, Guid cycleId)
    {
        var player = state.Players.Single(item => item.Kind == PlayerKind.AI);
        return state.Empires.Single(item => item.CycleId == cycleId && item.PlayerId == player.PlayerId);
    }

    private static Empire MakeOnlyEmpireAi(GameState state)
    {
        var empire = Assert.Single(state.Empires);
        state.Players.Single(player => player.PlayerId == empire.PlayerId).Kind = PlayerKind.AI;
        return empire;
    }

    private static Empire MakeEmpireAi(GameState state, string empireName)
    {
        var empire = state.Empires.Single(item => item.EmpireName == empireName);
        state.Players.Single(player => player.PlayerId == empire.PlayerId).Kind = PlayerKind.AI;
        return empire;
    }

    private static FleetOrder[] GetAiOrders(GameState state, Guid empireId)
    {
        var fleetIds = state.Fleets
            .Where(fleet => fleet.EmpireId == empireId)
            .Select(fleet => fleet.FleetId)
            .ToHashSet();
        return state.FleetOrders
            .Where(order => fleetIds.Contains(order.FleetId)
                            && order.CommandSource == FleetOrderCommandSource.GameAiPlanner)
            .OrderBy(order => order.SealedTick)
            .ThenBy(order => order.FleetId)
            .ToArray();
    }

}
