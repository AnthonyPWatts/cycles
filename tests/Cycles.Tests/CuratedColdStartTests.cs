using System.Text.Json;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class CuratedColdStartTests
{
    [Fact]
    public void Development_match_seeds_three_separated_empires_and_neutral_pressure()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var empires = state.Empires.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var humanNames = state.Players.Where(item => item.Kind == PlayerKind.Human).Select(item => item.Username).Order().ToArray();
        var neutral = Assert.Single(state.Factions, item => item.Kind == FactionKind.Neutral);

        Assert.Equal(["Tony", "Will"], humanNames);
        Assert.Equal("Ariadne", Assert.Single(state.Players, item => item.Kind == PlayerKind.AI).Username);
        Assert.Equal(3, empires.Length);
        Assert.Equal(3, state.MatchParticipants.Count(item => item.CycleId == cycle.CycleId));
        Assert.Equal(3, empires.Select(item => state.Systems.Single(system => system.SystemId == item.HomeSystemId).SectorId).Distinct().Count());
        Assert.Equal(6, state.Fleets.Count(item => item.FactionId == neutral.FactionId));
        Assert.All(
            state.Fleets.Where(item => item.FactionId == neutral.FactionId),
            fleet => Assert.Equal(neutral.FactionId, state.GetFleetFaction(fleet).FactionId));

        var homeOutputs = empires
            .Select(item => state.Systems.Single(system => system.SystemId == item.HomeSystemId))
            .Select(item => item.IndustryOutput + item.ResearchOutput + item.PopulationOutput)
            .ToArray();
        var homeStrategicValues = empires
            .Select(item => state.Systems.Single(system => system.SystemId == item.HomeSystemId).StrategicValue)
            .ToArray();
        Assert.True(homeOutputs.Max() - homeOutputs.Min() <= homeOutputs.Min() * 0.15m);
        Assert.True(homeStrategicValues.Max() - homeStrategicValues.Min() <= 5);

        var briefings = state.Events.Where(item => item.EventType == EventType.OpeningBriefingIssued).ToArray();
        Assert.Equal(3, briefings.Length);
        foreach (var empire in empires)
        {
            Assert.Equal(60, state.Fleets.Where(item => item.EmpireId == empire.EmpireId).Sum(item => item.ShipCount));
            var homeGuard = Assert.Single(state.Fleets, item => item.EmpireId == empire.EmpireId && item.CurrentSystemId == empire.HomeSystemId);
            Assert.NotNull(homeGuard.AdmiralId);

            var briefing = Assert.Single(briefings, item => item.EmpireId == empire.EmpireId);
            using var facts = JsonDocument.Parse(briefing.FactJson);
            Assert.Equal(GameSeeder.CuratedColdStartScenarioKey, facts.RootElement.GetProperty("scenarioKey").GetString());
            Assert.Equal(GameSeeder.DefaultDevelopmentScenarioSeed, facts.RootElement.GetProperty("scenarioSeed").GetInt32());
            Assert.Equal(GameSeeder.CanonicalGalaxyTopologyKey, facts.RootElement.GetProperty("mapVersion").GetString());
            var objectives = facts.RootElement.GetProperty("objectives");
            var moveFleet = state.Fleets.Single(item => item.FleetId == ReadGuid(objectives, "move", "fleetId"));
            var moveTarget = ReadGuid(objectives, "move", "targetSystemId");
            var surveyFleet = state.Fleets.Single(item => item.FleetId == ReadGuid(objectives, "colonise", "fleetId"));
            var surveySystem = ReadGuid(objectives, "colonise", "systemId");
            var vanguard = state.Fleets.Single(item => item.FleetId == ReadGuid(objectives, "attack", "fleetId"));
            var flashpoint = ReadGuid(objectives, "attack", "systemId");

            Assert.Contains(state.SystemLinks, item => item.Connects(moveFleet.CurrentSystemId, moveTarget));
            Assert.Equal(surveySystem, surveyFleet.CurrentSystemId);
            Assert.Equal(flashpoint, vanguard.CurrentSystemId);
            Assert.Equal(neutral.FactionId, ReadGuid(objectives, "attack", "targetFactionId"));
            Assert.Contains(state.Fleets, item => item.FactionId == neutral.FactionId && item.CurrentSystemId == flashpoint);
        }
    }

    [Fact]
    public void Following_one_opening_briefing_produces_a_move_outpost_and_neutral_battle()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var tony = state.Players.Single(item => item.Username == "Tony");
        var participant = state.GetParticipant(cycle.CycleId, tony.PlayerId)!;
        var briefing = state.Events.Single(item => item.EventType == EventType.OpeningBriefingIssued && item.EmpireId == participant.EmpireId);
        using var facts = JsonDocument.Parse(briefing.FactJson);
        var objectives = facts.RootElement.GetProperty("objectives");
        var moveFleetId = ReadGuid(objectives, "move", "fleetId");
        var moveTargetId = ReadGuid(objectives, "move", "targetSystemId");
        var colonisingFleetId = ReadGuid(objectives, "colonise", "fleetId");
        var attackingFleetId = ReadGuid(objectives, "attack", "fleetId");
        var attackSystemId = ReadGuid(objectives, "attack", "systemId");
        var targetFactionId = ReadGuid(objectives, "attack", "targetFactionId");

        OrderService.SubmitMoveOrder(state, moveFleetId, moveTargetId, TestState.Now);
        OrderService.SubmitColoniseOrder(state, colonisingFleetId, TestState.Now);
        OrderService.SubmitAttackOrderAgainstFaction(state, attackingFleetId, targetFactionId, TestState.Now);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(TickLogStatus.Completed, result.Status);
        Assert.Equal(3, result.OrdersProcessed);
        Assert.Equal(moveTargetId, state.Fleets.Single(item => item.FleetId == moveFleetId).CurrentSystemId);
        Assert.Contains(state.ColonialOutposts, item => item.SystemId == state.Fleets.Single(fleet => fleet.FleetId == colonisingFleetId).CurrentSystemId);
        var battle = Assert.Single(state.BattleRecords, item => item.SystemId == attackSystemId);
        Assert.Equal(targetFactionId, battle.DefenderFactionId);
        Assert.Equal(Guid.Empty, battle.DefenderEmpireId);
        Assert.Contains(state.Events, item => item.EventType == EventType.CombatResolved && item.FactJson == battle.FactJson);
        Assert.DoesNotContain(state.DiplomaticRelationships, item => item.CycleId == cycle.CycleId);
    }

    [Fact]
    public void Development_match_replays_stably_and_scenario_seed_changes_assignments_not_players()
    {
        var first = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var replay = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var variant = GameSeeder.CreateDevelopmentMatch(GameSeeder.DefaultDevelopmentScenarioSeed + 1, TestState.Now);

        Assert.Equal(first.Cycles.Single().CycleId, replay.Cycles.Single().CycleId);
        Assert.Equal(
            first.Fleets.OrderBy(item => item.FleetName).Select(item => (item.FleetId, item.CurrentSystemId, item.ShipCount)),
            replay.Fleets.OrderBy(item => item.FleetName).Select(item => (item.FleetId, item.CurrentSystemId, item.ShipCount)));
        Assert.Equal(
            first.Events.Where(item => item.EventType == EventType.OpeningBriefingIssued).OrderBy(item => item.EmpireId).Select(item => item.FactJson),
            replay.Events.Where(item => item.EventType == EventType.OpeningBriefingIssued).OrderBy(item => item.EmpireId).Select(item => item.FactJson));
        Assert.Equal(
            first.Players.OrderBy(item => item.Username).Select(item => (item.PlayerId, item.Username, item.Kind)),
            variant.Players.OrderBy(item => item.Username).Select(item => (item.PlayerId, item.Username, item.Kind)));
        Assert.NotEqual(
            first.MatchParticipants.Single(item => item.PlayerId == first.Players.Single(player => player.Username == "Tony").PlayerId).EmpireId,
            variant.MatchParticipants.Single(item => item.PlayerId == variant.Players.Single(player => player.Username == "Tony").PlayerId).EmpireId);
    }

    private static Guid ReadGuid(JsonElement objectives, string objective, string property) =>
        objectives.GetProperty(objective).GetProperty(property).GetGuid();
}
