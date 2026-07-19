namespace Cycles.Core;

public static class MoveJourneyTiming
{
    public static MoveJourneyProjection? TryProject(
        GameState state,
        Guid cycleId,
        Guid originSystemId,
        Guid destinationSystemId,
        int dispatchTickNumber)
    {
        var link = state.SystemLinks.SingleOrDefault(item =>
            item.CycleId == cycleId
            && item.Connects(originSystemId, destinationSystemId));

        return link is null
            ? null
            : Project(link.TravelTicks, dispatchTickNumber);
    }

    public static MoveJourneyProjection Project(int travelTicks, int dispatchTickNumber)
    {
        if (travelTicks <= 0)
        {
            throw new InvalidOperationException("Move journey travel time must be positive.");
        }

        if (dispatchTickNumber < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dispatchTickNumber),
                "Move journey dispatch tick must not be negative.");
        }

        return new MoveJourneyProjection(
            travelTicks,
            dispatchTickNumber,
            checked(dispatchTickNumber + travelTicks - 1));
    }
}

public sealed record MoveJourneyProjection(
    int TravelTicks,
    int DispatchTickNumber,
    int ArrivalTickNumber);
