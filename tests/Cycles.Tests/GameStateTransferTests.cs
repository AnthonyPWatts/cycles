using Cycles.Core;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace Cycles.Tests;

public sealed class GameStateTransferTests
{
    [Fact]
    public void Versioned_round_trip_preserves_every_persisted_collection_and_retained_history()
    {
        var state = CreateCompleteValidState();
        using var stream = new MemoryStream();

        GameStateTransfer.Write(stream, state, TestState.Now);
        stream.Position = 0;
        var document = GameStateTransfer.Read(stream);

        Assert.Equal(GameStateTransfer.CurrentFormatVersion, document.FormatVersion);
        Assert.Equal(TestState.Now, document.ExportedAt);
        foreach (var property in PersistedCollections())
        {
            var expected = Assert.IsAssignableFrom<ICollection>(property.GetValue(state));
            var actual = Assert.IsAssignableFrom<ICollection>(property.GetValue(document.State));
            Assert.True(expected.Count > 0, $"Test fixture must exercise {property.Name}.");
            Assert.Equal(expected.Count, actual.Count);
        }

        Assert.Equal("https://identity.example", document.State.Players[0].ExternalIssuer);
        Assert.Single(document.State.AdminRoleAuditRecords);
        Assert.Contains(document.State.ChronicleEntries, entry => entry.NarrativeContextJson == "{\"retained\":true}");
    }

    [Fact]
    public void Reader_rejects_incompatible_or_partial_documents_before_deserialisation()
    {
        using var incompatible = JsonStream("""
            {"formatVersion":999,"exportedAt":"2026-06-23T20:00:00Z","state":{}}
            """);
        var incompatibleException = Assert.Throws<InvalidOperationException>(() => GameStateTransfer.Read(incompatible));
        Assert.Contains("Unsupported", incompatibleException.Message, StringComparison.Ordinal);

        using var partial = JsonStream("""
            {"formatVersion":1,"exportedAt":"2026-06-23T20:00:00Z","state":{"players":[]}}
            """);
        var partialException = Assert.Throws<InvalidOperationException>(() => GameStateTransfer.Read(partial));
        Assert.Contains("adminRoleAuditRecords", partialException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ReaderNormalisesLegacyPriorityAllocations()
    {
        var state = CreateCompleteValidState();
        var priorities = state.EmpirePriorities[0];
        priorities.IndustryWeight = 30;
        priorities.ResearchWeight = 25;
        priorities.MilitaryWeight = 30;
        priorities.ExpansionWeight = 15;
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new GameStateTransferDocument(GameStateTransfer.CurrentFormatVersion, TestState.Now, state),
            GameStateJson.Options));

        var document = GameStateTransfer.Read(stream);
        var normalised = document.State.EmpirePriorities.Single(item => item.EmpirePriorityId == priorities.EmpirePriorityId);

        Assert.Equal(0, normalised.IndustryWeight);
        Assert.Equal(0, normalised.ResearchWeight);
        Assert.Equal(67, normalised.MilitaryWeight);
        Assert.Equal(33, normalised.ExpansionWeight);
    }

    [Fact]
    public void LegacyRuntimeReaderConvertsCompleteStateAndNormalisesPriorities()
    {
        var state = CreateCompleteValidState();
        var priorities = state.EmpirePriorities[0];
        priorities.IndustryWeight = 30;
        priorities.ResearchWeight = 25;
        priorities.MilitaryWeight = 30;
        priorities.ExpansionWeight = 15;
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(state, GameStateJson.Options));

        var converted = GameStateTransfer.ReadLegacyRuntimeState(stream);
        var normalised = converted.EmpirePriorities.Single(item => item.EmpirePriorityId == priorities.EmpirePriorityId);

