using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
public sealed class SqlServerGameStateStoreIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "CYCLES_SQL_INTEGRATION_CONNECTION_STRING";

    [Fact]
    public void Store_runs_and_round_trips_a_neutral_faction_battle_without_inventing_diplomacy()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var state = GameSeeder.CreateDevelopmentMatch();
        var cycle = state.GetActiveCycle()!;
        var empire = state.Empires.OrderBy(item => item.EmpireName).First();
        var briefing = state.Events.Single(item => item.EventType == EventType.OpeningBriefingIssued && item.EmpireId == empire.EmpireId);
        using var objectives = JsonDocument.Parse(briefing.FactJson);
        var attack = objectives.RootElement.GetProperty("objectives").GetProperty("attack");
        var fleetId = attack.GetProperty("fleetId").GetGuid();
        var neutralFactionId = attack.GetProperty("targetFactionId").GetGuid();
        OrderService.SubmitAttackOrderAgainstFaction(state, fleetId, neutralFactionId, TestState.Now);
        var store = new SqlServerGameStateStore(connectionString);
        store.Replace(state);

        var result = store.RunTick(cycle.CycleId, TestState.Now.AddHours(1));

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var loaded = store.LoadOrCreate();
        var battle = Assert.Single(loaded.BattleRecords, item => item.AttackerFactionId == state.GetEmpireFaction(empire.EmpireId).FactionId);
        Assert.Equal(neutralFactionId, battle.DefenderFactionId);
        Assert.Equal(Guid.Empty, battle.DefenderEmpireId);
        Assert.DoesNotContain(loaded.DiplomaticRelationships, item =>
            item.FirstEmpireId == empire.EmpireId || item.SecondEmpireId == empire.EmpireId);

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.BattleRecords WHERE BattleID = @BattleID AND DefenderEmpireID IS NULL AND DefenderFactionID = @FactionID;";
        command.Parameters.AddWithValue("@BattleID", battle.BattleId);
        command.Parameters.AddWithValue("@FactionID", neutralFactionId);
        Assert.Equal(1, Convert.ToInt32(command.ExecuteScalar()));
    }

    [Fact]
    public void Store_round_trips_legacy_systems_without_sector_membership_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateMovementState(linkSystems: true);
        state.Sectors.Clear();
        foreach (var system in state.Systems)
        {
            system.SectorId = Guid.Empty;
        }

        Assert.Empty(state.Sectors);
        Assert.All(state.Systems, item => Assert.Equal(Guid.Empty, item.SectorId));

        store.Replace(state);

        var loaded = store.LoadOrCreate();
        Assert.Empty(loaded.Sectors);
        Assert.All(loaded.Systems, item => Assert.Equal(Guid.Empty, item.SectorId));
    }

    [Fact]
    public void Store_round_trips_and_updates_state_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 90210);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var system = state.Systems.First(system => system.CycleId == cycle.CycleId);
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        var adminPlayer = state.Players.Single(player => player.PlayerId == attacker.PlayerId);
        adminPlayer.ExternalIssuer = "https://identity.example";
        adminPlayer.ExternalSubject = "integration-admin";
        adminPlayer.Role = PlayerRole.Admin;
        var adminAudit = new AdminRoleAuditRecord
        {
            TargetPlayerId = adminPlayer.PlayerId,
            Action = AdminRoleAuditAction.Bootstrap,
            Reason = "SQL integration bootstrap.",
            Source = "integration-test",
            CreatedAt = TestState.Now
        };
        state.AdminRoleAuditRecords.Add(adminAudit);
        var battle = new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 0,
            SystemId = system.SystemId,
            AttackerEmpireId = attacker.EmpireId,
            DefenderEmpireId = defender.EmpireId,
            AttackerFactionId = state.GetEmpireFaction(attacker.EmpireId).FactionId,
            DefenderFactionId = state.GetEmpireFaction(defender.EmpireId).FactionId,
            AttackerFleetIds = state.Fleets.First(fleet => fleet.EmpireId == attacker.EmpireId).FleetId.ToString(),
            DefenderFleetIds = state.Fleets.First(fleet => fleet.EmpireId == defender.EmpireId).FleetId.ToString(),
            AttackerShipsBefore = 10,
            DefenderShipsBefore = 12,
            AttackerLosses = 1,
            DefenderLosses = 2,
            Outcome = BattleOutcome.DefenderVictory,
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
        var sourceEvent = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 0,
            EventType = EventType.CombatResolved,
            SystemId = system.SystemId,
            EmpireId = attacker.EmpireId,
            FactionId = state.GetEmpireFaction(attacker.EmpireId).FactionId,
            Severity = EventSeverity.High,
            DisplayText = "A test battle was resolved.",
            FactJson = battle.FactJson,
            CreatedAt = TestState.Now
        };
        var chronicle = new ChronicleEntry
        {
            SourceEventId = sourceEvent.EventId,
            SourceBattleId = battle.BattleId,
            CycleId = cycle.CycleId,
            SystemId = system.SystemId,
            Title = "Failed generation",
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = 70,
            FactualSummary = "A battle needs generated prose.",
            NarrativeText = "",
            NarrativeStatus = NarrativeGenerationStatus.Failed,
            NarrativeContextJson = """{"source":"integration"}""",
            NarrativeFailureReason = "provider unavailable",
            CreatedAt = TestState.Now
        };
        state.Events.Add(sourceEvent);
        state.BattleRecords.Add(battle);
        state.ChronicleEntries.Add(chronicle);

        store.Replace(state);

        var loaded = store.LoadOrCreate();
        Assert.Equal(cycle.CycleId, loaded.GetActiveCycle()?.CycleId);
        Assert.Equal(8, loaded.Systems.Count);
        Assert.NotEmpty(loaded.Sectors);
        Assert.All(loaded.Systems, item => Assert.Contains(loaded.Sectors, sector => sector.SectorId == item.SectorId));
        Assert.Equal(2, loaded.Empires.Count);
        Assert.Equal(2, loaded.Admirals.Count);
        Assert.All(loaded.Fleets, fleet => Assert.NotNull(fleet.AdmiralId));
        var loadedAdmin = loaded.Players.Single(player => player.PlayerId == adminPlayer.PlayerId);
        Assert.Equal("https://identity.example", loadedAdmin.ExternalIssuer);
        Assert.Equal("integration-admin", loadedAdmin.ExternalSubject);
        Assert.Equal(PlayerRole.Admin, loadedAdmin.Role);
        var loadedAudit = Assert.Single(loaded.AdminRoleAuditRecords, item => item.AdminRoleAuditRecordId == adminAudit.AdminRoleAuditRecordId);
        Assert.Equal(AdminRoleAuditAction.Bootstrap, loadedAudit.Action);
        Assert.Equal("integration-test", loadedAudit.Source);
        var loadedChronicle = Assert.Single(loaded.ChronicleEntries, item => item.ChronicleEntryId == chronicle.ChronicleEntryId);
        Assert.Equal(NarrativeGenerationStatus.Failed, loadedChronicle.NarrativeStatus);
        Assert.Equal("""{"source":"integration"}""", loadedChronicle.NarrativeContextJson);
        Assert.Equal("provider unavailable", loadedChronicle.NarrativeFailureReason);

        var result = store.Update(current =>
        {
            var activeCycle = current.GetActiveCycle()
                ?? throw new InvalidOperationException("Stored state must contain an active Cycle.");
            return new TickEngine().RunTick(current, activeCycle.CycleId, DateTimeOffset.Parse("2026-06-23T12:00:00Z"));
        });

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        Assert.Equal(1, updated.GetActiveCycle()?.CurrentTickNumber);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
        Assert.Contains(updated.Events, item => item.CycleId == cycle.CycleId && item.TickNumber == 1 && item.EventType == EventType.ResourcesGenerated);
        Assert.Equal(2, updated.EmpireMetrics.Count(item => item.CycleId == cycle.CycleId && item.TickNumber == 1));
    }

    [Fact]
    public void Store_persists_order_submission_and_tick_outcome_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var fleet = state.Fleets.Single();
        var destination = state.Systems.Single(system => system.SystemName == "Destination");

        store.Replace(state);

        var original = store.Update(current => OrderService.SubmitHoldOrder(
            current,
            fleet.FleetId,
            TestState.Now));
        var order = store.Update(current => OrderService.SubmitMoveOrder(
            current,
            fleet.FleetId,
            destination.SystemId,
            TestState.Now.AddSeconds(1),
            original.FleetOrderId));

        Assert.Equal(FleetOrderStatus.Pending, order.Status);

        var afterOrder = store.LoadOrCreate();
        Assert.Single(afterOrder.FleetOrders, item => item.FleetOrderId == order.FleetOrderId);
        var superseded = Assert.Single(afterOrder.FleetOrders, item => item.FleetOrderId == original.FleetOrderId);
        Assert.Equal(FleetOrderStatus.Superseded, superseded.Status);
        Assert.Equal(order.FleetOrderId, superseded.SupersededByOrderId);

        var result = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        var movedFleet = updated.Fleets.Single(item => item.FleetId == fleet.FleetId);
        var processedOrder = updated.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);

        Assert.Equal(destination.SystemId, movedFleet.CurrentSystemId);
        Assert.Equal(FleetOrderStatus.Processed, processedOrder.Status);
        Assert.Equal(FleetOrderCommandSource.Human, processedOrder.CommandSource);
        Assert.Equal(1, processedOrder.SealedTick);
        Assert.Equal(TestState.Now, processedOrder.SealedAt);
        Assert.Equal(1, processedOrder.ProcessedTick);
        Assert.Equal(TurnResolutionStage.CommandOpen, updated.Cycles.Single(item => item.CycleId == cycle.CycleId).TurnStage);
        Assert.Contains(updated.Events, item => item.EventType == EventType.FleetMoved && item.SystemId == destination.SystemId);
        Assert.Single(updated.EmpireMetrics, item => item.CycleId == cycle.CycleId && item.TickNumber == 1);
    }

    [Fact]
    public void Store_refuses_to_mutate_an_existing_admin_role_audit_record_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var audit = new AdminRoleAuditRecord
        {
            TargetPlayerId = state.Players.Single().PlayerId,
            Action = AdminRoleAuditAction.Granted,
            Reason = "Original reason.",
            Source = "integration-test",
            CreatedAt = TestState.Now
        };
        state.AdminRoleAuditRecords.Add(audit);
        store.Replace(state);

        Assert.Throws<SqlException>(() => store.Update(current =>
        {
            current.AdminRoleAuditRecords.Single().Reason = "Rewritten reason.";
            return true;
        }));

        var reloaded = store.LoadOrCreate();
        Assert.Equal("Original reason.", reloaded.AdminRoleAuditRecords.Single().Reason);
    }

    [Fact]
    public void Store_dedicated_tick_runner_persists_colonial_outpost_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var fleet = state.Fleets.Single();
        var destination = state.Systems.Single(system => system.SystemName == "Destination");
        fleet.CurrentSystemId = destination.SystemId;
        state.EmpireResources.Single().Population = 100m;
        var order = OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);

        store.Replace(state);

        var result = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var updated = store.LoadOrCreate();
        var outpost = Assert.Single(updated.ColonialOutposts);
        Assert.Equal(fleet.EmpireId, outpost.EmpireId);
        Assert.Equal(destination.SystemId, outpost.SystemId);
        Assert.Equal(1, outpost.EstablishedTick);
        Assert.Equal(FleetOrderStatus.Processed, updated.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId).Status);
        Assert.Contains(updated.Events, item => item.EventType == EventType.ColonialOutpostEstablished);
    }

    [Fact]
    public void Store_dedicated_tick_runner_persists_admiral_battle_history_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 40, strategicValue: 35);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var secondHome = new GalaxySystem
        {
            CycleId = cycle.CycleId,
            SystemName = "Second Home",
            X = 250,
            Y = 250,
            IndustryOutput = 10,
            ResearchOutput = 10,
            PopulationOutput = 10,
            StrategicValue = 10,
            CreatedAt = TestState.Now
        };
        state.Systems.Add(secondHome);
        state.Empires.Single(empire => empire.EmpireName == "Second").HomeSystemId = secondHome.SystemId;
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        var defenderFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[1].EmpireId);
        var attackerAdmiral = AssignAdmiral(state, attackerFleet, "SQL Ardent");
        AssignAdmiral(state, defenderFleet, "SQL Shield");
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, TestState.Now);

        store.Replace(state);

        var result = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        var history = Assert.Single(updated.AdmiralBattleHistories, item => item.AdmiralId == attackerAdmiral.AdmiralId);
        var updatedAdmiral = updated.Admirals.Single(item => item.AdmiralId == attackerAdmiral.AdmiralId);

        Assert.Equal(1, updated.Cycles.Single(item => item.CycleId == cycle.CycleId).CurrentTickNumber);
        Assert.True(history.ReputationChange > 0);
        Assert.Equal(history.ReputationScoreAfter, updatedAdmiral.ReputationScore);
        Assert.Contains(updated.Events, item => item.EventType == EventType.AdmiralBattleReported);
    }

    [Fact]
    public void Store_dedicated_tick_runner_persists_treaty_breaking_aggression_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 40);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        state.Systems.Add(new GalaxySystem
        {
            SystemId = defender.HomeSystemId,
            CycleId = cycle.CycleId,
            SystemName = "Defender Home",
            X = 250,
            Y = 250,
            IndustryOutput = 10,
            ResearchOutput = 10,
            PopulationOutput = 10,
            StrategicValue = 10,
            CreatedAt = TestState.Now
        });
        var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == attacker.EmpireId);
        DiplomacyService.SetState(
            state,
            cycle.CycleId,
            attacker.EmpireId,
            defender.EmpireId,
            DiplomaticRelationshipState.Alliance,
            0,
            TestState.Now);
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, defender.EmpireId, TestState.Now);

        store.Replace(state);
        var result = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var updated = store.LoadOrCreate();
        var relationship = Assert.Single(updated.DiplomaticRelationships);
        Assert.Equal(DiplomaticRelationshipState.Neutral, relationship.State);
        Assert.Equal(1, relationship.UpdatedTick);
        Assert.Contains(updated.Events, item => item.EventType == EventType.DiplomaticAggression);
        Assert.Contains(updated.Events, item => item.EventType == EventType.TreatyCancelledByAggression);
    }

    [Fact]
    public void Store_dedicated_tick_runner_persists_failed_tick_recovery_state_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        state.EmpireResources.Clear();

        store.Replace(state);

        var result = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, result.Status);

        var updated = store.LoadOrCreate();
        var updatedCycle = updated.Cycles.Single(item => item.CycleId == cycle.CycleId);
        Assert.Equal(CycleStatus.RecoveryRequired, updatedCycle.Status);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Failed);
    }

    [Fact]
    public void Store_dedicated_tick_runner_loads_only_the_target_cycle_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var targetState = TestState.CreateMovementState(linkSystems: true);
        var unrelatedState = TestState.CreateSingleEmpireState();
        var targetCycle = targetState.GetActiveCycle() ?? throw new InvalidOperationException("Target state must contain an active Cycle.");
        var unrelatedCycle = unrelatedState.GetActiveCycle() ?? throw new InvalidOperationException("Unrelated state must contain an active Cycle.");
        var unrelatedFleet = unrelatedState.Fleets.Single();
        var state = CombineStates(targetState, unrelatedState);

        store.Replace(state);
        UpdateFleetStatus(connectionString, unrelatedFleet.FleetId, "NotARealStatus");

        try
        {
            var result = store.RunTick(targetCycle.CycleId, TestState.Now);

            Assert.Equal(TickLogStatus.Completed, result.Status);
        }
        finally
        {
            UpdateFleetStatus(connectionString, unrelatedFleet.FleetId, FleetStatus.Active.ToString());
        }

        var updated = store.LoadOrCreate();
        Assert.Equal(1, updated.Cycles.Single(item => item.CycleId == targetCycle.CycleId).CurrentTickNumber);
        Assert.Equal(0, updated.Cycles.Single(item => item.CycleId == unrelatedCycle.CycleId).CurrentTickNumber);
    }

    [Fact]
    public void Store_dedicated_tick_runner_loads_only_incremental_tick_workspace_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var empire = state.Empires.Single();
        var fleet = state.Fleets.Single();
        var system = state.Systems.Single();
        cycle.CurrentTickNumber = 1;

        var historicalEvent = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            EventType = EventType.ResourcesGenerated,
            SystemId = system.SystemId,
            EmpireId = empire.EmpireId,
            FactionId = state.GetEmpireFaction(empire.EmpireId).FactionId,
            Severity = EventSeverity.Low,
            DisplayText = "Historical event.",
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
        var historicalBattle = new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            SystemId = system.SystemId,
            AttackerEmpireId = empire.EmpireId,
            DefenderEmpireId = empire.EmpireId,
            AttackerFactionId = state.GetEmpireFaction(empire.EmpireId).FactionId,
            DefenderFactionId = state.GetEmpireFaction(empire.EmpireId).FactionId,
            AttackerFleetIds = fleet.FleetId.ToString(),
            DefenderFleetIds = fleet.FleetId.ToString(),
            AttackerShipsBefore = 1,
            DefenderShipsBefore = 1,
            Outcome = BattleOutcome.MutualDestruction,
            FactJson = "{}",
            CreatedAt = TestState.Now
        };
        var historicalChronicle = new ChronicleEntry
        {
            SourceEventId = historicalEvent.EventId,
            SourceBattleId = historicalBattle.BattleId,
            CycleId = cycle.CycleId,
            SystemId = system.SystemId,
            Title = "Historical chronicle.",
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = 10,
            FactualSummary = "Historical chronicle.",
            NarrativeText = "Historical chronicle.",
            CreatedAt = TestState.Now
        };
        var futureOrder = new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = FleetOrderType.Hold,
            SubmitTick = 1,
            ExecuteAfterTick = 10,
            Status = FleetOrderStatus.Pending,
            CreatedAt = TestState.Now
        };
        var ineligibleConstruction = new ShipConstruction
        {
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            ShipCount = 1,
            IndustrySpent = EconomyProcessor.ShipIndustryCost,
            StartedTick = 1,
            CompleteAfterTick = 10,
            Status = ShipConstructionStatus.Queued,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        state.Events.Add(historicalEvent);
        state.BattleRecords.Add(historicalBattle);
        state.ChronicleEntries.Add(historicalChronicle);
        state.FleetOrders.Add(futureOrder);
        state.ShipConstructions.Add(ineligibleConstruction);

        store.Replace(state);
        CorruptRowsOutsideNextTickWorkspace(
            connectionString,
            historicalEvent.EventId,
            historicalBattle.BattleId,
            historicalChronicle.ChronicleEntryId,
            futureOrder.FleetOrderId,
            ineligibleConstruction.ShipConstructionId);

        try
        {
            var result = store.RunTick(cycle.CycleId, TestState.Now.AddHours(1));

            Assert.Equal(TickLogStatus.Completed, result.Status);
            Assert.Equal(2, ReadInt(
                connectionString,
                "SELECT CurrentTickNumber FROM dbo.Cycles WHERE CycleID = @CycleID;",
                command => command.Parameters.AddWithValue("@CycleID", cycle.CycleId)));
            Assert.Equal(1, ReadInt(
                connectionString,
                "SELECT COUNT(*) FROM dbo.Events WHERE EventID = @EventID AND EventType = N'NotARealEventType';",
                command => command.Parameters.AddWithValue("@EventID", historicalEvent.EventId)));
            Assert.Equal(1, ReadInt(
                connectionString,
                "SELECT COUNT(*) FROM dbo.FleetOrders WHERE FleetOrderID = @FleetOrderID AND OrderType = N'NotARealOrderType';",
                command => command.Parameters.AddWithValue("@FleetOrderID", futureOrder.FleetOrderId)));
        }
        finally
        {
            store.Replace(TestState.CreateSingleEmpireState());
        }
    }

    [Fact]
    public void Store_dedicated_tick_runner_uses_cycle_scoped_application_lock_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");

        store.Replace(state);

        using (HoldSqlApplicationLock(connectionString, "Cycles.GameState"))
        {
            var result = store.RunTick(cycle.CycleId, TestState.Now);

            Assert.Equal(TickLogStatus.Completed, result.Status);
        }

        var cycleLockName = $"Cycles.Tick.{cycle.CycleId:D}";
        using (HoldSqlApplicationLock(connectionString, cycleLockName))
        {
            var ex = Assert.Throws<TimeoutException>(() => store.RunTick(cycle.CycleId, TestState.Now.AddHours(1)));

            Assert.Contains(cycleLockName, ex.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Store_active_cycle_update_uses_the_cycle_tick_lock_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        store.Replace(state);

        var cycleLockName = $"Cycles.Tick.{cycle.CycleId:D}";
        using (HoldSqlApplicationLock(connectionString, cycleLockName))
        {
            var exception = Assert.Throws<TimeoutException>(() => store.UpdateActiveCycleExclusively(current => current.Systems.Count));

            Assert.Contains(cycleLockName, exception.Message, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Store_run_tick_if_due_advances_cycle_once_across_concurrent_workers()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var firstStore = new SqlServerGameStateStore(connectionString);
        var secondStore = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        cycle.StartAt = TestState.Now;
        cycle.TickLengthMinutes = 60;
        firstStore.Replace(state);

        using var startBarrier = new Barrier(participantCount: 2);
        var firstAttempt = RunConcurrently(firstStore);
        var secondAttempt = RunConcurrently(secondStore);
        var results = await Task.WhenAll(firstAttempt, secondAttempt);

        Assert.Single(results, result => result is not null);
        var updated = firstStore.LoadOrCreate();
        Assert.Equal(1, updated.GetActiveCycle()?.CurrentTickNumber);
        Assert.Single(
            updated.TickLogs,
            log => log.CycleId == cycle.CycleId && log.Status == TickLogStatus.Completed);

        Task<TickResult?> RunConcurrently(SqlServerGameStateStore store) => Task.Run(() =>
        {
            if (!startBarrier.SignalAndWait(TimeSpan.FromSeconds(5)))
            {
                throw new TimeoutException("Concurrent tick attempts did not reach the start barrier.");
            }

            return store.RunTickIfDue(TestState.Now);
        });
    }

    [Fact]
    public void Store_rolls_back_when_duplicate_running_tick_is_detected()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            Status = TickLogStatus.Running
        });

        store.Replace(state);

        var ex = Assert.Throws<InvalidOperationException>(
            () => store.Update(current => new TickEngine().RunTick(current, cycle.CycleId, TestState.Now)));

        Assert.Contains("already running", ex.Message, StringComparison.OrdinalIgnoreCase);

        var updated = store.LoadOrCreate();
        Assert.Equal(0, updated.GetActiveCycle()?.CurrentTickNumber);
        Assert.Single(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.Status == TickLogStatus.Running);
    }

    [Fact]
    public void Store_updates_rows_and_removes_missing_rows_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 451);
        var retainedPlayer = state.Players[0];
        var removedPlayer = state.Players[1];
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");

        store.Replace(state);

        store.Update(current =>
        {
            var player = current.Players.Single(item => item.PlayerId == retainedPlayer.PlayerId);
            player.Username = "renamed-player";
            return player.PlayerId;
        });

        var updated = store.LoadOrCreate();
        Assert.Contains(updated.Players, item => item.PlayerId == retainedPlayer.PlayerId && item.Username == "renamed-player");
        Assert.Contains(updated.Players, item => item.PlayerId == removedPlayer.PlayerId);
        Assert.Equal(cycle.CycleId, updated.GetActiveCycle()?.CycleId);

        var replacement = GameSeeder.CreateDefault(systemCount: 6, empireCount: 1, seed: 452);
        store.Replace(replacement);

        var replaced = store.LoadOrCreate();
        Assert.DoesNotContain(replaced.Players, item => item.PlayerId == removedPlayer.PlayerId);
        Assert.DoesNotContain(replaced.Cycles, item => item.CycleId == cycle.CycleId);
        Assert.Single(replaced.Cycles);
        Assert.Equal(6, replaced.Systems.Count);
    }

    [Fact]
    public void Store_replaces_a_retained_empires_home_system_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var original = TestState.CreateSingleEmpireState(includeFleet: false);
        store.Replace(original);

        var replacement = original.DeepClone();
        var home = Assert.Single(replacement.Systems);
        var replacementHomeSystemId = Guid.NewGuid();
        home.SystemId = replacementHomeSystemId;
        Assert.Single(replacement.Empires).HomeSystemId = replacementHomeSystemId;

        store.Replace(replacement);

        var loaded = store.LoadOrCreate();
        Assert.Equal(replacementHomeSystemId, Assert.Single(loaded.Systems).SystemId);
        Assert.Equal(replacementHomeSystemId, Assert.Single(loaded.Empires).HomeSystemId);
    }

    [Fact]
    public void Store_replaces_external_identity_when_player_ids_change()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var original = GameSeeder.CreateDefault(systemCount: 6, empireCount: 1, seed: 901);
        original.Players[0].ExternalIssuer = "https://identity.example";
        original.Players[0].ExternalSubject = "subject-1";
        store.Replace(original);

        var replacement = GameSeeder.CreateDefault(systemCount: 6, empireCount: 1, seed: 902);
        replacement.Players[0].ExternalIssuer = "https://identity.example";
        replacement.Players[0].ExternalSubject = "subject-1";
        store.Replace(replacement);

        var loaded = store.LoadOrCreate();
        var player = Assert.Single(loaded.Players);
        Assert.Equal(replacement.Players[0].PlayerId, player.PlayerId);
        Assert.Equal("subject-1", player.ExternalSubject);
    }

    [Fact]
    public void Store_replaces_sector_ids_without_violating_cycle_uniqueness()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 903);
        store.Replace(state);

        var replacement = state.DeepClone();
        var replacementIds = replacement.Sectors.ToDictionary(sector => sector.SectorId, _ => Guid.NewGuid());
        foreach (var sector in replacement.Sectors)
        {
            sector.SectorId = replacementIds[sector.SectorId];
        }

        foreach (var system in replacement.Systems)
        {
            system.SectorId = replacementIds[system.SectorId];
        }

        store.Replace(replacement);

        var loaded = store.LoadOrCreate();
        Assert.Equal(
            replacement.Sectors.Select(sector => sector.SectorId).Order(),
            loaded.Sectors.Select(sector => sector.SectorId).Order());
        Assert.All(loaded.Systems, system => Assert.Contains(loaded.Sectors, sector => sector.SectorId == system.SectorId));
    }

    [Fact]
    public void Store_allows_failed_and_completed_logs_for_retried_tick_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-5),
            CompletedAt = TestState.Now.AddMinutes(-5),
            Status = TickLogStatus.Failed,
            DiagnosticLog = "failed attempt"
        });

        store.Replace(state);

        var result = store.Update(current => new TickEngine().RunTick(current, cycle.CycleId, TestState.Now));

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Failed);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
    }

    [Fact]
    public void Store_persists_recovery_clear_and_successful_retry_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var empire = state.Empires.Single();
        state.EmpireResources.Clear();
        store.Replace(state);

        var failed = store.RunTick(cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Failed, failed.Status);

        var retried = store.Update(current =>
        {
            current.EmpireResources.Add(new EmpireResource
            {
                EmpireId = empire.EmpireId,
                Industry = 100,
                Research = 100,
                Population = 100,
                UpdatedAt = TestState.Now
            });
            RecoveryService.ClearRecovery(
                current,
                cycle.CycleId,
                "sql-integration",
                "restored the missing resource row",
                TestState.Now.AddMinutes(1));
            return new TickEngine().RunTick(current, cycle.CycleId, TestState.Now.AddMinutes(1));
        });

        Assert.Equal(TickLogStatus.Completed, retried.Status);

        var updated = store.LoadOrCreate();
        var updatedCycle = updated.Cycles.Single(item => item.CycleId == cycle.CycleId);
        Assert.Equal(CycleStatus.Active, updatedCycle.Status);
        Assert.Equal(1, updatedCycle.CurrentTickNumber);
        Assert.Contains(updated.Events, item => item.CycleId == cycle.CycleId && item.EventType == EventType.RecoveryCleared);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Failed);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
    }

    [Fact]
    public void Store_persists_cycle_rankings_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateTwoEmpireContest(attackerShips: 80, defenderShips: 20);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var system = state.Systems.Single();
        var firstEmpire = state.Empires.Single(empire => empire.EmpireName == "First");
        var secondEmpire = state.Empires.Single(empire => empire.EmpireName == "Second");
        var firstHome = new GalaxySystem
        {
            CycleId = cycle.CycleId,
            SystemName = "First Home",
            X = 50,
            Y = 50,
            IndustryOutput = 10,
            ResearchOutput = 10,
            PopulationOutput = 10,
            StrategicValue = 10,
            CreatedAt = TestState.Now
        };
        var secondHome = new GalaxySystem
        {
            CycleId = cycle.CycleId,
            SystemName = "Second Home",
            X = 150,
            Y = 150,
            IndustryOutput = 10,
            ResearchOutput = 10,
            PopulationOutput = 10,
            StrategicValue = 10,
            CreatedAt = TestState.Now
        };
        state.Systems.Add(firstHome);
        state.Systems.Add(secondHome);
        firstEmpire.HomeSystemId = firstHome.SystemId;
        secondEmpire.HomeSystemId = secondHome.SystemId;
        state.BattleRecords.Add(new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            SystemId = system.SystemId,
            AttackerEmpireId = firstEmpire.EmpireId,
            DefenderEmpireId = secondEmpire.EmpireId,
            AttackerFactionId = state.GetEmpireFaction(firstEmpire.EmpireId).FactionId,
            DefenderFactionId = state.GetEmpireFaction(secondEmpire.EmpireId).FactionId,
            AttackerFleetIds = state.Fleets.First(fleet => fleet.EmpireId == firstEmpire.EmpireId).FleetId.ToString(),
            DefenderFleetIds = state.Fleets.First(fleet => fleet.EmpireId == secondEmpire.EmpireId).FleetId.ToString(),
            AttackerShipsBefore = 80,
            DefenderShipsBefore = 20,
            AttackerLosses = 15,
            DefenderLosses = 5,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = "{}",
            CreatedAt = TestState.Now
        });
        CycleEndService.CompleteCycle(state, cycle.CycleId, TestState.Now);

        store.Replace(state);

        var updated = store.LoadOrCreate();
        var rankings = updated.CycleRankings.Where(ranking => ranking.CycleId == cycle.CycleId).OrderBy(ranking => ranking.Rank).ToArray();
        var majorEvent = Assert.Single(updated.CycleMajorEvents, item => item.CycleId == cycle.CycleId);
        var historicalSignal = Assert.Single(updated.SystemHistoricalSignals, item => item.CycleId == cycle.CycleId && item.SystemId == system.SystemId);

        Assert.Equal(CycleStatus.Completed, updated.Cycles.Single(item => item.CycleId == cycle.CycleId).Status);
        Assert.Equal(2, rankings.Length);
        Assert.True(rankings[0].IsWinner);
        Assert.False(rankings[1].IsWinner);
        Assert.Equal(CycleMajorEventType.Battle, majorEvent.EventType);
        Assert.Equal(20, majorEvent.TotalLosses);
        Assert.Equal(SystemHistoricalSignalType.BattleActivity, historicalSignal.SignalType);
        Assert.Equal(1, historicalSignal.BattleCount);
        Assert.Equal(20, historicalSignal.TotalLosses);
        Assert.Equal(2, historicalSignal.HistoricalSignificanceIncrease);
        Assert.True(historicalSignal.HostedCycleLargestBattle);
    }

    [Fact]
    public void Store_round_trips_successor_cycle_and_player_continuity_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(
            systemCount: 8,
            empireCount: 2,
            seed: 7722,
            createdAt: TestState.Now);
        var sourceCycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        sourceCycle.CurrentTickNumber = 3;
        var sourcePlayers = state.Empires
            .Where(empire => empire.CycleId == sourceCycle.CycleId)
            .Select(empire => empire.PlayerId)
            .Order()
            .ToArray();
        CycleEndService.CompleteCycle(state, sourceCycle.CycleId, TestState.Now.AddHours(3));
        var continuity = CycleContinuityService.GenerateNextCycle(
            state,
            sourceCycle.CycleId,
            TestState.Now.AddDays(1),
            seed: 7723);

        store.Replace(state);

        var updated = store.LoadOrCreate();
        var successor = updated.Cycles.Single(item => item.CycleId == continuity.CycleId);
        var successorEmpires = updated.Empires.Where(empire => empire.CycleId == successor.CycleId).ToArray();

        Assert.Equal(CycleStatus.Completed, updated.Cycles.Single(item => item.CycleId == sourceCycle.CycleId).Status);
        Assert.Equal(CycleStatus.Active, successor.Status);
        Assert.Equal(2, updated.Cycles.Count);
        Assert.Equal(2, updated.CycleRankings.Count(item => item.CycleId == sourceCycle.CycleId));
        Assert.Equal(sourcePlayers, successorEmpires.Select(empire => empire.PlayerId).Order().ToArray());
        Assert.Single(updated.Sectors, sector => sector.CycleId == successor.CycleId);
        Assert.Equal(8, updated.Systems.Count(system => system.CycleId == successor.CycleId));
        Assert.Contains(updated.Events, item => item.CycleId == successor.CycleId && item.EventType == EventType.CycleSeeded);
    }

    private static GameState CombineStates(params GameState[] states) =>
        new()
        {
            Players = states.SelectMany(state => state.Players).ToList(),
            Cycles = states.SelectMany(state => state.Cycles).ToList(),
            Sectors = states.SelectMany(state => state.Sectors).ToList(),
            Systems = states.SelectMany(state => state.Systems).ToList(),
            Empires = states.SelectMany(state => state.Empires).ToList(),
            Factions = states.SelectMany(state => state.Factions).ToList(),
            MatchParticipants = states.SelectMany(state => state.MatchParticipants).ToList(),
            EmpireResources = states.SelectMany(state => state.EmpireResources).ToList(),
            EmpirePriorities = states.SelectMany(state => state.EmpirePriorities).ToList(),
            EmpireMetrics = states.SelectMany(state => state.EmpireMetrics).ToList(),
            CycleRankings = states.SelectMany(state => state.CycleRankings).ToList(),
            CycleMajorEvents = states.SelectMany(state => state.CycleMajorEvents).ToList(),
            SystemHistoricalSignals = states.SelectMany(state => state.SystemHistoricalSignals).ToList(),
            Admirals = states.SelectMany(state => state.Admirals).ToList(),
            AdmiralBattleHistories = states.SelectMany(state => state.AdmiralBattleHistories).ToList(),
            SystemLinks = states.SelectMany(state => state.SystemLinks).ToList(),
            Fleets = states.SelectMany(state => state.Fleets).ToList(),
            FleetOrders = states.SelectMany(state => state.FleetOrders).ToList(),
            ShipConstructions = states.SelectMany(state => state.ShipConstructions).ToList(),
            ColonialOutposts = states.SelectMany(state => state.ColonialOutposts).ToList(),
            DiplomaticRelationships = states.SelectMany(state => state.DiplomaticRelationships).ToList(),
            TickLogs = states.SelectMany(state => state.TickLogs).ToList(),
            Events = states.SelectMany(state => state.Events).ToList(),
            BattleRecords = states.SelectMany(state => state.BattleRecords).ToList(),
            ChronicleEntries = states.SelectMany(state => state.ChronicleEntries).ToList()
        };

    private static Admiral AssignAdmiral(GameState state, Fleet fleet, string name)
    {
        var admiral = new Admiral
        {
            CycleId = fleet.CycleId,
            EmpireId = fleet.EmpireId,
            AdmiralName = name,
            Status = AdmiralStatus.Active,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };
        state.Admirals.Add(admiral);
        fleet.AdmiralId = admiral.AdmiralId;
        return admiral;
    }

    private static void UpdateFleetStatus(string connectionString, Guid fleetId, string status)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE dbo.Fleets SET Status = @Status WHERE FleetID = @FleetID;";
        command.Parameters.AddWithValue("@Status", status);
        command.Parameters.AddWithValue("@FleetID", fleetId);
        command.ExecuteNonQuery();
    }

    private static void CorruptRowsOutsideNextTickWorkspace(
        string connectionString,
        Guid eventId,
        Guid battleId,
        Guid chronicleEntryId,
        Guid fleetOrderId,
        Guid shipConstructionId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.Events
            SET EventType = N'NotARealEventType'
            WHERE EventID = @EventID;

            UPDATE dbo.BattleRecords
            SET Outcome = N'NotARealBattleOutcome'
            WHERE BattleID = @BattleID;

            UPDATE dbo.ChronicleEntries
            SET EntryType = N'NotARealChronicleEntryType'
            WHERE ChronicleEntryID = @ChronicleEntryID;

            UPDATE dbo.FleetOrders
            SET OrderType = N'NotARealOrderType'
            WHERE FleetOrderID = @FleetOrderID;

            UPDATE dbo.ShipConstructions
            SET Status = N'NotARealConstructionStatus'
            WHERE ShipConstructionID = @ShipConstructionID;
            """;
        command.Parameters.AddWithValue("@EventID", eventId);
        command.Parameters.AddWithValue("@BattleID", battleId);
        command.Parameters.AddWithValue("@ChronicleEntryID", chronicleEntryId);
        command.Parameters.AddWithValue("@FleetOrderID", fleetOrderId);
        command.Parameters.AddWithValue("@ShipConstructionID", shipConstructionId);
        command.ExecuteNonQuery();
    }

    private static HeldSqlApplicationLock HoldSqlApplicationLock(string connectionString, string resourceName)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        var transaction = connection.BeginTransaction();

        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            SELECT @Result;
            """;
        command.Parameters.AddWithValue("@Resource", resourceName);
        var result = Convert.ToInt32(command.ExecuteScalar(), null);
        if (result < 0)
        {
            transaction.Dispose();
            connection.Dispose();
            throw new TimeoutException($"Could not acquire SQL Server application lock '{resourceName}'. Result code: {result}.");
        }

        return new HeldSqlApplicationLock(connection, transaction);
    }

    private static int ReadInt(string connectionString, string sql, Action<SqlCommand> configure)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        configure(command);
        return Convert.ToInt32(command.ExecuteScalar(), null);
    }

    private sealed class HeldSqlApplicationLock : IDisposable
    {
        private readonly SqlConnection _connection;
        private readonly SqlTransaction _transaction;

        public HeldSqlApplicationLock(SqlConnection connection, SqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public void Dispose()
        {
            _transaction.Rollback();
            _transaction.Dispose();
            _connection.Dispose();
        }
    }
}
