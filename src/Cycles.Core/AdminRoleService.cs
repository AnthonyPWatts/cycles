namespace Cycles.Core;

public static class AdminRoleService
{
    public static AdminRoleAuditRecord? ApplyBootstrap(
        GameState state,
        Player target,
        string source,
        string reason,
        DateTimeOffset now)
    {
        ValidateReason(reason);
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new InvalidOperationException("Admin bootstrap requires a configuration source or deployment revision.");
        }

        if (target.Role == PlayerRole.Admin)
        {
            return null;
        }

        target.Role = PlayerRole.Admin;
        return AppendAudit(state, null, target.PlayerId, AdminRoleAuditAction.Bootstrap, reason, source, now);
    }

    public static AdminRoleAuditRecord Grant(
        GameState state,
        Guid actorPlayerId,
        Guid targetPlayerId,
        string reason,
        DateTimeOffset now)
    {
        var (actor, target) = ValidateRoutineChange(state, actorPlayerId, targetPlayerId, reason);
        if (target.Role == PlayerRole.Admin)
        {
            throw new InvalidOperationException("The target player is already an administrator.");
        }

        target.Role = PlayerRole.Admin;
        return AppendAudit(state, actor.PlayerId, target.PlayerId, AdminRoleAuditAction.Granted, reason, "authenticated-admin", now);
    }

    public static AdminRoleAuditRecord Revoke(
        GameState state,
        Guid actorPlayerId,
        Guid targetPlayerId,
        string reason,
        DateTimeOffset now)
    {
        var (actor, target) = ValidateRoutineChange(state, actorPlayerId, targetPlayerId, reason);
        if (target.Role != PlayerRole.Admin)
        {
            throw new InvalidOperationException("The target player is not an administrator.");
        }

        var activeAdminCount = state.Players.Count(player => player.Status == PlayerStatus.Active && player.Role == PlayerRole.Admin);
        if (target.Status == PlayerStatus.Active && activeAdminCount <= 1)
        {
            throw new InvalidOperationException("The final active administrator cannot be revoked through the routine admin operation.");
        }

        target.Role = PlayerRole.Player;
        return AppendAudit(state, actor.PlayerId, target.PlayerId, AdminRoleAuditAction.Revoked, reason, "authenticated-admin", now);
    }

    private static (Player Actor, Player Target) ValidateRoutineChange(
        GameState state,
        Guid actorPlayerId,
        Guid targetPlayerId,
        string reason)
    {
        ValidateReason(reason);
        var actor = state.Players.SingleOrDefault(player => player.PlayerId == actorPlayerId)
            ?? throw new InvalidOperationException("Admin actor was not found.");
        if (actor.Status != PlayerStatus.Active || actor.Role != PlayerRole.Admin)
        {
            throw new InvalidOperationException("An active administrator is required to change admin roles.");
        }

        var target = state.Players.SingleOrDefault(player => player.PlayerId == targetPlayerId)
            ?? throw new InvalidOperationException("Target player was not found.");
        return (actor, target);
    }

    private static void ValidateReason(string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Admin role changes require a reason.");
        }
    }

    private static AdminRoleAuditRecord AppendAudit(
        GameState state,
        Guid? actorPlayerId,
        Guid targetPlayerId,
        AdminRoleAuditAction action,
        string reason,
        string source,
        DateTimeOffset now)
    {
        var audit = new AdminRoleAuditRecord
        {
            ActorPlayerId = actorPlayerId,
            TargetPlayerId = targetPlayerId,
            Action = action,
            Reason = reason.Trim(),
            Source = source.Trim(),
            Severity = EventSeverity.High,
            CreatedAt = now
        };
        state.AdminRoleAuditRecords.Add(audit);
        return audit;
    }
}
