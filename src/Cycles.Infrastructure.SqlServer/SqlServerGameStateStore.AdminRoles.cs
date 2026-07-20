using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    private const string AuthenticatedAdminAuditSource = "authenticated-admin";

    public AdminRoleCommandResult Change(AdminRoleCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            // Legacy whole-state writers still own Players and admin-role audits.
            // Bridge to that boundary first so a stale SaveUnsafe snapshot cannot
            // overwrite the focused role update or delete its audit record.
            AcquireApplicationLock(connection, transaction);
            AcquireSqlApplicationLock(connection, transaction, AccountAdminRoleLockName);
        }
        catch (TimeoutException)
        {
            return new AdminRoleCommandResult.Busy();
        }

        var actor = ReadAdminRolePlayer(connection, transaction, command.ActorPlayerId);
        if (actor is null || !AdminRoleService.IsActiveHumanAdministrator(actor))
        {
            transaction.Commit();
            return new AdminRoleCommandResult.Forbidden();
        }

        var target = ReadAdminRolePlayer(connection, transaction, command.TargetPlayerId);
        if (target is null)
        {
            transaction.Commit();
            return new AdminRoleCommandResult.Conflict(AdminRoleConflictReason.TargetUnavailable);
        }

        var activeHumanAdministratorCount = command.Change == AdminRoleChangeKind.Revoke
            ? CountActiveHumanAdministrators(connection, transaction)
            : 0;
        var ruleFailure = command.Change switch
        {
            AdminRoleChangeKind.Grant => AdminRoleService.EvaluateGrant(actor, target),
            AdminRoleChangeKind.Revoke => AdminRoleService.EvaluateRevoke(
                actor,
                target,
                activeHumanAdministratorCount),
            _ => throw new ArgumentOutOfRangeException(
                nameof(command),
                command.Change,
                "Admin role change is not supported.")
        };
        var rejected = MapAdminRoleRuleFailure(ruleFailure);
        if (rejected is not null)
        {
            transaction.Commit();
            return rejected;
        }

        var newRole = command.Change == AdminRoleChangeKind.Grant
            ? PlayerRole.Admin
            : PlayerRole.Player;
        var affectedRows = GuardedAdminRoleUpdate(
            connection,
            transaction,
            command,
            newRole);
        if (affectedRows != 1)
        {
            transaction.Commit();
            return new AdminRoleCommandResult.Conflict(AdminRoleConflictReason.TargetUnavailable);
        }

        var auditRecordId = Guid.NewGuid();
        var auditAction = command.Change == AdminRoleChangeKind.Grant
            ? AdminRoleAuditAction.Granted
            : AdminRoleAuditAction.Revoked;
        InsertAdminRoleAudit(
            connection,
            transaction,
            auditRecordId,
            command,
            auditAction);

        transaction.Commit();
        return new AdminRoleCommandResult.Success(new AdminRoleChangeReceipt(
            auditRecordId,
            command.ActorPlayerId,
            command.TargetPlayerId,
            newRole,
            auditAction,
            command.ChangedAt));
    }

    private static Player? ReadAdminRolePlayer(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId)
    {
        var players = ReadRows(
            connection,
            transaction,
            """
            SELECT PlayerID, PlayerKind, Role, Status
            FROM dbo.Players WITH (UPDLOCK, HOLDLOCK)
            WHERE PlayerID = @PlayerID;
            """,
            command => AddGuid(command, "@PlayerID", playerId),
            reader => new Player
            {
                PlayerId = GetGuid(reader, "PlayerID"),
                Kind = GetEnum<PlayerKind>(reader, "PlayerKind"),
                Role = GetEnum<PlayerRole>(reader, "Role"),
                Status = GetEnum<PlayerStatus>(reader, "Status")
            });
        return players.SingleOrDefault();
    }

    private static int CountActiveHumanAdministrators(
        SqlConnection connection,
        SqlTransaction transaction)
    {
        var playerIds = ReadRows(
            connection,
            transaction,
            """
            SELECT PlayerID
            FROM dbo.Players WITH (UPDLOCK, HOLDLOCK)
            WHERE PlayerKind = @HumanPlayerKind
              AND Role = @AdminPlayerRole
              AND Status = @ActivePlayerStatus;
            """,
            command =>
            {
                AddString(command, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
                AddString(command, "@AdminPlayerRole", PlayerRole.Admin.ToString(), 32);
                AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
            },
            reader => GetGuid(reader, "PlayerID"));
        return playerIds.Count;
    }

    private static int GuardedAdminRoleUpdate(
        SqlConnection connection,
        SqlTransaction transaction,
        AdminRoleCommand command,
        PlayerRole newRole)
    {
        using var update = CreateCommand(
            connection,
            transaction,
            """
            UPDATE target
            SET Role = @NewRole
            FROM dbo.Players AS target WITH (UPDLOCK, HOLDLOCK)
            WHERE target.PlayerID = @TargetPlayerID
              AND target.PlayerKind = @HumanPlayerKind
              AND target.Role = @ExpectedRole
              AND EXISTS
              (
                  SELECT 1
                  FROM dbo.Players AS actor WITH (UPDLOCK, HOLDLOCK)
                  WHERE actor.PlayerID = @ActorPlayerID
                    AND actor.PlayerKind = @HumanPlayerKind
                    AND actor.Role = @AdminPlayerRole
                    AND actor.Status = @ActivePlayerStatus
              )
              AND
              (
                  @IsRevoke = 0
                  OR target.Status <> @ActivePlayerStatus
                  OR EXISTS
                  (
                      SELECT 1
                      FROM dbo.Players AS otherAdministrator WITH (UPDLOCK, HOLDLOCK)
                      WHERE otherAdministrator.PlayerID <> target.PlayerID
                        AND otherAdministrator.PlayerKind = @HumanPlayerKind
                        AND otherAdministrator.Role = @AdminPlayerRole
                        AND otherAdministrator.Status = @ActivePlayerStatus
                  )
              );
            """);
        AddGuid(update, "@ActorPlayerID", command.ActorPlayerId);
        AddGuid(update, "@TargetPlayerID", command.TargetPlayerId);
        AddString(update, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
        AddString(update, "@AdminPlayerRole", PlayerRole.Admin.ToString(), 32);
        AddString(update, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
        AddString(
            update,
            "@ExpectedRole",
            command.Change == AdminRoleChangeKind.Grant
                ? PlayerRole.Player.ToString()
                : PlayerRole.Admin.ToString(),
            32);
        AddString(update, "@NewRole", newRole.ToString(), 32);
        AddBool(update, "@IsRevoke", command.Change == AdminRoleChangeKind.Revoke);
        return update.ExecuteNonQuery();
    }

    private static void InsertAdminRoleAudit(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid auditRecordId,
        AdminRoleCommand command,
        AdminRoleAuditAction action)
    {
        using var insert = CreateCommand(
            connection,
            transaction,
            """
            INSERT INTO dbo.AdminRoleAuditRecords
                (AdminRoleAuditRecordID, ActorPlayerID, TargetPlayerID, Action, Reason, Source, Severity, CreatedAt)
            VALUES
                (@AdminRoleAuditRecordID, @ActorPlayerID, @TargetPlayerID, @Action, @Reason, @Source, @Severity, @CreatedAt);
            """);
        AddGuid(insert, "@AdminRoleAuditRecordID", auditRecordId);
        AddGuid(insert, "@ActorPlayerID", command.ActorPlayerId);
        AddGuid(insert, "@TargetPlayerID", command.TargetPlayerId);
        AddString(insert, "@Action", action.ToString(), 32);
        AddString(insert, "@Reason", command.Reason, AdminRoleCommand.MaximumReasonLength);
        AddString(insert, "@Source", AuthenticatedAdminAuditSource, 256);
        AddString(insert, "@Severity", EventSeverity.High.ToString(), 32);
        AddDateTimeOffset(insert, "@CreatedAt", command.ChangedAt);
        insert.ExecuteNonQuery();
    }

    private static AdminRoleCommandResult? MapAdminRoleRuleFailure(AdminRoleRuleFailure failure) =>
        failure switch
        {
            AdminRoleRuleFailure.None => null,
            AdminRoleRuleFailure.ActorIsNotActiveHumanAdministrator => new AdminRoleCommandResult.Forbidden(),
            AdminRoleRuleFailure.TargetIsAutomated => new AdminRoleCommandResult.Conflict(
                AdminRoleConflictReason.TargetIsAutomated),
            AdminRoleRuleFailure.TargetIsAlreadyAdministrator => new AdminRoleCommandResult.Conflict(
                AdminRoleConflictReason.AlreadyAdministrator),
            AdminRoleRuleFailure.TargetIsNotAdministrator => new AdminRoleCommandResult.Conflict(
                AdminRoleConflictReason.NotAdministrator),
            AdminRoleRuleFailure.FinalActiveHumanAdministrator => new AdminRoleCommandResult.Conflict(
                AdminRoleConflictReason.FinalActiveAdministrator),
            _ => throw new ArgumentOutOfRangeException(
                nameof(failure),
                failure,
                "Admin role rule failure is not supported.")
        };
}
