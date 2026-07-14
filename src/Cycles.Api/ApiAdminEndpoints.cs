using Cycles.Core;

public static class ApiAdminEndpoints
{
    public static IResult RunTick(HttpContext httpContext, IGameStateStore store) =>
        RunTick(httpContext, store, allowDevelopmentPlayer: false, DateTimeOffset.UtcNow);

    public static IResult RunTick(HttpContext httpContext, IGameStateStore store, bool allowDevelopmentPlayer) =>
        RunTick(httpContext, store, allowDevelopmentPlayer, DateTimeOffset.UtcNow);

    public static IResult RunTick(HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        RunTick(httpContext, store, allowDevelopmentPlayer: false, now);

    public static IResult RunTick(
        HttpContext httpContext,
        IGameStateStore store,
        bool allowDevelopmentPlayer,
        DateTimeOffset now) =>
        TryResult(() =>
        {
            var state = store.LoadOrCreate();
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            if (!actor.IsAdmin && !allowDevelopmentPlayer)
            {
                throw new ApiForbiddenException("Only an administrator can run a tick.");
            }

            var result = store.RunTick(now);
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
