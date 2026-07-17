using System.Text.Json;

namespace Cycles.Core;

public static class OrderService
{
    public const decimal ColonisationPopulationCost = 100m;

    public static FleetOrder SubmitMoveOrder(
        GameState state,
        Guid fleetId,
        Guid targetSystemId,
        DateTimeOffset now,
        Guid? replacesOrderId = null)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);
        var target = state.Systems.SingleOrDefault(system => system.CycleId == cycle.CycleId && system.SystemId == targetSystemId)
            ?? throw new InvalidOperationException("Target system does not exist in the active cycle.");

        if (!state.SystemLinks.Any(link => link.CycleId == cycle.CycleId && link.Connects(fleet.CurrentSystemId, target.SystemId)))
        {
            throw new InvalidOperationException("Move orders must target an adjacent linked system.");
        }

        return AddFleetOrder(state, cycle, fleet, FleetOrderType.MoveFleet, target.SystemId, null, now, replacesOrderId);
    }

    public static FleetOrder SubmitAttackOrder(
        GameState state,
        Guid fleetId,
        Guid? targetEmpireId,
        DateTimeOffset now,
        Guid? replacesOrderId = null)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);

        if (targetEmpireId.HasValue
            && !state.Empires.Any(empire => empire.CycleId == cycle.CycleId && empire.EmpireId == targetEmpireId.Value))
        {
            throw new InvalidOperationException("Target empire does not exist in the active cycle.");
        }

        if (targetEmpireId == fleet.EmpireId)
        {
            throw new InvalidOperationException("A fleet cannot attack its own empire.");
        }

        return AddFleetOrder(state, cycle, fleet, FleetOrderType.Attack, null, targetEmpireId, now, replacesOrderId);
    }

    public static FleetOrder SubmitHoldOrder(
        GameState state,
        Guid fleetId,
        DateTimeOffset now,
        Guid? replacesOrderId = null)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);
        return AddFleetOrder(state, cycle, fleet, FleetOrderType.Hold, null, null, now, replacesOrderId);
    }

    public static FleetOrder SubmitColoniseOrder(
        GameState state,
        Guid fleetId,
        DateTimeOffset now,
        Guid? replacesOrderId = null)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);
        var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);

        if (fleet.CurrentSystemId == empire.HomeSystemId)
        {
            throw new InvalidOperationException("An empire cannot colonise its home system.");
        }

        if (state.ColonialOutposts.Any(item => item.CycleId == cycle.CycleId
                                                && item.EmpireId == fleet.EmpireId
                                                && item.SystemId == fleet.CurrentSystemId))
        {
            throw new InvalidOperationException("The empire already has a colonial outpost in this system.");
        }

        var empireFleetIds = state.Fleets
            .Where(item => item.CycleId == cycle.CycleId && item.EmpireId == fleet.EmpireId)
            .Select(item => item.FleetId)
            .ToHashSet();
        if (state.FleetOrders.Any(item => item.CycleId == cycle.CycleId
                                          && item.OrderType == FleetOrderType.Colonise
                                          && item.Status == FleetOrderStatus.Pending
                                          && item.TargetSystemId == fleet.CurrentSystemId
                                          && item.FleetId != fleet.FleetId
                                          && empireFleetIds.Contains(item.FleetId)))
        {
            throw new InvalidOperationException("The empire already has a pending colonisation order for this system.");
        }

        var resources = state.EmpireResources.Single(item => item.EmpireId == fleet.EmpireId);
        if (resources.Population < ColonisationPopulationCost)
        {
            throw new InvalidOperationException($"Colonisation requires {ColonisationPopulationCost:0.##} population.");
        }

        if (!HasLeadingPresence(state, cycle.CycleId, fleet.CurrentSystemId, fleet.EmpireId))
        {
            throw new InvalidOperationException("Colonisation requires the empire to have the leading influence in the system.");
        }

        return AddFleetOrder(state, cycle, fleet, FleetOrderType.Colonise, fleet.CurrentSystemId, null, now, replacesOrderId);
    }

    public static FleetOrder CancelFleetOrder(GameState state, Guid fleetOrderId, Guid requestingEmpireId, DateTimeOffset now)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var order = state.FleetOrders.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetOrderId == fleetOrderId)
            ?? throw new InvalidOperationException("Fleet order does not exist in the active cycle.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == order.FleetId)
            ?? throw new InvalidOperationException("Fleet for order does not exist in the active cycle.");
        var empire = state.Empires.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.EmpireId == requestingEmpireId)
            ?? throw new InvalidOperationException("Empire does not exist in the active cycle.");

        if (fleet.EmpireId != empire.EmpireId)
        {
            throw new InvalidOperationException("Only the owning empire can cancel this order.");
        }

        if (order.Status != FleetOrderStatus.Pending)
        {
            throw new InvalidOperationException("Only pending orders can be cancelled.");
        }

        if (cycle.CurrentTickNumber >= order.ExecuteAfterTick)
        {
            throw new InvalidOperationException("Orders can only be cancelled before their execution tick.");
        }

        order.Status = FleetOrderStatus.Cancelled;
        order.ProcessedTick = cycle.CurrentTickNumber;

        state.Events.Add(new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = cycle.CurrentTickNumber,
            EventType = EventType.OrderCancelled,
            EmpireId = empire.EmpireId,
            Severity = EventSeverity.Low,
            DisplayText = $"{empire.EmpireName} cancelled {fleet.FleetName}'s {FormatOrderType(order.OrderType)} order.",
            FactJson = JsonSerializer.Serialize(new
            {
                orderId = order.FleetOrderId,
                orderType = order.OrderType,
                fleetId = fleet.FleetId,
                empireId = empire.EmpireId
            }, GameStateJson.Options),
            CreatedAt = now
        });

        return order;
    }

    public static EmpirePriority UpdatePriorities(
        GameState state,
        Guid empireId,
        int industryWeight,
        int researchWeight,
        int militaryWeight,
        int expansionWeight,
        DateTimeOffset now)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var empire = state.Empires.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.EmpireId == empireId)
            ?? throw new InvalidOperationException("Empire does not exist in the active cycle.");

        StrategicPriorityPolicy.Validate(new EmpirePriority
        {
            IndustryWeight = industryWeight,
            ResearchWeight = researchWeight,
            MilitaryWeight = militaryWeight,
            ExpansionWeight = expansionWeight
        });

        var priority = state.EmpirePriorities.SingleOrDefault(item => item.EmpireId == empire.EmpireId);
        if (priority is null)
        {
            priority = new EmpirePriority { EmpireId = empire.EmpireId };
            state.EmpirePriorities.Add(priority);
        }

        priority.IndustryWeight = industryWeight;
        priority.ResearchWeight = researchWeight;
        priority.MilitaryWeight = militaryWeight;
        priority.ExpansionWeight = expansionWeight;
        priority.UpdatedAt = now;

        state.Events.Add(new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = cycle.CurrentTickNumber,
            EventType = EventType.PrioritiesChanged,
            EmpireId = empire.EmpireId,
            Severity = EventSeverity.Low,
            DisplayText = $"{empire.EmpireName} updated strategic priorities.",
            FactJson = JsonSerializer.Serialize(new
            {
                empire.EmpireId,
                industryWeight,
                researchWeight,
                militaryWeight,
                expansionWeight
            }, GameStateJson.Options),
            CreatedAt = now
        });

        return priority;
    }

    private static FleetOrder AddFleetOrder(
        GameState state,
        Cycle cycle,
        Fleet fleet,
        FleetOrderType orderType,
        Guid? targetSystemId,
        Guid? targetEmpireId,
        DateTimeOffset now,
        Guid? replacesOrderId)
    {
        var executeAfterTick = cycle.CurrentTickNumber + 1;
        var pendingOrders = state.FleetOrders
            .Where(item => item.CycleId == cycle.CycleId
                           && item.FleetId == fleet.FleetId
                           && item.ExecuteAfterTick == executeAfterTick
                           && item.Status == FleetOrderStatus.Pending)
            .ToArray();

        if (pendingOrders.Length > 1)
        {
            throw new FleetOrderReplacementConflictException(
                "This fleet has multiple pending orders for the same tick. Refresh after the order history is repaired.");
        }

        var pendingOrder = pendingOrders.SingleOrDefault();
        if (pendingOrder is null && replacesOrderId.HasValue)
        {
            throw new FleetOrderReplacementConflictException(
                "The pending order changed before its replacement could be confirmed. Refresh and try again.");
        }

        if (pendingOrder is not null)
        {
            if (replacesOrderId.HasValue && replacesOrderId.Value != pendingOrder.FleetOrderId)
            {
                throw new FleetOrderReplacementConflictException(
                    "The pending order changed before its replacement could be confirmed. Refresh and try again.");
            }

            if (HasSameIntent(pendingOrder, orderType, targetSystemId, targetEmpireId))
            {
                return pendingOrder;
            }

            if (!replacesOrderId.HasValue)
            {
                throw new FleetOrderReplacementConflictException(
                    "This fleet already has a pending order. Confirm its replacement and try again.");
            }
        }

        var order = new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = orderType,
            TargetSystemId = targetSystemId,
            TargetEmpireId = targetEmpireId,
            SubmitTick = cycle.CurrentTickNumber,
            ExecuteAfterTick = executeAfterTick,
            Status = FleetOrderStatus.Pending,
            CreatedAt = now
        };

        if (pendingOrder is not null)
        {
            pendingOrder.Status = FleetOrderStatus.Superseded;
            pendingOrder.ProcessedTick = cycle.CurrentTickNumber;
            pendingOrder.SupersededByOrderId = order.FleetOrderId;
        }

        state.FleetOrders.Add(order);
        return order;
    }

    private static bool HasSameIntent(
        FleetOrder order,
        FleetOrderType orderType,
        Guid? targetSystemId,
        Guid? targetEmpireId) =>
        order.OrderType == orderType
        && order.TargetSystemId == targetSystemId
        && order.TargetEmpireId == targetEmpireId;

    private static string FormatOrderType(FleetOrderType orderType) =>
        orderType switch
        {
            FleetOrderType.MoveFleet => "move",
            FleetOrderType.Hold => "hold",
            FleetOrderType.Attack => "attack",
            FleetOrderType.Colonise => "colonise",
            _ => orderType.ToString()
        };

    internal static bool HasLeadingPresence(GameState state, Guid cycleId, Guid systemId, Guid empireId)
    {
        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycleId, systemId);
        if (!presence.TryGetValue(empireId, out var empirePresence))
        {
            return false;
        }

        return presence
            .Where(item => item.Key != empireId)
            .All(item => empirePresence > item.Value);
    }

    private static (Cycle Cycle, Fleet Fleet) GetActiveFleetForOrder(GameState state, Guid fleetId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == fleetId)
            ?? throw new InvalidOperationException("Fleet does not exist in the active cycle.");

        if (fleet.Status != FleetStatus.Active || fleet.ShipCount <= 0)
        {
            throw new InvalidOperationException("Fleet is not active.");
        }

        return (cycle, fleet);
    }
}

public sealed class FleetOrderReplacementConflictException(string message) : InvalidOperationException(message);
