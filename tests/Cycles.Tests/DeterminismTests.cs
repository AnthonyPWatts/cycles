using Cycles.Core;

namespace Cycles.Tests;

public sealed class DeterminismTests
{
    [Fact]
    public void SeededGalaxyUsesSeedForStableLayoutFields()
    {
        var first = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 12345);
        var second = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 12345);

        Assert.Equal(ProjectSystems(first), ProjectSystems(second));
        Assert.Equal(ProjectLinks(first), ProjectLinks(second));
        Assert.Equal(ProjectHomes(first), ProjectHomes(second));
    }

    [Fact]
    public void CombatResolutionUsesPersistedIdsAndTickForDeterministicOutcome()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 60, defenderShips: 55);
        var first = state.DeepClone();
        var second = state.DeepClone();

        var firstResult = ResolveContest(first);
        var secondResult = ResolveContest(second);

        Assert.Equal(firstResult.Outcome, secondResult.Outcome);
        Assert.Equal(firstResult.AttackerLosses, secondResult.AttackerLosses);
        Assert.Equal(firstResult.DefenderLosses, secondResult.DefenderLosses);
        Assert.Equal(ProjectFleets(first), ProjectFleets(second));
    }

    private static BattleRecord ResolveContest(GameState state)
    {
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var system = state.Systems.Single(system => system.SystemName == "Contest");
        var attacker = state.Fleets.Single(fleet => fleet.EmpireId == firstEmpire.EmpireId);
        var defenders = state.Fleets
            .Where(fleet => fleet.CycleId == attacker.CycleId && fleet.EmpireId != attacker.EmpireId)
            .ToArray();

        return CombatResolver.Resolve(state, tickNumber: 7, TestState.Now, system, attacker, defenders);
    }

    private static IReadOnlyList<SeededSystem> ProjectSystems(GameState state) =>
        state.Systems
            .OrderBy(system => system.SystemName)
            .Select(system => new SeededSystem(
                system.SystemName,
                system.X,
                system.Y,
                system.IndustryOutput,
                system.ResearchOutput,
                system.PopulationOutput,
                system.StrategicValue,
                system.HistoricalSignificance))
            .ToArray();

    private static IReadOnlyList<SeededLink> ProjectLinks(GameState state)
    {
        var systemNames = state.Systems.ToDictionary(system => system.SystemId, system => system.SystemName);
        return state.SystemLinks
            .Select(link =>
            {
                var first = systemNames[link.SystemAId];
                var second = systemNames[link.SystemBId];
                return string.CompareOrdinal(first, second) < 0
                    ? new SeededLink(first, second, link.Distance, link.TravelTicks)
                    : new SeededLink(second, first, link.Distance, link.TravelTicks);
            })
            .OrderBy(link => link.First)
            .ThenBy(link => link.Second)
            .ToArray();
    }

    private static IReadOnlyList<HomeAssignment> ProjectHomes(GameState state)
    {
        var systemNames = state.Systems.ToDictionary(system => system.SystemId, system => system.SystemName);
        return state.Empires
            .OrderBy(empire => empire.EmpireName)
            .Select(empire => new HomeAssignment(empire.EmpireName, systemNames[empire.HomeSystemId]))
            .ToArray();
    }

    private static IReadOnlyList<FleetOutcome> ProjectFleets(GameState state) =>
        state.Fleets
            .OrderBy(fleet => fleet.FleetName)
            .Select(fleet => new FleetOutcome(fleet.FleetName, fleet.ShipCount, fleet.Status))
            .ToArray();

    private sealed record SeededSystem(
        string Name,
        int X,
        int Y,
        decimal Industry,
        decimal Research,
        decimal Population,
        int StrategicValue,
        int HistoricalSignificance);

    private sealed record SeededLink(string First, string Second, decimal Distance, int TravelTicks);

    private sealed record HomeAssignment(string EmpireName, string HomeSystemName);

    private sealed record FleetOutcome(string FleetName, int ShipCount, FleetStatus Status);
}
