using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;
using System.Diagnostics;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerExplicitResolutionAuthorityIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 20, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Admin_revocation_waits_for_an_authorised_explicit_resolution_to_commit()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var state = CreateState();
        var target = Assert.Single(state.Players);
        target.Role = PlayerRole.Admin;
        var actor = new Player
        {
            Username = "authority-race-actor",
            Kind = PlayerKind.Human,
            Role = PlayerRole.Admin,
            Status = PlayerStatus.Active,
            CreatedAt = Now
        };
        state.Players.Add(actor);

        var store = new SqlServerGameStateStore(connectionString, () => new GameState());
        store.Replace(state);
        var request = new ExplicitCycleResolutionRequest(
            CreateContext(state),
            requireAdminister: true);
        var systemId = Assert.Single(state.Systems).SystemId;

        using var tickBlocker = HoldSystemWriteLock(connectionString, systemId);
        using var resolutionStart = new Barrier(2);
        var resolutionTask = Task.Run(() =>
        {
            Assert.True(resolutionStart.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.ResolveExplicit(request, Now);
        });

        Assert.True(resolutionStart.SignalAndWait(TimeSpan.FromSeconds(5)));
        var resolutionSessionId = await WaitForBlockedSession(
            connectionString,
            tickBlocker.SessionId,
            TimeSpan.FromSeconds(5));

        using var revocationStart = new Barrier(2);
        var revocationTask = Task.Run(() =>
        {
            Assert.True(revocationStart.SignalAndWait(TimeSpan.FromSeconds(5)));
            return store.Change(new AdminRoleCommand(
                actor.PlayerId,
                target.PlayerId,
                AdminRoleChangeKind.Revoke,
                "Authority race regression.",
                Now.AddMinutes(1)));
        });

        Assert.True(revocationStart.SignalAndWait(TimeSpan.FromSeconds(5)));
        _ = await WaitForBlockedSession(
            connectionString,
            resolutionSessionId,
            TimeSpan.FromSeconds(5));
        Assert.False(revocationTask.IsCompleted);

        tickBlocker.Release();

        Assert.IsType<CycleResolutionResult.Completed>(await resolutionTask);
        Assert.IsType<AdminRoleCommandResult.Success>(await revocationTask);

        var persisted = store.LoadOrCreate();
        Assert.Equal(PlayerRole.Player, persisted.Players.Single(player => player.PlayerId == target.PlayerId).Role);
        Assert.Equal(1, Assert.Single(persisted.Cycles).CurrentTickNumber);
    }

    private static GameState CreateState()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);

        var game = Assert.Single(state.Games);
        game.Name = "explicit-authority-race";
        game.Purpose = GamePurpose.Standard;
        game.Status = GameLifecycleStatus.Active;
        game.CompletedAt = null;

        var cycle = Assert.Single(state.Cycles);
        cycle.Status = CycleStatus.Active;
        cycle.SchedulingMode = CycleSchedulingMode.Scheduled;
        cycle.NextTickAt = Now;
        foreach (var configuration in state.CycleConfigurations)
        {
            configuration.SchedulingMode = CycleSchedulingMode.Scheduled;
        }

        return state;
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

    private static HeldSystemWriteLock HoldSystemWriteLock(
        string connectionString,
        Guid systemId)
    {
        var connection = new SqlConnection(connectionString);
        connection.Open();
        var transaction = connection.BeginTransaction();
        try
        {
            using var session = connection.CreateCommand();
            session.Transaction = transaction;
            session.CommandText = "SELECT @@SPID;";
            var sessionId = Convert.ToInt32(session.ExecuteScalar(), null);

            using var hold = connection.CreateCommand();
            hold.Transaction = transaction;
            hold.CommandText = "UPDATE dbo.Systems SET SystemName = SystemName WHERE SystemID = @SystemID;";
            hold.Parameters.AddWithValue("@SystemID", systemId);
            Assert.Equal(1, hold.ExecuteNonQuery());
            return new HeldSystemWriteLock(connection, transaction, sessionId);
        }
        catch
        {
            transaction.Dispose();
            connection.Dispose();
            throw;
        }
    }

    private static async Task<int> WaitForBlockedSession(
        string connectionString,
        int blockingSessionId,
        TimeSpan timeout)
    {
        using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        var stopwatch = Stopwatch.StartNew();
        while (stopwatch.Elapsed < timeout)
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT TOP (1) session_id
                FROM sys.dm_exec_requests
                WHERE blocking_session_id = @BlockingSessionID
                  AND database_id = DB_ID()
                ORDER BY session_id;
                """;
            command.Parameters.AddWithValue("@BlockingSessionID", blockingSessionId);
            var result = await command.ExecuteScalarAsync();
            if (result is not null)
            {
                return Convert.ToInt32(result, null);
            }

            await Task.Delay(50);
        }

        throw new TimeoutException(
            $"No SQL request became blocked by session {blockingSessionId} within {timeout}.");
    }

    private sealed class HeldSystemWriteLock(
        SqlConnection connection,
        SqlTransaction transaction,
        int sessionId) : IDisposable
    {
        private bool released;

        public int SessionId { get; } = sessionId;

        public void Release()
        {
            if (released)
            {
                return;
            }

            transaction.Commit();
            released = true;
        }

        public void Dispose()
        {
            if (!released)
            {
                transaction.Rollback();
            }

            transaction.Dispose();
            connection.Dispose();
        }
    }
}
