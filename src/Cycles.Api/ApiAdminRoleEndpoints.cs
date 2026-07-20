using Cycles.Application;
using Cycles.Core;

public static class ApiAdminRoleEndpoints
{
    public static IResult Grant(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IPlayerAccountQuery accounts,
        IAdminRoleCommandStore roleCommands) =>
        Change(targetPlayerId, request, httpContext, accounts, roleCommands, grant: true, DateTimeOffset.UtcNow);

    public static IResult Revoke(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IPlayerAccountQuery accounts,
        IAdminRoleCommandStore roleCommands) =>
        Change(targetPlayerId, request, httpContext, accounts, roleCommands, grant: false, DateTimeOffset.UtcNow);

    internal static IResult Change(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IPlayerAccountQuery accounts,
        IAdminRoleCommandStore roleCommands,
        bool grant,
        DateTimeOffset now)
    {
        try
        {
            var actor = DevelopmentAuth.RequireAccount(httpContext, accounts);
            var result = roleCommands.Change(new AdminRoleCommand(
                actor.PlayerId,
                targetPlayerId,
                grant ? AdminRoleChangeKind.Grant : AdminRoleChangeKind.Revoke,
                request.Reason,
                now));
            return result switch
            {
                AdminRoleCommandResult.Success success => Results.Ok(new AdminRoleChangeResponse(
                    success.Value.AuditRecordId,
                    success.Value.TargetPlayerId,
                    success.Value.Role,
                    success.Value.Action,
                    success.Value.ChangedAt)),
                AdminRoleCommandResult.Forbidden => ApiErrorResponses.ToResult(
                    new ApiForbiddenException("Only an administrator can change admin roles."),
                    httpContext),
                AdminRoleCommandResult.Conflict conflict => MapConflict(conflict.Reason, httpContext),
                AdminRoleCommandResult.Busy => ApiErrorResponses.ToResult(
                    new ApiStateConflictException("Administrator roles are being changed by another request. Try again."),
                    httpContext),
                _ => throw new InvalidOperationException("The administrator role store returned an unsupported result.")
            };
        }
        catch (ArgumentException exception)
        {
            // Preserve the existing role endpoint contract while command
            // validation moves behind the focused application boundary.
            return ApiErrorResponses.ToResult(
                new ApiStateConflictException(exception.Message),
                httpContext);
        }
        catch (Exception exception) when (ApiErrorResponses.IsHandled(exception))
        {
            return ApiErrorResponses.ToResult(exception, httpContext);
        }
    }

    private static IResult MapConflict(AdminRoleConflictReason reason, HttpContext httpContext) =>
        reason switch
        {
            AdminRoleConflictReason.TargetUnavailable => ApiErrorResponses.ToResult(
                new ApiStateConflictException("The target player is not available."),
                httpContext),
            AdminRoleConflictReason.TargetIsAutomated => ApiErrorResponses.ToResult(
                new ApiStateConflictException("Automated players cannot be administrators."),
                httpContext),
            AdminRoleConflictReason.AlreadyAdministrator => ApiErrorResponses.ToResult(
                new ApiStateConflictException("The target player is already an administrator."),
                httpContext),
            AdminRoleConflictReason.NotAdministrator => ApiErrorResponses.ToResult(
                new ApiStateConflictException("The target player is not an administrator."),
                httpContext),
            AdminRoleConflictReason.FinalActiveAdministrator => ApiErrorResponses.ToResult(
                new ApiStateConflictException("The final active administrator cannot be revoked."),
                httpContext),
            _ => throw new InvalidOperationException("The administrator role store returned an unsupported conflict reason.")
        };
}

public sealed record AdminRoleChangeRequest(string Reason);

public sealed record AdminRoleChangeResponse(
    Guid AuditRecordId,
    Guid TargetPlayerId,
    PlayerRole Role,
    AdminRoleAuditAction Action,
    DateTimeOffset ChangedAt);
