using Cycles.Core;

namespace Cycles.Tests;

public sealed class CycleEndTests
{
    [Fact]
    public void CompleteCyclePersistsFinalRankingsAndMarksCycleCompleted()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        cycle.CurrentTickNumber = 12;
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");

        var rankings = CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        Assert.Equal(CycleStatus.Completed, cycle.Status);
        Assert.Equal(2, rankings.Count);
        Assert.Equal(2, state.CycleRankings.Count);

        var winner = rankings.Single(ranking => ranking.IsWinner);
        var runnerUp = rankings.Single(ranking => !ranking.IsWinner);
        Assert.Equal(firstEmpire.EmpireId, winner.EmpireId);
        Assert.Equal(secondEmpire.EmpireId, runnerUp.EmpireId);
        Assert.Equal(1, winner.Rank);
        Assert.Equal(2, runnerUp.Rank);
        Assert.Equal(80m, winner.MapControlPercent);
        Assert.Equal(20m, runnerUp.MapControlPercent);
        Assert.Equal(12, winner.CutoffTickNumber);
        Assert.Equal(TestState.Now, winner.CutoffAt);

        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Equal(EventSeverity.Historic, completionEvent.Severity);
        Assert.Equal(winner.EmpireId, completionEvent.EmpireId);
    }

    [Fact]
    public void CompleteCycleRejectsAlreadyCompletedCycle()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        cycle.Status = CycleStatus.Completed;

        var ex = Assert.Throws<InvalidOperationException>(() => CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now));

        Assert.Contains("Only active cycles", ex.Message, StringComparison.Ordinal);
        Assert.Empty(state.CycleRankings);
    }

    [Fact]
    public void CompleteCycleIncreasesHistoricalSignificanceForBattleSystems()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var system = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        state.BattleRecords.Add(CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 3, defenderLosses: 4));
        state.BattleRecords.Add(CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 12, defenderLosses: 8));

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        Assert.Equal(3, system.HistoricalSignificance);
        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Contains("historicalSignals", completionEvent.FactJson, StringComparison.Ordinal);
    }

    private static BattleRecord CreateBattle(
        Guid cycleId,
        Guid systemId,
        Guid attackerEmpireId,
        Guid defenderEmpireId,
        int attackerLosses,
        int defenderLosses) =>
        new()
        {
            CycleId = cycleId,
            TickNumber = 1,
            SystemId = systemId,
            AttackerEmpireId = attackerEmpireId,
            DefenderEmpireId = defenderEmpireId,
            AttackerFleetIds = Guid.NewGuid().ToString(),
            DefenderFleetIds = Guid.NewGuid().ToString(),
            AttackerShipsBefore = 80,
            DefenderShipsBefore = 20,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
}
