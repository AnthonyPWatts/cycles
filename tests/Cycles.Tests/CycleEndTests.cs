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
        var smallerBattle = CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 3, defenderLosses: 4);
        var largerBattle = CreateBattle(cycle.CycleId, system.SystemId, firstEmpire.EmpireId, secondEmpire.EmpireId, attackerLosses: 12, defenderLosses: 8);
        state.BattleRecords.Add(smallerBattle);
        state.BattleRecords.Add(largerBattle);

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        Assert.Equal(3, system.HistoricalSignificance);
        var signal = Assert.Single(state.SystemHistoricalSignals);
        Assert.Equal(system.SystemId, signal.SystemId);
        Assert.Equal(SystemHistoricalSignalType.BattleActivity, signal.SignalType);
        Assert.Equal(largerBattle.BattleId, signal.SourceBattleId);
        Assert.Equal(2, signal.BattleCount);
        Assert.Equal(27, signal.TotalLosses);
        Assert.Equal(20, signal.LargestBattleLosses);
        Assert.True(signal.HostedCycleLargestBattle);
        Assert.Equal(3, signal.HistoricalSignificanceIncrease);
        Assert.Equal(3, signal.HistoricalSignificanceAfter);
        Assert.Equal(TestState.Now, signal.CreatedAt);
        Assert.Contains("Contest recorded 2 battles", signal.Summary, StringComparison.Ordinal);
        Assert.Contains("totalLosses", signal.FactJson, StringComparison.Ordinal);
        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Contains("historicalSignals", completionEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void CompleteCyclePreservesTopTenPercentOfBattlesAsMajorEvents()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var system = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var battles = Enumerable.Range(1, 11)
            .Select(losses => CreateBattle(
                cycle.CycleId,
                system.SystemId,
                firstEmpire.EmpireId,
                secondEmpire.EmpireId,
                attackerLosses: losses,
                defenderLosses: 0,
                tickNumber: losses))
            .ToArray();
        state.BattleRecords.AddRange(battles);

        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        var majorEvents = state.CycleMajorEvents.OrderBy(item => item.SelectionRank).ToArray();
        Assert.Equal(2, majorEvents.Length);
        Assert.Equal(battles[10].BattleId, majorEvents[0].SourceBattleId);
        Assert.Equal(11, majorEvents[0].TotalLosses);
        Assert.Equal(CycleMajorEventType.Battle, majorEvents[0].EventType);
        Assert.Equal(TestState.Now, majorEvents[0].CreatedAt);
        Assert.Contains("Battle at Contest", majorEvents[0].Summary, StringComparison.Ordinal);
        Assert.Contains("totalLosses", majorEvents[0].FactJson, StringComparison.Ordinal);
        Assert.Equal(battles[9].BattleId, majorEvents[1].SourceBattleId);
        Assert.Equal(10, majorEvents[1].TotalLosses);

        var completionEvent = Assert.Single(state.Events, item => item.EventType == EventType.CycleCompleted);
        Assert.Contains("majorEvents", completionEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCyclePreservesHistoricalSystemsAndPlayerContinuity()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        var sourceSystem = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        state.BattleRecords.Add(CreateBattle(
            sourceCycle.CycleId,
            sourceSystem.SystemId,
            firstEmpire.EmpireId,
            secondEmpire.EmpireId,
            attackerLosses: 12,
            defenderLosses: 8));
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);

        var result = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876);

        var nextCycle = state.Cycles.Single(cycle => cycle.CycleId == result.CycleId);
        var nextSectors = state.Sectors.Where(sector => sector.CycleId == nextCycle.CycleId).ToArray();
        var nextSystems = state.Systems.Where(system => system.CycleId == nextCycle.CycleId).ToArray();
        var nextEmpires = state.Empires.Where(empire => empire.CycleId == nextCycle.CycleId).ToArray();
        var preservedSystem = Assert.Single(result.PreservedSystems);
        var seedEvent = Assert.Single(state.Events, item => item.CycleId == nextCycle.CycleId && item.EventType == EventType.CycleSeeded);

        Assert.Equal(CycleStatus.Active, nextCycle.Status);
        Assert.Equal(TestState.Now.AddDays(1), nextCycle.StartAt);
        Assert.Equal(9876, result.Seed);
        Assert.Equal(2, nextEmpires.Length);
        Assert.Equal(2, result.SuccessorEmpires.Count);
        Assert.NotEmpty(nextSectors);
        Assert.All(nextSystems, system => Assert.Contains(nextSectors, sector => sector.SectorId == system.SectorId));
        Assert.Contains(nextEmpires, empire => empire.PlayerId == firstEmpire.PlayerId && empire.EmpireName == "First Legacy");
        Assert.Contains(nextEmpires, empire => empire.PlayerId == secondEmpire.PlayerId && empire.EmpireName == "Second Remnant");
        Assert.Equal(sourceSystem.SystemId, preservedSystem.SourceSystemId);
        Assert.Equal("Contest", preservedSystem.SystemName);
        Assert.Contains(nextSystems, system => system.SystemId == preservedSystem.NewSystemId
                                               && system.SystemName == "Contest"
                                               && system.HistoricalSignificance >= 2);
        Assert.Contains("sourceCycleId", seedEvent.FactJson, StringComparison.Ordinal);
        Assert.Contains("preservedSystems", seedEvent.FactJson, StringComparison.Ordinal);
        Assert.Contains("successorEmpires", seedEvent.FactJson, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCycleRetainsCanonicalSectorScale()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);

        var result = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 9876);

        var sectors = state.Sectors.Where(sector => sector.CycleId == result.CycleId).ToArray();
        var systems = state.Systems.Where(system => system.CycleId == result.CycleId).ToArray();
        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, sectors.Length);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, systems.Length);
        Assert.All(sectors, sector => Assert.InRange(systems.Count(system => system.SectorId == sector.SectorId), 12, 24));
    }

    [Fact]
    public void GenerateNextCycleRejectsCycleThatIsNotCompleted()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            cycle.CycleId,
            TestState.Now.AddDays(1)));

        Assert.Contains("Only completed Cycles", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateNextCycleRejectsWhenAnotherCycleIsActive()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Test state must contain an active Cycle.");
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now);
        state.Cycles.Add(new Cycle
        {
            Name = "Already active",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(90),
            Status = CycleStatus.Active,
            CreatedAt = TestState.Now
        });

        var ex = Assert.Throws<InvalidOperationException>(() => CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1)));

        Assert.Contains("another Cycle is active", ex.Message, StringComparison.Ordinal);
    }

    private static BattleRecord CreateBattle(
        Guid cycleId,
        Guid systemId,
        Guid attackerEmpireId,
        Guid defenderEmpireId,
        int attackerLosses,
        int defenderLosses,
        int tickNumber = 1) =>
        new()
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
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
