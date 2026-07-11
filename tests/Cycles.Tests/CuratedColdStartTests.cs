using System.Text.Json;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class CuratedColdStartTests
{
    [Fact]
    public void Curated_cold_start_exposes_three_valid_opening_objectives()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var aurelian = state.Empires.Single(empire => empire.EmpireName == "Aurelian Compact");
        var khepri = state.Empires.Single(empire => empire.EmpireName == "Khepri Mandate");
        var briefing = Assert.Single(state.Events, item => item.EventType == EventType.OpeningBriefingIssued);

        using var facts = JsonDocument.Parse(briefing.FactJson);
        Assert.Equal(GameSeeder.CuratedColdStartScenarioKey, facts.RootElement.GetProperty("scenarioKey").GetString());
        var objectives = facts.RootElement.GetProperty("objectives");
        var moveFleet = state.Fleets.Single(fleet => fleet.FleetId == ReadGuid(objectives, "move", "fleetId"));
        var moveTarget = state.Systems.Single(system => system.SystemId == ReadGuid(objectives, "move", "targetSystemId"));
        var colonisingFleet = state.Fleets.Single(fleet => fleet.FleetId == ReadGuid(objectives, "colonise", "fleetId"));
        var colonisingSystem = state.Systems.Single(system => system.SystemId == ReadGuid(objectives, "colonise", "systemId"));
        var attackingFleet = state.Fleets.Single(fleet => fleet.FleetId == ReadGuid(objectives, "attack", "fleetId"));
        var attackSystem = state.Systems.Single(system => system.SystemId == ReadGuid(objectives, "attack", "systemId"));
        var targetEmpireId = ReadGuid(objectives, "attack", "targetEmpireId");

        Assert.Equal(cycle.CycleId, briefing.CycleId);
        Assert.Equal(aurelian.EmpireId, briefing.EmpireId);
        Assert.Equal("Aurelian Home Guard", moveFleet.FleetName);
        Assert.Equal("Nadir Crossing", moveTarget.SystemName);
        Assert.Contains(state.SystemLinks, link => link.Connects(moveFleet.CurrentSystemId, moveTarget.SystemId));
        Assert.Equal("Pale Harbour Survey", colonisingFleet.FleetName);
        Assert.Equal("Pale Harbour", colonisingSystem.SystemName);
        Assert.Equal(colonisingSystem.SystemId, colonisingFleet.CurrentSystemId);
        Assert.Equal("Treaty Gate Vanguard", attackingFleet.FleetName);
        Assert.Equal("Treaty Gate", attackSystem.SystemName);
        Assert.Equal(attackSystem.SystemId, attackingFleet.CurrentSystemId);
        Assert.Equal(khepri.EmpireId, targetEmpireId);
        Assert.Contains(state.Fleets, fleet => fleet.EmpireId == khepri.EmpireId && fleet.CurrentSystemId == attackSystem.SystemId);
        Assert.Equal(60, state.Fleets.Where(fleet => fleet.EmpireId == aurelian.EmpireId).Sum(fleet => fleet.ShipCount));
        Assert.Equal(60, state.Fleets.Where(fleet => fleet.EmpireId == khepri.EmpireId).Sum(fleet => fleet.ShipCount));
    }

    [Fact]
    public void Following_the_day_one_briefing_produces_a_move_outpost_battle_and_chronicle_entry()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var briefing = state.Events.Single(item => item.EventType == EventType.OpeningBriefingIssued);
        using var facts = JsonDocument.Parse(briefing.FactJson);
        var objectives = facts.RootElement.GetProperty("objectives");
        var moveFleetId = ReadGuid(objectives, "move", "fleetId");
        var moveTargetId = ReadGuid(objectives, "move", "targetSystemId");
        var colonisingFleetId = ReadGuid(objectives, "colonise", "fleetId");
        var attackingFleetId = ReadGuid(objectives, "attack", "fleetId");
        var attackSystemId = ReadGuid(objectives, "attack", "systemId");
        var targetEmpireId = ReadGuid(objectives, "attack", "targetEmpireId");

        OrderService.SubmitMoveOrder(state, moveFleetId, moveTargetId, TestState.Now);
        OrderService.SubmitColoniseOrder(state, colonisingFleetId, TestState.Now);
        OrderService.SubmitAttackOrder(state, attackingFleetId, targetEmpireId, TestState.Now);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(3, result.OrdersProcessed);
        Assert.All(state.FleetOrders, order => Assert.Equal(FleetOrderStatus.Processed, order.Status));
        Assert.Equal(moveTargetId, state.Fleets.Single(fleet => fleet.FleetId == moveFleetId).CurrentSystemId);
        Assert.Contains(state.ColonialOutposts, outpost => outpost.SystemId == state.Fleets.Single(fleet => fleet.FleetId == colonisingFleetId).CurrentSystemId);
        var battle = Assert.Single(state.BattleRecords, item => item.SystemId == attackSystemId);
        var chronicle = Assert.Single(state.ChronicleEntries, item => item.SourceEventId == state.Events.Single(eventRecord => eventRecord.EventType == EventType.CombatResolved && eventRecord.FactJson == battle.FactJson).EventId);
        Assert.True(chronicle.ImportanceScore >= ChronicleScoring.ChronicleThreshold);
        Assert.Contains(state.Events, item => item.EventType == EventType.FleetMoved && item.SystemId == moveTargetId);
        Assert.Contains(state.Events, item => item.EventType == EventType.ColonialOutpostEstablished);
        Assert.Contains(state.Events, item => item.EventType == EventType.ChronicleCreated);
    }

    [Fact]
    public void Curated_cold_start_uses_stable_persisted_identities()
    {
        var first = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var second = GameSeeder.CreateCuratedColdStart(TestState.Now);

        Assert.Equal(first.Cycles.Single().CycleId, second.Cycles.Single().CycleId);
        Assert.Equal(
            first.Fleets.OrderBy(fleet => fleet.FleetName).Select(fleet => fleet.FleetId),
            second.Fleets.OrderBy(fleet => fleet.FleetName).Select(fleet => fleet.FleetId));
        Assert.Equal(
            first.Events.Single(item => item.EventType == EventType.OpeningBriefingIssued).FactJson,
            second.Events.Single(item => item.EventType == EventType.OpeningBriefingIssued).FactJson);
    }

    private static Guid ReadGuid(JsonElement objectives, string objective, string property) =>
        objectives.GetProperty(objective).GetProperty(property).GetGuid();
}
