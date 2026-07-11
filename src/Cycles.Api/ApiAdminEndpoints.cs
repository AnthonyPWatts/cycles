using Cycles.Core;

public static class ApiAdminEndpoints
{
    public static IResult RunTick(HttpContext httpContext, IGameStateStore store) =>
        RunTick(httpContext, store, DateTimeOffset.UtcNow);

    public static IResult RunTick(HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() =>
        {
            var state = store.LoadOrCreate();
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            if (!actor.IsAdmin)
            {
                throw new ApiForbiddenException("Only an administrator can run a tick.");
            }

            return store.RunTick(now);
        });

    private static IResult TryResult<T>(Func<T> action)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (ApiUnauthorizedException ex)
        {
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status401Unauthorized);
        }
        catch (ApiForbiddenException ex)
        {
            return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status403Forbidden);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }
}
