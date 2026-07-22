using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;
using System.Text.Json;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerAccountCommandIntegrationTests
{
    private const string LegacyGlobalStateLockName = "Cycles.GameState";
    private static readonly DateTimeOffset SignInAt =
        new(2026, 7, 20, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Verified_invitation_binds_an_existing_human_account_without_creating_a_player()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://identity-{marker}.example";
        var subject = $"subject-{marker}";
        var secretEmail = $"private-{marker}@example.test";
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        var gameplayBefore = ReadGameplayFingerprint(connectionString);
        var store = CreateStore(connectionString);

        var result = store.SignInExternal(new ExternalPlayerSignInCommand(
            issuer,
            subject,
            new ExternalPlayerBinding(target.PlayerId, $"  {secretEmail}  ", bootstrap: null),
            SignInAt));

        var success = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(result);
        Assert.True(success.Value.Bound);
        Assert.Null(success.Value.BootstrapAuditRecordId);
        Assert.Equal(target.PlayerId, success.Value.Player.PlayerId);
        Assert.Equal(PlayerKind.Human, success.Value.Player.Kind);
        Assert.Equal(PlayerRole.Player, success.Value.Player.Role);
        Assert.Equal(PlayerStatus.Active, success.Value.Player.Status);
        Assert.Equal(SignInAt.AddDays(-10), success.Value.Player.CreatedAt);
        Assert.Equal(SignInAt, success.Value.Player.LastLoginAt);
        var json = JsonSerializer.Serialize(success.Value);
        Assert.DoesNotContain(secretEmail, json, StringComparison.Ordinal);
        Assert.DoesNotContain(issuer, json, StringComparison.Ordinal);
        Assert.DoesNotContain(subject, json, StringComparison.Ordinal);

        var stored = ReadPlayer(connectionString, success.Value.Player.PlayerId);
        Assert.Equal(target.Username, stored.Username);
        Assert.Equal(secretEmail, stored.Email);
        Assert.Equal("", stored.PasswordHash);
        Assert.Equal(issuer, stored.ExternalIssuer);
        Assert.Equal(subject, stored.ExternalSubject);
        Assert.Equal(PlayerKind.Human.ToString(), stored.PlayerKind);
        Assert.Equal(PlayerRole.Player.ToString(), stored.Role);
        Assert.Equal(PlayerStatus.Active.ToString(), stored.Status);
        Assert.Equal(gameplayBefore, ReadGameplayFingerprint(connectionString));
    }

    [Fact]
    public void Uninvited_identity_and_already_bound_target_are_rejected_without_creating_players()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = CreateStore(connectionString);
        var alreadyBound = InsertMappedPlayer(
            connectionString,
            PlayerKind.Human,
            PlayerStatus.Active,
            PlayerRole.Player,
            SignInAt);
        var playerCountBefore = Scalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Players;");

        var uninvited = store.SignInExternal(new ExternalPlayerSignInCommand(
            "https://uninvited.example",
            Guid.NewGuid().ToString("N"),
            binding: null,
            SignInAt.AddHours(1)));
        var conflictingBinding = store.SignInExternal(new ExternalPlayerSignInCommand(
            "https://different.example",
            Guid.NewGuid().ToString("N"),
            new ExternalPlayerBinding(
                alreadyBound.PlayerId,
                "different@example.test",
                bootstrap: null),
            SignInAt.AddHours(1)));

        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable>(uninvited);
        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable>(conflictingBinding);
        Assert.Equal(playerCountBefore, Scalar<int>(connectionString, "SELECT COUNT(*) FROM dbo.Players;"));
        var stored = ReadPlayer(connectionString, alreadyBound.PlayerId);
        Assert.Equal(alreadyBound.Issuer, stored.ExternalIssuer);
        Assert.Equal(alreadyBound.Subject, stored.ExternalSubject);
        Assert.Equal(PlayerRole.Player.ToString(), stored.Role);
        Assert.Equal(SignInAt, stored.LastLoginAt);
    }

    [Fact]
    public void Repeat_external_sign_in_preserves_profile_and_advances_login_monotonically()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://repeat-{marker}.example";
        var subject = $"repeat-{marker}";
        var store = CreateStore(connectionString);
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        var first = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(
            store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                new ExternalPlayerBinding(target.PlayerId, "original@example.test", bootstrap: null),
                SignInAt)));

        var older = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(
            store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                binding: null,
                SignInAt.AddHours(-1))));
        var newer = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(
            store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                binding: null,
                SignInAt.AddHours(1))));

        Assert.True(first.Value.Bound);
        Assert.False(older.Value.Bound);
        Assert.False(newer.Value.Bound);
        Assert.Equal(first.Value.Player.PlayerId, older.Value.Player.PlayerId);
        Assert.Equal(first.Value.Player.PlayerId, newer.Value.Player.PlayerId);
        Assert.Equal(SignInAt, older.Value.Player.LastLoginAt);
        Assert.Equal(SignInAt.AddHours(1), newer.Value.Player.LastLoginAt);
        var stored = ReadPlayer(connectionString, first.Value.Player.PlayerId);
        Assert.Equal(target.Username, stored.Username);
        Assert.Equal("original@example.test", stored.Email);
        Assert.Equal(
            1,
            Scalar<int>(
                connectionString,
                """
                SELECT COUNT(*)
                FROM dbo.Players
                WHERE ExternalIssuer COLLATE Latin1_General_100_BIN2 = @Issuer COLLATE Latin1_General_100_BIN2
                  AND ExternalSubject COLLATE Latin1_General_100_BIN2 = @Subject COLLATE Latin1_General_100_BIN2;
                """,
                ("@Issuer", issuer),
                ("@Subject", subject)));
    }

    [Fact]
    public void External_sign_in_rejects_suspended_and_automated_mappings_without_mutation()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = CreateStore(connectionString);
        var aiIdentity = InsertMappedPlayer(
            connectionString,
            PlayerKind.AI,
            PlayerStatus.Active,
            PlayerRole.Player,
            SignInAt.AddDays(-2));
        var suspendedIdentity = InsertMappedPlayer(
            connectionString,
            PlayerKind.Human,
            PlayerStatus.Suspended,
            PlayerRole.Player,
            SignInAt.AddDays(-2));

        var aiResult = store.SignInExternal(new ExternalPlayerSignInCommand(
            aiIdentity.Issuer,
            aiIdentity.Subject,
            new ExternalPlayerBinding(
                aiIdentity.PlayerId,
                "ai@example.test",
                new ConfiguredAdminBootstrap("configuration:must-not-apply")),
            SignInAt));
        var suspendedResult = store.SignInExternal(new ExternalPlayerSignInCommand(
            suspendedIdentity.Issuer,
            suspendedIdentity.Subject,
            new ExternalPlayerBinding(
                suspendedIdentity.PlayerId,
                "suspended@example.test",
                new ConfiguredAdminBootstrap("configuration:must-not-apply")),
            SignInAt));

        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable>(aiResult);
        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable>(suspendedResult);
        Assert.Equal(SignInAt.AddDays(-2), ReadPlayer(connectionString, aiIdentity.PlayerId).LastLoginAt);
        Assert.Equal(SignInAt.AddDays(-2), ReadPlayer(connectionString, suspendedIdentity.PlayerId).LastLoginAt);
        Assert.Equal(PlayerRole.Player.ToString(), ReadPlayer(connectionString, aiIdentity.PlayerId).Role);
        Assert.Equal(PlayerRole.Player.ToString(), ReadPlayer(connectionString, suspendedIdentity.PlayerId).Role);
        Assert.Equal(
            0,
            Scalar<int>(
                connectionString,
                "SELECT COUNT(*) FROM dbo.AdminRoleAuditRecords WHERE TargetPlayerID IN (@AIPlayerID, @SuspendedPlayerID);",
                ("@AIPlayerID", aiIdentity.PlayerId),
                ("@SuspendedPlayerID", suspendedIdentity.PlayerId)));
    }

    [Fact]
    public void Configured_bootstrap_is_atomic_and_audited_once()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://bootstrap-{marker}.example";
        var subject = $"bootstrap-{marker}";
        var bootstrap = new ConfiguredAdminBootstrap("configuration:test-revision");
        var store = CreateStore(connectionString);
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        var binding = new ExternalPlayerBinding(target.PlayerId, "administrator@example.test", bootstrap);

        var first = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(
            store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                binding,
                SignInAt)));
        var second = Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(
            store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                binding,
                SignInAt.AddMinutes(5))));

        Assert.Equal(PlayerRole.Admin, first.Value.Player.Role);
        Assert.NotNull(first.Value.BootstrapAuditRecordId);
        Assert.Null(second.Value.BootstrapAuditRecordId);
        Assert.Equal(
            1,
            Scalar<int>(
                connectionString,
                "SELECT COUNT(*) FROM dbo.AdminRoleAuditRecords WHERE TargetPlayerID = @PlayerID AND Action = N'Bootstrap';",
                ("@PlayerID", first.Value.Player.PlayerId)));
        Assert.Equal(
            "configuration:test-revision|Applied explicitly configured initial administrator identity.|High",
            Scalar<string>(
                connectionString,
                """
                SELECT Source + N'|' + Reason + N'|' + Severity
                FROM dbo.AdminRoleAuditRecords
                WHERE AdminRoleAuditRecordID = @AuditID
                  AND ActorPlayerID IS NULL;
                """,
                ("@AuditID", first.Value.BootstrapAuditRecordId!.Value)));
    }

    [Fact]
    public async Task Concurrent_configured_sign_ins_resolve_one_player_and_one_bootstrap_audit()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://concurrent-{marker}.example";
        var subject = $"concurrent-{marker}";
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        var command = new ExternalPlayerSignInCommand(
            issuer,
            subject,
            new ExternalPlayerBinding(
                target.PlayerId,
                "concurrent@example.test",
                new ConfiguredAdminBootstrap("configuration:concurrent-test")),
            SignInAt);

        var tasks = Enumerable.Range(0, 2)
            .Select(_ => Task.Run(() => CreateStore(connectionString).SignInExternal(command)))
            .ToArray();
        await Task.WhenAll(tasks);
        var successes = tasks
            .Select(task => Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Success>(task.Result))
            .ToArray();

        Assert.Single(successes, item => item.Value.Bound);
        Assert.Single(successes, item => !item.Value.Bound);
        Assert.Single(successes, item => item.Value.BootstrapAuditRecordId.HasValue);
        Assert.Equal(successes[0].Value.Player.PlayerId, successes[1].Value.Player.PlayerId);
        Assert.Equal(
            1,
            Scalar<int>(
                connectionString,
                "SELECT COUNT(*) FROM dbo.Players WHERE PlayerID = @PlayerID;",
                ("@PlayerID", successes[0].Value.Player.PlayerId)));
        Assert.Equal(
            1,
            Scalar<int>(
                connectionString,
                "SELECT COUNT(*) FROM dbo.AdminRoleAuditRecords WHERE TargetPlayerID = @PlayerID AND Action = N'Bootstrap';",
                ("@PlayerID", successes[0].Value.Player.PlayerId)));
    }

    [Fact]
    public void Held_identity_lock_returns_busy_without_creating_a_player()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://busy-{marker}.example";
        var subject = $"busy-{marker}";
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                DECLARE @Result int;
                EXEC @Result = sys.sp_getapplock
                    @Resource = @Resource,
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 0;
                SELECT @Result;
                """;
            lockCommand.Parameters.AddWithValue(
                "@Resource",
                SqlServerGameStateStore.BuildExternalIdentityLockName(issuer, subject));
            Assert.True(Convert.ToInt32(lockCommand.ExecuteScalar(), null) >= 0);
        }

        var result = CreateStore(connectionString).SignInExternal(new ExternalPlayerSignInCommand(
            issuer,
            subject,
            new ExternalPlayerBinding(target.PlayerId, "busy@example.test", bootstrap: null),
            SignInAt));

        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy>(result);
        transaction.Rollback();
        Assert.Equal(
            0,
            Scalar<int>(
                connectionString,
                """
                SELECT COUNT(*)
                FROM dbo.Players
                WHERE ExternalIssuer COLLATE Latin1_General_100_BIN2 = @Issuer COLLATE Latin1_General_100_BIN2
                  AND ExternalSubject COLLATE Latin1_General_100_BIN2 = @Subject COLLATE Latin1_General_100_BIN2;
                """,
                ("@Issuer", issuer),
                ("@Subject", subject)));
    }

    [Fact]
    public async Task Held_legacy_global_lock_returns_busy_without_account_mutation()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var existing = InsertMappedPlayer(
            connectionString,
            PlayerKind.Human,
            PlayerStatus.Active,
            PlayerRole.Player,
            SignInAt);
        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://global-busy-{marker}.example";
        var subject = $"global-busy-{marker}";
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                DECLARE @Result int;
                EXEC @Result = sys.sp_getapplock
                    @Resource = @Resource,
                    @LockMode = N'Exclusive',
                    @LockOwner = N'Transaction',
                    @LockTimeout = 0;
                SELECT @Result;
                """;
            lockCommand.Parameters.AddWithValue("@Resource", LegacyGlobalStateLockName);
            Assert.True(Convert.ToInt32(lockCommand.ExecuteScalar(), null) >= 0);
        }

        var signInTask = Task.Run(() => CreateStore(connectionString).SignInExternal(
            new ExternalPlayerSignInCommand(
                issuer,
                subject,
                binding: null,
                SignInAt.AddHours(1))));
        var recordLoginTask = Task.Run(() => CreateStore(connectionString).RecordLogin(
            new RecordPlayerLoginCommand(existing.PlayerId, SignInAt.AddHours(1))));

        await Task.WhenAll(signInTask, recordLoginTask);
        var signInResult = await signInTask;
        var recordLoginResult = await recordLoginTask;

        Assert.IsType<AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy>(signInResult);
        Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Busy>(recordLoginResult);
        transaction.Rollback();
        Assert.Equal(SignInAt, ReadPlayer(connectionString, existing.PlayerId).LastLoginAt);
        Assert.Equal(
            0,
            Scalar<int>(
                connectionString,
                """
                SELECT COUNT(*)
                FROM dbo.Players
                WHERE ExternalIssuer COLLATE Latin1_General_100_BIN2 = @Issuer COLLATE Latin1_General_100_BIN2
                  AND ExternalSubject COLLATE Latin1_General_100_BIN2 = @Subject COLLATE Latin1_General_100_BIN2;
                """,
                ("@Issuer", issuer),
                ("@Subject", subject)));
    }

    [Fact]
    public void Failed_bootstrap_audit_rolls_back_the_player()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        const string triggerName = "TR_AdminRoleAuditRecords_AccountStoreFailureTest";
        const string source = "configuration:account-store-failure-test";
        DropTrigger(connectionString, triggerName);
        Execute(
            connectionString,
            $$"""
            CREATE TRIGGER dbo.{{triggerName}}
            ON dbo.AdminRoleAuditRecords
            AFTER INSERT
            AS
            BEGIN
                SET NOCOUNT ON;
                IF EXISTS (SELECT 1 FROM inserted WHERE Source = N'{{source}}')
                    THROW 51991, 'Intentional account-store audit failure.', 1;
            END;
            """);

        var marker = Guid.NewGuid().ToString("N");
        var issuer = $"https://rollback-{marker}.example";
        var subject = $"rollback-{marker}";
        var target = InsertUnboundPlayer(connectionString, PlayerKind.Human, PlayerStatus.Active, PlayerRole.Player);
        try
        {
            var store = CreateStore(connectionString);

            Assert.Throws<SqlException>(() => store.SignInExternal(new ExternalPlayerSignInCommand(
                issuer,
                subject,
                new ExternalPlayerBinding(
                    target.PlayerId,
                    "rollback@example.test",
                    new ConfiguredAdminBootstrap(source)),
                SignInAt)));

            Assert.Equal(
                1,
                Scalar<int>(
                    connectionString,
                    """
                    SELECT COUNT(*)
                    FROM dbo.Players
                    WHERE PlayerID = @PlayerID
                      AND ExternalIssuer = N''
                      AND ExternalSubject = N'';
                    """,
                    ("@PlayerID", target.PlayerId)));
        }
        finally
        {
            DropTrigger(connectionString, triggerName);
        }
    }

    [Fact]
    public void Record_login_is_monotonic_and_available_only_to_active_humans()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = CreateStore(connectionString);
        var human = InsertMappedPlayer(
            connectionString,
            PlayerKind.Human,
            PlayerStatus.Active,
            PlayerRole.Player,
            SignInAt);
        var ai = InsertMappedPlayer(
            connectionString,
            PlayerKind.AI,
            PlayerStatus.Active,
            PlayerRole.Player,
            SignInAt);
        var suspended = InsertMappedPlayer(
            connectionString,
            PlayerKind.Human,
            PlayerStatus.Suspended,
            PlayerRole.Player,
            SignInAt);

        var older = store.RecordLogin(new RecordPlayerLoginCommand(
            human.PlayerId,
            SignInAt.AddHours(-1)));
        var newer = store.RecordLogin(new RecordPlayerLoginCommand(
            human.PlayerId,
            SignInAt.AddHours(1)));

        Assert.Equal(
            SignInAt,
            Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Success>(older).Value.LastLoginAt);
        Assert.Equal(
            SignInAt.AddHours(1),
            Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Success>(newer).Value.LastLoginAt);
        Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Unavailable>(
            store.RecordLogin(new RecordPlayerLoginCommand(ai.PlayerId, SignInAt.AddHours(2))));
        Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Unavailable>(
            store.RecordLogin(new RecordPlayerLoginCommand(suspended.PlayerId, SignInAt.AddHours(2))));
        Assert.IsType<AccountCommandResult<PlayerAccountSnapshot>.Unavailable>(
            store.RecordLogin(new RecordPlayerLoginCommand(Guid.NewGuid(), SignInAt.AddHours(2))));
        Assert.Equal(SignInAt, ReadPlayer(connectionString, ai.PlayerId).LastLoginAt);
        Assert.Equal(SignInAt, ReadPlayer(connectionString, suspended.PlayerId).LastLoginAt);
    }

    [Fact]
    public void Trusted_player_selection_is_bounded_redacted_and_exactly_scoped()
    {
        var connectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        SqlServerCycleScopeFixtureIds ids;
        using (var connection = new SqlConnection(connectionString))
        {
            connection.Open();
            using var transaction = connection.BeginTransaction();
            ids = SqlServerCycleScopeFixture.Insert(connection, transaction);
            transaction.Commit();
        }

        var query = (ITrustedPlayerSelectionQuery)CreateStore(connectionString);

        var players = query.List(new GameCycleScope(ids.GameA, ids.CycleA));

        Assert.InRange(players.Count, 1, TrustedPlayerSelectionSnapshot.MaximumResults);
        Assert.Contains(players, item => item.PlayerId == ids.PlayerA);
        Assert.Contains(players, item => item.PlayerId == ids.PlayerB);
        Assert.Single(players, item => item.PlayerId == ids.PlayerA);
        Assert.Single(players, item => item.PlayerId == ids.PlayerB);
        Assert.All(players, item => Assert.DoesNotContain("@", JsonSerializer.Serialize(item), StringComparison.Ordinal));
        Assert.Empty(query.List(new GameCycleScope(ids.GameA, ids.CycleB)));

        Execute(
            connectionString,
            "UPDATE dbo.GameEnrolments SET Status = N'Withdrawn', EndedAt = @EndedAt WHERE GameEnrolmentID = @EnrolmentID;",
            ("@EnrolmentID", ids.EnrolmentAB),
            ("@EndedAt", SignInAt.AddHours(1)));
        Assert.DoesNotContain(
            query.List(new GameCycleScope(ids.GameA, ids.CycleA)),
            item => item.PlayerId == ids.PlayerB);

        Execute(
            connectionString,
            """
            UPDATE dbo.GameEnrolments
            SET Status = N'Enrolled', EndedAt = NULL
            WHERE GameEnrolmentID = @EnrolmentID;

            UPDATE dbo.MatchParticipants
            SET Status = N'Withdrawn', EndedAt = @EndedAt
            WHERE MatchParticipantID = @ParticipantID;
            """,
            ("@EnrolmentID", ids.EnrolmentAB),
            ("@EndedAt", SignInAt.AddHours(2)),
            ("@ParticipantID", ids.ParticipantA2));
        Assert.DoesNotContain(
            query.List(new GameCycleScope(ids.GameA, ids.CycleA)),
            item => item.PlayerId == ids.PlayerB);
    }

    private static SqlServerGameStateStore CreateStore(string connectionString) =>
        new(connectionString, () => new GameState());

    private static UnboundPlayer InsertUnboundPlayer(
        string connectionString,
        PlayerKind kind,
        PlayerStatus status,
        PlayerRole role)
    {
        var playerId = Guid.NewGuid();
        var username = $"invited-{playerId:N}";
        Execute(
            connectionString,
            """
            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject,
                 PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@PlayerID, @Username, N'', N'', N'', N'',
                 @PlayerKind, @Role, @CreatedAt, NULL, @Status);
            """,
            ("@PlayerID", playerId),
            ("@Username", username),
            ("@PlayerKind", kind.ToString()),
            ("@Role", role.ToString()),
            ("@CreatedAt", SignInAt.AddDays(-10)),
            ("@Status", status.ToString()));
        return new UnboundPlayer(playerId, username);
    }

    private static MappedIdentity InsertMappedPlayer(
        string connectionString,
        PlayerKind kind,
        PlayerStatus status,
        PlayerRole role,
        DateTimeOffset? lastLoginAt)
    {
        var playerId = Guid.NewGuid();
        var marker = playerId.ToString("N");
        var issuer = $"https://mapped-{marker}.example";
        var subject = $"mapped-{marker}";
        Execute(
            connectionString,
            """
            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject,
                 PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@PlayerID, @Username, N'', N'', @Issuer, @Subject,
                 @PlayerKind, @Role, @CreatedAt, @LastLoginAt, @Status);
            """,
            ("@PlayerID", playerId),
            ("@Username", $"mapped-{marker}"),
            ("@Issuer", issuer),
            ("@Subject", subject),
            ("@PlayerKind", kind.ToString()),
            ("@Role", role.ToString()),
            ("@CreatedAt", SignInAt.AddDays(-10)),
            ("@LastLoginAt", lastLoginAt ?? (object)DBNull.Value),
            ("@Status", status.ToString()));
        return new MappedIdentity(playerId, issuer, subject);
    }

    private static StoredPlayer ReadPlayer(string connectionString, Guid playerId)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Username, Email, PasswordHash, ExternalIssuer, ExternalSubject,
                   PlayerKind, Role, Status, LastLoginAt
            FROM dbo.Players
            WHERE PlayerID = @PlayerID;
            """;
        command.Parameters.AddWithValue("@PlayerID", playerId);
        using var reader = command.ExecuteReader();
        Assert.True(reader.Read());
        return new StoredPlayer(
            reader.GetString(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetDateTimeOffset(8));
    }

    private static string ReadGameplayFingerprint(string connectionString)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                JSON_QUERY((SELECT * FROM dbo.Games ORDER BY GameID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Games,
                JSON_QUERY((SELECT * FROM dbo.Cycles ORDER BY CycleID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Cycles,
                JSON_QUERY((SELECT * FROM dbo.GameEnrolments ORDER BY GameEnrolmentID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Enrolments,
                JSON_QUERY((SELECT * FROM dbo.MatchParticipants ORDER BY MatchParticipantID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Participants,
                JSON_QUERY((SELECT * FROM dbo.Empires ORDER BY EmpireID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Empires,
                JSON_QUERY((SELECT * FROM dbo.Admirals ORDER BY AdmiralID FOR JSON PATH, INCLUDE_NULL_VALUES)) AS Admirals
            FOR JSON PATH, WITHOUT_ARRAY_WRAPPER;
            """;
        return (string)command.ExecuteScalar()!;
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

    private static void DropTrigger(string connectionString, string triggerName) =>
        Execute(
            connectionString,
            $"IF OBJECT_ID(N'dbo.{triggerName}', N'TR') IS NOT NULL DROP TRIGGER dbo.{triggerName};");

    private static void AddParameters(
        SqlCommand command,
        IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }
    }

    private sealed record MappedIdentity(Guid PlayerId, string Issuer, string Subject);

    private sealed record UnboundPlayer(Guid PlayerId, string Username);

    private sealed record StoredPlayer(
        string Username,
        string Email,
        string PasswordHash,
        string ExternalIssuer,
        string ExternalSubject,
        string PlayerKind,
        string Role,
        string Status,
        DateTimeOffset? LastLoginAt);
}
