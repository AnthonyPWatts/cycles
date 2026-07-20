using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;
using System.Collections;
using System.Reflection;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleSchedulingIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Due_query_is_deterministic_and_excludes_training_self_paced_and_recovery_cycles()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var expected = CreateState(
            "expected",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now.AddMinutes(-10));
        var invalidConfiguration = CreateState(
            "invalid-configuration",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now.AddMinutes(-30));
        var later = CreateState(
            "later",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now.AddMinutes(-5));
        var training = CreateState(
            "training",
            GamePurpose.Training,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now.AddMinutes(-30));
        var selfPaced = CreateState(
            "self-paced",
            GamePurpose.Training,
            CycleSchedulingMode.SelfPaced,
            CycleStatus.Active,
            nextTickAt: null);
        var recovery = CreateState(
            "recovery",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.RecoveryRequired,
            nextTickAt: null);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(Combine(invalidConfiguration, expected, later, training, selfPaced, recovery));
        CorruptConfigurationToDraft(
            connectionString,
            Assert.Single(invalidConfiguration.CycleConfigurations).CycleConfigurationId);

        var first = Assert.IsType<DueCycleWorkItem>(store.GetNextDue(Now));

        Assert.Equal(Assert.Single(expected.Games).GameId, first.Scope.GameId);
        Assert.Equal(Assert.Single(expected.Cycles).CycleId, first.Scope.CycleId);
        Assert.Equal(Now.AddMinutes(-10), first.NextTickAt);

        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE dbo.Cycles SET NextTickAt = @NextTickAt WHERE CycleID = @CycleID;";
            command.Parameters.AddWithValue("@NextTickAt", Now.AddHours(1));
            command.Parameters.AddWithValue("@CycleID", first.Scope.CycleId);
            Assert.Equal(1, command.ExecuteNonQuery());
        }

        var second = Assert.IsType<DueCycleWorkItem>(store.GetNextDue(Now));
        Assert.Equal(Assert.Single(later.Cycles).CycleId, second.Scope.CycleId);
    }

    [Fact]
    public void Configuration_updates_cannot_break_materialized_cycle_scheduling_agreement()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "configuration-agreement",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var configuration = Assert.Single(state.CycleConfigurations);
        CorruptConfigurationToDraft(connectionString, configuration.CycleConfigurationId);

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.CycleConfigurations
            SET Status = N'Materialized',
                SchedulingMode = N'SelfPaced',
                LockedAt = @Now,
                MaterializedAt = @Now
            WHERE CycleConfigurationID = @CycleConfigurationID;
            """;
        command.Parameters.AddWithValue("@CycleConfigurationID", configuration.CycleConfigurationId);
        command.Parameters.AddWithValue("@Now", Now);

        var exception = Assert.Throws<SqlException>(() => command.ExecuteNonQuery());

        Assert.Contains(
            "does not match its materialized configuration",
            exception.Message,
            StringComparison.Ordinal);
        command.CommandText = """
            UPDATE dbo.CycleConfigurations
            SET Status = N'Materialized',
                SchedulingMode = N'Scheduled',
                LockedAt = @Now,
                MaterializedAt = @Now
            WHERE CycleConfigurationID = @CycleConfigurationID;
            """;
        Assert.Equal(1, command.ExecuteNonQuery());
        Assert.Equal(
            CycleSchedulingMode.Scheduled,
            Assert.Single(store.LoadOrCreate().CycleConfigurations).SchedulingMode);
    }

    [Fact]
    public async Task Concurrent_workers_resolve_one_due_candidate_exactly_once()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "concurrent",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var firstStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var secondStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        firstStore.Replace(state);
        var candidate = Assert.IsType<DueCycleWorkItem>(firstStore.GetNextDue(Now));
        using var barrier = new Barrier(2);

        var first = Resolve(firstStore);
        var second = Resolve(secondStore);
        var outcomes = await Task.WhenAll(first, second);

        Assert.Single(outcomes, outcome => outcome is CycleResolutionResult.Completed);
        Assert.Single(outcomes, outcome => outcome is CycleResolutionResult.Stale);
        var persisted = firstStore.LoadOrCreate();
        var cycle = Assert.Single(persisted.Cycles);
        Assert.Equal(1, cycle.CurrentTickNumber);
        Assert.Equal(Now.AddMinutes(cycle.TickLengthMinutes), cycle.NextTickAt);
        Assert.Single(persisted.TickLogs, log =>
            log.CycleId == cycle.CycleId && log.Status == TickLogStatus.Completed);

        Task<CycleResolutionResult> Resolve(SqlServerGameStateStore store) => Task.Run(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.ResolveIfDue(candidate, Now);
        });
    }

    [Fact]
    public void Player_authorised_explicit_resolution_supports_self_paced_training_without_entering_the_due_queue()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "training",
            GamePurpose.Training,
            CycleSchedulingMode.SelfPaced,
            CycleStatus.Active,
            nextTickAt: null);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var game = Assert.Single(state.Games);
        var cycle = Assert.Single(state.Cycles);

        Assert.Null(store.GetNextDue(Now.AddDays(1)));
        var result = store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(
                CreateContext(state),
                ExplicitCycleResolutionPolicy.SelfPacedParticipant,
                expectedCurrentTickNumber: 0),
            Now);

        Assert.IsType<CycleResolutionResult.Completed>(result);
        var persisted = Assert.Single(store.LoadOrCreate().Cycles);
        Assert.Equal(1, persisted.CurrentTickNumber);
        Assert.Null(persisted.NextTickAt);
    }

    [Fact]
    public async Task Self_paced_resolution_uses_the_observed_tick_as_a_concurrency_token()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "self-paced-concurrency",
            GamePurpose.Training,
            CycleSchedulingMode.SelfPaced,
            CycleStatus.Active,
            nextTickAt: null);
        var firstStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var secondStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        firstStore.Replace(state);
        var request = new ExplicitCycleResolutionRequest(
            CreateContext(state),
            ExplicitCycleResolutionPolicy.SelfPacedParticipant,
            expectedCurrentTickNumber: 0);
        using var barrier = new Barrier(2);

        var first = Resolve(firstStore);
        var second = Resolve(secondStore);
        var outcomes = await Task.WhenAll(first, second);

        Assert.Single(outcomes, outcome => outcome is CycleResolutionResult.Completed);
        Assert.Single(outcomes, outcome => outcome is CycleResolutionResult.Stale);
        var persisted = firstStore.LoadOrCreate();
        Assert.Equal(1, Assert.Single(persisted.Cycles).CurrentTickNumber);
        Assert.Single(persisted.TickLogs, item => item.Status == TickLogStatus.Completed);

        Task<CycleResolutionResult> Resolve(SqlServerGameStateStore store) => Task.Run(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.ResolveExplicit(request, Now);
        });
    }

    [Fact]
    public void Explicit_resolution_policies_reject_the_wrong_game_pacing()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var scheduled = CreateState(
            "scheduled-policy",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(scheduled);

        Assert.IsType<CycleResolutionResult.Unavailable>(store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(
                CreateContext(scheduled),
                ExplicitCycleResolutionPolicy.SelfPacedParticipant,
                expectedCurrentTickNumber: 0),
            Now));

        var training = CreateState(
            "training-policy",
            GamePurpose.Training,
            CycleSchedulingMode.SelfPaced,
            CycleStatus.Active,
            nextTickAt: null);
        store.Replace(training);

        Assert.IsType<CycleResolutionResult.Unavailable>(store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(
                CreateContext(training),
                ExplicitCycleResolutionPolicy.DevelopmentStandard,
                expectedCurrentTickNumber: 0),
            Now));
        Assert.Equal(0, Assert.Single(store.LoadOrCreate().Cycles).CurrentTickNumber);
    }

    [Fact]
    public void Due_resolution_failure_enters_recovery_and_clears_the_schedule()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "failure",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        state.EmpireResources.Clear();
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var candidate = Assert.IsType<DueCycleWorkItem>(store.GetNextDue(Now));

        var result = store.ResolveIfDue(candidate, Now);

        Assert.IsType<CycleResolutionResult.RecoveryRequired>(result);
        var persisted = Assert.Single(store.LoadOrCreate().Cycles);
        Assert.Equal(CycleStatus.RecoveryRequired, persisted.Status);
        Assert.Null(persisted.NextTickAt);
    }

    [Fact]
    public void Explicit_resolution_rejects_a_mismatched_game_cycle_tuple()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "tuple",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var cycle = Assert.Single(state.Cycles);

        var actualContext = CreateContext(state);
        var mismatchedContext = new GameCommandContext(
            new GameAccessContext(
                actualContext.GameAccess.PlayerId,
                Guid.NewGuid(),
                actualContext.GameAccess.GameEnrolmentId,
                actualContext.GameAccess.Permissions),
            actualContext.CycleId,
            actualContext.MatchParticipantId,
            actualContext.EmpireId);
        var result = store.ResolveExplicit(
            new ExplicitCycleResolutionRequest(mismatchedContext, requireAdminister: false),
            Now);

        Assert.IsType<CycleResolutionResult.Unavailable>(result);
        Assert.Equal(0, Assert.Single(store.LoadOrCreate().Cycles).CurrentTickNumber);
    }

    [Fact]
    public void Explicit_resolution_maps_game_lock_timeout_to_busy_without_running_a_tick()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "busy",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var game = Assert.Single(state.Games);
        var cycle = Assert.Single(state.Cycles);
        var gameLockName = $"Cycles.Game.{game.GameId:D}";

        CycleResolutionResult result;
        using (HoldSqlApplicationLock(connectionString, gameLockName))
        {
            result = store.ResolveExplicit(
                new ExplicitCycleResolutionRequest(CreateContext(state), requireAdminister: false),
                Now);
        }

        Assert.IsType<CycleResolutionResult.Busy>(result);
        var persisted = store.LoadOrCreate();
        Assert.Equal(0, Assert.Single(persisted.Cycles).CurrentTickNumber);
        Assert.Empty(persisted.TickLogs);
    }

    [Fact]
    public void Explicit_resolution_revalidates_required_admin_authority()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "admin-authority",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);

        var request = new ExplicitCycleResolutionRequest(CreateContext(state), requireAdminister: true);
        var result = store.ResolveExplicit(
            request,
            Now);

        Assert.IsType<CycleResolutionResult.Forbidden>(result);
        Assert.Equal(0, Assert.Single(store.LoadOrCreate().Cycles).CurrentTickNumber);

        store.Update(current =>
        {
            Assert.Single(current.Players).Role = PlayerRole.Admin;
            return 0;
        });

        var authorised = store.ResolveExplicit(request, Now);

        Assert.IsType<CycleResolutionResult.Completed>(authorised);
        Assert.Equal(1, Assert.Single(store.LoadOrCreate().Cycles).CurrentTickNumber);
    }

    [Fact]
    public async Task Cycle_completion_and_scheduled_resolution_share_one_lock_domain()
    {
        var connectionString = ConnectionString();
        if (connectionString is null)
        {
            return;
        }

        var state = CreateState(
            "completion-race",
            GamePurpose.Standard,
            CycleSchedulingMode.Scheduled,
            CycleStatus.Active,
            Now);
        var resolutionStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        var operatorStore = new SqlServerGameStateStore(connectionString, () => new GameState());
        resolutionStore.Replace(state);
        var candidate = Assert.IsType<DueCycleWorkItem>(resolutionStore.GetNextDue(Now));
        using var barrier = new Barrier(2);

        var resolutionTask = Task.Run(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            return resolutionStore.ResolveIfDue(candidate, Now);
        });
        var completionTask = Task.Run(() =>
        {
            Assert.True(barrier.SignalAndWait(TimeSpan.FromSeconds(5)));
            return operatorStore.CompleteCycle(candidate.Scope.CycleId, Now.AddMinutes(1));
        });

        var resolution = await resolutionTask;
        _ = await completionTask;
        var persisted = resolutionStore.LoadOrCreate();
        var cycle = Assert.Single(persisted.Cycles);

        Assert.Equal(CycleStatus.Completed, cycle.Status);
        Assert.Null(cycle.NextTickAt);
        Assert.True(resolution is CycleResolutionResult.Completed or CycleResolutionResult.Unavailable);
        if (resolution is CycleResolutionResult.Completed)
        {
            Assert.Equal(1, cycle.CurrentTickNumber);
            Assert.Single(persisted.TickLogs, item =>
                item.CycleId == cycle.CycleId && item.Status == TickLogStatus.Completed);
        }
        else
        {
            Assert.Equal(0, cycle.CurrentTickNumber);
            Assert.Empty(persisted.TickLogs);
        }
    }

    private static string? ConnectionString() =>
        Environment.GetEnvironmentVariable(SqlIntegrationGuard.ConnectionStringEnvironmentVariable);

    private static GameState CreateState(
        string name,
        GamePurpose purpose,
        CycleSchedulingMode schedulingMode,
        CycleStatus status,
        DateTimeOffset? nextTickAt)
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = Assert.Single(state.Cycles);
        cycle.Name = name;
        cycle.Status = status;
        LegacyGameFoundation.Apply(state);

        var gameId = Guid.NewGuid();
        var game = Assert.Single(state.Games);
        game.GameId = gameId;
        game.Name = name;
        game.Purpose = purpose;
        game.Status = GameLifecycleStatus.Active;
        game.CompletedAt = null;
        foreach (var configuration in state.CycleConfigurations)
        {
            configuration.GameId = gameId;
            configuration.SchedulingMode = schedulingMode;
        }

        cycle.GameId = gameId;
        cycle.SchedulingMode = schedulingMode;
        cycle.NextTickAt = nextTickAt;
        foreach (var enrolment in state.GameEnrolments)
        {
            enrolment.GameId = gameId;
        }

        foreach (var participant in state.MatchParticipants)
        {
            participant.GameId = gameId;
        }

        foreach (var gameEvent in state.GameLifecycleEvents)
        {
            gameEvent.GameLifecycleEventId = Guid.NewGuid();
            gameEvent.GameId = gameId;
        }

        return state;
    }

    private static GameState Combine(params GameState[] states)
    {
        var combined = new GameState();
        foreach (var property in typeof(GameState)
                     .GetProperties(BindingFlags.Instance | BindingFlags.Public)
                     .Where(property => property.PropertyType.IsGenericType
                                        && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>)))
        {
            var target = Assert.IsAssignableFrom<IList>(property.GetValue(combined));
            foreach (var state in states)
            {
                var source = Assert.IsAssignableFrom<IList>(property.GetValue(state));
                foreach (var item in source)
                {
                    target.Add(item);
                }
            }
        }

        return combined;
    }

    private static GameCommandContext CreateContext(GameState state)
    {
        var game = Assert.Single(state.Games);
        var cycle = Assert.Single(state.Cycles);
        var participant = Assert.Single(state.MatchParticipants);
        var enrolment = Assert.Single(state.GameEnrolments);
        return new GameCommandContext(
            new GameAccessContext(
                participant.PlayerId,
                game.GameId,
                enrolment.GameEnrolmentId,
                GamePermission.Read),
            cycle.CycleId,
            participant.MatchParticipantId,
            participant.EmpireId);
    }

    private static HeldSqlApplicationLock HoldSqlApplicationLock(
        string connectionString,
        string resourceName)
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
            throw new TimeoutException(
                $"Could not acquire SQL Server application lock '{resourceName}'. Result code: {result}.");
        }

        return new HeldSqlApplicationLock(connection, transaction);
    }

    private static void CorruptConfigurationToDraft(
        string connectionString,
        Guid configurationId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using (var disable = connection.CreateCommand())
        {
            disable.CommandText = "DISABLE TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance ON dbo.CycleConfigurations;";
            disable.ExecuteNonQuery();
        }

        try
        {
            using var update = connection.CreateCommand();
            update.CommandText = """
                UPDATE dbo.CycleConfigurations
                SET Status = N'Draft',
                    LockedAt = NULL,
                    MaterializedAt = NULL
                WHERE CycleConfigurationID = @CycleConfigurationID;
                """;
            update.Parameters.AddWithValue("@CycleConfigurationID", configurationId);
            Assert.Equal(1, update.ExecuteNonQuery());
        }
        finally
        {
            using var enable = connection.CreateCommand();
            enable.CommandText = "ENABLE TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance ON dbo.CycleConfigurations;";
            enable.ExecuteNonQuery();
        }
    }

    private sealed class HeldSqlApplicationLock : IDisposable
    {
        private readonly SqlConnection connection;
        private readonly SqlTransaction transaction;

        public HeldSqlApplicationLock(
            SqlConnection connection,
            SqlTransaction transaction)
        {
            this.connection = connection;
            this.transaction = transaction;
        }

        public void Dispose()
        {
            transaction.Rollback();
            transaction.Dispose();
            connection.Dispose();
        }
    }
}
