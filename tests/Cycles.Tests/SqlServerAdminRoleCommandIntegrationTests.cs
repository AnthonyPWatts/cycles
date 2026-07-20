using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerAdminRoleCommandIntegrationTests
{
    private static readonly DateTimeOffset ChangedAt = new(2026, 7, 20, 15, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Grant_and_revoke_update_the_guarded_role_and_append_immutable_audits()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var grant = Assert.IsType<AdminRoleCommandResult.Success>(store.Change(new AdminRoleCommand(
            fixture.ActorPlayerId,
            fixture.TargetPlayerId,
            AdminRoleChangeKind.Grant,
            "  On-call operator.  ",
            ChangedAt)));

        Assert.Equal(fixture.ActorPlayerId, grant.Value.ActorPlayerId);
        Assert.Equal(fixture.TargetPlayerId, grant.Value.TargetPlayerId);
        Assert.Equal(PlayerRole.Admin, grant.Value.Role);
        Assert.Equal(AdminRoleAuditAction.Granted, grant.Value.Action);
        Assert.Equal(ChangedAt, grant.Value.ChangedAt);
        Assert.Equal("Admin", ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        var originalGrantAudit = ReadAudit(fixture.ConnectionString, grant.Value.AuditRecordId);
        Assert.Equal(fixture.ActorPlayerId, originalGrantAudit.ActorPlayerId);
        Assert.Equal(fixture.TargetPlayerId, originalGrantAudit.TargetPlayerId);
        Assert.Equal("Granted", originalGrantAudit.Action);
        Assert.Equal("On-call operator.", originalGrantAudit.Reason);
        Assert.Equal("authenticated-admin", originalGrantAudit.Source);
        Assert.Equal("High", originalGrantAudit.Severity);
        Assert.Equal(ChangedAt, originalGrantAudit.CreatedAt);

        var revokeAt = ChangedAt.AddHours(1);
        var revoke = Assert.IsType<AdminRoleCommandResult.Success>(store.Change(new AdminRoleCommand(
            fixture.ActorPlayerId,
            fixture.TargetPlayerId,
            AdminRoleChangeKind.Revoke,
            "Rotation ended.",
            revokeAt)));

        Assert.Equal(PlayerRole.Player, revoke.Value.Role);
        Assert.Equal(AdminRoleAuditAction.Revoked, revoke.Value.Action);
        Assert.Equal("Player", ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(2, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(originalGrantAudit, ReadAudit(fixture.ConnectionString, grant.Value.AuditRecordId));
    }

    [Fact]
    public void Command_reauthorises_an_active_human_admin_and_rejects_an_automated_target()
    {
        var fixture = CreateFixture(targetKind: PlayerKind.AI);
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET Status = N'Suspended' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.ActorPlayerId));

        Assert.IsType<AdminRoleCommandResult.Forbidden>(store.Change(CreateGrant(fixture)));

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET Status = N'Active', PlayerKind = N'AI' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.ActorPlayerId));

        Assert.IsType<AdminRoleCommandResult.Forbidden>(store.Change(CreateGrant(fixture)));

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET PlayerKind = N'Human' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.ActorPlayerId));

        var automatedTarget = Assert.IsType<AdminRoleCommandResult.Conflict>(store.Change(CreateGrant(fixture)));
        Assert.Equal(AdminRoleConflictReason.TargetIsAutomated, automatedTarget.Reason);
        Assert.Equal("Player", ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(0, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
    }

    [Fact]
    public void Command_returns_typed_conflicts_for_stale_or_missing_targets()
    {
        var fixture = CreateFixture(targetRole: PlayerRole.Admin);
        if (fixture is null)
        {
            return;
        }

        var store = CreateStore(fixture.ConnectionString);
        var alreadyAdmin = Assert.IsType<AdminRoleCommandResult.Conflict>(store.Change(CreateGrant(fixture)));
        Assert.Equal(AdminRoleConflictReason.AlreadyAdministrator, alreadyAdmin.Reason);

        Execute(
            fixture.ConnectionString,
            "UPDATE dbo.Players SET Role = N'Player' WHERE PlayerID = @PlayerID;",
            ("@PlayerID", fixture.TargetPlayerId));
        var notAdmin = Assert.IsType<AdminRoleCommandResult.Conflict>(store.Change(new AdminRoleCommand(
            fixture.ActorPlayerId,
            fixture.TargetPlayerId,
            AdminRoleChangeKind.Revoke,
            "No longer required.",
            ChangedAt)));
        Assert.Equal(AdminRoleConflictReason.NotAdministrator, notAdmin.Reason);

        var missing = Assert.IsType<AdminRoleCommandResult.Conflict>(store.Change(new AdminRoleCommand(
            fixture.ActorPlayerId,
            Guid.NewGuid(),
            AdminRoleChangeKind.Grant,
            "On-call operator.",
            ChangedAt)));
        Assert.Equal(AdminRoleConflictReason.TargetUnavailable, missing.Reason);
        Assert.Equal(0, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
    }

    [Fact]
    public void Automated_admin_does_not_bypass_the_final_active_human_admin_guard()
    {
        var fixture = CreateFixture(targetKind: PlayerKind.AI, targetRole: PlayerRole.Admin);
        if (fixture is null)
        {
            return;
        }

        var otherActiveHumanAdministrators = ReadOtherActiveHumanAdministrators(
            fixture.ConnectionString,
            fixture.ActorPlayerId);
        foreach (var playerId in otherActiveHumanAdministrators)
        {
            Execute(
                fixture.ConnectionString,
                "UPDATE dbo.Players SET Status = N'Suspended' WHERE PlayerID = @PlayerID;",
                ("@PlayerID", playerId));
        }

        AdminRoleCommandResult result;
        try
        {
            result = CreateStore(fixture.ConnectionString).Change(new AdminRoleCommand(
                fixture.ActorPlayerId,
                fixture.ActorPlayerId,
                AdminRoleChangeKind.Revoke,
                "No longer required.",
                ChangedAt));
        }
        finally
        {
            foreach (var playerId in otherActiveHumanAdministrators)
            {
                Execute(
                    fixture.ConnectionString,
                    "UPDATE dbo.Players SET Status = N'Active' WHERE PlayerID = @PlayerID;",
                    ("@PlayerID", playerId));
            }
        }

        var conflict = Assert.IsType<AdminRoleCommandResult.Conflict>(result);
        Assert.Equal(AdminRoleConflictReason.FinalActiveAdministrator, conflict.Reason);
        Assert.Equal("Admin", ReadRole(fixture.ConnectionString, fixture.ActorPlayerId));
        Assert.Equal(0, CountAudits(fixture.ConnectionString, fixture.ActorPlayerId));
    }

    [Fact]
    public void Audit_insert_failure_rolls_back_the_guarded_role_update()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        Execute(
            fixture.ConnectionString,
            """
            CREATE OR ALTER TRIGGER dbo.TR_AdminRoleAuditRecords_TestFailure
            ON dbo.AdminRoleAuditRecords
            AFTER INSERT
            AS
            BEGIN
                THROW 51000, 'Forced admin audit failure.', 1;
            END;
            """);
        try
        {
            Assert.Throws<SqlException>(() => CreateStore(fixture.ConnectionString).Change(CreateGrant(fixture)));
        }
        finally
        {
            Execute(
                fixture.ConnectionString,
                "DROP TRIGGER IF EXISTS dbo.TR_AdminRoleAuditRecords_TestFailure;");
        }

        Assert.Equal("Player", ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(0, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
    }

    [Fact]
    public void Command_returns_busy_when_the_account_role_lock_is_held()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        AcquireTransactionApplicationLock(connection, transaction, "Cycles.Account.AdminRole");

        var result = CreateStore(fixture.ConnectionString).Change(CreateGrant(fixture));

        Assert.IsType<AdminRoleCommandResult.Busy>(result);
        Assert.Equal("Player", ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(0, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
        transaction.Rollback();
    }

    [Fact]
    public void Command_returns_busy_without_mutation_when_the_legacy_game_state_lock_is_held()
    {
        var fixture = CreateFixture();
        if (fixture is null)
        {
            return;
        }

        var roleBefore = ReadRole(fixture.ConnectionString, fixture.TargetPlayerId);
        var auditsBefore = CountAudits(fixture.ConnectionString, fixture.TargetPlayerId);
        using var connection = new SqlConnection(fixture.ConnectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        AcquireTransactionApplicationLock(connection, transaction, "Cycles.GameState");

        var result = CreateStore(fixture.ConnectionString).Change(CreateGrant(fixture));

        Assert.IsType<AdminRoleCommandResult.Busy>(result);
        Assert.Equal(roleBefore, ReadRole(fixture.ConnectionString, fixture.TargetPlayerId));
        Assert.Equal(auditsBefore, CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
        transaction.Rollback();
    }

    [Fact]
    public async Task Concurrent_self_revokes_leave_one_active_human_admin_and_one_new_audit()
    {
        var fixture = CreateFixture(targetRole: PlayerRole.Admin);
        if (fixture is null)
        {
            return;
        }

        var otherActiveHumanAdministrators = ReadOtherActiveHumanAdministrators(
                fixture.ConnectionString,
                fixture.ActorPlayerId)
            .Where(playerId => playerId != fixture.TargetPlayerId)
            .ToArray();
        foreach (var playerId in otherActiveHumanAdministrators)
        {
            Execute(
                fixture.ConnectionString,
                "UPDATE dbo.Players SET Status = N'Suspended' WHERE PlayerID = @PlayerID;",
                ("@PlayerID", playerId));
        }

        var auditCountBefore = CountAudits(fixture.ConnectionString, fixture.ActorPlayerId)
            + CountAudits(fixture.ConnectionString, fixture.TargetPlayerId);
        try
        {
            using var start = new Barrier(participantCount: 2);
            var first = Task.Run(() => RevokeSelf(fixture, fixture.ActorPlayerId, start));
            var second = Task.Run(() => RevokeSelf(fixture, fixture.TargetPlayerId, start));

            var results = await Task.WhenAll(first, second);

            Assert.Single(results.OfType<AdminRoleCommandResult.Success>());
            var conflict = Assert.Single(results.OfType<AdminRoleCommandResult.Conflict>());
            Assert.Equal(AdminRoleConflictReason.FinalActiveAdministrator, conflict.Reason);
            Assert.Equal(1, CountActiveHumanAdministrators(fixture.ConnectionString));
            Assert.Equal(
                auditCountBefore + 1,
                CountAudits(fixture.ConnectionString, fixture.ActorPlayerId)
                    + CountAudits(fixture.ConnectionString, fixture.TargetPlayerId));
        }
        finally
        {
            foreach (var playerId in otherActiveHumanAdministrators)
            {
                Execute(
                    fixture.ConnectionString,
                    "UPDATE dbo.Players SET Status = N'Active' WHERE PlayerID = @PlayerID;",
                    ("@PlayerID", playerId));
            }
        }
    }

    private static AdminRoleCommand CreateGrant(AdminRoleFixture fixture) =>
        new(
            fixture.ActorPlayerId,
            fixture.TargetPlayerId,
            AdminRoleChangeKind.Grant,
            "On-call operator.",
            ChangedAt);

    private static AdminRoleCommandResult RevokeSelf(
        AdminRoleFixture fixture,
        Guid playerId,
        Barrier start)
    {
        start.SignalAndWait();
        return CreateStore(fixture.ConnectionString).Change(new AdminRoleCommand(
            playerId,
            playerId,
            AdminRoleChangeKind.Revoke,
            "Concurrent administrator rotation.",
            ChangedAt));
    }

    private static SqlServerGameStateStore CreateStore(string connectionString) =>
        new(connectionString, () => new GameState());

    private static AdminRoleFixture? CreateFixture(
        PlayerKind targetKind = PlayerKind.Human,
        PlayerRole targetRole = PlayerRole.Player)
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlIntegrationGuard.ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return null;
        }

        var actorPlayerId = Guid.NewGuid();
        var targetPlayerId = Guid.NewGuid();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@ActorPlayerID, @ActorUsername, N'', N'', N'', N'', N'Human', N'Admin', @CreatedAt, NULL, N'Active'),
                (@TargetPlayerID, @TargetUsername, N'', N'', N'', N'', @TargetPlayerKind, @TargetPlayerRole, @CreatedAt, NULL, N'Active');
            """;
        command.Parameters.AddWithValue("@ActorPlayerID", actorPlayerId);
        command.Parameters.AddWithValue("@ActorUsername", $"Admin actor {actorPlayerId:D}");
        command.Parameters.AddWithValue("@TargetPlayerID", targetPlayerId);
        command.Parameters.AddWithValue("@TargetUsername", $"Admin target {targetPlayerId:D}");
        command.Parameters.AddWithValue("@TargetPlayerKind", targetKind.ToString());
        command.Parameters.AddWithValue("@TargetPlayerRole", targetRole.ToString());
        command.Parameters.AddWithValue("@CreatedAt", ChangedAt.AddDays(-1));
        command.ExecuteNonQuery();
        return new AdminRoleFixture(connectionString, actorPlayerId, targetPlayerId);
    }

    private static string ReadRole(string connectionString, Guid playerId) =>
        Scalar<string>(
            connectionString,
            "SELECT Role FROM dbo.Players WHERE PlayerID = @PlayerID;",
            ("@PlayerID", playerId));

    private static int CountAudits(string connectionString, Guid targetPlayerId) =>
        Scalar<int>(
            connectionString,
            "SELECT COUNT(*) FROM dbo.AdminRoleAuditRecords WHERE TargetPlayerID = @TargetPlayerID;",
            ("@TargetPlayerID", targetPlayerId));

    private static int CountActiveHumanAdministrators(string connectionString) =>
        Scalar<int>(
            connectionString,
            """
            SELECT COUNT(*)
            FROM dbo.Players
            WHERE PlayerKind = N'Human'
              AND Role = N'Admin'
              AND Status = N'Active';
            """);

    private static AdminRoleAuditRow ReadAudit(string connectionString, Guid auditRecordId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT ActorPlayerID, TargetPlayerID, Action, Reason, Source, Severity, CreatedAt
            FROM dbo.AdminRoleAuditRecords
            WHERE AdminRoleAuditRecordID = @AdminRoleAuditRecordID;
            """;
        command.Parameters.AddWithValue("@AdminRoleAuditRecordID", auditRecordId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        var row = new AdminRoleAuditRow(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetDateTimeOffset(6));
        Assert.False(reader.Read());
        return row;
    }

    private static IReadOnlyList<Guid> ReadOtherActiveHumanAdministrators(
        string connectionString,
        Guid excludedPlayerId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT PlayerID
            FROM dbo.Players
            WHERE PlayerID <> @ExcludedPlayerID
              AND PlayerKind = N'Human'
              AND Role = N'Admin'
              AND Status = N'Active';
            """;
        command.Parameters.AddWithValue("@ExcludedPlayerID", excludedPlayerId);
        using var reader = command.ExecuteReader();
        var playerIds = new List<Guid>();
        while (reader.Read())
        {
            playerIds.Add(reader.GetGuid(0));
        }

        return playerIds;
    }

    private static T Scalar<T>(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }

    private static void Execute(
        string connectionString,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    private static void AcquireTransactionApplicationLock(
        SqlConnection connection,
        SqlTransaction transaction,
        string resource)
    {
        using var acquire = connection.CreateCommand();
        acquire.Transaction = transaction;
        acquire.CommandText = """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = 0;
            SELECT @Result;
            """;
        acquire.Parameters.AddWithValue("@Resource", resource);
        Assert.True(Convert.ToInt32(acquire.ExecuteScalar(), null) >= 0);
    }

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }

    private sealed record AdminRoleFixture(
        string ConnectionString,
        Guid ActorPlayerId,
        Guid TargetPlayerId);

    private sealed record AdminRoleAuditRow(
        Guid ActorPlayerId,
        Guid TargetPlayerId,
        string Action,
        string Reason,
        string Source,
        string Severity,
        DateTimeOffset CreatedAt);
}
