using System.Security.Cryptography;
using System.Text;

namespace Cycles.Core;

internal static class GameAiPlanner
{
    internal const decimal MinimumAttackStrengthRatio = 1.25m;

    public static IReadOnlyList<FleetOrder> Plan(
        GameState state,
        Guid cycleId,
        int tickNumber,
        DateTimeOffset now,
        IReadOnlyCollection<Guid> fleetIds)
    {
        var cycle = state.Cycles.Single(item => item.CycleId == cycleId);
        var requestedFleetIds = fleetIds.ToHashSet();
        var aiEmpires = state.Empires
            .Where(empire => empire.CycleId == cycleId
                             && empire.Status == EmpireStatus.Active
                             && state.Players.Single(player => player.PlayerId == empire.PlayerId).Kind == PlayerKind.AI)
            .OrderBy(empire => empire.EmpireId)
            .ToArray();
        var orders = new List<FleetOrder>();

        foreach (var empire in aiEmpires)
        {
            var availableFleets = state.Fleets
                .Where(fleet => requestedFleetIds.Contains(fleet.FleetId)
                                && fleet.CycleId == cycleId
                                && fleet.EmpireId == empire.EmpireId
                                && fleet.Status == FleetStatus.Active
                                && fleet.ShipCount > 0)
                .OrderBy(fleet => fleet.FleetId)
                .ToArray();
            if (availableFleets.Length == 0)
            {
                continue;
            }

            var assignedFleetIds = new HashSet<Guid>();
            var fleetsHoldingAgainstLocalThreats = PlanLocalCombat(
                state,
                cycle,
                empire,
                availableFleets,
                assignedFleetIds,
                orders,
                tickNumber,
                now);

            PlanColonisation(
                state,
                cycle,
                empire,
                availableFleets,
                assignedFleetIds,
                fleetsHoldingAgainstLocalThreats,
                orders,
                tickNumber,
                now);

            PlanExpansionMoves(
                state,
                cycle,
                empire,
                availableFleets,
                assignedFleetIds,
                fleetsHoldingAgainstLocalThreats,
                orders,
                tickNumber,
                now);
        }

        return orders
            .OrderBy(order => order.FleetId)
            .ThenBy(order => order.FleetOrderId)
            .ToArray();
    }

    private static HashSet<Guid> PlanLocalCombat(
        GameState state,
        Cycle cycle,
        Empire empire,
        IReadOnlyCollection<Fleet> availableFleets,
        HashSet<Guid> assignedFleetIds,
        ICollection<FleetOrder> orders,
        int tickNumber,
        DateTimeOffset now)
    {
        var ownFactionId = state.GetEmpireFaction(empire.EmpireId).FactionId;
        var fleetsHoldingAgainstLocalThreats = new HashSet<Guid>();

        foreach (var localFleets in availableFleets
                     .GroupBy(fleet => fleet.CurrentSystemId)
                     .OrderBy(group => group.Key))
        {
            var attackableTargets = state.Fleets
                .Where(fleet => fleet.CycleId == cycle.CycleId
                                && fleet.CurrentSystemId == localFleets.Key
                                && fleet.Status == FleetStatus.Active
                                && fleet.ShipCount > 0
                                && state.GetFactionId(fleet) != ownFactionId)
                .GroupBy(state.GetFactionId)
                .Select(group => new LocalTarget(
                    group.Key,
                    state.Factions.Single(faction => faction.FactionId == group.Key).EmpireId,
                    group.Sum(fleet => fleet.ShipCount)))
                .Where(target => MayAttack(state, cycle.CycleId, empire.EmpireId, target.EmpireId))
                .OrderBy(target => target.ShipCount)
                .ThenBy(target => target.FactionId)
                .ToArray();
            if (attackableTargets.Length == 0)
            {
                continue;
            }

            var attackers = localFleets.OrderBy(fleet => fleet.FleetId).ToArray();
            var attackerShips = attackers.Sum(fleet => fleet.ShipCount);
            var target = attackableTargets.FirstOrDefault(item =>
                attackerShips >= item.ShipCount * MinimumAttackStrengthRatio);
            if (target is null)
            {
                fleetsHoldingAgainstLocalThreats.UnionWith(attackers.Select(fleet => fleet.FleetId));
                continue;
            }

            foreach (var fleet in attackers)
            {
                orders.Add(CreateOrder(
                    cycle,
                    fleet,
                    FleetOrderType.Attack,
                    targetSystemId: null,
                    target.EmpireId,
                    target.FactionId,
                    tickNumber,
                    now));
                assignedFleetIds.Add(fleet.FleetId);
            }
        }

        return fleetsHoldingAgainstLocalThreats;
    }

