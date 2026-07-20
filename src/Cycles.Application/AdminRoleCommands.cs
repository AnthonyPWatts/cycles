using Cycles.Core;

namespace Cycles.Application;

public interface IAdminRoleCommandStore
{
    AdminRoleCommandResult Change(AdminRoleCommand command);
}

public enum AdminRoleChangeKind
{
    Grant,
    Revoke
}

public sealed record AdminRoleCommand
{
    public const int MaximumReasonLength = 1024;

    public AdminRoleCommand(
        Guid actorPlayerId,
        Guid targetPlayerId,
        AdminRoleChangeKind change,
        string reason,
        DateTimeOffset changedAt)
    {
        if (actorPlayerId == Guid.Empty)
        {
            throw new ArgumentException("Actor player identifier cannot be empty.", nameof(actorPlayerId));
        }

        if (targetPlayerId == Guid.Empty)
        {
            throw new ArgumentException("Target player identifier cannot be empty.", nameof(targetPlayerId));
        }

        if (!Enum.IsDefined(change))
        {
            throw new ArgumentOutOfRangeException(nameof(change), change, "Admin role change is not supported.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Admin role changes require a reason.", nameof(reason));
        }

        var normalisedReason = reason.Trim();
        if (normalisedReason.Length > MaximumReasonLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(reason),
                normalisedReason.Length,
                $"Admin role change reasons cannot exceed {MaximumReasonLength} characters.");
        }

        ActorPlayerId = actorPlayerId;
        TargetPlayerId = targetPlayerId;
        Change = change;
        Reason = normalisedReason;
        ChangedAt = changedAt;
    }

    public Guid ActorPlayerId { get; }

    public Guid TargetPlayerId { get; }

    public AdminRoleChangeKind Change { get; }

    public string Reason { get; }

    public DateTimeOffset ChangedAt { get; }
}

public sealed record AdminRoleChangeReceipt(
    Guid AuditRecordId,
    Guid ActorPlayerId,
    Guid TargetPlayerId,
    PlayerRole Role,
    AdminRoleAuditAction Action,
    DateTimeOffset ChangedAt);

public enum AdminRoleConflictReason
{
    TargetUnavailable,
    TargetIsAutomated,
    AlreadyAdministrator,
    NotAdministrator,
    FinalActiveAdministrator
}

public abstract record AdminRoleCommandResult
{
    private AdminRoleCommandResult()
    {
    }

    public sealed record Success(AdminRoleChangeReceipt Value) : AdminRoleCommandResult;

    public sealed record Forbidden() : AdminRoleCommandResult;

    public sealed record Conflict(AdminRoleConflictReason Reason) : AdminRoleCommandResult;

    public sealed record Busy() : AdminRoleCommandResult;
}
