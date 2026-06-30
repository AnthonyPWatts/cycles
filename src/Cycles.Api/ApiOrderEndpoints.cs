using Cycles.Core;

public static class ApiOrderEndpoints
{
    public static IResult SubmitMove(MoveFleetRequest request, IGameStateStore store) =>
        SubmitMove(request, store, DateTimeOffset.UtcNow);

    public static IResult SubmitMove(MoveFleetRequest request, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state => OrderService.SubmitMoveOrder(
            state,
            request.FleetId,
            request.TargetSystemId,
            now)));

    public static IResult SubmitAttack(AttackFleetRequest request, IGameStateStore store) =>
        SubmitAttack(request, store, DateTimeOffset.UtcNow);

    public static IResult SubmitAttack(AttackFleetRequest request, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state => OrderService.SubmitAttackOrder(
            state,
            request.FleetId,
            request.TargetEmpireId,
            now)));

    public static IResult Cancel(CancelFleetOrderRequest request, IGameStateStore store) =>
        Cancel(request, store, DateTimeOffset.UtcNow);

    public static IResult Cancel(CancelFleetOrderRequest request, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state => OrderService.CancelFleetOrder(
            state,
            request.FleetOrderId,
            request.EmpireId,
            now)));

    public static IResult UpdatePriorities(PriorityRequest request, IGameStateStore store) =>
        UpdatePriorities(request, store, DateTimeOffset.UtcNow);

    public static IResult UpdatePriorities(PriorityRequest request, IGameStateStore store, DateTimeOffset now) =>
        TryResult(() => store.Update(state => OrderService.UpdatePriorities(
            state,
            request.EmpireId,
            request.IndustryWeight,
            request.ResearchWeight,
            request.MilitaryWeight,
            request.ExpansionWeight,
            now)));

    private static IResult TryResult<T>(Func<T> action)
    {
        try
        {
            return Results.Ok(action());
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
