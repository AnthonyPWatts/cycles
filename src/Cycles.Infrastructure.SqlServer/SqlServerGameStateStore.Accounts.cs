using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Buffers.Binary;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    private const string AccountIdentityLockPrefix = "Cycles.Account.Identity.";
    private const string AccountAdminRoleLockName = "Cycles.Account.AdminRole";
    private const string AdminBootstrapReason =
        "Applied explicitly configured initial administrator identity.";
    private const string BinaryIdentityCollation = "Latin1_General_100_BIN2";

    public AccountCommandResult<ExternalPlayerSignInSnapshot> SignInExternal(
        ExternalPlayerSignInCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // Transitional lock order while the legacy whole-state writer remains online:
            // global state -> external identity -> administrator role.
            AcquireSqlApplicationLock(connection, transaction, ApplicationLockName);
            AcquireSqlApplicationLock(
                connection,
                transaction,
                BuildExternalIdentityLockName(command.Issuer, command.Subject));

            if (command.Binding?.Bootstrap is not null)
            {
                AcquireSqlApplicationLock(connection, transaction, AccountAdminRoleLockName);
            }
        }
        catch (TimeoutException)
        {
            return new AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy();
        }

        var existing = ReadAccountByExternalIdentityForUpdate(
            connection,
            transaction,
            command.Issuer,
            command.Subject);

        if (existing is not null
            && (existing.Kind != PlayerKind.Human || existing.Status != PlayerStatus.Active))
        {
            transaction.Commit();
            return new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable();
        }

        var bound = false;
        Guid? bootstrapAuditRecordId = null;

        if (existing is null)
        {
            if (command.Binding is null)
            {
                transaction.Commit();
                return new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable();
            }

            var target = ReadExternalBindingTargetForUpdate(
                connection,
                transaction,
                command.Binding.PlayerId);
            if (target is null
                || target.Account.Kind != PlayerKind.Human
                || target.Account.Status != PlayerStatus.Active
                || target.ExternalIssuer.Length != 0
                || target.ExternalSubject.Length != 0)
            {
                transaction.Commit();
                return new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable();
            }

            BindExternalPlayer(
                connection,
                transaction,
                command);
            bound = true;

            if (command.Binding.Bootstrap is not null)
            {
                bootstrapAuditRecordId = InsertBootstrapAudit(
                    connection,
                    transaction,
                    command.Binding.PlayerId,
                    command.Binding.Bootstrap.Source,
                    command.SignedInAt);
            }
        }
        else
        {
            var applyBootstrap = command.Binding?.PlayerId == existing.PlayerId
                && command.Binding.Bootstrap is not null
                && existing!.Role != PlayerRole.Admin;
            UpdateExternalPlayerLogin(
                connection,
                transaction,
                existing.PlayerId,
                command.SignedInAt,
                applyBootstrap);

            if (applyBootstrap)
            {
                bootstrapAuditRecordId = InsertBootstrapAudit(
                    connection,
                    transaction,
                    existing.PlayerId,
                    command.Binding!.Bootstrap!.Source,
                    command.SignedInAt);
            }
        }

        var playerId = existing?.PlayerId ?? command.Binding!.PlayerId;
        var account = ReadAccountById(connection, transaction, playerId)
            ?? throw new InvalidOperationException(
                "The external player disappeared during the account transaction.");

        transaction.Commit();
        return new AccountCommandResult<ExternalPlayerSignInSnapshot>.Success(
            new ExternalPlayerSignInSnapshot(account, bound, bootstrapAuditRecordId));
    }

    public AccountCommandResult<PlayerAccountSnapshot> RecordLogin(
        RecordPlayerLoginCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            // RecordLogin mutates Players, which is still also persisted by the
            // legacy whole-state writer. Serialize with that bridge until it is removed.
            AcquireSqlApplicationLock(connection, transaction, ApplicationLockName);
        }
        catch (TimeoutException)
        {
            return new AccountCommandResult<PlayerAccountSnapshot>.Busy();
        }

        var accounts = ReadRows(
            connection,
            transaction,
            """
            UPDATE dbo.Players
            SET LastLoginAt =
                CASE
                    WHEN LastLoginAt IS NULL OR LastLoginAt < @SignedInAt THEN @SignedInAt
                    ELSE LastLoginAt
                END
            OUTPUT
                inserted.PlayerID,
                inserted.Username,
                inserted.PlayerKind,
                inserted.Role,
                inserted.Status,
                inserted.CreatedAt,
                inserted.LastLoginAt
            WHERE PlayerID = @PlayerID
              AND PlayerKind = @HumanPlayerKind
              AND Status = @ActivePlayerStatus;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", command.PlayerId);
                AddDateTimeOffset(sqlCommand, "@SignedInAt", command.SignedInAt);
                AddString(sqlCommand, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
                AddString(sqlCommand, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
            },
            ReadPlayerAccountSnapshot);

        transaction.Commit();
        var account = accounts.SingleOrDefault();
        return account is null
            ? new AccountCommandResult<PlayerAccountSnapshot>.Unavailable()
            : new AccountCommandResult<PlayerAccountSnapshot>.Success(account);
    }

    public IReadOnlyList<TrustedPlayerSelectionSnapshot> List(GameCycleScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var players = ReadRows(
            connection,
            transaction,
            """
            SELECT TOP (@MaximumResults)
                player.PlayerID,
                player.Username,
                participant.Status AS ParticipantStatus
            FROM dbo.MatchParticipants AS participant
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.CycleID = participant.CycleID
               AND cycle.GameID = participant.GameID
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.GameID = participant.GameID
               AND enrolment.PlayerID = participant.PlayerID
               AND enrolment.Status <> @WithdrawnEnrolmentStatus
            INNER JOIN dbo.Players AS player
                ON player.PlayerID = participant.PlayerID
            WHERE participant.GameID = @GameID
              AND participant.CycleID = @CycleID
              AND participant.Status IN
                  (@ActiveParticipantStatus, @DefeatedParticipantStatus, @CompletedParticipantStatus)
              AND player.PlayerKind = @HumanPlayerKind
              AND player.Status = @ActivePlayerStatus
            ORDER BY player.Username, player.PlayerID;
            """,
            sqlCommand =>
            {
                AddInt(
                    sqlCommand,
                    "@MaximumResults",
                    TrustedPlayerSelectionSnapshot.MaximumResults);
                AddGuid(sqlCommand, "@GameID", scope.GameId);
                AddGuid(sqlCommand, "@CycleID", scope.CycleId);
                AddString(
                    sqlCommand,
                    "@WithdrawnEnrolmentStatus",
                    GameEnrolmentStatus.Withdrawn.ToString(),
                    32);
                AddString(
                    sqlCommand,
                    "@ActiveParticipantStatus",
                    MatchParticipantStatus.Active.ToString(),
                    32);
                AddString(
                    sqlCommand,
                    "@DefeatedParticipantStatus",
                    MatchParticipantStatus.Defeated.ToString(),
                    32);
                AddString(
                    sqlCommand,
                    "@CompletedParticipantStatus",
                    MatchParticipantStatus.Completed.ToString(),
                    32);
                AddString(sqlCommand, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
                AddString(sqlCommand, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
            },
            reader => new TrustedPlayerSelectionSnapshot(
                GetGuid(reader, "PlayerID"),
                GetString(reader, "Username"),
                GetEnum<MatchParticipantStatus>(reader, "ParticipantStatus")));

        transaction.Commit();
        return players.AsReadOnly();
    }

    internal static string BuildExternalIdentityLockName(string issuer, string subject)
    {
        ArgumentNullException.ThrowIfNull(issuer);
        ArgumentNullException.ThrowIfNull(subject);

        var issuerBytes = Encoding.UTF8.GetBytes(issuer);
        var subjectBytes = Encoding.UTF8.GetBytes(subject);
        var fingerprintInput = new byte[8 + issuerBytes.Length + subjectBytes.Length];
        BinaryPrimitives.WriteInt32BigEndian(fingerprintInput.AsSpan(0, 4), issuerBytes.Length);
        issuerBytes.CopyTo(fingerprintInput, 4);
        var subjectLengthOffset = 4 + issuerBytes.Length;
        BinaryPrimitives.WriteInt32BigEndian(
            fingerprintInput.AsSpan(subjectLengthOffset, 4),
            subjectBytes.Length);
        subjectBytes.CopyTo(fingerprintInput, subjectLengthOffset + 4);

        return $"{AccountIdentityLockPrefix}{Convert.ToHexString(SHA256.HashData(fingerprintInput))}";
    }

    private static PlayerAccountSnapshot? ReadAccountByExternalIdentityForUpdate(
        SqlConnection connection,
        SqlTransaction transaction,
        string issuer,
        string subject)
    {
        var accounts = ReadRows(
            connection,
            transaction,
            $$"""
            SELECT
                PlayerID,
                Username,
                PlayerKind,
                Role,
                Status,
                CreatedAt,
                LastLoginAt
            FROM dbo.Players WITH (UPDLOCK, HOLDLOCK)
            WHERE ExternalIssuer COLLATE {{BinaryIdentityCollation}}
                    = @ExternalIssuer COLLATE {{BinaryIdentityCollation}}
              AND ExternalSubject COLLATE {{BinaryIdentityCollation}}
                    = @ExternalSubject COLLATE {{BinaryIdentityCollation}};
            """,
            sqlCommand =>
            {
                AddString(sqlCommand, "@ExternalIssuer", issuer, 256);
                AddString(sqlCommand, "@ExternalSubject", subject, 256);
            },
            ReadPlayerAccountSnapshot);

        return accounts.SingleOrDefault();
    }

    private static PlayerAccountSnapshot? ReadAccountById(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId)
    {
        var accounts = ReadRows(
            connection,
            transaction,
            """
            SELECT
                PlayerID,
                Username,
                PlayerKind,
                Role,
                Status,
                CreatedAt,
                LastLoginAt
            FROM dbo.Players
            WHERE PlayerID = @PlayerID;
            """,
            sqlCommand => AddGuid(sqlCommand, "@PlayerID", playerId),
            ReadPlayerAccountSnapshot);

        return accounts.SingleOrDefault();
    }

    private static ExternalBindingTarget? ReadExternalBindingTargetForUpdate(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId)
    {
        var targets = ReadRows(
            connection,
            transaction,
            """
            SELECT
                PlayerID,
                Username,
                PlayerKind,
                Role,
                Status,
                CreatedAt,
                LastLoginAt,
                ExternalIssuer,
                ExternalSubject
            FROM dbo.Players WITH (UPDLOCK, HOLDLOCK)
            WHERE PlayerID = @PlayerID;
            """,
            sqlCommand => AddGuid(sqlCommand, "@PlayerID", playerId),
            reader => new ExternalBindingTarget(
                ReadPlayerAccountSnapshot(reader),
                GetString(reader, "ExternalIssuer"),
                GetString(reader, "ExternalSubject")));

        return targets.SingleOrDefault();
    }

    private static PlayerAccountSnapshot ReadPlayerAccountSnapshot(SqlDataReader reader) =>
        new(
            GetGuid(reader, "PlayerID"),
            GetString(reader, "Username"),
            GetEnum<PlayerKind>(reader, "PlayerKind"),
            GetEnum<PlayerRole>(reader, "Role"),
            GetEnum<PlayerStatus>(reader, "Status"),
            GetDateTimeOffset(reader, "CreatedAt"),
            GetNullableDateTimeOffset(reader, "LastLoginAt"));

    private static void BindExternalPlayer(
        SqlConnection connection,
        SqlTransaction transaction,
        ExternalPlayerSignInCommand command) =>
        Execute(
            connection,
            transaction,
            """
            UPDATE dbo.Players
            SET
                Email = @Email,
                ExternalIssuer = @ExternalIssuer,
                ExternalSubject = @ExternalSubject,
                Role = CASE WHEN @ApplyBootstrap = 1 THEN @AdminRole ELSE Role END,
                LastLoginAt = @SignedInAt
            WHERE PlayerID = @PlayerID
              AND PlayerKind = @HumanPlayerKind
              AND Status = @ActivePlayerStatus
              AND ExternalIssuer = N''
              AND ExternalSubject = N'';
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", command.Binding!.PlayerId);
                AddString(sqlCommand, "@Email", command.Binding.VerifiedEmail, 256);
                AddString(sqlCommand, "@ExternalIssuer", command.Issuer, 256);
                AddString(sqlCommand, "@ExternalSubject", command.Subject, 256);
                AddBool(sqlCommand, "@ApplyBootstrap", command.Binding.Bootstrap is not null);
                AddString(sqlCommand, "@AdminRole", PlayerRole.Admin.ToString(), 32);
                AddString(sqlCommand, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
                AddDateTimeOffset(sqlCommand, "@SignedInAt", command.SignedInAt);
                AddString(sqlCommand, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
            });

    private static void UpdateExternalPlayerLogin(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId,
        DateTimeOffset signedInAt,
        bool applyBootstrap) =>
        Execute(
            connection,
            transaction,
            """
            UPDATE dbo.Players
            SET
                LastLoginAt =
                    CASE
                        WHEN LastLoginAt IS NULL OR LastLoginAt < @SignedInAt THEN @SignedInAt
                        ELSE LastLoginAt
                    END,
                Role = CASE WHEN @ApplyBootstrap = 1 THEN @AdminRole ELSE Role END
            WHERE PlayerID = @PlayerID;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", playerId);
                AddDateTimeOffset(sqlCommand, "@SignedInAt", signedInAt);
                AddBool(sqlCommand, "@ApplyBootstrap", applyBootstrap);
                AddString(sqlCommand, "@AdminRole", PlayerRole.Admin.ToString(), 32);
            });

    private static Guid InsertBootstrapAudit(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid targetPlayerId,
        string source,
        DateTimeOffset signedInAt)
    {
        var auditRecordId = Guid.NewGuid();
        Execute(
            connection,
            transaction,
            """
            INSERT INTO dbo.AdminRoleAuditRecords
            (
                AdminRoleAuditRecordID,
                ActorPlayerID,
                TargetPlayerID,
                Action,
                Reason,
                Source,
                Severity,
                CreatedAt
            )
            VALUES
            (
                @AdminRoleAuditRecordID,
                NULL,
                @TargetPlayerID,
                @Action,
                @Reason,
                @Source,
                @Severity,
                @CreatedAt
            );
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@AdminRoleAuditRecordID", auditRecordId);
                AddGuid(sqlCommand, "@TargetPlayerID", targetPlayerId);
                AddString(sqlCommand, "@Action", AdminRoleAuditAction.Bootstrap.ToString(), 32);
                AddString(sqlCommand, "@Reason", AdminBootstrapReason, 1024);
                AddString(sqlCommand, "@Source", source, 256);
                AddString(sqlCommand, "@Severity", EventSeverity.High.ToString(), 32);
                AddDateTimeOffset(sqlCommand, "@CreatedAt", signedInAt);
            });
        return auditRecordId;
    }

    private sealed record ExternalBindingTarget(
        PlayerAccountSnapshot Account,
        string ExternalIssuer,
        string ExternalSubject);
}
