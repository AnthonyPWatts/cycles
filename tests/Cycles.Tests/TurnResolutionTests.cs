using Cycles.Core;

namespace Cycles.Tests;

public sealed class TurnResolutionTests
{
    [Fact]
    public void TickSealsOneDeterministicIntentionForEveryActiveFleet()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var tony = state.Players.Single(item => item.Username == "Tony");
        var tonyEmpireId = state.GetParticipant(cycle.CycleId, tony.PlayerId)!.EmpireId;
        var commandedFleet = state.Fleets
            .Where(item => item.EmpireId == tonyEmpireId)
            .OrderBy(item => item.FleetId)
            .First();
        OrderService.SubmitHoldOrder(state, commandedFleet.FleetId, TestState.Now);

        var replay = state.DeepClone();
        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));
        new TickEngine().RunTick(replay, cycle.CycleId, TestState.Now.AddHours(1));

        var sealedOrders = state.FleetOrders.Where(item => item.SealedTick == 1).ToArray();
        Assert.Equal(15, result.OrdersProcessed);
        Assert.Equal(15, sealedOrders.Length);
        Assert.All(sealedOrders, order =>
        {
            Assert.Equal(FleetOrderStatus.Processed, order.Status);
            Assert.Equal(TestState.Now.AddHours(1), order.SealedAt);
        });
        Assert.Single(sealedOrders, item => item.CommandSource == FleetOrderCommandSource.Human);
        Assert.Equal(5, sealedOrders.Count(item => item.CommandSource == FleetOrderCommandSource.ImplicitHold));
        Assert.Equal(3, sealedOrders.Count(item => item.CommandSource == FleetOrderCommandSource.GameAiPlanner));
        Assert.Equal(6, sealedOrders.Count(item => item.CommandSource == FleetOrderCommandSource.NeutralPlanner));
        Assert.Equal(
            sealedOrders.OrderBy(item => item.FleetId).Select(item => item.FleetOrderId),
            replay.FleetOrders.Where(item => item.SealedTick == 1).OrderBy(item => item.FleetId).Select(item => item.FleetOrderId));
        Assert.Equal(TurnResolutionStage.CommandOpen, state.Cycles.Single().TurnStage);
    }

    [Fact]
    public void MovementPrecedesCombatRegardlessOfSubmissionTimestamp()
    {
        var source = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 40);
        var cycle = source.GetActiveCycle()!;
        var contest = source.Systems.Single(item => item.SystemName == "Contest");
        var retreat = new GalaxySystem
        {
            CycleId = cycle.CycleId,
            SystemName = "Retreat",
            IndustryOutput = 0,
            ResearchOutput = 0,
            PopulationOutput = 0,
            StrategicValue = 1,
            CreatedAt = TestState.Now
        };
        source.Systems.Add(retreat);
        source.SystemLinks.Add(new SystemLink
        {
            CycleId = cycle.CycleId,
            SystemAId = contest.SystemId,
            SystemBId = retreat.SystemId,
            Distance = 1,
            TravelTicks = 1
        });
        var attacker = source.Empires.Single(item => item.EmpireName == "First");
        var defender = source.Empires.Single(item => item.EmpireName == "Second");
        var attackerFleet = source.Fleets.Single(item => item.EmpireId == attacker.EmpireId);
        var defenderFleet = source.Fleets.Single(item => item.EmpireId == defender.EmpireId);
        var attack = OrderService.SubmitAttackOrder(source, attackerFleet.FleetId, defender.EmpireId, TestState.Now);
        var move = OrderService.SubmitMoveOrder(source, defenderFleet.FleetId, retreat.SystemId, TestState.Now.AddMinutes(1));

        var attackSubmittedFirst = source.DeepClone();
        var moveSubmittedFirst = source.DeepClone();
        moveSubmittedFirst.FleetOrders.Single(item => item.FleetOrderId == attack.FleetOrderId).CreatedAt = TestState.Now.AddMinutes(1);
        moveSubmittedFirst.FleetOrders.Single(item => item.FleetOrderId == move.FleetOrderId).CreatedAt = TestState.Now;

        new TickEngine().RunTick(attackSubmittedFirst, cycle.CycleId, TestState.Now.AddHours(1));
        new TickEngine().RunTick(moveSubmittedFirst, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Empty(attackSubmittedFirst.BattleRecords);
        Assert.Empty(moveSubmittedFirst.BattleRecords);
        Assert.Equal(retreat.SystemId, attackSubmittedFirst.Fleets.Single(item => item.FleetId == defenderFleet.FleetId).CurrentSystemId);
        Assert.Equal(retreat.SystemId, moveSubmittedFirst.Fleets.Single(item => item.FleetId == defenderFleet.FleetId).CurrentSystemId);
        Assert.Equal(
            ProjectOrderOutcomes(attackSubmittedFirst),
            ProjectOrderOutcomes(moveSubmittedFirst));
    }

    [Fact]
    public void DueShipsStayBehindWhenTheExistingHomeFleetHasASealedMove()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var home = state.Systems.Single(item => item.SystemName == "Origin");
        var destination = state.Systems.Single(item => item.SystemName == "Destination");
        var fleet = Assert.Single(state.Fleets);
        var construction = new ShipConstruction
        {
            CycleId = cycle.CycleId,
            EmpireId = fleet.EmpireId,
            ShipCount = 7,
            IndustrySpent = 175,
            StartedTick = 0,
            CompleteAfterTick = 1,
            Status = ShipConstructionStatus.Queued,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        state.ShipConstructions.Add(construction);
        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var movedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
        var reinforcements = state.Fleets.Single(item => item.FleetId == construction.ShipConstructionId);
        Assert.Equal(destination.SystemId, movedFleet.CurrentSystemId);
        Assert.Equal(25, movedFleet.ShipCount);
        Assert.Equal(home.SystemId, reinforcements.CurrentSystemId);
        Assert.Equal(7, reinforcements.ShipCount);
        Assert.DoesNotContain(state.FleetOrders, item => item.FleetId == reinforcements.FleetId);
    }

    [Fact]
    public void SameFactionAttacksAtOneSystemResolveAsOneBattle()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 40, defenderShips: 60);
        var cycle = state.GetActiveCycle()!;
        var first = state.Empires.Single(item => item.EmpireName == "First");
        var second = state.Empires.Single(item => item.EmpireName == "Second");
        var system = state.Systems.Single(item => item.SystemName == "Contest");
        var firstFleet = state.Fleets.Single(item => item.EmpireId == first.EmpireId);
        var secondFleet = new Fleet
        {
            CycleId = cycle.CycleId,
            EmpireId = first.EmpireId,
            FactionId = state.GetEmpireFaction(first.EmpireId).FactionId,
            FleetName = "Second attack fleet",
            CurrentSystemId = system.SystemId,
            ShipCount = 30,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        };
        state.Fleets.Add(secondFleet);
        OrderService.SubmitAttackOrder(state, firstFleet.FleetId, second.EmpireId, TestState.Now);
        OrderService.SubmitAttackOrder(state, secondFleet.FleetId, second.EmpireId, TestState.Now.AddMinutes(1));

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var battle = Assert.Single(state.BattleRecords);
        Assert.Equal(70, battle.AttackerShipsBefore);
        Assert.Equal(60, battle.DefenderShipsBefore);
        Assert.Equal(
            new[] { firstFleet.FleetId, secondFleet.FleetId }.Order(),
            battle.AttackerFleetIds.Split(',').Select(Guid.Parse).Order());
        Assert.Equal(2, state.FleetOrders.Count(item =>
            item.OrderType == FleetOrderType.Attack && item.Status == FleetOrderStatus.Processed));
    }

    [Fact]
    public void ProgressionUnlockedByResolutionAffectsTheNextCommandWindow()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var resources = Assert.Single(state.EmpireResources);
        resources.Research = EconomyProcessor.SurveyProjectionResearchThreshold - 40m;
        var system = Assert.Single(state.Systems);
        system.IndustryOutput = 0;
        system.ResearchOutput = 40m;
        system.PopulationOutput = 0;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Single(state.Events, item => item.EventType == EventType.DoctrineUnlocked);
        Assert.Equal(100m, state.EmpireMetrics.Single(item => item.TickNumber == 1).TotalEffectivePresence);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(2));

        Assert.Equal(105m, state.EmpireMetrics.Single(item => item.TickNumber == 2).TotalEffectivePresence);
    }

    [Fact]
    public void HumanCommandsAreRejectedAfterTheWindowStartsClosing()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var empire = Assert.Single(state.Empires);
        cycle.TurnStage = TurnResolutionStage.Closing;

        var orderError = Assert.Throws<InvalidOperationException>(
            () => OrderService.SubmitHoldOrder(state, fleet.FleetId, TestState.Now));
        var priorityError = Assert.Throws<InvalidOperationException>(() => OrderService.UpdatePriorities(
            state,
            empire.EmpireId,
            0,
            0,
            0,
            100,
            TestState.Now));

        Assert.Contains("not open", orderError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not open", priorityError.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(state.FleetOrders);
    }

    private static IReadOnlyList<(Guid FleetId, FleetOrderStatus Status, string? RejectionReason)> ProjectOrderOutcomes(
        GameState state) =>
        state.FleetOrders
            .OrderBy(item => item.FleetId)
            .Select(item => (item.FleetId, item.Status, item.RejectionReason))
            .ToArray();
}
