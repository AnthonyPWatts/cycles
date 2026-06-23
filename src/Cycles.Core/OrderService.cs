using System.Text.Json;

namespace Cycles.Core;

public static class OrderService
{
    public static FleetOrder SubmitMoveOrder(GameState state, Guid fleetId, Guid targetSystemId, DateTimeOffset now)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);
        var target = state.Systems.SingleOrDefault(system => system.CycleId == cycle.CycleId && system.SystemId == targetSystemId)
            ?? throw new InvalidOperationException("Target system does not exist in the active cycle.");

        if (!state.SystemLinks.Any(link => link.CycleId == cycle.CycleId && link.Connects(fleet.CurrentSystemId, target.SystemId)))
        {
            throw new InvalidOperationException("Move orders must target an adjacent linked system.");
        }

        return AddFleetOrder(state, cycle, fleet, FleetOrderType.MoveFleet, target.SystemId, null, now);
    }

    public static FleetOrder SubmitAttackOrder(GameState state, Guid fleetId, Guid? targetEmpireId, DateTimeOffset now)
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

        return AddFleetOrder(state, cycle, fleet, FleetOrderType.Attack, null, targetEmpireId, now);
    }

    public static FleetOrder SubmitHoldOrder(GameState state, Guid fleetId, DateTimeOffset now)
    {
        var (cycle, fleet) = GetActiveFleetForOrder(state, fleetId);
        return AddFleetOrder(state, cycle, fleet, FleetOrderType.Hold, null, null, now);
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

        var weights = new[] { industryWeight, researchWeight, militaryWeight, expansionWeight };
        if (weights.Any(weight => weight < 0))
        {
            throw new InvalidOperationException("Priority weights cannot be negative.");
        }

        if (weights.Sum() == 0)
        {
            throw new InvalidOperationException("At least one priority weight must be greater than zero.");
        }

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
        DateTimeOffset now)
    {
        var order = new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = orderType,
            TargetSystemId = targetSystemId,
            TargetEmpireId = targetEmpireId,
            SubmitTick = cycle.CurrentTickNumber,
            ExecuteAfterTick = cycle.CurrentTickNumber + 1,
            Status = FleetOrderStatus.Pending,
            CreatedAt = now
        };

        state.FleetOrders.Add(order);
        return order;
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
