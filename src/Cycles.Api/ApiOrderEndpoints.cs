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
            return ToCommandResponse(OrderService.SubmitMoveOrder(
                state,
                request.FleetId,
                request.TargetSystemId,
                now));
        }));

    public static IResult SubmitAttack(AttackFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
        SubmitAttack(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult SubmitAttack(AttackFleetRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            DevelopmentAuth.RequireCommandableFleet(state, actor, request.FleetId);
            return ToCommandResponse(OrderService.SubmitAttackOrder(
                state,
                request.FleetId,
                request.TargetEmpireId,
                now));
        }));

    public static IResult SubmitColonise(ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
        SubmitColonise(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult SubmitColonise(ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            DevelopmentAuth.RequireCommandableFleet(state, actor, request.FleetId);
            return ToCommandResponse(OrderService.SubmitColoniseOrder(state, request.FleetId, now));
        }));

    public static IResult Cancel(CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store) =>
        Cancel(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult Cancel(CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            var empireId = DevelopmentAuth.ResolveOrderOwnerEmpireId(state, actor, request.FleetOrderId);
            return ToCommandResponse(OrderService.CancelFleetOrder(
                state,
                request.FleetOrderId,
                empireId,
                now));
        }));

    public static IResult UpdatePriorities(PriorityRequest request, HttpContext httpContext, IGameStateStore store) =>
        UpdatePriorities(request, httpContext, store, DateTimeOffset.UtcNow);

    public static IResult UpdatePriorities(PriorityRequest request, HttpContext httpContext, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state =>
        {
            var actor = DevelopmentAuth.RequireActor(httpContext, state);
            var empireId = DevelopmentAuth.ResolveEmpireId(state, actor, request.EmpireId);
            return ToCommandResponse(OrderService.UpdatePriorities(
                state,
                empireId,
                request.IndustryWeight,
                request.ResearchWeight,
                request.MilitaryWeight,
                request.ExpansionWeight,
                now));
        }));

    private static FleetOrderCommandResponse ToCommandResponse(FleetOrder order) =>
        new(
            order.FleetOrderId,
            order.CycleId,
            order.FleetId,
            order.OrderType,
            order.TargetSystemId,
            order.TargetEmpireId,
            order.SubmitTick,
            order.ExecuteAfterTick,
            order.ProcessedTick,
            order.Status,
            order.RejectionReason,
            order.CreatedAt);

    private static PriorityCommandResponse ToCommandResponse(EmpirePriority priorities) =>
        new(
            priorities.EmpirePriorityId,
            priorities.EmpireId,
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight,
            priorities.UpdatedAt);

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

public sealed record FleetOrderCommandResponse(
    Guid FleetOrderId,
    Guid CycleId,
    Guid FleetId,
    FleetOrderType OrderType,
    Guid? TargetSystemId,
    Guid? TargetEmpireId,
    int SubmitTick,
    int ExecuteAfterTick,
    int? ProcessedTick,
    FleetOrderStatus Status,
    string? RejectionReason,
    DateTimeOffset CreatedAt);

public sealed record PriorityCommandResponse(
    Guid EmpirePriorityId,
    Guid EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight,
    DateTimeOffset UpdatedAt);
