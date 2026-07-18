using System.Security.Cryptography;
using System.Text;

namespace Cycles.Core;

internal static class TurnLedgerSealer
{
    public static SealedTurnLedger Seal(
        GameState state,
        Guid cycleId,
        int tickNumber,
        DateTimeOffset now)
    {
        var cycle = state.Cycles.Single(item => item.CycleId == cycleId);
        if (cycle.TurnStage != TurnResolutionStage.CommandOpen)
        {
            throw new InvalidOperationException($"The command window is not open; the Cycle is {cycle.TurnStage}.");
        }

        cycle.TurnStage = TurnResolutionStage.Closing;

        var dueOrders = state.FleetOrders
            .Where(order => order.CycleId == cycleId
                            && order.Status == FleetOrderStatus.Pending
                            && order.ExecuteAfterTick <= tickNumber)
            .ToList();
        var dueOrdersByFleet = dueOrders
            .GroupBy(order => order.FleetId)
            .ToDictionary(group => group.Key, group => group.ToArray());

        foreach (var fleetOrders in dueOrdersByFleet.Values)
        {
            if (fleetOrders.Length > 1)
            {
                throw new InvalidOperationException(
                    $"Fleet {fleetOrders[0].FleetId} has multiple due intentions for tick {tickNumber}.");
            }
        }

        var eligibleFleets = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.Status == FleetStatus.Active
                            && fleet.ShipCount > 0)
            .OrderBy(fleet => fleet.FleetId)
            .ToArray();

        var plannedOrders = GameAiPlanner.Plan(
            state,
            cycleId,
            tickNumber,
            now,
            eligibleFleets
                .Where(fleet => !dueOrdersByFleet.ContainsKey(fleet.FleetId)
                                && ResolveMissingCommandSource(state, fleet) == FleetOrderCommandSource.GameAiPlanner)
                .Select(fleet => fleet.FleetId)
                .ToArray());
        foreach (var plannedOrder in plannedOrders)
        {
            if (!eligibleFleets.Any(fleet => fleet.FleetId == plannedOrder.FleetId)
                || dueOrdersByFleet.ContainsKey(plannedOrder.FleetId)
                || plannedOrder.CycleId != cycleId
                || plannedOrder.ExecuteAfterTick != tickNumber
                || plannedOrder.Status != FleetOrderStatus.Pending
                || plannedOrder.CommandSource != FleetOrderCommandSource.GameAiPlanner)
            {
                throw new InvalidOperationException("The game-AI planner produced an invalid or conflicting intention.");
            }

            if (state.FleetOrders.Any(order => order.FleetOrderId == plannedOrder.FleetOrderId))
            {
                throw new InvalidOperationException(
                    $"The deterministic game-AI identifier for fleet {plannedOrder.FleetId} and tick {tickNumber} already exists.");
            }

            state.FleetOrders.Add(plannedOrder);
            dueOrders.Add(plannedOrder);
            dueOrdersByFleet[plannedOrder.FleetId] = [plannedOrder];
        }

        foreach (var fleet in eligibleFleets)
        {
            if (dueOrdersByFleet.TryGetValue(fleet.FleetId, out var existingOrders))
            {
                existingOrders[0].CommandSource = ResolveExistingCommandSource(state, fleet);
                continue;
            }

            var source = ResolveMissingCommandSource(state, fleet);
            var hold = new FleetOrder
            {
                FleetOrderId = CreateDeterministicHoldId(cycleId, tickNumber, fleet.FleetId, source),
                CycleId = cycleId,
                FleetId = fleet.FleetId,
                OrderType = FleetOrderType.Hold,
                SubmitTick = cycle.CurrentTickNumber,
                ExecuteAfterTick = tickNumber,
                Status = FleetOrderStatus.Pending,
                CommandSource = source,
                CreatedAt = now
            };

            if (state.FleetOrders.Any(order => order.FleetOrderId == hold.FleetOrderId))
            {
                throw new InvalidOperationException(
                    $"The deterministic Hold identifier for fleet {fleet.FleetId} and tick {tickNumber} already exists.");
            }

            state.FleetOrders.Add(hold);
            dueOrders.Add(hold);
        }

        foreach (var order in dueOrders)
        {
            order.SealedTick = tickNumber;
            order.SealedAt = now;
        }

        cycle.TurnStage = TurnResolutionStage.Sealed;
        return new SealedTurnLedger(
            tickNumber,
            dueOrders
                .OrderBy(order => order.FleetId)
                .ThenBy(order => order.FleetOrderId)
                .ToArray());
    }

    private static FleetOrderCommandSource ResolveExistingCommandSource(GameState state, Fleet fleet)
    {
        var faction = state.GetFleetFaction(fleet);
        if (faction.Kind == FactionKind.Neutral)
        {
            return FleetOrderCommandSource.NeutralPlanner;
        }

        var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
        var player = state.Players.Single(item => item.PlayerId == empire.PlayerId);
        return player.Kind == PlayerKind.AI
            ? FleetOrderCommandSource.GameAiPlanner
            : FleetOrderCommandSource.Human;
    }

    private static FleetOrderCommandSource ResolveMissingCommandSource(GameState state, Fleet fleet)
    {
        var existingSource = ResolveExistingCommandSource(state, fleet);
        return existingSource == FleetOrderCommandSource.Human
            ? FleetOrderCommandSource.ImplicitHold
            : existingSource;
    }

    private static Guid CreateDeterministicHoldId(
        Guid cycleId,
        int tickNumber,
        Guid fleetId,
        FleetOrderCommandSource source)
    {
        var input = Encoding.UTF8.GetBytes($"cycles:hold:{cycleId:D}:{tickNumber}:{fleetId:D}:{source}");
        var hash = SHA256.HashData(input);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }
}

internal sealed record SealedTurnLedger(
    int TickNumber,
    IReadOnlyList<FleetOrder> Orders)
{
    public IEnumerable<FleetOrder> OrdersFor(FleetOrderType orderType) =>
        Orders.Where(order => order.OrderType == orderType);
}
