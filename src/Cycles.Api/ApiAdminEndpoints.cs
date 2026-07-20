using Cycles.Application;
using Cycles.Core;

public static class ApiAdminEndpoints
{
    public static IResult RunTick(
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        ICycleResolutionStore resolutions) =>
        RunTick(
            httpContext,
            gameId,
            games,
            resolutions,
            allowDevelopmentPlayer: false,
            DateTimeOffset.UtcNow);

    public static IResult RunTick(
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        ICycleResolutionStore resolutions,
        bool allowDevelopmentPlayer) =>
        RunTick(
            httpContext,
            gameId,
            games,
            resolutions,
            allowDevelopmentPlayer,
            DateTimeOffset.UtcNow);

    public static IResult RunTick(
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        ICycleResolutionStore resolutions,
        DateTimeOffset now) =>
        RunTick(
            httpContext,
            gameId,
            games,
            resolutions,
            allowDevelopmentPlayer: false,
            now);

    public static IResult RunTick(
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        ICycleResolutionStore resolutions,
        bool allowDevelopmentPlayer,
        DateTimeOffset now) =>
        TryResult(() =>
        {
            var request = games.Query(httpContext, gameId, (state, context) =>
            {
                var actor = DevelopmentAuth.RequireActor(state, context);
                if (!actor.IsAdmin && !allowDevelopmentPlayer)
                {
                    throw new ApiForbiddenException("Only an administrator can run a tick.");
                }

                if (!actor.IsAdmin)
                {
                    _ = DevelopmentAuth.RequireCommandableEmpire(state, actor, context);
                }

                return new ExplicitCycleResolutionRequest(
                    context,
                    requireAdminister: !allowDevelopmentPlayer);
            });

            var result = resolutions.ResolveExplicit(request, now) switch
            {
                CycleResolutionResult.Completed completed => completed.Value,
                CycleResolutionResult.RecoveryRequired recovery => recovery.Value,
                CycleResolutionResult.Forbidden =>
                    throw new ApiForbiddenException("The authenticated player is no longer authorised to resolve this Game."),
                CycleResolutionResult.Busy =>
                    throw new ApiStateConflictException("The selected Game is temporarily busy. Try again."),
                CycleResolutionResult.Unavailable =>
                    throw new ApiStateConflictException("The selected Game's current Cycle is no longer available for resolution."),
                CycleResolutionResult.Stale or CycleResolutionResult.NotDue =>
                    throw new ApiStateConflictException("The selected Game's resolution context changed. Refresh and try again."),
                _ => throw new InvalidOperationException("The Cycle resolution store returned an unsupported result.")
            };
            return new TickCommandResponse(
                result.TickNumber,
                result.Status,
                result.OrdersProcessed,
                result.EventsCreated,
                result.BattlesCreated,
                result.ChronicleEntriesCreated);
        });

    private static IResult TryResult<T>(Func<T> action)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (Exception ex) when (ApiErrorResponses.IsHandled(ex))
        {
            return ApiErrorResponses.ToResult(ex);
        }
    }
}

public sealed record TickCommandResponse(
    int TickNumber,
    TickLogStatus Status,
    int OrdersProcessed,
    int EventsCreated,
    int BattlesCreated,
    int ChronicleEntriesCreated);
