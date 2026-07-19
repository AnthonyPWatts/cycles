using Cycles.Core;
using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Cycles.Tests;

public sealed class GameStateTransferTests
{
    private const string ValidContentHash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

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
        var game = Assert.Single(document.State.Games);
        Assert.Equal(GameFoundationConstants.LegacyGameId, game.GameId);
        Assert.Single(document.State.CycleConfigurations);
        Assert.Equal(
            state.GameEnrolments.Select(item => item.PlayerId).OrderBy(item => item),
            document.State.GameEnrolments.Select(item => item.PlayerId).OrderBy(item => item));
        Assert.Equal(
            GameFoundationConstants.LegacyImportFactJson,
            Assert.Single(document.State.GameLifecycleEvents).FactJson);
        Assert.Contains(document.State.ChronicleEntries, entry => entry.NarrativeContextJson == "{\"retained\":true}");
        var superseded = Assert.Single(document.State.FleetOrders, order => order.Status == FleetOrderStatus.Superseded);
        Assert.Contains(document.State.FleetOrders, order => order.FleetOrderId == superseded.SupersededByOrderId);
    }

    [Fact]
    public void Version_four_import_adds_the_deterministic_legacy_game_foundation()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var expectedCycleIds = state.Cycles.Select(item => item.CycleId).OrderBy(item => item).ToArray();
        var expectedPlayerIds = state.MatchParticipants.Select(item => item.PlayerId).Distinct().OrderBy(item => item).ToArray();
        var root = JsonSerializer.SerializeToNode(
            new GameStateTransferDocument(4, TestState.Now, state),
            GameStateJson.Options)!.AsObject();
        var stateNode = root["state"]!.AsObject();
        stateNode.Remove("games");
        stateNode.Remove("cycleConfigurations");
        stateNode.Remove("gameEnrolments");
        stateNode.Remove("gameLifecycleEvents");
        foreach (var cycle in stateNode["cycles"]!.AsArray().Select(item => item!.AsObject()))
        {
            cycle.Remove("gameId");
            cycle.Remove("cycleConfigurationId");
            cycle.Remove("previousCycleId");
            cycle.Remove("mapProfileKey");
            cycle.Remove("mapProfileVersion");
            cycle.Remove("mapProfileContentHash");
            cycle.Remove("mapSeed");
            cycle.Remove("scenarioProfileKey");
            cycle.Remove("scenarioProfileVersion");
            cycle.Remove("scenarioProfileContentHash");
            cycle.Remove("scenarioSeed");
            cycle.Remove("cyclePolicyKey");
            cycle.Remove("cyclePolicyVersion");
            cycle.Remove("cyclePolicyContentHash");
            cycle.Remove("profileProvenanceStatus");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));
        var document = GameStateTransfer.Read(stream);

        Assert.Equal(4, document.FormatVersion);
        var game = Assert.Single(document.State.Games);
        Assert.Equal(GameFoundationConstants.LegacyGameId, game.GameId);
        Assert.Equal(expectedCycleIds, document.State.Cycles.Select(item => item.CycleId).OrderBy(item => item));
        Assert.Equal(expectedCycleIds, document.State.CycleConfigurations.Select(item => item.CycleConfigurationId).OrderBy(item => item));
        Assert.Equal(expectedPlayerIds, document.State.GameEnrolments.Select(item => item.PlayerId).OrderBy(item => item));
        Assert.All(document.State.Cycles, cycle =>
        {
            Assert.Equal(GameFoundationConstants.LegacyGameId, cycle.GameId);
            Assert.Equal(cycle.CycleId, cycle.CycleConfigurationId);
            Assert.Null(cycle.PreviousCycleId);
        });
        Assert.Equal(GameFoundationConstants.LegacyLifecycleEventId, Assert.Single(document.State.GameLifecycleEvents).GameLifecycleEventId);
    }

    [Fact]
    public void Version_five_import_derives_participant_game_scope_and_normalised_battle_membership()
    {
        var state = CreateCompleteValidState();
        var battle = Assert.Single(state.BattleRecords);
        var attacker = Assert.Single(state.BattleFleetParticipants, item => item.Side == BattleFleetSide.Attacker);
        var defender = Assert.Single(state.BattleFleetParticipants, item => item.Side == BattleFleetSide.Defender);
        battle.AttackerFleetIds = attacker.FleetId.ToString("D");
        battle.DefenderFleetIds = $"[\"{defender.FleetId:D}\"]";
        var root = JsonSerializer.SerializeToNode(
            new GameStateTransferDocument(5, TestState.Now, state),
            GameStateJson.Options)!.AsObject();
        var stateNode = root["state"]!.AsObject();
        stateNode.Remove("battleFleetParticipants");
        foreach (var participant in stateNode["matchParticipants"]!.AsArray().Select(item => item!.AsObject()))
        {
            participant.Remove("gameId");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));
        var document = GameStateTransfer.Read(stream);

        Assert.Equal(5, document.FormatVersion);
        Assert.All(document.State.MatchParticipants, participant =>
            Assert.Equal(
                document.State.Cycles.Single(cycle => cycle.CycleId == participant.CycleId).GameId,
                participant.GameId));
        Assert.Equal(
            new[]
            {
                (battle.BattleId, battle.CycleId, attacker.FleetId, BattleFleetSide.Attacker),
                (battle.BattleId, battle.CycleId, defender.FleetId, BattleFleetSide.Defender)
            }.OrderBy(item => item.Item4).ThenBy(item => item.FleetId),
            document.State.BattleFleetParticipants
                .Select(item => (item.BattleId, item.CycleId, item.FleetId, item.Side))
                .OrderBy(item => item.Side)
                .ThenBy(item => item.FleetId));
        Assert.Equal(attacker.FleetId.ToString("D"), Assert.Single(document.State.BattleRecords).AttackerFleetIds);
        Assert.Equal(defender.FleetId.ToString("D"), Assert.Single(document.State.BattleRecords).DefenderFleetIds);
    }

    [Fact]
    public void Version_six_validation_requires_exact_battle_membership_and_match_participant_game_scope()
    {
        var state = CreateCompleteValidState();
        var participant = state.MatchParticipants[0];
        participant.GameId = Guid.NewGuid();
        state.BattleFleetParticipants.RemoveAt(state.BattleFleetParticipants.Count - 1);

        var validation = GameStateTransfer.Validate(state);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Contains("Match participant", StringComparison.Ordinal)
            && error.Contains("Game", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error =>
            error.Contains("Battle", StringComparison.Ordinal)
            && error.Contains("fleet", StringComparison.OrdinalIgnoreCase)
            && error.Contains("membership", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Version_five_allows_one_operational_cycle_in_each_game_but_not_two_in_one_game()
    {
        var valid = new GameState();
        AddOperationalGame(valid, "First");
        AddOperationalGame(valid, "Second");
        using var stream = new MemoryStream();

        GameStateTransfer.Write(stream, valid, TestState.Now);
        stream.Position = 0;
        var document = GameStateTransfer.Read(stream);

        Assert.Equal(2, document.State.Games.Count);
        Assert.Equal(2, document.State.Cycles.Count(item => item.Status == CycleStatus.Active));
        Assert.True(GameStateTransfer.Validate(document.State).IsValid);

        var invalid = new GameState();
        var game = AddOperationalGame(invalid, "Shared");
        AddOperationalCycle(invalid, game, 2, "Shared successor");

        var validation = GameStateTransfer.Validate(invalid);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Contains($"Game {game.GameId}", StringComparison.Ordinal)
            && error.Contains("more than one Active or RecoveryRequired Cycle", StringComparison.Ordinal));
    }

    [Fact]
    public void Version_five_requires_foundation_collections_and_validates_foundation_provenance()
    {
        var missingCollectionState = new GameState();
        AddOperationalGame(missingCollectionState, "Strict");
        var root = JsonSerializer.SerializeToNode(
            new GameStateTransferDocument(GameStateTransfer.CurrentFormatVersion, TestState.Now, missingCollectionState),
            GameStateJson.Options)!.AsObject();
        root["state"]!.AsObject().Remove("gameLifecycleEvents");
        using var missingCollection = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));

        var missingException = Assert.Throws<InvalidOperationException>(() => GameStateTransfer.Read(missingCollection));

        Assert.Contains("gameLifecycleEvents", missingException.Message, StringComparison.Ordinal);

        var invalid = new GameState();
        AddOperationalGame(invalid, "Invalid");
        var configuration = Assert.Single(invalid.CycleConfigurations);
        configuration.MapProfileContentHash = "changed";
        var lifecycleEvent = Assert.Single(invalid.GameLifecycleEvents);
        lifecycleEvent.FactJson = "{";
        invalid.GameLifecycleEvents.Add(new GameLifecycleEvent
        {
            GameLifecycleEventId = lifecycleEvent.GameLifecycleEventId,
            GameId = lifecycleEvent.GameId,
            Type = GameLifecycleEventType.StatusChanged,
            FactJson = "{}",
            CreatedAt = TestState.Now
        });

        var validation = GameStateTransfer.Validate(invalid);

        Assert.Contains(validation.Errors, error => error.Contains("duplicate identifier", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("provenance does not match", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error =>
            error.Contains("Game lifecycle event", StringComparison.Ordinal)
            && error.Contains("fact JSON is invalid", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_enforces_game_lifecycle_timestamps_and_operational_cycle_alignment()
    {
        var completedWithOperationalCycle = new GameState();
        var completedGame = AddOperationalGame(completedWithOperationalCycle, "Completed");
        completedGame.Status = GameLifecycleStatus.Completed;
        completedGame.CompletedAt = TestState.Now;

        var completedValidation = GameStateTransfer.Validate(completedWithOperationalCycle);

        Assert.Contains(completedValidation.Errors, error =>
            error.Contains($"Game {completedGame.GameId}", StringComparison.Ordinal)
            && error.Contains("cannot retain an Active or RecoveryRequired Cycle", StringComparison.Ordinal));

        var activeWithoutOperationalCycle = new GameState();
        var activeGame = AddOperationalGame(activeWithoutOperationalCycle, "Active");
        Assert.Single(activeWithoutOperationalCycle.Cycles).Status = CycleStatus.Completed;

        var activeValidation = GameStateTransfer.Validate(activeWithoutOperationalCycle);

        Assert.Contains(activeValidation.Errors, error =>
            error.Contains($"Active Game {activeGame.GameId}", StringComparison.Ordinal)
            && error.Contains("exactly one Active or RecoveryRequired Cycle", StringComparison.Ordinal));

        activeGame.Status = GameLifecycleStatus.Completed;
        activeGame.CancelledAt = TestState.Now;

        var timestampValidation = GameStateTransfer.Validate(activeWithoutOperationalCycle);

        Assert.Contains(timestampValidation.Errors, error =>
            error.Contains($"Game {activeGame.GameId}", StringComparison.Ordinal)
            && error.Contains("status and terminal timestamps disagree", StringComparison.Ordinal));
    }

    [Fact]
    public void Completed_cycle_exports_early_actual_end_and_preserves_original_materialized_schedule()
    {
        var state = new GameState();
        var game = AddOperationalGame(state, "Early completion");
        var cycle = Assert.Single(state.Cycles);
        var configuration = Assert.Single(state.CycleConfigurations);
        var scheduledEndAt = Assert.IsType<DateTimeOffset>(configuration.ScheduledEndAt);
        var actualEndAt = cycle.StartAt.AddDays(10);
        Assert.True(actualEndAt < scheduledEndAt);
        MatchControl.CompleteCycle(state, cycle.CycleId, actualEndAt);
        game.Status = GameLifecycleStatus.Completed;
        game.CompletedAt = actualEndAt;

        var validation = GameStateTransfer.Validate(state);

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        using var stream = new MemoryStream();
        GameStateTransfer.Write(stream, state, TestState.Now);
        stream.Position = 0;

        var exported = GameStateTransfer.Read(stream).State;

        Assert.Equal(actualEndAt, Assert.Single(exported.Cycles).EndAt);
        Assert.Equal(scheduledEndAt, Assert.Single(exported.CycleConfigurations).ScheduledEndAt);
    }

    [Fact]
    public void Validation_enforces_configuration_bounds_timing_versions_and_status_timestamps()
    {
        var state = new GameState();
        AddOperationalGame(state, "Invalid configuration");
        var configuration = Assert.Single(state.CycleConfigurations);
        configuration.MapProfileVersion = 0;
        configuration.MinimumHumanSeats = 1;
        configuration.MaximumHumanSeats = null;
        configuration.TickLengthMinutes = 0;
        configuration.ScheduledEndAt = configuration.ScheduledStartAt;
        configuration.CancelledAt = TestState.Now;

        var validation = GameStateTransfer.Validate(state);

        Assert.Contains(validation.Errors, error => error.Contains("map profile version must be positive", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("human-seat bounds", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("tick length must be positive", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("scheduled end must follow", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("status and lock, materialization or cancellation timestamps disagree", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_requires_sha256_shaped_hashes_and_defined_lifecycle_enums()
    {
        var state = new GameState();
        var game = AddOperationalGame(state, "Invalid audit");
        game.GamePolicyContentHash = "abc";
        var configuration = Assert.Single(state.CycleConfigurations);
        var cycle = Assert.Single(state.Cycles);
        configuration.MapProfileContentHash = "abc";
        cycle.MapProfileContentHash = "abc";
        var gameEvent = Assert.Single(state.GameLifecycleEvents);
        gameEvent.Type = (GameLifecycleEventType)999;
        gameEvent.FromStatus = "999";
        gameEvent.ToStatus = "1";

        var validation = GameStateTransfer.Validate(state);

        Assert.Contains(validation.Errors, error =>
            error.Contains("policy content hash", StringComparison.Ordinal)
            && error.Contains("64 hexadecimal characters", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error =>
            error.Contains("map profile content hash", StringComparison.Ordinal)
            && error.Contains("64 hexadecimal characters", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("invalid event type", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("invalid from status '999'", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("invalid to status '1'", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_forked_cyclic_or_non_increasing_cycle_lineage()
    {
        var state = new GameState();
        var game = AddOperationalGame(state, "Lineage");
        var first = Assert.Single(state.Cycles);
        var second = AddOperationalCycle(state, game, 2, "Second");
        var third = AddOperationalCycle(state, game, 3, "Third");
        first.PreviousCycleId = second.CycleId;
        second.PreviousCycleId = first.CycleId;
        third.PreviousCycleId = first.CycleId;
        foreach (var cycle in state.Cycles)
        {
            cycle.Status = CycleStatus.Completed;
        }
        game.Status = GameLifecycleStatus.Completed;
        game.CompletedAt = TestState.Now;

        var validation = GameStateTransfer.Validate(state);

        Assert.Contains(validation.Errors, error => error.Contains($"Cycle {first.CycleId} has more than one direct successor", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("predecessor cycle", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error =>
            error.Contains($"Cycle {first.CycleId}", StringComparison.Ordinal)
            && error.Contains("higher configuration sequence", StringComparison.Ordinal));
    }

    [Fact]
    public void Version_five_export_does_not_rederive_a_complete_legacy_foundation()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        var game = Assert.Single(state.Games);
        var authoritativeCreatedAt = game.CreatedAt.AddDays(-7);
        game.CreatedAt = authoritativeCreatedAt;
        var enrolment = Assert.Single(state.GameEnrolments);
        var authoritativeStatusChangedAt = enrolment.EnrolledAt.AddMinutes(1);
        enrolment.StatusChangedAt = authoritativeStatusChangedAt;
        using var stream = new MemoryStream();

        GameStateTransfer.Write(stream, state, TestState.Now);

        Assert.Equal(authoritativeCreatedAt, game.CreatedAt);
        Assert.Equal(authoritativeStatusChangedAt, enrolment.StatusChangedAt);
        stream.Position = 0;
        var document = GameStateTransfer.Read(stream);
        Assert.Equal(authoritativeCreatedAt, Assert.Single(document.State.Games).CreatedAt);
        Assert.Equal(authoritativeStatusChangedAt, Assert.Single(document.State.GameEnrolments).StatusChangedAt);
    }

    [Fact]
    public void Import_infers_departure_tick_for_legacy_in_transit_fleet()
    {
        var state = TestState.CreateMovementState(linkSystems: true, travelTicks: 2);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        state.Fleets.Single().DepartureTickNumber = null;
        LegacyGameFoundation.Apply(state);
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new GameStateTransferDocument(GameStateTransfer.CurrentFormatVersion, TestState.Now, state),
            GameStateJson.Options));

        var document = GameStateTransfer.Read(stream);

        Assert.Equal(1, Assert.Single(document.State.Fleets).DepartureTickNumber);
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
    public void Reader_accepts_version_one_documents_from_before_sector_persistence()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var root = JsonSerializer.SerializeToNode(
            new GameStateTransferDocument(1, TestState.Now, state),
            GameStateJson.Options)!.AsObject();
        root["formatVersion"] = 1;
        var stateNode = root["state"]!.AsObject();
        stateNode.Remove("sectors");
        foreach (var system in stateNode["systems"]!.AsArray().Select(item => item!.AsObject()))
        {
            system.Remove("sectorId");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));
        var document = GameStateTransfer.Read(stream);

        Assert.Equal(1, document.FormatVersion);
        Assert.Empty(document.State.Sectors);
        Assert.All(document.State.Systems, item => Assert.Equal(Guid.Empty, item.SectorId));
    }

    [Fact]
    public void Reader_backfills_doctrine_state_from_version_three_unlock_events()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        state.Events.Add(new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 3,
            EventType = EventType.DoctrineUnlocked,
            EmpireId = empire.EmpireId,
            Severity = EventSeverity.Normal,
            FactJson = JsonSerializer.Serialize(
                new { doctrine = EconomyProcessor.SurveyProjectionDoctrineKey },
                GameStateJson.Options),
            DisplayText = "Survey Projection unlocked.",
            CreatedAt = TestState.Now
        });
        var root = JsonSerializer.SerializeToNode(
            new GameStateTransferDocument(3, TestState.Now, state),
            GameStateJson.Options)!.AsObject();
        root["formatVersion"] = 3;
        root["state"]!.AsObject().Remove("empireDoctrineUnlocks");

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));
        var document = GameStateTransfer.Read(stream);

        var unlock = Assert.Single(document.State.EmpireDoctrineUnlocks);
        Assert.Equal(cycle.CycleId, unlock.CycleId);
        Assert.Equal(empire.EmpireId, unlock.EmpireId);
        Assert.Equal(EconomyProcessor.SurveyProjectionDoctrineKey, unlock.DoctrineKey);
        Assert.Equal(3, unlock.UnlockedTickNumber);
    }

    [Fact]
    public void Legacy_reader_accepts_runtime_state_from_before_sector_persistence()
    {
        var root = JsonSerializer.SerializeToNode(
            GameSeeder.CreateCuratedColdStart(TestState.Now),
            GameStateJson.Options)!.AsObject();
        root.Remove("sectors");
        foreach (var system in root["systems"]!.AsArray().Select(item => item!.AsObject()))
        {
            system.Remove("sectorId");
        }

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(root.ToJsonString(GameStateJson.Options)));
        var state = GameStateTransfer.ReadLegacyRuntimeState(stream);

        Assert.Empty(state.Sectors);
        Assert.All(state.Systems, item => Assert.Equal(Guid.Empty, item.SectorId));
    }

    [Fact]
    public void Validation_rejects_duplicate_sector_names_within_a_cycle()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        state.Sectors[1].SectorName = $"  {state.Sectors[0].SectorName.ToUpperInvariant()}  ";

        var validation = GameStateTransfer.Validate(state);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("more than one sector named", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_multiple_pending_orders_for_the_same_fleet_and_execution_tick()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        var fleet = Assert.Single(state.Fleets);
        state.FleetOrders.AddRange(
        [
            new FleetOrder
            {
                CycleId = cycle.CycleId,
                FleetId = fleet.FleetId,
                OrderType = FleetOrderType.Hold,
                SubmitTick = 0,
                ExecuteAfterTick = 1,
                CreatedAt = TestState.Now
            },
            new FleetOrder
            {
                CycleId = cycle.CycleId,
                FleetId = fleet.FleetId,
                OrderType = FleetOrderType.Hold,
                SubmitTick = 0,
                ExecuteAfterTick = 1,
                CreatedAt = TestState.Now.AddSeconds(1)
            }
        ]);

        var validation = GameStateTransfer.Validate(state);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error => error.Contains("more than one pending order", StringComparison.Ordinal));
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

    [Fact]
    public void Legacy_defeated_participant_does_not_inherit_a_future_scheduled_cycle_end()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        cycle.EndAt = TestState.Now.AddDays(90);
        empire.Status = EmpireStatus.Defeated;
        state.Factions.Clear();
        state.MatchParticipants.Clear();
        using var stream = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(
            new GameStateTransferDocument(2, TestState.Now, state),
            GameStateJson.Options));

        var document = GameStateTransfer.Read(stream);
        var participant = Assert.Single(document.State.MatchParticipants);

        Assert.Equal(MatchParticipantStatus.Defeated, participant.Status);
        Assert.Equal(empire.CreatedAt, participant.EndedAt);
        Assert.NotEqual(cycle.EndAt, participant.EndedAt);
    }

    [Fact]
    public void Validation_rejects_participant_ownership_disagreement()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var participant = state.MatchParticipants.First();
        participant.PlayerId = state.Players.First(item => item.PlayerId != participant.PlayerId).PlayerId;

        var validation = GameStateTransfer.Validate(state);

        Assert.Contains(validation.Errors, error => error.Contains("does not match its Empire's player ownership", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_cross_cycle_faction_references()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var foreignCycle = new Cycle
        {
            Name = "Foreign",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(1),
            CreatedAt = TestState.Now,
            Status = CycleStatus.Active
        };
        var foreignFaction = new Faction
        {
            CycleId = foreignCycle.CycleId,
            FactionName = "Foreign faction",
            Kind = FactionKind.Neutral,
            Status = FactionStatus.Active,
            CreatedAt = TestState.Now
        };
        state.Cycles.Add(foreignCycle);
        state.Factions.Add(foreignFaction);
        var fleet = state.Fleets.First(item => item.CycleId == cycle.CycleId);
        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = FleetOrderType.Attack,
            TargetFactionId = foreignFaction.FactionId,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            CreatedAt = TestState.Now
        });
        state.Events.First(item => item.CycleId == cycle.CycleId).FactionId = foreignFaction.FactionId;
        var system = state.Systems.First(item => item.CycleId == cycle.CycleId);
        state.BattleRecords.Add(new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 0,
            SystemId = system.SystemId,
            AttackerEmpireId = fleet.EmpireId,
            DefenderEmpireId = Guid.Empty,
            AttackerFactionId = fleet.FactionId,
            DefenderFactionId = foreignFaction.FactionId,
            AttackerFleetIds = $"[\"{fleet.FleetId:D}\"]",
            DefenderFleetIds = "[]",
            AttackerShipsBefore = fleet.ShipCount,
            DefenderShipsBefore = 1,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = "{}",
            CreatedAt = TestState.Now
        });

        var validation = GameStateTransfer.Validate(state);

        Assert.Contains(validation.Errors, error => error.Contains("Fleet order", StringComparison.Ordinal) && error.Contains("target faction", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("Event", StringComparison.Ordinal) && error.Contains("faction", StringComparison.Ordinal));
        Assert.Contains(validation.Errors, error => error.Contains("Battle", StringComparison.Ordinal) && error.Contains("defender faction", StringComparison.Ordinal));
    }

    [Fact]
    public void Validation_rejects_every_audited_cross_cycle_reference_before_sql_persistence()
    {
        var state = CreateCompleteValidState();
        var mainCycle = state.GetActiveCycle()!;
        var foreignCycleId = Guid.NewGuid();
        var foreignSystem = new GalaxySystem
        {
            CycleId = foreignCycleId,
            SectorId = state.Sectors[0].SectorId,
            SystemName = "Foreign system",
            CreatedAt = TestState.Now
        };
        var foreignEmpire = new Empire
        {
            CycleId = foreignCycleId,
            PlayerId = state.Players[0].PlayerId,
            EmpireName = "Foreign empire",
            HomeSystemId = foreignSystem.SystemId,
            CreatedAt = TestState.Now
        };
        var foreignFaction = new Faction
        {
            CycleId = foreignCycleId,
            EmpireId = foreignEmpire.EmpireId,
            FactionName = "Foreign faction",
            Kind = FactionKind.Empire,
            CreatedAt = TestState.Now
        };
        var foreignAdmiral = new Admiral
        {
            CycleId = foreignCycleId,
            EmpireId = foreignEmpire.EmpireId,
            AdmiralName = "Foreign admiral",
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        var foreignFleet = new Fleet
        {
            CycleId = foreignCycleId,
            EmpireId = foreignEmpire.EmpireId,
            FactionId = foreignFaction.FactionId,
            AdmiralId = foreignAdmiral.AdmiralId,
            FleetName = "Foreign fleet",
            CurrentSystemId = foreignSystem.SystemId,
            ShipCount = 1,
            CreatedAt = TestState.Now
        };
        var foreignDefenderFleet = new Fleet
        {
            CycleId = foreignCycleId,
            EmpireId = foreignEmpire.EmpireId,
            FactionId = foreignFaction.FactionId,
            FleetName = "Foreign defender fleet",
            CurrentSystemId = foreignSystem.SystemId,
            ShipCount = 1,
            CreatedAt = TestState.Now
        };
        var foreignBattle = new BattleRecord
        {
            CycleId = foreignCycleId,
            SystemId = foreignSystem.SystemId,
            AttackerEmpireId = foreignEmpire.EmpireId,
            DefenderEmpireId = foreignEmpire.EmpireId,
            AttackerFactionId = foreignFaction.FactionId,
            DefenderFactionId = foreignFaction.FactionId,
            AttackerFleetIds = foreignFleet.FleetId.ToString("D"),
            DefenderFleetIds = foreignDefenderFleet.FleetId.ToString("D"),
            AttackerShipsBefore = 1,
            DefenderShipsBefore = 1,
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
        state.Systems.Add(foreignSystem);
        state.Empires.Add(foreignEmpire);
        state.Factions.Add(foreignFaction);
        state.Admirals.Add(foreignAdmiral);
        state.Fleets.AddRange([foreignFleet, foreignDefenderFleet]);
        state.BattleRecords.Add(foreignBattle);
        state.BattleFleetParticipants.AddRange(
        [
            new BattleFleetParticipant
            {
                BattleId = foreignBattle.BattleId,
                CycleId = foreignCycleId,
                FleetId = foreignFleet.FleetId,
                Side = BattleFleetSide.Attacker
            },
            new BattleFleetParticipant
            {
                BattleId = foreignBattle.BattleId,
                CycleId = foreignCycleId,
                FleetId = foreignDefenderFleet.FleetId,
                Side = BattleFleetSide.Defender
            }
        ]);

        var fleet = state.Fleets.First(item => item.CycleId == mainCycle.CycleId);
        fleet.DestinationSystemId = foreignSystem.SystemId;
        var order = state.FleetOrders.First(item => item.CycleId == mainCycle.CycleId);
        order.TargetSystemId = foreignSystem.SystemId;
        order.TargetEmpireId = foreignEmpire.EmpireId;
        var history = state.AdmiralBattleHistories.Single(item => item.CycleId == mainCycle.CycleId);
        history.AdmiralId = foreignAdmiral.AdmiralId;
        history.SystemId = foreignSystem.SystemId;
        history.FleetId = foreignFleet.FleetId;
        var gameEvent = state.Events.First(item => item.CycleId == mainCycle.CycleId);
        gameEvent.EmpireId = foreignEmpire.EmpireId;
        var battle = state.BattleRecords.Single(item => item.CycleId == mainCycle.CycleId);
        battle.AttackerEmpireId = foreignEmpire.EmpireId;
        battle.DefenderEmpireId = foreignEmpire.EmpireId;
        var chronicle = state.ChronicleEntries.Single(item => item.CycleId == mainCycle.CycleId);
        chronicle.SystemId = foreignSystem.SystemId;
        var majorEvent = state.CycleMajorEvents.Single(item => item.CycleId == mainCycle.CycleId);
        majorEvent.SystemId = foreignSystem.SystemId;
        majorEvent.SourceBattleId = foreignBattle.BattleId;
        var signal = state.SystemHistoricalSignals.Single(item => item.CycleId == mainCycle.CycleId);
        signal.SystemId = foreignSystem.SystemId;
        signal.SourceBattleId = foreignBattle.BattleId;
        var outpost = state.ColonialOutposts.Single(item => item.CycleId == mainCycle.CycleId);
        outpost.SystemId = foreignSystem.SystemId;

        var errors = GameStateTransfer.Validate(state).Errors;

        Assert.Contains(errors, error => error.Contains($"Fleet {fleet.FleetId} and its destination system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Fleet order {order.FleetOrderId} and its target system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Fleet order {order.FleetOrderId} and its target empire", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Admiral battle history {history.AdmiralBattleHistoryId} and its admiral", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Admiral battle history {history.AdmiralBattleHistoryId} and its system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Admiral battle history {history.AdmiralBattleHistoryId} and its fleet", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Event {gameEvent.EventId} and its empire", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Battle {battle.BattleId} and its attacker empire", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Battle {battle.BattleId} and its defender empire", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Chronicle entry {chronicle.ChronicleEntryId} and its system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Cycle major event {majorEvent.CycleMajorEventId} and its system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Cycle major event {majorEvent.CycleMajorEventId} and its source battle", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"System historical signal {signal.SystemHistoricalSignalId} and its system", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"System historical signal {signal.SystemHistoricalSignalId} and its source battle", StringComparison.Ordinal));
        Assert.Contains(errors, error => error.Contains($"Colonial outpost {outpost.ColonialOutpostId} and its system", StringComparison.Ordinal));
    }

    [Fact]
    public void Legacy_battle_fleet_parser_accepts_trimmed_D_guids_only()
    {
        var fleetId = Guid.NewGuid();

        Assert.Equal([fleetId], BattleFleetParticipantCompatibility.ParseLegacyFleetIds($"  {fleetId:D}  ", "fleet list"));
        Assert.Equal([fleetId], BattleFleetParticipantCompatibility.ParseLegacyFleetIds($"[\"  {fleetId:D}  \"]", "fleet list"));

        foreach (var invalid in new[] { $"{fleetId:D}JUNK", fleetId.ToString("N"), fleetId.ToString("B") })
        {
            var error = Assert.Throws<InvalidOperationException>(() =>
                BattleFleetParticipantCompatibility.ParseLegacyFleetIds(invalid, "fleet list"));
            Assert.Contains("invalid fleet identifier", error.Message, StringComparison.Ordinal);
        }
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
        state.EmpireDoctrineUnlocks.Add(new EmpireDoctrineUnlock
        {
            CycleId = cycle.CycleId,
            EmpireId = firstEmpire.EmpireId,
            DoctrineKey = EconomyProcessor.SurveyProjectionDoctrineKey,
            UnlockedTickNumber = 1,
            UnlockedAt = TestState.Now
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
        state.BattleFleetParticipants.AddRange(
        [
            new BattleFleetParticipant
            {
                BattleId = battle.BattleId,
                CycleId = cycle.CycleId,
                FleetId = firstFleet.FleetId,
                Side = BattleFleetSide.Attacker
            },
            new BattleFleetParticipant
            {
                BattleId = battle.BattleId,
                CycleId = cycle.CycleId,
                FleetId = secondFleet.FleetId,
                Side = BattleFleetSide.Defender
            }
        ]);
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
        var replacementOrder = new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = firstFleet.FleetId,
            OrderType = FleetOrderType.Hold,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            Status = FleetOrderStatus.Pending,
            CreatedAt = TestState.Now.AddSeconds(1)
        };
        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = firstFleet.FleetId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = system.SystemId,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            ProcessedTick = 0,
            Status = FleetOrderStatus.Superseded,
            SupersededByOrderId = replacementOrder.FleetOrderId,
            CreatedAt = TestState.Now
        });
        state.FleetOrders.Add(replacementOrder);
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
        LegacyGameFoundation.Apply(state);
        return state;
    }

    private static Game AddOperationalGame(GameState state, string name)
    {
        var game = new Game
        {
            Name = name,
            Purpose = GamePurpose.Standard,
            Status = GameLifecycleStatus.Active,
            Visibility = GameVisibility.Private,
            CreationSource = GameCreationSource.Operator,
            GamePolicyKey = "test-game-policy",
            GamePolicyVersion = 1,
            GamePolicyContentHash = ValidContentHash,
            PolicyProvenanceStatus = ProvenanceStatus.Verified,
            CreatedAt = TestState.Now,
            FirstStartedAt = TestState.Now
        };
        state.Games.Add(game);
        AddOperationalCycle(state, game, 1, $"{name} Cycle");
        state.GameLifecycleEvents.Add(new GameLifecycleEvent
        {
            GameId = game.GameId,
            Type = GameLifecycleEventType.Created,
            ToStatus = GameLifecycleStatus.Active.ToString(),
            FactJson = "{}",
            CreatedAt = TestState.Now
        });
        return game;
    }

    private static Cycle AddOperationalCycle(GameState state, Game game, int sequenceNumber, string name)
    {
        var configuration = new CycleConfiguration
        {
            GameId = game.GameId,
            SequenceNumber = sequenceNumber,
            Status = CycleConfigurationStatus.Materialized,
            ProvenanceStatus = ProvenanceStatus.Verified,
            MapProfileKey = "test-map",
            MapProfileVersion = 1,
            MapProfileContentHash = ValidContentHash,
            MapSeed = 1000 + sequenceNumber,
            ScenarioProfileKey = "test-scenario",
            ScenarioProfileVersion = 1,
            ScenarioProfileContentHash = ValidContentHash,
            ScenarioSeed = 2000 + sequenceNumber,
            CyclePolicyKey = "test-cycle-policy",
            CyclePolicyVersion = 1,
            CyclePolicyContentHash = ValidContentHash,
            ScheduledStartAt = TestState.Now.AddDays(sequenceNumber - 1),
            ScheduledEndAt = TestState.Now.AddDays(sequenceNumber + 89),
            TickLengthMinutes = 60,
            CreatedAt = TestState.Now,
            LockedAt = TestState.Now,
            MaterializedAt = TestState.Now
        };
        var cycle = new Cycle
        {
            GameId = game.GameId,
            CycleConfigurationId = configuration.CycleConfigurationId,
            Name = name,
            StartAt = configuration.ScheduledStartAt.Value,
            EndAt = configuration.ScheduledEndAt.Value,
            TickLengthMinutes = configuration.TickLengthMinutes.Value,
            Status = CycleStatus.Active,
            TurnStage = TurnResolutionStage.CommandOpen,
            MapProfileKey = configuration.MapProfileKey,
            MapProfileVersion = configuration.MapProfileVersion,
            MapProfileContentHash = configuration.MapProfileContentHash,
            MapSeed = configuration.MapSeed,
            ScenarioProfileKey = configuration.ScenarioProfileKey,
            ScenarioProfileVersion = configuration.ScenarioProfileVersion,
            ScenarioProfileContentHash = configuration.ScenarioProfileContentHash,
            ScenarioSeed = configuration.ScenarioSeed,
            CyclePolicyKey = configuration.CyclePolicyKey,
            CyclePolicyVersion = configuration.CyclePolicyVersion,
            CyclePolicyContentHash = configuration.CyclePolicyContentHash,
            ProfileProvenanceStatus = configuration.ProvenanceStatus,
            CreatedAt = TestState.Now
        };
        state.CycleConfigurations.Add(configuration);
        state.Cycles.Add(cycle);
        return cycle;
    }

    private static PropertyInfo[] PersistedCollections() => typeof(GameState)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.PropertyType.IsGenericType
                           && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
        .ToArray();

    private static MemoryStream JsonStream(string json) => new(Encoding.UTF8.GetBytes(json));
}