        Assert.Equal(GameStateTransfer.CountRecords(state), GameStateTransfer.CountRecords(converted));
        Assert.Equal(0, normalised.IndustryWeight);
        Assert.Equal(0, normalised.ResearchWeight);
        Assert.Equal(67, normalised.MilitaryWeight);
        Assert.Equal(33, normalised.ExpansionWeight);
    }

    [Fact]
    public void LegacyRuntimeReaderRejectsPartialStateBeforeDeserialisation()
    {
        using var stream = JsonStream("""
            {"players":[]}
            """);

        var exception = Assert.Throws<InvalidOperationException>(() => GameStateTransfer.ReadLegacyRuntimeState(stream));

        Assert.Contains("adminRoleAuditRecords", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validator_reports_referential_and_recovery_inconsistency()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        state.Fleets.Single().EmpireId = Guid.NewGuid();
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            Status = TickLogStatus.Running
        });

        var result = GameStateTransfer.Validate(state);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.Contains("Fleet", StringComparison.Ordinal) && error.Contains("empire", StringComparison.Ordinal));
        Assert.Contains(result.Errors, error => error.Contains("must require recovery", StringComparison.Ordinal));
    }

    private static GameState CreateCompleteValidState()
    {
        var state = GameSeeder.CreateCuratedColdStart();
        var cycle = state.GetActiveCycle()!;
        cycle.CurrentTickNumber = 1;
        var player = state.Players[0];
        var firstEmpire = state.Empires[0];
        var secondEmpire = state.Empires[1];
        var firstFleet = state.Fleets.First(fleet => fleet.EmpireId == firstEmpire.EmpireId);
        var secondFleet = state.Fleets.First(fleet => fleet.EmpireId == secondEmpire.EmpireId);
        var system = state.Systems.First(item => item.CycleId == cycle.CycleId);
        var admiral = state.Admirals.First(item => item.EmpireId == firstEmpire.EmpireId);

        player.ExternalIssuer = "https://identity.example";
        player.ExternalSubject = "subject-1";
        state.AdminRoleAuditRecords.Add(new AdminRoleAuditRecord
        {
            TargetPlayerId = player.PlayerId,
            Action = AdminRoleAuditAction.Bootstrap,
            Reason = "Initial operator.",
            Source = "test",
            CreatedAt = TestState.Now
        });
        state.EmpireMetrics.Add(new EmpireMetric
        {
            CycleId = cycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            TickNumber = 1,
            Rank = 1,
            CreatedAt = TestState.Now
        });
        state.CycleRankings.Add(new CycleRanking
        {
            CycleId = cycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            Rank = 1,
            CutoffTickNumber = 1,
            CutoffAt = TestState.Now
        });

        var battle = new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            SystemId = system.SystemId,
            AttackerEmpireId = firstEmpire.EmpireId,
            DefenderEmpireId = secondEmpire.EmpireId,
            AttackerFleetIds = firstFleet.FleetId.ToString("D"),
            DefenderFleetIds = secondFleet.FleetId.ToString("D"),
            AttackerShipsBefore = firstFleet.ShipCount,
            DefenderShipsBefore = secondFleet.ShipCount,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = "{\"retained\":true}",
            CreatedAt = TestState.Now
        };
        state.BattleRecords.Add(battle);
        state.CycleMajorEvents.Add(new CycleMajorEvent
        {
            CycleId = cycle.CycleId,
            SourceBattleId = battle.BattleId,
            SystemId = system.SystemId,
            EventType = CycleMajorEventType.Battle,
            TickNumber = 1,
            SelectionRank = 1,
            Summary = "Retained battle.",
            FactJson = "{\"retained\":true}",
            CreatedAt = TestState.Now
        });
        state.SystemHistoricalSignals.Add(new SystemHistoricalSignal
        {
            CycleId = cycle.CycleId,
            SystemId = system.SystemId,
            SignalType = SystemHistoricalSignalType.BattleActivity,
            SourceBattleId = battle.BattleId,
            BattleCount = 1,
            Summary = "Retained signal.",
            FactJson = "{\"retained\":true}",
            CreatedAt = TestState.Now
        });
        state.ColonialOutposts.Add(new ColonialOutpost
        {
            CycleId = cycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            SystemId = system.SystemId,
            EstablishedTick = 1,
            CreatedAt = TestState.Now
        });
        if (state.DiplomaticRelationships.Count == 0)
        {
            state.DiplomaticRelationships.Add(new DiplomaticRelationship
            {
                CycleId = cycle.CycleId,
                FirstEmpireId = firstEmpire.EmpireId,
                SecondEmpireId = secondEmpire.EmpireId,
                UpdatedTick = 1,
                UpdatedAt = TestState.Now
            });
        }

        state.AdmiralBattleHistories.Add(new AdmiralBattleHistory
        {
            CycleId = cycle.CycleId,
            AdmiralId = admiral.AdmiralId,
            BattleId = battle.BattleId,
            SystemId = system.SystemId,
            FleetId = firstFleet.FleetId,
            ShipsCommandedBefore = firstFleet.ShipCount,
            AdmiralStatusAfter = admiral.Status,
            CreatedAt = TestState.Now
        });
        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = firstFleet.FleetId,
            OrderType = FleetOrderType.Hold,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            ProcessedTick = 1,
            Status = FleetOrderStatus.Processed,
            CreatedAt = TestState.Now
        });
        state.ShipConstructions.Add(new ShipConstruction
        {
            CycleId = cycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            ShipCount = 1,
            IndustrySpent = 1,
            StartedTick = 0,
            CompleteAfterTick = 1,
            CompletedTick = 1,
            Status = ShipConstructionStatus.Completed,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        });
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            CompletedAt = TestState.Now.AddSeconds(1),
            Status = TickLogStatus.Completed,
            DiagnosticLog = "Retained tick."
        });
        state.ChronicleEntries.Add(new ChronicleEntry
        {
            SourceBattleId = battle.BattleId,
            CycleId = cycle.CycleId,
            SystemId = system.SystemId,
            Title = "Retained history",
            EntryType = ChronicleEntryType.Battle,
            FactualSummary = "A retained battle.",
            NarrativeText = "A retained battle.",
            NarrativeContextJson = "{\"retained\":true}",
            CreatedAt = TestState.Now
        });
        return state;
    }

    private static PropertyInfo[] PersistedCollections() => typeof(GameState)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.PropertyType.IsGenericType
                           && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
        .ToArray();

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
