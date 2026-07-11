using Cycles.Core;

namespace Cycles.Tests;

public sealed class BalanceScenarioTests
{
    [Fact]
    public void ScenarioIsRepeatableForTheSameInputs()
    {
        var options = new BalanceScenarioOptions(TickCount: 24, SystemCount: 12, EmpireCount: 3, Seed: 90210);

        var first = BalanceScenarioRunner.Run(options);
        var second = BalanceScenarioRunner.Run(options);

        Assert.Equal(Project(first), Project(second));
    }

    [Fact]
    public void ScenarioExercisesExistingEconomyColonisationAndCombatRules()
    {
        var result = BalanceScenarioRunner.Run(new BalanceScenarioOptions(
            TickCount: 48,
            SystemCount: 12,
            EmpireCount: 3,
            Seed: 71421));

        Assert.Equal(48, result.CompletedTicks);
        Assert.True(result.OrdersProcessed > 0);
        Assert.True(result.ColonialOutposts > 0);
        Assert.True(result.Battles > 0);
        Assert.True(result.CompletedShipConstructions > 0);
        Assert.Equal(3, result.DoctrineUnlocks);
        Assert.All(result.Empires, empire =>
        {
            Assert.True(empire.Industry >= 0);
            Assert.True(empire.Research >= 0);
            Assert.True(empire.Population >= 0);
            Assert.True(empire.ActiveShips >= 0);
        });
    }

    [Fact]
    public void ComparisonExercisesDistinctStrategicPolicies()
    {
        var results = BalanceScenarioRunner.Compare(new BalanceScenarioOptions(
            TickCount: 48,
            SystemCount: 12,
            EmpireCount: 3,
            Seed: 71421));

        Assert.Equal(Enum.GetValues<BalanceScenarioStrategy>(), results.Select(result => result.Options.Strategy));
        Assert.All(results, result => Assert.Equal(48, result.CompletedTicks));

        var military = results.Single(result => result.Options.Strategy == BalanceScenarioStrategy.Military);
        var expansion = results.Single(result => result.Options.Strategy == BalanceScenarioStrategy.Expansion);
        var cautious = results.Single(result => result.Options.Strategy == BalanceScenarioStrategy.Cautious);

        Assert.True(military.CompletedShips > expansion.CompletedShips);
        Assert.Equal(0, military.ColonialOutposts);
        Assert.True(expansion.ColonialOutposts > military.ColonialOutposts);
        Assert.Equal(0, cautious.Battles);
    }

    [Fact]
    public void ScenarioRejectsInvalidDimensions()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceScenarioRunner.Run(
            new BalanceScenarioOptions(TickCount: 0, SystemCount: 8, EmpireCount: 2)));
        Assert.Throws<ArgumentOutOfRangeException>(() => BalanceScenarioRunner.Run(
            new BalanceScenarioOptions(TickCount: 8, SystemCount: 1, EmpireCount: 2)));
    }

    [Fact]
    public void ScenarioStopsWithEvidenceBeforeRetainedHistoryBecomesUnbounded()
    {
        var result = BalanceScenarioRunner.Run(new BalanceScenarioOptions(
            TickCount: 500,
            SystemCount: 12,
            EmpireCount: 3,
            Seed: 71421,
            RetainedRecordLimit: 500));

        Assert.True(result.CompletedTicks < result.Options.TickCount);
        Assert.True(result.RetainedRecords >= result.Options.RetainedRecordLimit);
        Assert.Contains("retained simulation records", result.StopReason, StringComparison.Ordinal);
    }

    private static BalanceProjection Project(BalanceScenarioResult result) => new(
        result.RendezvousSystem,
        result.CompletedTicks,
        result.OrdersProcessed,
        result.Battles,
        result.ChronicleEntries,
        result.ColonialOutposts,
        result.CompletedShipConstructions,
        result.CompletedShips,
        result.DoctrineUnlocks,
        result.MapControlGap,
        result.RetainedRecords,
        result.StopReason,
        result.Empires.Select(empire => new EmpireProjection(
            empire.EmpireName,
            empire.ActiveShips,
            empire.ShipGrowthFactor,
            empire.Industry,
            empire.Research,
            empire.Population,
            empire.ColonialOutposts,
            empire.MapControlPercent,
            empire.BattlesWon,
            empire.BattlesLost)).ToArray());

    private record BalanceProjection(
        string RendezvousSystem,
        int CompletedTicks,
        int OrdersProcessed,
        int Battles,
        int ChronicleEntries,
        int ColonialOutposts,
        int CompletedShipConstructions,
        int CompletedShips,
        int DoctrineUnlocks,
        decimal MapControlGap,
        int RetainedRecords,
        string? StopReason,
        IReadOnlyList<EmpireProjection> Empires)
    {
        public virtual bool Equals(BalanceProjection? other) =>
            other is not null
            && RendezvousSystem == other.RendezvousSystem
            && CompletedTicks == other.CompletedTicks
            && OrdersProcessed == other.OrdersProcessed
            && Battles == other.Battles
            && ChronicleEntries == other.ChronicleEntries
            && ColonialOutposts == other.ColonialOutposts
            && CompletedShipConstructions == other.CompletedShipConstructions
            && CompletedShips == other.CompletedShips
            && DoctrineUnlocks == other.DoctrineUnlocks
            && MapControlGap == other.MapControlGap
            && RetainedRecords == other.RetainedRecords
            && StopReason == other.StopReason
            && Empires.SequenceEqual(other.Empires);

        public override int GetHashCode()
        {
            var hash = new HashCode();
            hash.Add(RendezvousSystem);
            hash.Add(CompletedTicks);
            hash.Add(OrdersProcessed);
            hash.Add(Battles);
            hash.Add(ChronicleEntries);
            hash.Add(ColonialOutposts);
            hash.Add(CompletedShipConstructions);
            hash.Add(CompletedShips);
            hash.Add(DoctrineUnlocks);
            hash.Add(RetainedRecords);
            hash.Add(StopReason);
            return hash.ToHashCode();
        }
    }

    private sealed record EmpireProjection(
        string EmpireName,
        int ActiveShips,
        decimal ShipGrowthFactor,
        decimal Industry,
        decimal Research,
        decimal Population,
        int ColonialOutposts,
        decimal MapControlPercent,
        int BattlesWon,
        int BattlesLost);
}
