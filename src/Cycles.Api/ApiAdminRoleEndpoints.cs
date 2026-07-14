using Cycles.Core;

public static class ApiAdminRoleEndpoints
{
    public static IResult Grant(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IGameStateStore store) =>
        Change(targetPlayerId, request, httpContext, store, grant: true, DateTimeOffset.UtcNow);

    public static IResult Revoke(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IGameStateStore store) =>
        Change(targetPlayerId, request, httpContext, store, grant: false, DateTimeOffset.UtcNow);

    internal static IResult Change(
        Guid targetPlayerId,
        AdminRoleChangeRequest request,
        HttpContext httpContext,
        IGameStateStore store,
        bool grant,
        DateTimeOffset now)
    {
        try
        {
            return Results.Ok(store.Update(state =>
            {
                var actor = DevelopmentAuth.RequireActor(httpContext, state);
                if (!actor.IsAdmin)
                {
                    throw new ApiForbiddenException("Only an administrator can change admin roles.");
                }

                var audit = grant
                    ? AdminRoleService.Grant(state, actor.Player.PlayerId, targetPlayerId, request.Reason, now)
                    : AdminRoleService.Revoke(state, actor.Player.PlayerId, targetPlayerId, request.Reason, now);
                var target = state.Players.Single(player => player.PlayerId == targetPlayerId);
                return new AdminRoleChangeResponse(
                    audit.AdminRoleAuditRecordId,
                    target.PlayerId,
                    target.Role,
                    audit.Action,
                    audit.CreatedAt);
            }));
        }
        catch (ApiUnauthorizedException exception)
        {
            return ApiErrorResponses.ToResult(exception, httpContext);
        }
        catch (ApiForbiddenException exception)
        {
            return ApiErrorResponses.ToResult(exception, httpContext);
        }
        catch (InvalidOperationException exception)
        {
            return ApiErrorResponses.ToResult(new ApiStateConflictException(exception.Message), httpContext);
        }
    }
}

public sealed record AdminRoleChangeRequest(string Reason);

public sealed record AdminRoleChangeResponse(
    Guid AuditRecordId,
    Guid TargetPlayerId,
    PlayerRole Role,
    AdminRoleAuditAction Action,
    DateTimeOffset ChangedAt);
