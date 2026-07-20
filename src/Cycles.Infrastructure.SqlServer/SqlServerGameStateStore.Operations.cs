using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    public IReadOnlyList<CycleRanking> CompleteCycle(
        Guid cycleId,
        DateTimeOffset cutoffAt)
    {
        var scope = GetRequiredCycleScope(cycleId);
        return ExecuteOperatorCycleMutation(
            scope,
            state => CycleEndService.CompleteCycle(state, cycleId, cutoffAt),
            SaveCycleCompletionUnsafe);
    }

    public EventRecord ClearRecovery(
        Guid cycleId,
        string operatorName,
        string reason,
        DateTimeOffset now)
    {
        var scope = GetRequiredCycleScope(cycleId);
        return ExecuteOperatorCycleMutation(
            scope,
            state => RecoveryService.ClearRecovery(state, cycleId, operatorName, reason, now),
            SaveRecoveryStateUnsafe);
    }

    public TickResult RetryRecovery(
        Guid cycleId,
        string operatorName,
        string reason,
        DateTimeOffset now)
    {
        var scope = GetRequiredCycleScope(cycleId);
        return ExecuteOperatorCycleMutation(
            scope,
            state =>
            {
                RecoveryService.ClearRecovery(state, cycleId, operatorName, reason, now);
                return new TickEngine().RunTick(state, cycleId, now);
            },
            (connection, transaction, state, mutationScope) =>
                SaveTickOutcomeUnsafe(connection, transaction, state, mutationScope.CycleId));
    }

    public EventRecord MarkTickAbandoned(
        Guid tickLogId,
        string operatorName,
        string reason,
        DateTimeOffset now,
        TimeSpan suspicionThreshold)
    {
        var scope = GetRequiredTickScope(tickLogId);
        return ExecuteOperatorCycleMutation(
            scope,
            state => RecoveryService.MarkTickAbandoned(
                state,
                tickLogId,
                operatorName,
                reason,
                now,
                suspicionThreshold),
            SaveRecoveryStateUnsafe);
    }

    private T ExecuteOperatorCycleMutation<T>(
        GameCycleScope scope,
        Func<GameState, T> mutation,
        Action<SqlConnection, SqlTransaction, GameState, GameCycleScope> save)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        AcquireGameResolutionLock(connection, transaction, scope.GameId);
        AcquireCycleTickLock(connection, transaction, scope.CycleId);

        var state = LoadFocusedOperatorCycleStateUnsafe(connection, transaction, scope);
        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == scope.CycleId);
        if (cycle?.GameId != scope.GameId || state.Games.SingleOrDefault()?.GameId != scope.GameId)
        {
            throw new InvalidOperationException("The requested Game and Cycle scope is no longer available.");
        }

        var value = mutation(state);
        save(connection, transaction, state, scope);
        transaction.Commit();
        return value;
    }

    private GameCycleScope GetRequiredCycleScope(Guid cycleId)
    {
        if (cycleId == Guid.Empty)
        {
            throw new ArgumentException("Cycle identifier cannot be empty.", nameof(cycleId));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT GameID FROM dbo.Cycles WHERE CycleID = @CycleID AND GameID IS NOT NULL;";
        AddGuid(command, "@CycleID", cycleId);
        var gameId = command.ExecuteScalar() as Guid?;
        return gameId.HasValue
            ? new GameCycleScope(gameId.Value, cycleId)
            : throw new InvalidOperationException("Cycle was not found or has no Game scope.");
    }

    private GameCycleScope GetRequiredTickScope(Guid tickLogId)
    {
        if (tickLogId == Guid.Empty)
        {
            throw new ArgumentException("Tick log identifier cannot be empty.", nameof(tickLogId));
        }

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT cycle.GameID, cycle.CycleID
            FROM dbo.TickLogs AS log
            INNER JOIN dbo.Cycles AS cycle ON cycle.CycleID = log.CycleID
            WHERE log.TickLogID = @TickLogID
              AND cycle.GameID IS NOT NULL;
            """;
        AddGuid(command, "@TickLogID", tickLogId);
        using var reader = command.ExecuteReader();
        return reader.Read()
            ? new GameCycleScope(GetGuid(reader, "GameID"), GetGuid(reader, "CycleID"))
            : throw new InvalidOperationException("Tick attempt was not found or has no Game scope.");
    }

    private static GameState LoadFocusedOperatorCycleStateUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCycleScope scope)
    {
        var loadContext = ReadOperatorLoadContext(connection, transaction, scope);
        var state = LoadFocusedViewStateUnsafe(connection, transaction, loadContext);
        state.Cycles = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.Cycles WHERE GameID = @GameID",
            command => AddGuid(command, "@GameID", scope.GameId),
            ReadCycle);
        state.MatchParticipants = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.MatchParticipants WHERE GameID = @GameID",
            command => AddGuid(command, "@GameID", scope.GameId),
            ReadMatchParticipant);
        state.GameEnrolments = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.GameEnrolments WHERE GameID = @GameID",
            command => AddGuid(command, "@GameID", scope.GameId),
            ReadGameEnrolment);
        state.GameLifecycleEvents = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.GameLifecycleEvents WHERE GameID = @GameID",
            command => AddGuid(command, "@GameID", scope.GameId),
            ReadGameLifecycleEvent);
        return state;
    }

    private static GameCommandContext ReadOperatorLoadContext(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCycleScope scope)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT TOP (1)
                player.PlayerID,
                enrolment.GameEnrolmentID,
                participant.MatchParticipantID,
                participant.EmpireID
            FROM dbo.Cycles AS cycle
            INNER JOIN dbo.Games AS game ON game.GameID = cycle.GameID
            INNER JOIN dbo.MatchParticipants AS participant
                ON participant.GameID = game.GameID
               AND participant.CycleID = cycle.CycleID
            INNER JOIN dbo.Players AS player ON player.PlayerID = participant.PlayerID
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.GameID = game.GameID
               AND enrolment.PlayerID = player.PlayerID
            WHERE game.GameID = @GameID
              AND cycle.CycleID = @CycleID
            ORDER BY participant.MatchParticipantID;
            """);
        AddGuid(command, "@GameID", scope.GameId);
        AddGuid(command, "@CycleID", scope.CycleId);
        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("The requested Game and Cycle have no operator-loadable participant scope.");
        }

        return new GameCommandContext(
            new GameAccessContext(
                GetGuid(reader, "PlayerID"),
                scope.GameId,
                GetGuid(reader, "GameEnrolmentID"),
                GamePermission.Read),
            scope.CycleId,
            GetGuid(reader, "MatchParticipantID"),
            GetGuid(reader, "EmpireID"));
    }

    private static void SaveRecoveryStateUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        GameState state,
        GameCycleScope scope)
    {
        UpsertCycle(connection, transaction, state.Cycles.Single(item => item.CycleId == scope.CycleId));
        foreach (var tickLog in state.TickLogs.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertTickLog(connection, transaction, tickLog);
        }

        foreach (var eventRecord in state.Events.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertEvent(connection, transaction, eventRecord);
        }
    }

    private static void SaveCycleCompletionUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        GameState state,
        GameCycleScope scope)
    {
        UpsertCycle(connection, transaction, state.Cycles.Single(item => item.CycleId == scope.CycleId));
        foreach (var participant in state.MatchParticipants.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertMatchParticipant(connection, transaction, participant);
        }

        foreach (var system in state.Systems.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertSystem(connection, transaction, system);
        }

        Execute(
            connection,
            transaction,
            "DELETE FROM dbo.CycleRankings WHERE CycleID = @CycleID;",
            command => AddGuid(command, "@CycleID", scope.CycleId));
        foreach (var ranking in state.CycleRankings.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertCycleRanking(connection, transaction, ranking);
        }

        Execute(
            connection,
            transaction,
            "DELETE FROM dbo.SystemHistoricalSignals WHERE CycleID = @CycleID;",
            command => AddGuid(command, "@CycleID", scope.CycleId));
        foreach (var signal in state.SystemHistoricalSignals.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertSystemHistoricalSignal(connection, transaction, signal);
        }

        Execute(
            connection,
            transaction,
            "DELETE FROM dbo.CycleMajorEvents WHERE CycleID = @CycleID;",
            command => AddGuid(command, "@CycleID", scope.CycleId));
        foreach (var majorEvent in state.CycleMajorEvents.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertCycleMajorEvent(connection, transaction, majorEvent);
        }

        foreach (var eventRecord in state.Events.Where(item => item.CycleId == scope.CycleId))
        {
            UpsertEvent(connection, transaction, eventRecord);
        }

        foreach (var enrolment in state.GameEnrolments.Where(item => item.GameId == scope.GameId))
        {
            UpsertGameEnrolment(connection, transaction, enrolment);
        }

        foreach (var lifecycleEvent in state.GameLifecycleEvents.Where(item => item.GameId == scope.GameId))
        {
            UpsertGameLifecycleEvent(connection, transaction, lifecycleEvent);
        }

        UpsertGame(connection, transaction, state.Games.Single(item => item.GameId == scope.GameId));
    }
}
