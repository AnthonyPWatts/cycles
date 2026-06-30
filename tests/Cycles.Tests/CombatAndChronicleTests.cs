using Cycles.Core;

namespace Cycles.Tests;

public sealed class CombatAndChronicleTests
{
    [Fact]
    public void AttackOrderCreatesBattleRecordEventAndChronicleEntry()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 160, defenderShips: 140, strategicValue: 65, historicalSignificance: 2);
        var cycle = state.GetActiveCycle()!;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);

        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);
        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var battle = Assert.Single(state.BattleRecords);
        Assert.Contains(attackerFleet.FleetId.ToString(), battle.AttackerFleetIds, StringComparison.Ordinal);
        Assert.Contains(state.Events, item => item.EventType == EventType.CombatResolved && item.FactJson == battle.FactJson);
        var entry = Assert.Single(state.ChronicleEntries, entry => entry.SourceBattleId == battle.BattleId);
        Assert.NotEqual(entry.FactualSummary, entry.NarrativeText);
        Assert.Contains("First", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("Second", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("Contest", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("tick 1", entry.NarrativeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains((battle.AttackerLosses + battle.DefenderLosses).ToString(), entry.NarrativeText, StringComparison.Ordinal);
    }

    [Fact]
    public void CombatResolutionIsDeterministicForTheSameFacts()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 180, strategicValue: 45, historicalSignificance: 1);
        var first = state.DeepClone();
        var second = state.DeepClone();

        RunAttack(first);
        RunAttack(second);

        var firstBattle = Assert.Single(first.BattleRecords);
        var secondBattle = Assert.Single(second.BattleRecords);
        Assert.Equal(firstBattle.Outcome, secondBattle.Outcome);
        Assert.Equal(firstBattle.AttackerLosses, secondBattle.AttackerLosses);
        Assert.Equal(firstBattle.DefenderLosses, secondBattle.DefenderLosses);
    }

    [Fact]
    public void MultiFleetDefenderLossesAreDistributedAndDestroyedFleetsAreMarked()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 200, defenderShips: 1);
        var cycle = state.GetActiveCycle()!;
        var defenderEmpireId = state.Empires[1].EmpireId;
        var systemId = state.Systems.Single().SystemId;
        state.Fleets.Add(new Fleet
        {
            CycleId = cycle.CycleId,
            EmpireId = defenderEmpireId,
            FleetName = "Second defender",
            CurrentSystemId = systemId,
            ShipCount = 1,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        });

        RunAttack(state);

        var battle = Assert.Single(state.BattleRecords);
        Assert.Equal(2, battle.DefenderShipsBefore);
        Assert.True(battle.DefenderLosses >= 1);
        Assert.All(state.Fleets.Where(fleet => fleet.EmpireId == defenderEmpireId && fleet.ShipCount == 0), fleet =>
        {
            Assert.Equal(FleetStatus.Destroyed, fleet.Status);
        });
    }

    [Fact]
    public void ChronicleScoringFavoursMajorBattles()
    {
        var system = new GalaxySystem { StrategicValue = 30, HistoricalSignificance = 1 };
        var minor = new BattleRecord
        {
            AttackerShipsBefore = 10,
            DefenderShipsBefore = 10,
            AttackerLosses = 2,
            DefenderLosses = 3,
            Outcome = BattleOutcome.AttackerVictory
        };
        var major = new BattleRecord
        {
            AttackerShipsBefore = 80,
            DefenderShipsBefore = 180,
            AttackerLosses = 20,
            DefenderLosses = 160,
            Outcome = BattleOutcome.AttackerVictory
        };

        Assert.True(ChronicleScoring.ScoreBattle(major, system) > ChronicleScoring.ScoreBattle(minor, system));
        Assert.True(ChronicleScoring.ScoreBattle(major, system) >= ChronicleScoring.ChronicleThreshold);
    }

    [Fact]
    public void ChronicleBattleReportIncludesRequiredFacts()
    {
        var battle = new BattleRecord
        {
            CycleId = Guid.NewGuid(),
            TickNumber = 7,
            SystemId = Guid.NewGuid(),
            AttackerEmpireId = Guid.NewGuid(),
            DefenderEmpireId = Guid.NewGuid(),
            AttackerShipsBefore = 40,
            DefenderShipsBefore = 120,
            AttackerLosses = 9,
            DefenderLosses = 81,
            Outcome = BattleOutcome.AttackerVictory
        };
        var system = new GalaxySystem
        {
            SystemId = battle.SystemId,
            CycleId = battle.CycleId,
            SystemName = "Aster Vale",
            StrategicValue = 45,
            HistoricalSignificance = 2
        };
        var sourceEvent = new EventRecord { EventId = Guid.NewGuid(), CycleId = battle.CycleId };
        var attacker = new Empire { EmpireId = battle.AttackerEmpireId, EmpireName = "Aurelian Compact" };
        var defender = new Empire { EmpireId = battle.DefenderEmpireId, EmpireName = "Khepri Mandate" };

        var entry = ChronicleScoring.CreateBattleEntry(battle, sourceEvent, system, attacker, defender, importance: 90, TestState.Now);

        Assert.Equal(sourceEvent.EventId, entry.SourceEventId);
        Assert.Equal(battle.BattleId, entry.SourceBattleId);
        Assert.Equal("attacker victory", ExtractOutcome(entry.FactualSummary));
        Assert.Contains("Aurelian Compact", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("Khepri Mandate", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("Aster Vale", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("tick 7", entry.NarrativeText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("9", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("81", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("90", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("outnumbered 40 to 120", entry.NarrativeText, StringComparison.Ordinal);
    }

    private static void RunAttack(GameState state)
    {
        var cycle = state.GetActiveCycle()!;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
    }

    private static string ExtractOutcome(string factualSummary)
    {
        const string marker = "Outcome: ";
        var start = factualSummary.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0);
        start += marker.Length;
        var end = factualSummary.IndexOf('.', start);
        Assert.True(end > start);
        return factualSummary[start..end];
    }
}
