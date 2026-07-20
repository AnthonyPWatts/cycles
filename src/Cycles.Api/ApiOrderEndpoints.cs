using Cycles.Application;
using Cycles.Core;

public static class ApiOrderEndpoints
{
    public static IResult SubmitMove(
        MoveFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        SubmitMove(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult SubmitMove(
        MoveFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            DevelopmentAuth.RequireCommandableFleet(state, actor, context, request.FleetId);
            return ToCommandResponse(state, OrderService.SubmitMoveOrder(
                state,
                request.FleetId,
                request.TargetSystemId,
                now,
                request.ReplacesOrderId));
        }));

    public static IResult SubmitRecall(
        RecallFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        SubmitRecall(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult SubmitRecall(
        RecallFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            DevelopmentAuth.RequireCommandableFleet(state, actor, context, request.FleetId);
            return ToCommandResponse(state, OrderService.SubmitRecallOrder(state, request.FleetId, now));
        }));

    public static IResult SubmitAttack(
        AttackFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        SubmitAttack(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult SubmitAttack(
        AttackFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            DevelopmentAuth.RequireCommandableFleet(state, actor, context, request.FleetId);
            var order = request.TargetFactionId.HasValue
                ? OrderService.SubmitAttackOrderAgainstFaction(
                    state,
                    request.FleetId,
                    request.TargetFactionId,
                    now,
                    request.ReplacesOrderId)
                : OrderService.SubmitAttackOrder(
                    state,
                    request.FleetId,
                    request.TargetEmpireId,
                    now,
                    request.ReplacesOrderId);
            return ToCommandResponse(state, order);
        }));

    public static IResult SubmitColonise(
        ColoniseFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        SubmitColonise(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult SubmitColonise(
        ColoniseFleetRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            DevelopmentAuth.RequireCommandableFleet(state, actor, context, request.FleetId);
            return ToCommandResponse(state, OrderService.SubmitColoniseOrder(
                state,
                request.FleetId,
                now,
                request.ReplacesOrderId));
        }));

    public static IResult Cancel(
        CancelFleetOrderRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        Cancel(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult Cancel(
        CancelFleetOrderRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            var empireId = DevelopmentAuth.ResolveOrderOwnerEmpireId(state, actor, context, request.FleetOrderId);
            return ToCommandResponse(state, OrderService.CancelFleetOrder(
                state,
                request.FleetOrderId,
                empireId,
                now));
        }), invalidOperationIsConflict: true);

    public static IResult UpdatePriorities(
        PriorityRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games) =>
        UpdatePriorities(request, httpContext, gameId, games, DateTimeOffset.UtcNow);

    public static IResult UpdatePriorities(
        PriorityRequest request,
        HttpContext httpContext,
        Guid gameId,
        SelectedGameRequestService games,
        DateTimeOffset now) =>
        TryResult(() => games.Command(httpContext, gameId, (state, context) =>
        {
            var actor = DevelopmentAuth.RequireActor(state, context);
            if (!actor.IsAdmin)
            {
                _ = DevelopmentAuth.RequireCommandableEmpire(state, actor, context);
            }
            var empireId = DevelopmentAuth.ResolveEmpireId(state, actor, context, request.EmpireId);
            return ToCommandResponse(OrderService.UpdatePriorities(
                state,
                empireId,
                request.IndustryWeight,
                request.ResearchWeight,
                request.MilitaryWeight,
                request.ExpansionWeight,
                now));
        }));

    private static FleetOrderCommandResponse ToCommandResponse(GameState state, FleetOrder order) =>
        new(
            order.FleetOrderId,
            order.CycleId,
            order.FleetId,
            order.OrderType,
            order.TargetSystemId,
            order.TargetEmpireId,
            order.TargetFactionId,
            order.SubmitTick,
            order.ExecuteAfterTick,
            order.ProcessedTick,
            order.Status,
            order.CommandSource,
            order.SealedTick,
            order.SealedAt,
            order.RejectionReason,
            order.SupersededByOrderId,
            order.CreatedAt,
            MoveJourneyPresentationContract.CreateOrderProjection(state, order));

    private static PriorityCommandResponse ToCommandResponse(EmpirePriority priorities) =>
        new(
            priorities.EmpirePriorityId,
            priorities.EmpireId,
            priorities.IndustryWeight,
            priorities.ResearchWeight,
            priorities.MilitaryWeight,
            priorities.ExpansionWeight,
            priorities.UpdatedAt);

    private static IResult TryResult<T>(Func<T> action, bool invalidOperationIsConflict = false)
    {
        try
        {
            return Results.Ok(action());
        }
        catch (ApiUnauthorizedException ex)
        {
            return ApiErrorResponses.ToResult(ex);
        }
        catch (ApiForbiddenException ex)
        {
            return ApiErrorResponses.ToResult(ex);
        }
        catch (ApiNotFoundException ex)
        {
            return ApiErrorResponses.ToResult(ex);
        }
        catch (FleetOrderReplacementConflictException ex)
        {
            return ApiErrorResponses.ToResult(new ApiStateConflictException(ex.Message));
        }
        catch (InvalidOperationException ex)
        {
            return ApiErrorResponses.ToResult(invalidOperationIsConflict
                ? new ApiStateConflictException(ex.Message)
                : new ApiValidationException(ex.Message));
        }
        catch (ArgumentException ex)
        {
            return ApiErrorResponses.ToResult(ex);
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
    Guid? TargetFactionId,
    int SubmitTick,
    int ExecuteAfterTick,
    int? ProcessedTick,
    FleetOrderStatus Status,
    FleetOrderCommandSource CommandSource,
    int? SealedTick,
    DateTimeOffset? SealedAt,
    string? RejectionReason,
    Guid? SupersededByOrderId,
    DateTimeOffset CreatedAt,
    MoveJourneyProjectionResponse? MoveJourneyProjection);

public sealed record PriorityCommandResponse(
    Guid EmpirePriorityId,
    Guid EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight,
    DateTimeOffset UpdatedAt);