    private static void PlanColonisation(
        GameState state,
        Cycle cycle,
        Empire empire,
        IReadOnlyCollection<Fleet> availableFleets,
        HashSet<Guid> assignedFleetIds,
        IReadOnlySet<Guid> fleetsHoldingAgainstLocalThreats,
        ICollection<FleetOrder> orders,
        int tickNumber,
        DateTimeOffset now)
    {
        var resources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
        var coloniesAvailable = (int)decimal.Floor(resources.Population / OrderService.ColonisationPopulationCost);
        if (coloniesAvailable == 0)
        {
            return;
        }

        var existingOutpostSystemIds = state.ColonialOutposts
            .Where(outpost => outpost.CycleId == cycle.CycleId && outpost.EmpireId == empire.EmpireId)
            .Select(outpost => outpost.SystemId)
            .ToHashSet();
        var candidates = availableFleets
            .Where(fleet => !assignedFleetIds.Contains(fleet.FleetId)
                            && !fleetsHoldingAgainstLocalThreats.Contains(fleet.FleetId)
                            && fleet.CurrentSystemId != empire.HomeSystemId
                            && !existingOutpostSystemIds.Contains(fleet.CurrentSystemId)
                            && OrderService.HasLeadingPresence(state, cycle.CycleId, fleet.CurrentSystemId, empire.EmpireId))
            .GroupBy(fleet => fleet.CurrentSystemId)
            .Select(group => new
            {
                System = state.Systems.Single(system => system.SystemId == group.Key),
                Fleet = group.OrderBy(fleet => fleet.FleetId).First()
            })
            .OrderByDescending(candidate => CalculateSystemValue(candidate.System))
            .ThenBy(candidate => candidate.System.SystemId)
            .ThenBy(candidate => candidate.Fleet.FleetId)
            .Take(coloniesAvailable)
            .ToArray();

        foreach (var candidate in candidates)
        {
            orders.Add(CreateOrder(
                cycle,
                candidate.Fleet,
                FleetOrderType.Colonise,
                candidate.System.SystemId,
                targetEmpireId: null,
                targetFactionId: null,
                tickNumber,
                now));
            assignedFleetIds.Add(candidate.Fleet.FleetId);
        }
    }

    private static void PlanExpansionMoves(
        GameState state,
        Cycle cycle,
        Empire empire,
        IReadOnlyCollection<Fleet> availableFleets,
        IReadOnlySet<Guid> assignedFleetIds,
        IReadOnlySet<Guid> fleetsHoldingAgainstLocalThreats,
        ICollection<FleetOrder> orders,
        int tickNumber,
        DateTimeOffset now)
    {
        var occupiedOrClaimedSystemIds = state.Fleets
            .Where(fleet => fleet.CycleId == cycle.CycleId
                            && fleet.EmpireId == empire.EmpireId
                            && fleet.Status != FleetStatus.Destroyed)
            .SelectMany(fleet => fleet.DestinationSystemId.HasValue
                ? new[] { fleet.CurrentSystemId, fleet.DestinationSystemId.Value }
                : new[] { fleet.CurrentSystemId })
            .ToHashSet();
        var establishedSystemIds = state.ColonialOutposts
            .Where(outpost => outpost.CycleId == cycle.CycleId && outpost.EmpireId == empire.EmpireId)
            .Select(outpost => outpost.SystemId)
            .Append(empire.HomeSystemId)
            .ToHashSet();
        var reservedObjectiveIds = new HashSet<Guid>();

        foreach (var fleet in availableFleets.OrderBy(item => item.FleetId))
        {
            if (assignedFleetIds.Contains(fleet.FleetId)
                || fleetsHoldingAgainstLocalThreats.Contains(fleet.FleetId))
            {
                continue;
            }

            var paths = CalculatePaths(state, cycle.CycleId, fleet.CurrentSystemId);
            var objective = state.Systems
                .Where(system => system.CycleId == cycle.CycleId
                                 && system.SystemId != fleet.CurrentSystemId
                                 && !establishedSystemIds.Contains(system.SystemId)
                                 && !occupiedOrClaimedSystemIds.Contains(system.SystemId)
                                 && !reservedObjectiveIds.Contains(system.SystemId)
                                 && paths.TryGetValue(system.SystemId, out var path)
                                 && path.FirstHopSystemId.HasValue)
                .Select(system => new
                {
                    System = system,
                    Path = paths[system.SystemId]
                })
                .OrderByDescending(candidate => CalculateSystemValue(candidate.System))
                .ThenBy(candidate => candidate.Path.TravelTicks)
                .ThenBy(candidate => candidate.System.SystemId)
                .FirstOrDefault();
            if (objective is null)
            {
                continue;
            }

            orders.Add(CreateOrder(
                cycle,
                fleet,
                FleetOrderType.MoveFleet,
                objective.Path.FirstHopSystemId!.Value,
                targetEmpireId: null,
                targetFactionId: null,
                tickNumber,
                now));
            reservedObjectiveIds.Add(objective.System.SystemId);
            occupiedOrClaimedSystemIds.Add(objective.System.SystemId);
        }
    }

