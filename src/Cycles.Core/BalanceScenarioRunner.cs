using System.Security.Cryptography;
using System.Text;

namespace Cycles.Core;

public sealed record BalanceScenarioOptions(
    int TickCount = 48,
    int SystemCount = 24,
    int EmpireCount = 4,
    int Seed = 71421,
    int RetainedRecordLimit = 15_000);

public sealed record BalanceEmpireResult(
    string EmpireName,
    int InitialShips,
    int ActiveShips,
    decimal ShipGrowthFactor,
    decimal Industry,
    decimal Research,
    decimal Population,
    int ColonialOutposts,
    decimal MapControlPercent,
    int BattlesWon,
    int BattlesLost);

public sealed record BalanceScenarioResult(
    BalanceScenarioOptions Options,
    string RendezvousSystem,
    int CompletedTicks,
    int OrdersProcessed,
    int Battles,
    int ChronicleEntries,
    int ColonialOutposts,
    int CompletedShipConstructions,
    int DoctrineUnlocks,
    decimal MapControlGap,
    int RetainedRecords,
    string? StopReason,
    IReadOnlyList<BalanceEmpireResult> Empires);

public static class BalanceScenarioRunner
{
    private const int ExpeditionShipCount = 30;
    private const int MinimumHomeShipsToLaunch = 60;
    private static readonly DateTimeOffset ScenarioStart = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static BalanceScenarioResult Run(BalanceScenarioOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.TickCount, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.EmpireCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RetainedRecordLimit, 100);
        if (options.SystemCount < options.EmpireCount)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "There must be at least one system per empire.");
        }

        var state = GameSeeder.CreateDeterministicScenario(
            options.SystemCount,
            options.EmpireCount,
            options.Seed,
            ScenarioStart);
        var cycle = state.GetActiveCycle()!;
        CreateExpeditionFleets(state, cycle.CycleId, options.Seed);
        var rendezvous = SelectRendezvousSystem(state, cycle.CycleId);
        var initialShips = state.Empires.ToDictionary(
            empire => empire.EmpireId,
            empire => ActiveShipCount(state, empire.EmpireId));
        var ordersProcessed = 0;
        string? stopReason = null;
        var engine = new TickEngine();

        for (var tick = 1; tick <= options.TickCount; tick++)
        {
            var retainedRecords = GameStateRecordCounter.CountCycleRecords(state, cycle.CycleId);
            if (tick > 1 && retainedRecords >= options.RetainedRecordLimit)
            {
                stopReason = $"Stopped before tick {tick} because retained simulation records reached {retainedRecords:N0}, above the configured limit of {options.RetainedRecordLimit:N0}.";
                break;
            }

            var now = ScenarioStart.AddMinutes(cycle.TickLengthMinutes * tick);
            LaunchAvailableExpeditions(state, cycle.CycleId, options.Seed, now);
            SubmitScenarioOrders(state, cycle.CycleId, rendezvous.SystemId, now);
            var tickResult = engine.RunTick(state, cycle.CycleId, now);
            if (tickResult.Status != TickLogStatus.Completed)
            {
                throw new InvalidOperationException($"Balance scenario tick {tick} failed.");
            }

            ordersProcessed += tickResult.OrdersProcessed;
        }

        var completedCycle = state.Cycles.Single(item => item.CycleId == cycle.CycleId);
        var finalMetrics = state.EmpireMetrics
            .Where(metric => metric.CycleId == cycle.CycleId && metric.TickNumber == completedCycle.CurrentTickNumber)
            .ToDictionary(metric => metric.EmpireId);
        var empireResults = state.Empires
            .Where(empire => empire.CycleId == cycle.CycleId)
            .OrderBy(empire => empire.EmpireName)
            .Select(empire => CreateEmpireResult(state, empire, initialShips[empire.EmpireId], finalMetrics[empire.EmpireId]))
            .ToArray();
        var mapControlValues = empireResults.Select(result => result.MapControlPercent).ToArray();

        return new BalanceScenarioResult(
            options,
            rendezvous.SystemName,
            completedCycle.CurrentTickNumber,
            ordersProcessed,
            state.BattleRecords.Count(battle => battle.CycleId == cycle.CycleId),
            state.ChronicleEntries.Count(entry => entry.CycleId == cycle.CycleId),
            state.ColonialOutposts.Count(outpost => outpost.CycleId == cycle.CycleId),
            state.ShipConstructions.Count(construction => construction.CycleId == cycle.CycleId
                                                        && construction.Status == ShipConstructionStatus.Completed),
            state.Events.Count(item => item.CycleId == cycle.CycleId && item.EventType == EventType.DoctrineUnlocked),
            mapControlValues.Max() - mapControlValues.Min(),
            GameStateRecordCounter.CountCycleRecords(state, cycle.CycleId),
            stopReason,
            empireResults);
    }

    private static void SubmitScenarioOrders(GameState state, Guid cycleId, Guid rendezvousSystemId, DateTimeOffset now)
    {
        foreach (var fleet in state.Fleets
                     .Where(fleet => fleet.CycleId == cycleId
                                     && fleet.FleetName.Contains(" Scenario Expedition ", StringComparison.Ordinal)
                                     && fleet.Status == FleetStatus.Active
                                     && fleet.ShipCount > 0)
                     .OrderBy(fleet => state.Empires.Single(empire => empire.EmpireId == fleet.EmpireId).EmpireName)
                     .ThenBy(fleet => fleet.FleetName))
        {
            if (state.FleetOrders.Any(order => order.FleetId == fleet.FleetId && order.Status == FleetOrderStatus.Pending))
            {
                continue;
            }

            var hostileEmpire = state.Fleets
                .Where(other => other.CycleId == cycleId
                                && other.CurrentSystemId == fleet.CurrentSystemId
                                && other.EmpireId != fleet.EmpireId
                                && other.Status == FleetStatus.Active
                                && other.ShipCount > 0)
                .Select(other => state.Empires.Single(empire => empire.EmpireId == other.EmpireId))
                .OrderBy(empire => empire.EmpireName)
                .FirstOrDefault();
            if (hostileEmpire is not null)
            {
                OrderService.SubmitAttackOrder(state, fleet.FleetId, hostileEmpire.EmpireId, now);
                continue;
            }

            var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
            var resources = state.EmpireResources.Single(item => item.EmpireId == fleet.EmpireId);
            var empireFleetIds = state.Fleets
                .Where(item => item.CycleId == cycleId && item.EmpireId == fleet.EmpireId)
                .Select(item => item.FleetId)
                .ToHashSet();
            var canColonise = fleet.CurrentSystemId != empire.HomeSystemId
                              && resources.Population >= OrderService.ColonisationPopulationCost
                              && !state.ColonialOutposts.Any(item => item.EmpireId == fleet.EmpireId
                                                                    && item.SystemId == fleet.CurrentSystemId)
                              && !state.FleetOrders.Any(item => item.CycleId == cycleId
                                                                && item.OrderType == FleetOrderType.Colonise
                                                                && item.Status == FleetOrderStatus.Pending
                                                                && item.TargetSystemId == fleet.CurrentSystemId
                                                                && empireFleetIds.Contains(item.FleetId))
                              && OrderService.HasLeadingPresence(state, cycleId, fleet.CurrentSystemId, fleet.EmpireId);
            if (canColonise)
            {
                OrderService.SubmitColoniseOrder(state, fleet.FleetId, now);
                continue;
            }

            if (fleet.CurrentSystemId == rendezvousSystemId)
            {
                continue;
            }

            var nextSystemId = FindNextHop(state, cycleId, fleet.CurrentSystemId, rendezvousSystemId);
            OrderService.SubmitMoveOrder(state, fleet.FleetId, nextSystemId, now);
        }
    }

    private static void CreateExpeditionFleets(GameState state, Guid cycleId, int seed)
    {
        LaunchAvailableExpeditions(state, cycleId, seed, ScenarioStart);
    }

    private static void LaunchAvailableExpeditions(GameState state, Guid cycleId, int seed, DateTimeOffset now)
    {
        foreach (var empire in state.Empires.Where(empire => empire.CycleId == cycleId).OrderBy(empire => empire.EmpireName))
        {
            var homeFleet = state.Fleets
                .Where(fleet => fleet.EmpireId == empire.EmpireId
                                && fleet.CurrentSystemId == empire.HomeSystemId
                                && fleet.Status == FleetStatus.Active
                                && string.Equals(
                                    fleet.FleetName,
                                    $"{empire.EmpireName} Home Fleet",
                                    StringComparison.Ordinal))
                .OrderBy(fleet => fleet.CreatedAt)
                .FirstOrDefault();
            if (homeFleet is null || homeFleet.ShipCount < MinimumHomeShipsToLaunch)
            {
                continue;
            }

            var wave = state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId
                                                   && fleet.FleetName.Contains(" Scenario Expedition ", StringComparison.Ordinal)) + 1;
            homeFleet.ShipCount -= ExpeditionShipCount;
            state.Fleets.Add(new Fleet
            {
                FleetId = CreateDeterministicId(seed, $"{empire.EmpireId:N}:expedition:{wave}"),
                CycleId = cycleId,
                EmpireId = empire.EmpireId,
                FleetName = $"{empire.EmpireName} Scenario Expedition {wave:000}",
                CurrentSystemId = empire.HomeSystemId,
                ShipCount = ExpeditionShipCount,
                Status = FleetStatus.Active,
                CreatedAt = now
            });
        }
    }

    private static Guid CreateDeterministicId(int seed, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"cycles-balance:{seed}:{value}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static GalaxySystem SelectRendezvousSystem(GameState state, Guid cycleId)
    {
        var homes = state.Empires
            .Where(empire => empire.CycleId == cycleId)
            .Select(empire => state.Systems.Single(system => system.SystemId == empire.HomeSystemId))
            .ToArray();

        return state.Systems
            .Where(system => system.CycleId == cycleId)
            .OrderBy(system => homes.Sum(home => SquaredDistance(system, home)))
            .ThenByDescending(system => system.StrategicValue)
            .ThenBy(system => system.SystemName)
            .First();
    }

    private static Guid FindNextHop(GameState state, Guid cycleId, Guid startSystemId, Guid targetSystemId)
    {
        var names = state.Systems
            .Where(system => system.CycleId == cycleId)
            .ToDictionary(system => system.SystemId, system => system.SystemName);
        var links = state.SystemLinks.Where(link => link.CycleId == cycleId).ToArray();
        var queue = new Queue<Guid>();
        var previous = new Dictionary<Guid, Guid?> { [startSystemId] = null };
        queue.Enqueue(startSystemId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            foreach (var neighbour in links
                         .Where(link => link.SystemAId == current || link.SystemBId == current)
                         .Select(link => link.SystemAId == current ? link.SystemBId : link.SystemAId)
                         .Distinct()
                         .OrderBy(systemId => names[systemId]))
            {
                if (previous.ContainsKey(neighbour))
                {
                    continue;
                }

                previous[neighbour] = current;
                if (neighbour == targetSystemId)
                {
                    return TraceFirstHop(previous, startSystemId, targetSystemId);
                }

                queue.Enqueue(neighbour);
            }
        }

        throw new InvalidOperationException("The seeded galaxy graph is not connected.");
    }

    private static Guid TraceFirstHop(IReadOnlyDictionary<Guid, Guid?> previous, Guid startSystemId, Guid targetSystemId)
    {
        var current = targetSystemId;
        while (previous[current] != startSystemId)
        {
            current = previous[current]
                ?? throw new InvalidOperationException("The target system cannot be traced to the starting system.");
        }

        return current;
    }

    private static BalanceEmpireResult CreateEmpireResult(
        GameState state,
        Empire empire,
        int initialShips,
        EmpireMetric finalMetric)
    {
        var activeShips = ActiveShipCount(state, empire.EmpireId);
        var resources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
        var battles = state.BattleRecords.Where(battle => battle.AttackerEmpireId == empire.EmpireId
                                                          || battle.DefenderEmpireId == empire.EmpireId).ToArray();
        return new BalanceEmpireResult(
            empire.EmpireName,
            initialShips,
            activeShips,
            initialShips == 0 ? 0 : decimal.Round((decimal)activeShips / initialShips, 2),
            resources.Industry,
            resources.Research,
            resources.Population,
            state.ColonialOutposts.Count(item => item.EmpireId == empire.EmpireId),
            finalMetric.MapControlPercent,
            battles.Count(battle => WonBattle(battle, empire.EmpireId)),
            battles.Count(battle => LostBattle(battle, empire.EmpireId)));
    }

    private static bool WonBattle(BattleRecord battle, Guid empireId) =>
        (battle.AttackerEmpireId == empireId && battle.Outcome == BattleOutcome.AttackerVictory)
        || (battle.DefenderEmpireId == empireId && battle.Outcome == BattleOutcome.DefenderVictory);

    private static bool LostBattle(BattleRecord battle, Guid empireId) =>
        (battle.AttackerEmpireId == empireId && battle.Outcome == BattleOutcome.DefenderVictory)
        || (battle.DefenderEmpireId == empireId && battle.Outcome == BattleOutcome.AttackerVictory);

    private static int ActiveShipCount(GameState state, Guid empireId) =>
        state.Fleets
            .Where(fleet => fleet.EmpireId == empireId && fleet.Status != FleetStatus.Destroyed)
            .Sum(fleet => fleet.ShipCount);

    private static long SquaredDistance(GalaxySystem first, GalaxySystem second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return ((long)x * x) + ((long)y * y);
    }
}
