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
        var participants = state.BattleFleetParticipants
            .Where(item => item.BattleId == battle.BattleId)
            .OrderBy(item => item.Side)
            .ThenBy(item => item.FleetId)
            .ToArray();
        Assert.Collection(
            participants,
            item =>
            {
                Assert.Equal(cycle.CycleId, item.CycleId);
                Assert.Equal(attackerFleet.FleetId, item.FleetId);
                Assert.Equal(BattleFleetSide.Attacker, item.Side);
            },
            item =>
            {
                Assert.Equal(cycle.CycleId, item.CycleId);
                Assert.Equal(state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[1].EmpireId).FleetId, item.FleetId);
                Assert.Equal(BattleFleetSide.Defender, item.Side);
            });
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
    public void AttackOrderRecordsAssignedAdmiralBattleHistoryAndReputation()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 40, strategicValue: 35);
        var cycle = state.GetActiveCycle()!;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        var admiral = AssignAdmiral(state, attackerFleet, "Cela Ardent");

        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);
        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var battle = Assert.Single(state.BattleRecords);
        var history = Assert.Single(state.AdmiralBattleHistories, item => item.AdmiralId == admiral.AdmiralId);
        var updatedAdmiral = state.Admirals.Single(item => item.AdmiralId == admiral.AdmiralId);
        Assert.Equal(battle.BattleId, history.BattleId);
        Assert.Equal(battle.SystemId, history.SystemId);
        Assert.Equal(attackerFleet.FleetId, history.FleetId);
        Assert.Equal(AdmiralBattleRole.Attacker, history.Role);
        Assert.True(history.ReputationChange > 0);
        Assert.Equal(history.ReputationScoreAfter, updatedAdmiral.ReputationScore);
        Assert.Contains(state.Events, item => item.EventType == EventType.AdmiralBattleReported && item.EmpireId == updatedAdmiral.EmpireId);
        Assert.Contains(history.AdmiralBattleHistoryId.ToString(), battle.FactJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DestroyedAssignedFleetMarksAdmiralKilled()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 1, defenderShips: 200);
        var cycle = state.GetActiveCycle()!;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        var admiral = AssignAdmiral(state, attackerFleet, "Lio Harrow");

        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var updatedFleet = state.Fleets.Single(item => item.FleetId == attackerFleet.FleetId);
        var updatedAdmiral = state.Admirals.Single(item => item.AdmiralId == admiral.AdmiralId);
        Assert.Equal(FleetStatus.Destroyed, updatedFleet.Status);
        Assert.Equal(AdmiralStatus.Killed, updatedAdmiral.Status);
        var history = Assert.Single(state.AdmiralBattleHistories, item => item.AdmiralId == admiral.AdmiralId);
        Assert.Equal(AdmiralStatus.Killed, history.AdmiralStatusAfter);
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
        var participants = state.BattleFleetParticipants.Where(item => item.BattleId == battle.BattleId).ToArray();
        Assert.Single(participants, item => item.Side == BattleFleetSide.Attacker);
        Assert.Equal(
            state.Fleets.Where(fleet => fleet.EmpireId == defenderEmpireId).Select(fleet => fleet.FleetId).Order(),
            participants.Where(item => item.Side == BattleFleetSide.Defender).Select(item => item.FleetId).Order());
        Assert.All(participants, item => Assert.Equal(cycle.CycleId, item.CycleId));
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
    public void AdmiralBattleHistoryCanLiftChronicleImportance()
    {
        var system = new GalaxySystem { StrategicValue = 20 };
        var battle = new BattleRecord
        {
            AttackerShipsBefore = 30,
            DefenderShipsBefore = 30,
            AttackerLosses = 5,
            DefenderLosses = 5,
            Outcome = BattleOutcome.AttackerVictory
        };
        var history = new AdmiralBattleHistory
        {
            ReputationChange = 80,
            AdmiralStatusAfter = AdmiralStatus.Legendary,
            IsFamousSystemAssociation = true
        };

        var withoutAdmirals = ChronicleScoring.ScoreBattle(battle, system);
        var withAdmirals = ChronicleScoring.ScoreBattle(battle, system, [history]);

        Assert.True(withAdmirals > withoutAdmirals);
        Assert.True(withAdmirals >= ChronicleScoring.ChronicleThreshold);
    }

    [Fact]
    public void ChronicleBattleReportIncludesRequiredFacts()
    {
        var (battle, sourceEvent, system, attacker, defender) = CreateChronicleBattleInputs();

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
        Assert.Equal(NarrativeGenerationStatus.Generated, entry.NarrativeStatus);
        Assert.Equal(TestState.Now, entry.NarrativeGeneratedAt);
        Assert.Null(entry.NarrativeFailureReason);
        Assert.Contains("Aurelian Compact", entry.NarrativeContextJson, StringComparison.Ordinal);
        Assert.Contains("attackerLosses", entry.NarrativeContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ChronicleBattleReportIncludesAdmiralContextWhenProvided()
    {
        var (battle, sourceEvent, system, attacker, defender) = CreateChronicleBattleInputs();
        var admiral = new Admiral
        {
            CycleId = battle.CycleId,
            EmpireId = attacker.EmpireId,
            AdmiralName = "Asha Teral",
            ReputationScore = 115,
            Status = AdmiralStatus.Legendary
        };
        var history = new AdmiralBattleHistory
        {
            CycleId = battle.CycleId,
            AdmiralId = admiral.AdmiralId,
            BattleId = battle.BattleId,
            SystemId = battle.SystemId,
            FleetId = Guid.NewGuid(),
            Role = AdmiralBattleRole.Attacker,
            Outcome = AdmiralBattleOutcome.Victory,
            ReputationChange = 30,
            ReputationScoreAfter = 115,
            AdmiralStatusAfter = AdmiralStatus.Legendary,
            IsFamousSystemAssociation = true
        };

        var entry = ChronicleScoring.CreateBattleEntry(
            battle,
            sourceEvent,
            system,
            attacker,
            defender,
            importance: 95,
            TestState.Now,
            [admiral],
            [history]);

        Assert.Contains("Asha Teral", entry.NarrativeText, StringComparison.Ordinal);
        Assert.Contains("Asha Teral", entry.NarrativeContextJson, StringComparison.Ordinal);
        Assert.Contains("admiralFacts", entry.NarrativeContextJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ChronicleBattleNarrativeSourceCapturesGenerationFacts()
    {
        var (battle, sourceEvent, system, attacker, defender) = CreateChronicleBattleInputs();

        var source = ChronicleBattleNarrativeSource.FromBattle(battle, sourceEvent, system, attacker, defender, importance: 90);

        Assert.Equal(sourceEvent.EventId, source.SourceEventId);
        Assert.Equal(battle.BattleId, source.SourceBattleId);
        Assert.Equal("Aster Vale", source.SystemName);
        Assert.Equal("Aurelian Compact", source.AttackerEmpireName);
        Assert.Equal("Khepri Mandate", source.DefenderEmpireName);
        Assert.Equal(90, source.TotalLosses);
        Assert.True(source.AttackerWasUnderdog);
        Assert.False(source.DefenderWasUnderdog);
    }

    [Fact]
    public void ChronicleRequiredFactValidatorRejectsMissingFacts()
    {
        var (battle, sourceEvent, system, attacker, defender) = CreateChronicleBattleInputs();
        var source = ChronicleBattleNarrativeSource.FromBattle(battle, sourceEvent, system, attacker, defender, importance: 90);

        var result = ChronicleRequiredFactValidator.ValidateBattleReport(
            source,
            "Aurelian Compact fought at Aster Vale on tick 7 with 9 losses and 90 importance.");

        Assert.False(result.IsValid);
        Assert.Contains("defender empire", result.MissingFacts);
        Assert.Contains("defender losses", result.MissingFacts);
        Assert.Contains("battle outcome", result.MissingFacts);
        var ex = Assert.Throws<InvalidOperationException>(result.ThrowIfInvalid);
        Assert.Contains("defender empire", ex.Message, StringComparison.Ordinal);
    }

    private static void RunAttack(GameState state)
    {
        var cycle = state.GetActiveCycle()!;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
    }

    private static Admiral AssignAdmiral(GameState state, Fleet fleet, string name, int reputationScore = 0)
    {
        var admiral = new Admiral
        {
            CycleId = fleet.CycleId,
            EmpireId = fleet.EmpireId,
            AdmiralName = name,
            ReputationScore = reputationScore,
            Status = reputationScore >= AdmiralService.LegendaryReputationThreshold ? AdmiralStatus.Legendary : AdmiralStatus.Active,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        state.Admirals.Add(admiral);
        fleet.AdmiralId = admiral.AdmiralId;
        return admiral;
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

    private static (BattleRecord Battle, EventRecord SourceEvent, GalaxySystem System, Empire Attacker, Empire Defender) CreateChronicleBattleInputs()
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

        return (battle, sourceEvent, system, attacker, defender);
    }
}