    private static Dictionary<Guid, RoutePath> CalculatePaths(GameState state, Guid cycleId, Guid originSystemId)
    {
        var cycleSystems = state.Systems
            .Where(system => system.CycleId == cycleId)
            .Select(system => system.SystemId)
            .ToHashSet();
        var links = state.SystemLinks
            .Where(link => link.CycleId == cycleId)
            .ToArray();
        var paths = cycleSystems.ToDictionary(
            systemId => systemId,
            systemId => systemId == originSystemId
                ? new RoutePath(0, null)
                : new RoutePath(int.MaxValue, null));
        var unvisited = cycleSystems.ToHashSet();

        while (unvisited.Count > 0)
        {
            var current = unvisited
                .OrderBy(systemId => paths[systemId].TravelTicks)
                .ThenBy(systemId => systemId)
                .First();
            var currentPath = paths[current];
            if (currentPath.TravelTicks == int.MaxValue)
            {
                break;
            }

            unvisited.Remove(current);
            foreach (var neighbour in links
                         .Where(link => link.SystemAId == current || link.SystemBId == current)
                         .Select(link => new
                         {
                             SystemId = link.SystemAId == current ? link.SystemBId : link.SystemAId,
                             link.TravelTicks
                         })
                         .Where(item => unvisited.Contains(item.SystemId))
                         .OrderBy(item => item.SystemId))
            {
                var candidate = new RoutePath(
                    currentPath.TravelTicks + neighbour.TravelTicks,
                    current == originSystemId ? neighbour.SystemId : currentPath.FirstHopSystemId);
                var existing = paths[neighbour.SystemId];
                if (candidate.TravelTicks < existing.TravelTicks
                    || (candidate.TravelTicks == existing.TravelTicks
                        && CompareNullableGuids(candidate.FirstHopSystemId, existing.FirstHopSystemId) < 0))
                {
                    paths[neighbour.SystemId] = candidate;
                }
            }
        }

        return paths;
    }

    private static bool MayAttack(GameState state, Guid cycleId, Guid attackerEmpireId, Guid? defenderEmpireId)
    {
        if (!defenderEmpireId.HasValue)
        {
            return true;
        }

        return DiplomacyService.GetState(state, cycleId, attackerEmpireId, defenderEmpireId.Value)
            is DiplomaticRelationshipState.Neutral or DiplomaticRelationshipState.War;
    }

    private static decimal CalculateSystemValue(GalaxySystem system) =>
        (system.StrategicValue * 10m)
        + system.IndustryOutput
        + system.ResearchOutput
        + system.PopulationOutput
        + (system.HistoricalSignificance * 5m);

    private static FleetOrder CreateOrder(
        Cycle cycle,
        Fleet fleet,
        FleetOrderType orderType,
        Guid? targetSystemId,
        Guid? targetEmpireId,
        Guid? targetFactionId,
        int tickNumber,
        DateTimeOffset now) =>
        new()
        {
            FleetOrderId = CreateDeterministicOrderId(
                cycle.CycleId,
                tickNumber,
                fleet.FleetId,
                orderType,
                targetSystemId,
                targetEmpireId,
                targetFactionId),
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = orderType,
            TargetSystemId = targetSystemId,
            TargetEmpireId = targetEmpireId,
            TargetFactionId = targetFactionId,
            SubmitTick = cycle.CurrentTickNumber,
            ExecuteAfterTick = tickNumber,
            Status = FleetOrderStatus.Pending,
            CommandSource = FleetOrderCommandSource.GameAiPlanner,
            CreatedAt = now
        };

    private static Guid CreateDeterministicOrderId(
        Guid cycleId,
        int tickNumber,
        Guid fleetId,
        FleetOrderType orderType,
        Guid? targetSystemId,
        Guid? targetEmpireId,
        Guid? targetFactionId)
    {
        var input = Encoding.UTF8.GetBytes(string.Join(
            ':',
            "cycles",
            "game-ai",
            cycleId.ToString("D"),
            tickNumber,
            fleetId.ToString("D"),
            orderType,
            targetSystemId?.ToString("D") ?? "-",
            targetEmpireId?.ToString("D") ?? "-",
            targetFactionId?.ToString("D") ?? "-"));
        var hash = SHA256.HashData(input);
        Span<byte> guidBytes = stackalloc byte[16];
        hash.AsSpan(0, guidBytes.Length).CopyTo(guidBytes);
        guidBytes[7] = (byte)((guidBytes[7] & 0x0F) | 0x50);
        guidBytes[8] = (byte)((guidBytes[8] & 0x3F) | 0x80);
        return new Guid(guidBytes);
    }

    private static int CompareNullableGuids(Guid? first, Guid? second)
    {
        if (!first.HasValue)
        {
            return second.HasValue ? -1 : 0;
        }

        return second.HasValue ? first.Value.CompareTo(second.Value) : 1;
    }

    private sealed record LocalTarget(Guid FactionId, Guid? EmpireId, int ShipCount);

    private sealed record RoutePath(int TravelTicks, Guid? FirstHopSystemId);
}
