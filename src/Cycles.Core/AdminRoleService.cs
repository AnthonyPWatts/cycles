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
        ThrowIfRejected(EvaluateGrant(actor, target));

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
        var activeHumanAdminCount = state.Players.Count(IsActiveHumanAdministrator);
        ThrowIfRejected(EvaluateRevoke(actor, target, activeHumanAdminCount));

        target.Role = PlayerRole.Player;
        return AppendAudit(state, actor.PlayerId, target.PlayerId, AdminRoleAuditAction.Revoked, reason, "authenticated-admin", now);
    }

    public static bool IsActiveHumanAdministrator(Player player)
    {
        ArgumentNullException.ThrowIfNull(player);
        return player.Status == PlayerStatus.Active
               && player.Kind == PlayerKind.Human
               && player.Role == PlayerRole.Admin;
    }

    public static AdminRoleRuleFailure EvaluateGrant(Player actor, Player target)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);

        var commonFailure = EvaluateCommonRules(actor, target);
        if (commonFailure != AdminRoleRuleFailure.None)
        {
            return commonFailure;
        }

        return target.Role == PlayerRole.Admin
            ? AdminRoleRuleFailure.TargetIsAlreadyAdministrator
            : AdminRoleRuleFailure.None;
    }

    public static AdminRoleRuleFailure EvaluateRevoke(
        Player actor,
        Player target,
        int activeHumanAdministratorCount)
    {
        ArgumentNullException.ThrowIfNull(actor);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentOutOfRangeException.ThrowIfNegative(activeHumanAdministratorCount);

        var commonFailure = EvaluateCommonRules(actor, target);
        if (commonFailure != AdminRoleRuleFailure.None)
        {
            return commonFailure;
        }

        if (target.Role != PlayerRole.Admin)
        {
            return AdminRoleRuleFailure.TargetIsNotAdministrator;
        }

        return target.Status == PlayerStatus.Active && activeHumanAdministratorCount <= 1
            ? AdminRoleRuleFailure.FinalActiveHumanAdministrator
            : AdminRoleRuleFailure.None;
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
        if (!IsActiveHumanAdministrator(actor))
        {
            throw new InvalidOperationException("An active human administrator is required to change admin roles.");
        }

        var target = state.Players.SingleOrDefault(player => player.PlayerId == targetPlayerId)
            ?? throw new InvalidOperationException("Target player was not found.");
        return (actor, target);
    }

    private static AdminRoleRuleFailure EvaluateCommonRules(Player actor, Player target)
    {
        if (!IsActiveHumanAdministrator(actor))
        {
            return AdminRoleRuleFailure.ActorIsNotActiveHumanAdministrator;
        }

        return target.Kind == PlayerKind.Human
            ? AdminRoleRuleFailure.None
            : AdminRoleRuleFailure.TargetIsAutomated;
    }

    private static void ThrowIfRejected(AdminRoleRuleFailure failure)
    {
        var message = failure switch
        {
            AdminRoleRuleFailure.None => null,
            AdminRoleRuleFailure.ActorIsNotActiveHumanAdministrator =>
                "An active human administrator is required to change admin roles.",
            AdminRoleRuleFailure.TargetIsAutomated =>
                "Administrator roles cannot be granted to or revoked from automated players.",
            AdminRoleRuleFailure.TargetIsAlreadyAdministrator =>
                "The target player is already an administrator.",
            AdminRoleRuleFailure.TargetIsNotAdministrator =>
                "The target player is not an administrator.",
            AdminRoleRuleFailure.FinalActiveHumanAdministrator =>
                "The final active administrator cannot be revoked through the routine admin operation.",
            _ => throw new ArgumentOutOfRangeException(nameof(failure), failure, "Admin role rule failure is not supported.")
        };

        if (message is not null)
        {
            throw new InvalidOperationException(message);
        }
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

public enum AdminRoleRuleFailure
{
    None,
    ActorIsNotActiveHumanAdministrator,
    TargetIsAutomated,
    TargetIsAlreadyAdministrator,
    TargetIsNotAdministrator,
    FinalActiveHumanAdministrator
}
