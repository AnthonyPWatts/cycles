using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerTrainingProvisioningIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Concurrent_requests_create_one_private_training_game_and_return_the_same_attempt()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        using var start = new Barrier(3);
        var tasks = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() =>
            {
                Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
                return store.ProvisionTwinReaches(
                    new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now));
            }))
            .ToArray();

        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
        var results = (await Task.WhenAll(tasks))
            .Select(result => Assert.IsType<TrainingGameProvisioningResult.Success>(result).Value)
            .ToArray();

        Assert.Single(results, result => result.Created);
        Assert.Single(results.Select(result => result.GameId).Distinct());
        Assert.Single(results.Select(result => result.CycleId).Distinct());

        var persisted = store.LoadOrCreate();
        var game = Assert.Single(persisted.Games);
        var cycle = Assert.Single(persisted.Cycles);
        Assert.Equal(results[0].GameId, game.GameId);
        Assert.Equal(results[0].CycleId, cycle.CycleId);
        Assert.Equal(GamePurpose.Training, game.Purpose);
        Assert.Equal(GameVisibility.Private, game.Visibility);
        Assert.Equal(CycleSchedulingMode.SelfPaced, cycle.SchedulingMode);
        Assert.Null(cycle.NextTickAt);
        Assert.Single(persisted.GameEnrolments);
        Assert.Single(persisted.MatchParticipants);
        Assert.Equal(2, persisted.Sectors.Count);
        Assert.Equal(10, persisted.Systems.Count);
        Assert.Equal(13, persisted.SystemLinks.Count);

        var context = ((IGameCommandAccessQuery)store).Get(
            player.PlayerId,
            new GameCycleScope(results[0].GameId, results[0].CycleId));
        Assert.NotNull(context);
        var paused = Journey(store.ChangeStatus(
            new TutorialStatusCommand(
                player.PlayerId,
                results[0].GameId,
                TutorialRunStatus.Paused,
                Now.AddMinutes(1))));
        Assert.False(paused.CanResolve);
        Assert.IsType<CycleResolutionResult.Unavailable>(
            store.ResolveExplicit(
                new ExplicitCycleResolutionRequest(
                    context!,
                    requireAdminister: false,
                    requireActiveTutorialRun: true),
                Now.AddMinutes(2)));
        var resumed = Journey(store.ChangeStatus(
            new TutorialStatusCommand(
                player.PlayerId,
                results[0].GameId,
                TutorialRunStatus.Active,
                Now.AddMinutes(3))));
        Assert.False(resumed.CanResolve);
        Execute(store, context!, state =>
        {
            var fleet = state.Fleets.Single(item => item.FleetName == "Home Guard");
            var destination = state.Systems.Single(item => item.SystemName == "Firstlight");
            return OrderService.SubmitMoveOrder(
                state,
                fleet.FleetId,
                destination.SystemId,
                Now.AddMinutes(4));
        });
        Assert.True(Journey(store.GetJourney(player.PlayerId, results[0].GameId)).CanResolve);
    }

    [Fact]
    public void First_normal_move_resolves_and_is_visible_after_a_new_store_returns_to_the_game()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        var provisioned = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;
        var scope = new GameCycleScope(provisioned.GameId, provisioned.CycleId);
        var context = ((IGameCommandAccessQuery)store).Get(player.PlayerId, scope);
        Assert.NotNull(context);

        var move = Assert.IsType<ScopedQueryResult<MoveFixture>.Success>(
            ((ICycleViewQuery)store).Query(context!, state =>
            {
                var fleet = state.Fleets.Single(item => item.FleetName == "Home Guard");
                var destination = state.Systems.Single(item => item.SystemName == "Firstlight");
                return new MoveFixture(fleet.FleetId, destination.SystemId);
            })).Value;
        var submitted = ((ICycleCommandStore)store).Execute(
            context!,
            state => OrderService.SubmitMoveOrder(
                state,
                move.FleetId,
                move.DestinationSystemId,
                Now.AddMinutes(1)));
        Assert.IsType<ScopedCommandResult<FleetOrder>.Success>(submitted);

        var resolved = store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(context!, requireAdminister: false),
            Now.AddMinutes(2));
        Assert.IsType<CycleResolutionResult.Completed>(resolved);

        var returnedStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var returnedContext = ((IGameCommandAccessQuery)returnedStore).Get(player.PlayerId, scope);
        Assert.NotNull(returnedContext);
        var returned = Assert.IsType<ScopedQueryResult<ReturnedMove>.Success>(
            ((ICycleViewQuery)returnedStore).Query(returnedContext!, state =>
            {
                var cycle = state.Cycles.Single(item => item.CycleId == provisioned.CycleId);
                var fleet = state.Fleets.Single(item => item.FleetId == move.FleetId);
                var order = state.FleetOrders.Single(item => item.FleetId == move.FleetId);
                return new ReturnedMove(
                    cycle.CurrentTickNumber,
                    fleet.CurrentSystemId,
                    order.Status);
            })).Value;

        Assert.Equal(1, returned.TickNumber);
        Assert.Equal(move.DestinationSystemId, returned.CurrentSystemId);
        Assert.Equal(FleetOrderStatus.Processed, returned.OrderStatus);
        Assert.False(
            ((IDueCycleQuery)returnedStore).GetNextDue(Now.AddDays(1))?.Scope.CycleId
                == provisioned.CycleId);
    }

    [Fact]
    public void Legacy_galaxy_upgrade_remains_explicit_when_training_adds_a_second_active_cycle()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var state = GameSeeder.CreateDevelopmentMatch(createdAt: Now.AddDays(-1));
        var player = state.Players.First(item =>
            item.Kind == PlayerKind.Human && item.Status == PlayerStatus.Active);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var training = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;

        var legacyScope = ((ILegacyRuntimeScopeQuery)store).GetRequired();
        var result = store.UpgradeGalaxyTopology(legacyScope.CycleId);

        Assert.False(result.Changed);
        var persisted = store.LoadOrCreate();
        Assert.Equal(2, persisted.Cycles.Count(item => item.Status == CycleStatus.Active));
        Assert.Equal(
            GameSeeder.CanonicalGalaxySystemCount,
            persisted.Systems.Count(item => item.CycleId == legacyScope.CycleId));
        Assert.Equal(10, persisted.Systems.Count(item => item.CycleId == training.CycleId));
    }

    [Fact]
    public void Four_resolution_core_journey_is_derived_from_real_mechanics_and_persists_completion()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        var provisioned = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;
        var context = ((IGameCommandAccessQuery)store).Get(
            player.PlayerId,
            new GameCycleScope(provisioned.GameId, provisioned.CycleId));
        Assert.NotNull(context);

        var initial = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("T0", initial.CurrentLesson?.Key);
        Assert.False(initial.CurrentLesson?.MechanicalEvidence.Satisfied);

        Execute(store, context!, state =>
        {
            var fleet = state.Fleets.Single(item => item.FleetName == "Home Guard");
            var destination = state.Systems.Single(item => item.SystemName == "Firstlight");
            return OrderService.SubmitMoveOrder(
                state,
                fleet.FleetId,
                destination.SystemId,
                Now.AddMinutes(1));
        });
        Assert.True(Journey(store.GetJourney(player.PlayerId, provisioned.GameId)).CanResolve);
        Resolve(store, context!, Now.AddMinutes(2));
        var moveEvidence = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("WaitingForAcknowledgement", moveEvidence.CurrentLesson?.CompletionState);
        var grow = Journey(store.Acknowledge(
            new TutorialAcknowledgementCommand(
                player.PlayerId,
                provisioned.GameId,
                TutorialJourneyEvaluator.MoveOutcomeAcknowledgement,
                Now.AddMinutes(3))));
        Assert.Equal("T1", grow.CurrentLesson?.Key);
        var duplicateAcknowledgement = Journey(store.Acknowledge(
            new TutorialAcknowledgementCommand(
                player.PlayerId,
                provisioned.GameId,
                TutorialJourneyEvaluator.MoveOutcomeAcknowledgement,
                Now.AddMinutes(3).AddSeconds(1))));
        Assert.Equal("T1", duplicateAcknowledgement.CurrentLesson?.Key);

        Execute(store, context!, state =>
        {
            OrderService.UpdatePriorities(
                state,
                context!.EmpireId,
                industryWeight: 0,
                researchWeight: 0,
                militaryWeight: 40,
                expansionWeight: 60,
                Now.AddMinutes(4));
            var surveyWing = state.Fleets.Single(item => item.FleetName == "Survey Wing");
            return OrderService.SubmitColoniseOrder(
                state,
                surveyWing.FleetId,
                Now.AddMinutes(4));
        });
        Assert.True(Journey(store.GetJourney(player.PlayerId, provisioned.GameId)).CanResolve);
        Resolve(store, context!, Now.AddMinutes(5));
        var fight = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("T2", fight.CurrentLesson?.Key);

        Execute(store, context!, state =>
        {
            var vanguard = state.Fleets.Single(item => item.FleetName == "Vanguard");
            var targetFactionId = state.Fleets
                .Where(item => item.CycleId == vanguard.CycleId
                               && item.CurrentSystemId == vanguard.CurrentSystemId
                               && item.FactionId != vanguard.FactionId
                               && item.Status == FleetStatus.Active)
                .Select(item => item.FactionId)
                .Single();
            return OrderService.SubmitAttackOrderAgainstFaction(
                state,
                vanguard.FleetId,
                targetFactionId,
                Now.AddMinutes(6));
        });
        Assert.True(Journey(store.GetJourney(player.PlayerId, provisioned.GameId)).CanResolve);
        Resolve(store, context!, Now.AddMinutes(7));
        var battleEvidence = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("WaitingForAcknowledgement", battleEvidence.CurrentLesson?.CompletionState);
        var choose = Journey(store.Acknowledge(
            new TutorialAcknowledgementCommand(
                player.PlayerId,
                provisioned.GameId,
                TutorialJourneyEvaluator.BattleOutcomeAcknowledgement,
                Now.AddMinutes(8))));
        Assert.Equal("T3", choose.CurrentLesson?.Key);

        Execute(store, context!, state =>
        {
            var fleet = state.Fleets.First(item =>
                item.EmpireId == context!.EmpireId
                && item.Status == FleetStatus.Active
                && item.ShipCount > 0);
            return OrderService.SubmitHoldOrder(state, fleet.FleetId, Now.AddMinutes(9));
        });
        Assert.True(Journey(store.GetJourney(player.PlayerId, provisioned.GameId)).CanResolve);
        Resolve(store, context!, Now.AddMinutes(10));
        var choiceEvidence = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("WaitingForAcknowledgement", choiceEvidence.CurrentLesson?.CompletionState);
        var completed = Journey(store.Acknowledge(
            new TutorialAcknowledgementCommand(
                player.PlayerId,
                provisioned.GameId,
                TutorialJourneyEvaluator.ChoiceOutcomeAcknowledgement,
                Now.AddMinutes(11))));
        Assert.True(completed.CoreCompleted);
        Assert.Equal("Completed", completed.JourneyStatus);

        var returnedStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var returned = Journey(returnedStore.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.True(returned.CoreCompleted);
        Assert.Equal("Completed", returned.Run.Status.ToString());
    }

    [Fact]
    public async Task Fresh_attempt_racing_resolution_is_atomic_and_duplicate_request_returns_one_replacement()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        var provisioned = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;
        var context = ((IGameCommandAccessQuery)store).Get(
            player.PlayerId,
            new GameCycleScope(provisioned.GameId, provisioned.CycleId));
        Assert.NotNull(context);
        Execute(store, context!, state =>
        {
            var fleet = state.Fleets.Single(item => item.FleetName == "Home Guard");
            var destination = state.Systems.Single(item => item.SystemName == "Firstlight");
            return OrderService.SubmitMoveOrder(
                state,
                fleet.FleetId,
                destination.SystemId,
                Now.AddSeconds(30));
        });
        var requestId = Guid.NewGuid();
        using var start = new Barrier(3);
        var resolutionTask = Task.Run(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.ResolveExplicit(
                new ExplicitCycleResolutionRequest(
                    context!,
                    requireAdminister: false,
                    requireActiveTutorialRun: true),
                Now.AddMinutes(1));
        });
        var freshTask = Task.Run(() =>
        {
            Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.StartFresh(
                new FreshTrainingAttemptCommand(
                    player.PlayerId,
                    provisioned.GameId,
                    requestId,
                    Now.AddMinutes(1)));
        });

        Assert.True(start.SignalAndWait(TimeSpan.FromSeconds(5)));
        await Task.WhenAll(resolutionTask, freshTask);
        var resolutionResult = await resolutionTask;
        var freshResult = await freshTask;
        Assert.True(
            resolutionResult is CycleResolutionResult.Completed
                or CycleResolutionResult.Unavailable);
        if (freshResult is TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Busy)
        {
            freshResult = store.StartFresh(
                new FreshTrainingAttemptCommand(
                    player.PlayerId,
                    provisioned.GameId,
                    requestId,
                    Now.AddMinutes(2)));
        }

        var replacement = Assert.IsType<TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Success>(
            freshResult).Value;
        Assert.True(replacement.Created);

        var replay = Assert.IsType<TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Success>(
            store.StartFresh(
                new FreshTrainingAttemptCommand(
                    player.PlayerId,
                    provisioned.GameId,
                    requestId,
                    Now.AddMinutes(3)))).Value;
        Assert.False(replay.Created);
        Assert.Equal(replacement.TutorialRunId, replay.TutorialRunId);
        Assert.Equal(replacement.GameId, replay.GameId);

        var persisted = store.LoadOrCreate();
        Assert.Equal(2, persisted.Games.Count);
        Assert.Equal(GameLifecycleStatus.Terminated, persisted.Games.Single(item => item.GameId == provisioned.GameId).Status);
        Assert.Equal(GameLifecycleStatus.Active, persisted.Games.Single(item => item.GameId == replacement.GameId).Status);
        Assert.Equal(CycleStatus.Completed, persisted.Cycles.Single(item => item.CycleId == provisioned.CycleId).Status);
        Assert.Equal(0, persisted.Cycles.Single(item => item.CycleId == replacement.CycleId).CurrentTickNumber);

        var crossGame = Assert.Throws<SqlException>(() => ExecuteSql(
            connectionString,
            """
            INSERT dbo.TutorialRuns
            (
                TutorialRunID, GameID, CycleID, PlayerID, TutorialKey, DefinitionVersion,
                Status, OriginatingRequestID, SupersededByTutorialRunID,
                StartedAt, StatusChangedAt, EndedAt
            )
            VALUES
            (
                NEWID(), @GameID, @ForeignCycleID, @PlayerID, N'scope-fixture', 1,
                N'Skipped', NULL, NULL, @Now, @Now, @Now
            );
            """,
            ("@GameID", replacement.GameId),
            ("@ForeignCycleID", provisioned.CycleId),
            ("@PlayerID", player.PlayerId),
            ("@Now", Now.AddMinutes(3))));
        Assert.Contains("FK_TutorialRuns_Cycles", crossGame.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Tutorial_migration_backfills_an_existing_training_game_and_is_restartable()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var player = CreatePlayer();
        var store = PrepareStore(connectionString, player);
        var provisioned = Assert.IsType<TrainingGameProvisioningResult.Success>(
            store.ProvisionTwinReaches(
                new TrainingGameProvisioningCommand(player.PlayerId, Guid.NewGuid(), Now))).Value;
        ExecuteSql(
            connectionString,
            """
            DELETE FROM dbo.TutorialRuns WHERE GameID = @GameID;
            DELETE FROM dbo.SchemaMigrations WHERE MigrationID = N'026_add_tutorial_runs';
            """,
            ("@GameID", provisioned.GameId));

        var migrator = new SqlServerMigrator(connectionString);
        Assert.Equal(
            "026_add_tutorial_runs",
            Assert.Single(migrator.MigrateThrough("026_add_tutorial_runs")).MigrationId);
        var backfilled = Journey(store.GetJourney(player.PlayerId, provisioned.GameId));
        Assert.Equal("T0", backfilled.CurrentLesson?.Key);
        var skipped = Journey(store.ChangeStatus(
            new TutorialStatusCommand(
                player.PlayerId,
                provisioned.GameId,
                TutorialRunStatus.Skipped,
                Now.AddMinutes(1))));
        Assert.Equal("Skipped", skipped.JourneyStatus);
        Assert.Equal(
            1,
            ExecuteScalar(
                connectionString,
                "SELECT COUNT(*) FROM dbo.TutorialSkips WHERE PlayerID = @PlayerID;",
                ("@PlayerID", player.PlayerId)));

        ExecuteSql(
            connectionString,
            "DELETE FROM dbo.SchemaMigrations WHERE MigrationID = N'027_add_tutorial_skips';");
        Assert.Equal(
            "027_add_tutorial_skips",
            Assert.Single(migrator.MigrateThrough("027_add_tutorial_skips")).MigrationId);
        Assert.Equal(
            1,
            ExecuteScalar(
                connectionString,
                "SELECT COUNT(*) FROM dbo.TutorialRuns WHERE GameID = @GameID;",
                ("@GameID", provisioned.GameId)));
    }

    private static TutorialJourneySnapshot Journey(
        TutorialAttemptResult<TutorialJourneySnapshot> result) =>
        Assert.IsType<TutorialAttemptResult<TutorialJourneySnapshot>.Success>(result).Value;

    private static void Execute(
        SqlServerGameStateStore store,
        GameCommandContext context,
        Func<GameState, FleetOrder> command) =>
        Assert.IsType<ScopedCommandResult<FleetOrder>.Success>(
            ((ICycleCommandStore)store).Execute(context, command));

    private static void Resolve(
        SqlServerGameStateStore store,
        GameCommandContext context,
        DateTimeOffset at) =>
        Assert.IsType<CycleResolutionResult.Completed>(
            store.ResolveExplicit(
                new ExplicitCycleResolutionRequest(
                    context,
                    requireAdminister: false,
                    requireActiveTutorialRun: true),
                at));

    private static void ExecuteSql(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        command.ExecuteNonQuery();
    }

    private static int ExecuteScalar(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = new SqlCommand(sql, connection);
        foreach (var parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter.Name, parameter.Value);
        }
        return Convert.ToInt32(command.ExecuteScalar(), null);
    }

    private static SqlServerGameStateStore PrepareStore(string connectionString, Player player)
    {
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(new GameState { Players = [player] });
        return store;
    }

    private static Player CreatePlayer() => new()
    {
        PlayerId = Guid.NewGuid(),
        Username = "training-provisioning-player",
        Kind = PlayerKind.Human,
        Role = PlayerRole.Player,
        Status = PlayerStatus.Active,
        CreatedAt = Now.AddDays(-1)
    };

    private sealed record MoveFixture(Guid FleetId, Guid DestinationSystemId);

    private sealed record ReturnedMove(
        int TickNumber,
        Guid CurrentSystemId,
        FleetOrderStatus OrderStatus);
}
