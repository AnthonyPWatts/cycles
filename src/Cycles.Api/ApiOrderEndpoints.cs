using Cycles.Core;

public static class ApiOrderEndpoints
{
    public static IResult SubmitMove(MoveFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
        SubmitMove(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult SubmitMove(MoveFleetRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            DevelopmentAuth.RequireCommandableFleet(state, actor, request.FleetId);
            return OrderService.SubmitMoveOrder(
                state,
                request.FleetId,
                request.TargetSystemId,
                now);
        }));

    public static IResult SubmitAttack(AttackFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
        SubmitAttack(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult SubmitAttack(AttackFleetRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            DevelopmentAuth.RequireCommandableFleet(state, actor, request.FleetId);
            return OrderService.SubmitAttackOrder(
                state,
                request.FleetId,
                request.TargetEmpireId,
                now);
        }));

    public static IResult SubmitColonise(ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
        SubmitColonise(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult SubmitColonise(ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            DevelopmentAuth.RequireCommandableFleet(state, actor, request.FleetId);
            return OrderService.SubmitColoniseOrder(state, request.FleetId, now);
        }));

    public static IResult Cancel(CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store) =>
        Cancel(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult Cancel(CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            var empireId = DevelopmentAuth.ResolveOrderOwnerEmpireId(state, actor, request.FleetOrderId);
            return OrderService.CancelFleetOrder(
                state,
                request.FleetOrderId,
                empireId,
                now);
        }));

    public static IResult UpdatePriorities(PriorityRequest request, HttpContext httpContext, IGameStateStore store) =>
        UpdatePriorities(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult UpdatePriorities(PriorityRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            var empireId = DevelopmentAuth.ResolveEmpireId(state, actor, request.EmpireId);
            return OrderService.UpdatePriorities(
                state,
                empireId,
                request.IndustryWeight,
                request.ResearchWeight,
                request.MilitaryWeight,
                request.ExpansionWeight,
                now);
        }));

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
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new ErrorResponse(ex.Message));
        }
    }
}
