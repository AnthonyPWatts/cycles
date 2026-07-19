using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameStateTests
{
    [Fact]
    public void DeepClone_preserves_values_without_sharing_mutable_entities()
    {
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 612);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var fleet = state.Fleets.First();
        var targetSystem = state.Systems.First(system => system.SystemId != fleet.CurrentSystemId);
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        var admiral = state.Admirals.Single(item => item.EmpireId == attacker.EmpireId);
        state.Players[0].Role = PlayerRole.Admin;
        state.Players[0].ExternalIssuer = "https://identity.example";
        state.Players[0].ExternalSubject = "subject-1";
        state.AdminRoleAuditRecords.Add(new AdminRoleAuditRecord
        {
            TargetPlayerId = state.Players[0].PlayerId,
            Action = AdminRoleAuditAction.Bootstrap,
            Reason = "Configured bootstrap.",
            Source = "test",
            CreatedAt = TestState.Now
        });
        var battle = new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            SystemId = fleet.CurrentSystemId,
            AttackerEmpireId = attacker.EmpireId,
            DefenderEmpireId = defender.EmpireId,
            AttackerFleetIds = fleet.FleetId.ToString(),
            DefenderFleetIds = state.Fleets.Last().FleetId.ToString(),
            AttackerShipsBefore = 50,
            DefenderShipsBefore = 40,
            AttackerLosses = 10,
            DefenderLosses = 20,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = """{"kind":"test"}""",
            CreatedAt = TestState.Now
        };
        var eventRecord = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            EventType = EventType.CombatResolved,
            SystemId = fleet.CurrentSystemId,
            EmpireId = attacker.EmpireId,
            Severity = EventSeverity.High,
            FactJson = battle.FactJson,
            DisplayText = "A test battle was resolved.",
            CreatedAt = TestState.Now
        };

        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = targetSystem.SystemId,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            CreatedAt = TestState.Now
        });
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            CompletedAt = TestState.Now,
            Status = TickLogStatus.Completed,
            DiagnosticLog = "Processed 1 order."
        });
        state.CycleRankings.Add(new CycleRanking
        {
            CycleId = cycle.CycleId,
            EmpireId = attacker.EmpireId,
            Rank = 1,
            IsWinner = true,
            MapControlPercent = 60,
            TotalEffectivePresence = 120,
            ActiveShipCount = 50,
            CutoffTickNumber = 1,
            CutoffAt = TestState.Now
        });
        state.CycleMajorEvents.Add(new CycleMajorEvent
        {
            CycleId = cycle.CycleId,
            SourceBattleId = battle.BattleId,
            SystemId = battle.SystemId,
            EventType = CycleMajorEventType.Battle,
            TickNumber = battle.TickNumber,
            SelectionRank = 1,
            ImportanceScore = 75,
            TotalLosses = 30,
            Summary = "A test battle was selected as major history.",
            FactJson = """{"kind":"major"}""",
            CreatedAt = TestState.Now
        });
        state.SystemHistoricalSignals.Add(new SystemHistoricalSignal
        {
            CycleId = cycle.CycleId,
            SystemId = battle.SystemId,
            SignalType = SystemHistoricalSignalType.BattleActivity,
            SourceBattleId = battle.BattleId,
            BattleCount = 1,
            TotalLosses = 30,
            LargestBattleLosses = 30,
            HostedCycleLargestBattle = true,
            HistoricalSignificanceIncrease = 2,
            HistoricalSignificanceAfter = 4,
            Summary = "A test system accumulated battle history.",
            FactJson = """{"kind":"system-signal"}""",
            CreatedAt = TestState.Now
        });
        state.AdmiralBattleHistories.Add(new AdmiralBattleHistory
        {
            CycleId = cycle.CycleId,
            AdmiralId = admiral.AdmiralId,
            BattleId = battle.BattleId,
            SystemId = battle.SystemId,
            FleetId = fleet.FleetId,
            Role = AdmiralBattleRole.Attacker,
            Outcome = AdmiralBattleOutcome.Victory,
            ShipsCommandedBefore = 50,
            ShipsLost = 10,
            ReputationChange = 35,
            ReputationScoreAfter = 35,
            AdmiralStatusAfter = AdmiralStatus.Active,
            IsFamousSystemAssociation = true,
            CreatedAt = TestState.Now
        });
        state.EmpireDoctrineUnlocks.Add(new EmpireDoctrineUnlock
        {
            CycleId = cycle.CycleId,
            EmpireId = attacker.EmpireId,
            DoctrineKey = EconomyProcessor.SurveyProjectionDoctrineKey,
            UnlockedTickNumber = 1,
            UnlockedAt = TestState.Now
        });
        state.Events.Add(eventRecord);
        state.BattleRecords.Add(battle);
        state.ChronicleEntries.Add(new ChronicleEntry
        {
            SourceEventId = eventRecord.EventId,
            SourceBattleId = battle.BattleId,
            CycleId = cycle.CycleId,
            SystemId = fleet.CurrentSystemId,
            Title = "Test Chronicle",
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = 80,
            FactualSummary = "A test battle was preserved.",
            NarrativeText = "A test battle was preserved.",
            NarrativeStatus = NarrativeGenerationStatus.Generated,
            NarrativeContextJson = """{"kind":"narrative-context"}""",
            NarrativeGeneratedAt = TestState.Now,
            CreatedAt = TestState.Now
        });

        var clone = state.DeepClone();

        Assert.Equal(state.Players.Count, clone.Players.Count);
        Assert.Equal(PlayerRole.Admin, clone.Players[0].Role);
        Assert.Equal("https://identity.example", clone.Players[0].ExternalIssuer);
        Assert.Equal("subject-1", clone.Players[0].ExternalSubject);
        Assert.Single(clone.AdminRoleAuditRecords);
        Assert.NotSame(state.AdminRoleAuditRecords[0], clone.AdminRoleAuditRecords[0]);
        Assert.Equal(state.Cycles.Count, clone.Cycles.Count);
        Assert.Equal(state.Empires.Count, clone.Empires.Count);
        Assert.Equal(state.EmpireResources.Count, clone.EmpireResources.Count);
        Assert.Equal(state.EmpireDoctrineUnlocks.Count, clone.EmpireDoctrineUnlocks.Count);
        Assert.NotSame(state.EmpireDoctrineUnlocks[0], clone.EmpireDoctrineUnlocks[0]);
        Assert.Equal(state.EmpirePriorities.Count, clone.EmpirePriorities.Count);
        Assert.Equal(state.Systems.Count, clone.Systems.Count);
        Assert.Equal(state.SystemLinks.Count, clone.SystemLinks.Count);
        Assert.Equal(state.Fleets.Count, clone.Fleets.Count);
        Assert.Equal(state.FleetOrders.Count, clone.FleetOrders.Count);
        Assert.Equal(state.TickLogs.Count, clone.TickLogs.Count);
        Assert.Equal(state.CycleRankings.Count, clone.CycleRankings.Count);
        Assert.Equal(state.CycleMajorEvents.Count, clone.CycleMajorEvents.Count);
        Assert.Equal(state.SystemHistoricalSignals.Count, clone.SystemHistoricalSignals.Count);
        Assert.Equal(state.Admirals.Count, clone.Admirals.Count);
        Assert.Equal(state.AdmiralBattleHistories.Count, clone.AdmiralBattleHistories.Count);
        Assert.Equal(state.Events.Count, clone.Events.Count);
        Assert.Equal(state.BattleRecords.Count, clone.BattleRecords.Count);
        Assert.Equal(state.ChronicleEntries.Count, clone.ChronicleEntries.Count);

        Assert.Equal(state.Fleets[0].FleetId, clone.Fleets[0].FleetId);
        Assert.NotSame(state.Fleets[0], clone.Fleets[0]);
        Assert.Equal(state.FleetOrders[0].FleetOrderId, clone.FleetOrders[0].FleetOrderId);
        Assert.NotSame(state.FleetOrders[0], clone.FleetOrders[0]);
        Assert.Equal(state.Events.Last().EventId, clone.Events.Last().EventId);
        Assert.NotSame(state.Events.Last(), clone.Events.Last());
        Assert.Equal(state.CycleRankings[0].CycleRankingId, clone.CycleRankings[0].CycleRankingId);
        Assert.NotSame(state.CycleRankings[0], clone.CycleRankings[0]);
        Assert.Equal(state.CycleMajorEvents[0].CycleMajorEventId, clone.CycleMajorEvents[0].CycleMajorEventId);
        Assert.NotSame(state.CycleMajorEvents[0], clone.CycleMajorEvents[0]);
        Assert.Equal(state.SystemHistoricalSignals[0].SystemHistoricalSignalId, clone.SystemHistoricalSignals[0].SystemHistoricalSignalId);
        Assert.NotSame(state.SystemHistoricalSignals[0], clone.SystemHistoricalSignals[0]);
        Assert.Equal(state.Admirals[0].AdmiralId, clone.Admirals[0].AdmiralId);
        Assert.NotSame(state.Admirals[0], clone.Admirals[0]);
        Assert.Equal(state.AdmiralBattleHistories[0].AdmiralBattleHistoryId, clone.AdmiralBattleHistories[0].AdmiralBattleHistoryId);
        Assert.NotSame(state.AdmiralBattleHistories[0], clone.AdmiralBattleHistories[0]);
        Assert.Equal(NarrativeGenerationStatus.Generated, clone.ChronicleEntries[0].NarrativeStatus);
        Assert.Equal("""{"kind":"narrative-context"}""", clone.ChronicleEntries[0].NarrativeContextJson);
        Assert.Equal(TestState.Now, clone.ChronicleEntries[0].NarrativeGeneratedAt);

        clone.Fleets[0].ShipCount += 10;
        clone.FleetOrders[0].Status = FleetOrderStatus.Processed;
        clone.Events.Last().DisplayText = "Changed in clone.";
        clone.CycleRankings[0].Rank = 2;
        clone.CycleMajorEvents[0].Summary = "Changed in clone.";
        clone.SystemHistoricalSignals[0].Summary = "Changed in clone.";
        clone.Admirals[0].ReputationScore = 999;
        clone.AdmiralBattleHistories[0].ReputationScoreAfter = 999;
        clone.ChronicleEntries[0].NarrativeStatus = NarrativeGenerationStatus.Failed;
        clone.ChronicleEntries[0].NarrativeContextJson = """{"changed":true}""";

        Assert.NotEqual(state.Fleets[0].ShipCount, clone.Fleets[0].ShipCount);
        Assert.Equal(FleetOrderStatus.Pending, state.FleetOrders[0].Status);
        Assert.Equal("A test battle was resolved.", state.Events.Last().DisplayText);
        Assert.Equal(1, state.CycleRankings[0].Rank);
        Assert.Equal("A test battle was selected as major history.", state.CycleMajorEvents[0].Summary);
        Assert.Equal("A test system accumulated battle history.", state.SystemHistoricalSignals[0].Summary);
        Assert.NotEqual(999, state.Admirals[0].ReputationScore);
        Assert.NotEqual(999, state.AdmiralBattleHistories[0].ReputationScoreAfter);
        Assert.Equal(NarrativeGenerationStatus.Generated, state.ChronicleEntries[0].NarrativeStatus);
        Assert.Equal("""{"kind":"narrative-context"}""", state.ChronicleEntries[0].NarrativeContextJson);
    }
}
