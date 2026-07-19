using Cycles.Core;

public static class MoveJourneyPresentationContract
{
    public static IReadOnlyCollection<LegalMoveDestinationResponse> CreateLegalDestinations(
        GameState state,
        Cycle cycle,
        Fleet fleet)
    {
        if (fleet.Status != FleetStatus.Active || fleet.ShipCount <= 0)
        {
            return [];
        }

        var systemsById = state.Systems
            .Where(item => item.CycleId == cycle.CycleId)
            .ToDictionary(item => item.SystemId);
        var dispatchTickNumber = cycle.CurrentTickNumber + 1;

        return state.SystemLinks
            .Where(item => item.CycleId == cycle.CycleId
                           && (item.SystemAId == fleet.CurrentSystemId || item.SystemBId == fleet.CurrentSystemId))
            .Select(link =>
            {
                var destinationSystemId = link.SystemAId == fleet.CurrentSystemId
                    ? link.SystemBId
                    : link.SystemAId;
                if (!systemsById.TryGetValue(destinationSystemId, out var destination))
                {
                    return null;
                }

                var projection = MoveJourneyTiming.Project(link.TravelTicks, dispatchTickNumber);
                return new LegalMoveDestinationResponse(
                    destination.SystemId,
                    destination.SystemName,
                    projection.TravelTicks,
                    projection.DispatchTickNumber,
                    projection.ArrivalTickNumber);
            })
            .OfType<LegalMoveDestinationResponse>()
            .OrderBy(item => item.SystemName)
            .ToArray();
    }

    public static MoveJourneyProjectionResponse? CreateOrderProjection(GameState state, FleetOrder order)
    {
        if (order.OrderType != FleetOrderType.MoveFleet
            || order.Status != FleetOrderStatus.Pending
            || !order.TargetSystemId.HasValue)
        {
            return null;
        }

        var fleet = state.Fleets.SingleOrDefault(item => item.FleetId == order.FleetId);
        if (fleet is null)
        {
            return new MoveJourneyProjectionResponse(
                RouteAvailable: false,
                ActivationTickNumber: order.ExecuteAfterTick,
                TravelTicks: null,
                DispatchTickNumber: null,
                ArrivalTickNumber: null);
        }

        var projection = MoveJourneyTiming.TryProject(
            state,
            order.CycleId,
            fleet.CurrentSystemId,
            order.TargetSystemId.Value,
            order.ExecuteAfterTick);

        return projection is null
            ? new MoveJourneyProjectionResponse(
                RouteAvailable: false,
                ActivationTickNumber: order.ExecuteAfterTick,
                TravelTicks: null,
                DispatchTickNumber: null,
                ArrivalTickNumber: null)
            : new MoveJourneyProjectionResponse(
                RouteAvailable: true,
                ActivationTickNumber: order.ExecuteAfterTick,
                projection.TravelTicks,
                projection.DispatchTickNumber,
                projection.ArrivalTickNumber);
    }
}

public sealed record LegalMoveDestinationResponse(
    Guid SystemId,
    string SystemName,
    int TravelTicks,
    int ProjectedDispatchTickNumber,
    int ProjectedArrivalTickNumber);

public sealed record MoveJourneyProjectionResponse(
    bool RouteAvailable,
    int ActivationTickNumber,
    int? TravelTicks,
    int? DispatchTickNumber,
    int? ArrivalTickNumber);
