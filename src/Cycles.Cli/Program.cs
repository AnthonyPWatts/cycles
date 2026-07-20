using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using System.Text;

var command = args.ElementAtOrDefault(0)?.ToLowerInvariant() ?? "show";

try
{
    GameProfileCatalogue.EnsureValid();

    if (command == "db")
    {
        return RunDatabaseCommand(args);
    }

    if (command == "state")
    {
        return RunStateTransferCommand(args);
    }

    if (command == "recovery")
    {
        return RunRecoveryCommand(args);
    }

    if (command == "cycle")
    {
        return RunCycleCommand(args);
    }

    if (command == "galaxy")
    {
        return RunGalaxyCommand(args);
    }

    if (command == "balance")
    {
        return RunBalanceScenario(args);
    }

    switch (command)
    {
        case "seed":
            Seed(args, CreateRequiredGameplayStore(args, 1));
            break;
        case "tick":
            Tick(CreateRequiredGameplayStore(args, 1));
            break;
        case "show":
            Show(CreateRequiredGameplayStore(args, 1));
            break;
        case "diagnostics":
            ShowDiagnostics(CreateRequiredGameplayStore(args, 1), GetRunningTickSuspicionThreshold());
            break;
        case "move":
            SubmitMove(args, CreateRequiredGameplayStore(args, 1));
            break;
        case "attack":
            SubmitAttack(args, CreateRequiredGameplayStore(args, 1));
            break;
        case "hold":
            SubmitHold(args, CreateRequiredGameplayStore(args, 1));
            break;
        default:
            PrintUsage();
            return 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

return 0;

static void Seed(string[] args, IGameStateStore store)
{
    if (!args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase))
    {
        throw new InvalidOperationException("Seeding replaces all Cycles state in the SQL database. Re-run with --confirm-replace against the intended local or disposable target.");
    }

    var generationArgs = args.Where(argument => !argument.StartsWith("--", StringComparison.Ordinal)).ToArray();
    var useCuratedColdStart = generationArgs.Length <= 2;
    var state = useCuratedColdStart
        ? GameSeeder.CreateCuratedColdStart()
        : GameSeeder.CreateDefault(
            ParseOptionalInt(generationArgs, 2, 24),
            ParseOptionalInt(generationArgs, 3, 4),
            ParseOptionalInt(generationArgs, 4, 71421));

    store.Replace(state);
    Console.WriteLine($"Seeded {store.Description}");
    Console.WriteLine($"Scenario: {(useCuratedColdStart ? GameSeeder.CuratedColdStartScenarioKey : "custom")}");
    Console.WriteLine($"Cycle: {state.GetActiveCycle()?.Name}");
    Console.WriteLine($"Systems: {state.Systems.Count}; empires: {state.Empires.Count}; fleets: {state.Fleets.Count}");
}

static void Tick(IGameStateStore store)
{
    var result = store.RunTick(DateTimeOffset.UtcNow);

    Console.WriteLine($"Tick {result.TickNumber}: {result.Status}");
    Console.WriteLine($"Orders: {result.OrdersProcessed}; events: {result.EventsCreated}; battles: {result.BattlesCreated}; Chronicle entries: {result.ChronicleEntriesCreated}");
}

static void Show(IGameStateStore store)
{
    var state = store.LoadOrCreate();
    var cycle = GetDisplayCycle(state);

    Console.WriteLine($"{cycle.Name} | tick {cycle.CurrentTickNumber} | {cycle.Status}");
    Console.WriteLine();
    Console.WriteLine("Empires");
    foreach (var empire in state.Empires.Where(empire => empire.CycleId == cycle.CycleId).OrderBy(empire => empire.EmpireName))
    {
        var home = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
        var resources = state.EmpireResources.Single(resource => resource.EmpireId == empire.EmpireId);
        var priorities = state.EmpirePriorities.SingleOrDefault(priority => priority.EmpireId == empire.EmpireId);
        var fleetCount = state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId && fleet.Status != FleetStatus.Destroyed);
        Console.WriteLine($"- {empire.EmpireName} ({empire.EmpireId})");
        Console.WriteLine($"  Home: {home.SystemName}; fleets: {fleetCount}");
        Console.WriteLine($"  Resources: industry {resources.Industry:0.##}, research {resources.Research:0.##}, population {resources.Population:0.##}");
        if (priorities is not null)
        {
            Console.WriteLine($"  Priorities: industry {priorities.IndustryWeight}%, research {priorities.ResearchWeight}%, military {priorities.MilitaryWeight}%, expansion {priorities.ExpansionWeight}%");
        }
    }

    var admirals = state.Admirals
        .Where(admiral => admiral.CycleId == cycle.CycleId)
        .OrderByDescending(admiral => admiral.ReputationScore)
        .ThenBy(admiral => admiral.AdmiralName)
        .ToArray();
    if (admirals.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Admirals");
        foreach (var admiral in admirals)
        {
            var empire = state.Empires.Single(item => item.EmpireId == admiral.EmpireId);
            var assignedFleet = state.Fleets.SingleOrDefault(fleet => fleet.AdmiralId == admiral.AdmiralId);
            var battleCount = state.AdmiralBattleHistories.Count(history => history.AdmiralId == admiral.AdmiralId);
            var famousCount = state.AdmiralBattleHistories.Count(history => history.AdmiralId == admiral.AdmiralId && history.IsFamousSystemAssociation);
            Console.WriteLine($"- {admiral.AdmiralName} ({admiral.Status}, reputation {admiral.ReputationScore})");
            Console.WriteLine($"  {empire.EmpireName}; fleet: {assignedFleet?.FleetName ?? "unassigned"}; battles: {battleCount}; famous systems: {famousCount}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Fleets");
    foreach (var fleet in state.Fleets.Where(fleet => fleet.CycleId == cycle.CycleId).OrderBy(fleet => fleet.FleetName))
    {
        var faction = state.GetFleetFaction(fleet);
        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        var destination = fleet.DestinationSystemId.HasValue
            ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value).SystemName
            : null;
        var admiral = fleet.AdmiralId.HasValue
            ? state.Admirals.SingleOrDefault(item => item.AdmiralId == fleet.AdmiralId.Value)
            : null;
        Console.WriteLine($"- {fleet.FleetName} ({fleet.FleetId})");
        Console.WriteLine($"  {faction.FactionName}; {fleet.ShipCount} ships; {fleet.Status}; at {system.SystemName}{(destination is null ? "" : $" -> {destination} on tick {fleet.ArrivalTickNumber}")}");
        if (admiral is not null)
        {
            Console.WriteLine($"  Admiral: {admiral.AdmiralName}; reputation {admiral.ReputationScore}; {admiral.Status}");
        }
    }

    var constructions = state.ShipConstructions
        .Where(construction => construction.CycleId == cycle.CycleId)
        .OrderBy(construction => construction.Status == ShipConstructionStatus.Queued ? 0 : 1)
        .ThenBy(construction => construction.CompleteAfterTick)
        .ThenBy(construction => construction.StartedTick)
        .Take(12)
        .ToArray();
    if (constructions.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Ship construction");
        foreach (var construction in constructions)
        {
            var empire = state.Empires.Single(item => item.EmpireId == construction.EmpireId);
            var timing = construction.CompletedTick.HasValue
                ? $"completed tick {construction.CompletedTick.Value}"
                : $"completes after tick {construction.CompleteAfterTick}";
            Console.WriteLine($"- {empire.EmpireName}: {construction.ShipCount} ship(s), {construction.Status}, started tick {construction.StartedTick}, {timing}, industry {construction.IndustrySpent:0.##}");
        }
    }

    Console.WriteLine();
    Console.WriteLine("Recent events");
    foreach (var item in state.Events.Where(item => item.CycleId == cycle.CycleId).OrderByDescending(item => item.CreatedAt).Take(8).Reverse())
    {
        Console.WriteLine($"- T{item.TickNumber}: {item.DisplayText}");
    }

    if (state.ChronicleEntries.Any(entry => entry.CycleId == cycle.CycleId))
    {
        Console.WriteLine();
        Console.WriteLine("Chronicle");
        foreach (var entry in state.ChronicleEntries.Where(entry => entry.CycleId == cycle.CycleId).OrderByDescending(entry => entry.ImportanceScore))
        {
            Console.WriteLine($"- {entry.Title} ({entry.ImportanceScore}): {entry.FactualSummary}");
        }
    }

    if (state.CycleRankings.Any(ranking => ranking.CycleId == cycle.CycleId))
    {
        Console.WriteLine();
        Console.WriteLine("Final rankings");
        foreach (var ranking in state.CycleRankings.Where(ranking => ranking.CycleId == cycle.CycleId).OrderBy(ranking => ranking.Rank))
        {
            var empire = state.Empires.Single(item => item.EmpireId == ranking.EmpireId);
            var winnerText = ranking.IsWinner ? " winner" : "";
            Console.WriteLine($"- #{ranking.Rank}{winnerText}: {empire.EmpireName} ({ranking.MapControlPercent:0.##}% map control, {ranking.ActiveShipCount} active ships)");
        }
    }

    var historicalSignals = state.SystemHistoricalSignals
        .Where(signal => signal.CycleId == cycle.CycleId)
        .OrderByDescending(signal => signal.HistoricalSignificanceIncrease)
        .ThenBy(signal => state.Systems.Single(system => system.SystemId == signal.SystemId).SystemName)
        .ToArray();
    if (historicalSignals.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("System history signals");
        foreach (var signal in historicalSignals)
        {
            var system = state.Systems.Single(item => item.SystemId == signal.SystemId);
            var battleText = signal.BattleCount == 1 ? "1 battle" : $"{signal.BattleCount} battles";
            Console.WriteLine($"- {system.SystemName}: {battleText}, {signal.TotalLosses} losses, +{signal.HistoricalSignificanceIncrease} history");
        }
    }
}

static void ShowDiagnostics(IGameStateStore store, TimeSpan runningTickSuspicionThreshold)
{
    var diagnostics = OperationalDiagnosticsService.Create(
        store.LoadOrCreate(),
        DateTimeOffset.UtcNow,
        runningTickSuspicionThreshold);

    Console.WriteLine($"Store: {store.Description}");
    if (!diagnostics.CycleId.HasValue)
    {
        Console.WriteLine("Cycle: none");
        return;
    }

    Console.WriteLine($"Cycle: {diagnostics.CycleName} ({diagnostics.CycleId})");
    Console.WriteLine($"Status: {diagnostics.CycleStatus}; current tick: {diagnostics.CurrentTickNumber}; cadence: {diagnostics.TickLengthMinutes} minute(s)");
    Console.WriteLine($"Last completed: {FormatDateTime(diagnostics.LastCompletedAt)}; next due: {FormatDateTime(diagnostics.NextDueAt)}; due now: {diagnostics.IsTickDue}");
    Console.WriteLine($"Tick logs: {diagnostics.CompletedTickLogs} completed; {diagnostics.FailedTickLogs} failed; {diagnostics.RunningTickLogs} running");
    Console.WriteLine($"Orders: {diagnostics.PendingOrders} pending; {diagnostics.OrdersDueNextTick} due by next tick");
    Console.WriteLine($"Construction: {diagnostics.QueuedShipConstructions} queued; {diagnostics.ConstructionsDueNextTick} due by next tick");
    Console.WriteLine($"Running-tick suspicion threshold: {diagnostics.RunningTickSuspicionThreshold.TotalMinutes:0.##} minute(s) (diagnostic only)");
    if (diagnostics.SuspiciousRunningTicks.Count > 0)
    {
        Console.WriteLine("Suspicious persisted running attempts");
        foreach (var attempt in diagnostics.SuspiciousRunningTicks)
        {
            Console.WriteLine($"- {attempt.CycleName} ({attempt.CycleId}) tick {attempt.TickNumber}; attempt {attempt.TickLogId}");
            Console.WriteLine($"  Started: {attempt.StartedAt:u}; age: {FormatDuration(attempt.Elapsed)}");
            if (attempt.DiagnosticContext is not null)
            {
                Console.WriteLine($"  Context: {attempt.DiagnosticContext}");
            }
        }
        Console.WriteLine("Inspection required: age alone never fails, retries, repairs, or cancels an attempt. Use recovery abandon only after confirming the attempt is abandoned.");
    }

    if (diagnostics.RecentFinishedTicks.Count > 0)
    {
        Console.WriteLine("Recent authoritative attempt durations");
        foreach (var attempt in diagnostics.RecentFinishedTicks)
        {
            Console.WriteLine($"- {attempt.CycleName} tick {attempt.TickNumber}; attempt {attempt.TickLogId}; {attempt.Status}; {FormatDuration(attempt.Elapsed)}");
        }
    }
    if (diagnostics.RequiresRecovery)
    {
        Console.WriteLine("Action required: inspect recovery details, repair the underlying state, then clear or retry recovery with an operator and reason.");
    }
}

static int RunCycleCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    switch (subcommand)
    {
        case "end":
            {
                var requestedCycleId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
                var store = CreateRequiredSqlServerStore(args, 2);
                var cycleId = requestedCycleId ?? store.GetRequired().CycleId;
                var rankings = store.CompleteCycle(cycleId, DateTimeOffset.UtcNow);

                var winner = rankings.Single(ranking => ranking.IsWinner);
                Console.WriteLine($"Cycle ended at tick {winner.CutoffTickNumber}.");
                Console.WriteLine($"Winner: {winner.EmpireId} with {winner.MapControlPercent:0.##}% map control.");
                Console.WriteLine("Final rankings:");
                foreach (var ranking in rankings.OrderBy(ranking => ranking.Rank))
                {
                    var winnerText = ranking.IsWinner ? " winner" : "";
                    Console.WriteLine($"- #{ranking.Rank}{winnerText}: {ranking.EmpireId} ({ranking.MapControlPercent:0.##}% map control, {ranking.ActiveShipCount} active ships)");
                }

                return 0;
            }

        case "next":
            {
                var requestedCycleId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
                var seed = args.Length > 4 ? int.Parse(args[4]) : (int?)null;
                var store = CreateRequiredSqlServerStore(args, 2);
                var result = store.Update(state =>
                {
                    var cycleId = requestedCycleId
                        ?? state.Cycles
                            .Where(cycle => cycle.Status == CycleStatus.Completed)
                            .OrderByDescending(cycle => cycle.EndAt)
                            .ThenByDescending(cycle => cycle.CreatedAt)
                            .FirstOrDefault()
                            ?.CycleId
                        ?? throw new InvalidOperationException("No completed cycle exists.");
                    return CycleContinuityService.GenerateNextCycle(state, cycleId, DateTimeOffset.UtcNow, seed);
                });

                Console.WriteLine($"Generated successor Cycle: {result.CycleId}");
                Console.WriteLine($"Source Cycle: {result.SourceCycleId}");
                Console.WriteLine($"Seed: {result.Seed}");
                Console.WriteLine($"Successor empires: {result.SuccessorEmpires.Count}");
                Console.WriteLine($"Preserved systems: {result.PreservedSystems.Count}");
                foreach (var system in result.PreservedSystems.OrderBy(system => system.SystemName))
                {
                    Console.WriteLine($"- {system.SystemName}: history {system.HistoricalSignificance}, strategic value {system.StrategicValue}");
                }

                return 0;
            }

        default:
            PrintUsage();
            return 2;
    }
}

static int RunBalanceScenario(string[] args)
{
    var compare = string.Equals(args.ElementAtOrDefault(1), "compare", StringComparison.OrdinalIgnoreCase);
    var offset = compare ? 1 : 0;
    var options = new BalanceScenarioOptions(
        TickCount: ParseOptionalInt(args, 1 + offset, 48),
        SystemCount: ParseOptionalInt(args, 2 + offset, 24),
        EmpireCount: ParseOptionalInt(args, 3 + offset, 4),
        Seed: ParseOptionalInt(args, 4 + offset, 71421),
        Strategy: compare ? BalanceScenarioStrategy.Balanced : ParseBalanceStrategy(args.ElementAtOrDefault(5)));

    if (compare)
    {
        var comparison = BalanceScenarioRunner.Compare(options);
        Console.WriteLine($"Balance comparison | seed {options.Seed} | {options.TickCount} ticks | {options.SystemCount} systems | {options.EmpireCount} empires");
        Console.WriteLine("Strategy | Orders | Battles | Colonies | Ships built | Map gap | Active ships | Industry");
        foreach (var comparisonResult in comparison)
        {
            Console.WriteLine(
                $"{comparisonResult.Options.Strategy} | {comparisonResult.OrdersProcessed:N0} | {comparisonResult.Battles:N0} | {comparisonResult.ColonialOutposts:N0} | "
                + $"{comparisonResult.CompletedShips:N0} | {comparisonResult.MapControlGap:0.##} | {FormatRange(comparisonResult.Empires.Select(empire => (decimal)empire.ActiveShips))} | "
                + FormatRange(comparisonResult.Empires.Select(empire => empire.Industry)));
        }

        return 0;
    }

    var result = BalanceScenarioRunner.Run(options);

    Console.WriteLine($"Balance scenario | {options.Strategy} | seed {options.Seed} | {options.TickCount} ticks | {options.SystemCount} systems | {options.EmpireCount} empires");
    Console.WriteLine($"Rendezvous: {result.RendezvousSystem}");
    Console.WriteLine($"Orders: {result.OrdersProcessed}; battles: {result.Battles}; colonies: {result.ColonialOutposts}; Chronicle entries: {result.ChronicleEntries}");
    Console.WriteLine($"Completed ship construction: {result.CompletedShipConstructions} batches / {result.CompletedShips} ships; doctrine unlocks: {result.DoctrineUnlocks}; map-control gap: {result.MapControlGap:0.##} points");
    Console.WriteLine($"Retained records: {result.RetainedRecords:N0}");
    Console.WriteLine($"Timing: order planning {result.OrderPlanningMilliseconds:0.00} ms; tick processing {result.TickProcessingMilliseconds:0.00} ms");
    if (result.StopReason is not null)
    {
        Console.WriteLine($"Partial run: {result.StopReason}");
    }
    Console.WriteLine();
    Console.WriteLine("Empire | Strategy | Ships | Growth | Map | Colonies | Industry | Research | Population | W-L");
    foreach (var empire in result.Empires)
    {
        Console.WriteLine($"{empire.EmpireName} | {empire.Strategy} | {empire.ActiveShips} | {empire.ShipGrowthFactor:0.00}x | {empire.MapControlPercent:0.00}% | {empire.ColonialOutposts} | {empire.Industry:0.00} | {empire.Research:0.00} | {empire.Population:0.00} | {empire.BattlesWon}-{empire.BattlesLost}");
    }

    return 0;
}

static BalanceScenarioStrategy ParseBalanceStrategy(string? value) =>
    value?.ToLowerInvariant() switch
    {
        null or "balanced" => BalanceScenarioStrategy.Balanced,
        "military" => BalanceScenarioStrategy.Military,
        "expansion" => BalanceScenarioStrategy.Expansion,
        "cautious" => BalanceScenarioStrategy.Cautious,
        "mixed" => BalanceScenarioStrategy.Mixed,
        _ => throw new ArgumentException("Balance strategy must be balanced, military, expansion, cautious, or mixed.")
    };

static string FormatRange(IEnumerable<decimal> values)
{
    var materialised = values.ToArray();
    return $"{materialised.Min():0.##}-{materialised.Max():0.##}";
}

static int RunRecoveryCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    if (!IsRecoverySubcommand(subcommand))
    {
        ShowRecovery(CreateRequiredSqlServerStore(args, 1), showDetails: false);
        return 0;
    }

    switch (subcommand)
    {
        case "details":
            {
                ShowRecovery(CreateRequiredSqlServerStore(args, 2), showDetails: true);
                return 0;
            }

        case "clear":
            {
                var store = CreateRequiredSqlServerStore(args, 2);
                var cycleId = ParseRequiredGuid(args, 3, "cycle id");
                var operatorName = ParseRequiredOption(args, "--operator");
                var reason = ParseRequiredOption(args, "--reason");
                var recoveryEvent = store.ClearRecovery(cycleId, operatorName, reason, DateTimeOffset.UtcNow);

                Console.WriteLine($"Recovery cleared for cycle {cycleId}.");
                Console.WriteLine($"Audit event: {recoveryEvent.EventId}");
                return 0;
            }

        case "retry":
            {
                var store = CreateRequiredSqlServerStore(args, 2);
                var cycleId = ParseRequiredGuid(args, 3, "cycle id");
                var operatorName = ParseRequiredOption(args, "--operator");
                var reason = ParseRequiredOption(args, "--reason");
                var now = DateTimeOffset.UtcNow;
                var result = store.RetryRecovery(cycleId, operatorName, reason, now);

                Console.WriteLine($"Retry tick {result.TickNumber}: {result.Status}");
                Console.WriteLine($"Orders: {result.OrdersProcessed}; events: {result.EventsCreated}; battles: {result.BattlesCreated}; Chronicle entries: {result.ChronicleEntriesCreated}");
                return 0;
            }

        case "abandon":
            {
                var store = CreateRequiredSqlServerStore(args, 2);
                var tickLogId = ParseRequiredGuid(args, 3, "tick attempt id");
                var operatorName = ParseRequiredOption(args, "--operator");
                var reason = ParseRequiredOption(args, "--reason");
                var auditEvent = store.MarkTickAbandoned(
                    tickLogId,
                    operatorName,
                    reason,
                    DateTimeOffset.UtcNow,
                    GetRunningTickSuspicionThreshold());

                Console.WriteLine($"Tick attempt {tickLogId} was marked abandoned.");
                Console.WriteLine($"Audit event: {auditEvent.EventId}");
                Console.WriteLine("The Cycle remains recovery-required. Repair the cause, then use recovery clear or recovery retry.");
                return 0;
            }

        default:
            PrintUsage();
            return 2;
    }
}

static void ShowRecovery(IGameStateStore store, bool showDetails)
{
    var state = store.LoadOrCreate();
    var recoveryCycles = state.Cycles
        .Where(cycle => cycle.Status == CycleStatus.RecoveryRequired)
        .OrderBy(cycle => cycle.Name)
        .ToArray();
    var concerningLogs = state.TickLogs
        .Where(log => log.Status is TickLogStatus.Failed or TickLogStatus.Running)
        .OrderByDescending(log => log.StartedAt)
        .ToArray();

    if (recoveryCycles.Length == 0 && concerningLogs.Length == 0)
    {
        Console.WriteLine("No recovery-required cycles or unfinished/failed tick logs.");
        return;
    }

    if (recoveryCycles.Length > 0)
    {
        Console.WriteLine("Recovery-required cycles");
        foreach (var cycle in recoveryCycles)
        {
            Console.WriteLine($"- {cycle.Name} ({cycle.CycleId})");
            Console.WriteLine($"  Current tick: {cycle.CurrentTickNumber}; status: {cycle.Status}");
        }
    }

    if (concerningLogs.Length > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Unfinished or failed tick logs");
        foreach (var log in concerningLogs)
        {
            var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == log.CycleId);
            Console.WriteLine($"- {cycle?.Name ?? log.CycleId.ToString()} tick {log.TickNumber}: {log.Status}");
            Console.WriteLine($"  Started: {log.StartedAt:u}; completed: {FormatCompletedAt(log.CompletedAt)}");
            if (!string.IsNullOrWhiteSpace(log.DiagnosticLog))
            {
                Console.WriteLine($"  Diagnostic: {FormatDiagnostic(log.DiagnosticLog, showDetails)}");
            }
        }
    }
}

static void SubmitMove(string[] args, IGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var targetSystemId = ParseRequiredGuid(args, 3, "target system id");
    var confirmReplacement = args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase);
    var order = store.UpdateActiveCycleExclusively(state => OrderService.SubmitMoveOrder(
        state,
        fleetId,
        targetSystemId,
        DateTimeOffset.UtcNow,
        GetConfirmedReplacementOrderId(state, fleetId, confirmReplacement)));
    Console.WriteLine($"Move order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitAttack(string[] args, IGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var targetEmpireArgument = args.ElementAtOrDefault(3);
    var targetEmpireId = targetEmpireArgument is not null && !targetEmpireArgument.StartsWith("--", StringComparison.Ordinal)
        ? Guid.Parse(targetEmpireArgument)
        : (Guid?)null;
    var confirmReplacement = args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase);
    var order = store.UpdateActiveCycleExclusively(state => OrderService.SubmitAttackOrder(
        state,
        fleetId,
        targetEmpireId,
        DateTimeOffset.UtcNow,
        GetConfirmedReplacementOrderId(state, fleetId, confirmReplacement)));
    Console.WriteLine($"Attack order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitHold(string[] args, IGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var confirmReplacement = args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase);
    var order = store.UpdateActiveCycleExclusively(state => OrderService.SubmitHoldOrder(
        state,
        fleetId,
        DateTimeOffset.UtcNow,
        GetConfirmedReplacementOrderId(state, fleetId, confirmReplacement)));
    Console.WriteLine($"Hold order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static Guid? GetConfirmedReplacementOrderId(GameState state, Guid fleetId, bool confirmReplacement)
{
    if (!confirmReplacement)
    {
        return null;
    }

    var cycle = state.GetActiveCycle()
        ?? throw new InvalidOperationException("No active Cycle exists.");
    var pendingOrders = state.FleetOrders
        .Where(order => order.CycleId == cycle.CycleId
                        && order.FleetId == fleetId
                        && order.ExecuteAfterTick == cycle.CurrentTickNumber + 1
                        && order.Status == FleetOrderStatus.Pending)
        .ToArray();

    return pendingOrders.Length switch
    {
        0 => null,
        1 => pendingOrders[0].FleetOrderId,
        _ => throw new InvalidOperationException(
            "This fleet has multiple pending orders for the same tick. Repair the order history before replacing one.")
    };
}

static int RunDatabaseCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    var connectionString = ParseRequiredSqlServerConnectionString(args, 2);
    var migrator = new SqlServerMigrator(connectionString);

    switch (subcommand)
    {
        case "init":
        case "migrate":
            var appliedMigrations = migrator.Migrate();
            if (appliedMigrations.Count == 0)
            {
                Console.WriteLine("Database schema is current.");
                return 0;
            }

            Console.WriteLine($"Applied {appliedMigrations.Count} migration(s):");
            foreach (var migration in appliedMigrations)
            {
                Console.WriteLine($"- {migration.MigrationId}: {migration.Description}");
            }

            return 0;

        case "status":
            var status = migrator.GetStatus();
            Console.WriteLine(status.DatabaseExists
                ? "Database exists."
                : "Database does not exist.");
            Console.WriteLine($"Applied migrations: {status.AppliedMigrationIds.Count}");

            if (status.PendingMigrations.Count == 0)
            {
                Console.WriteLine("Pending migrations: none");
                return 0;
            }

            Console.WriteLine($"Pending migrations: {status.PendingMigrations.Count}");
            foreach (var migration in status.PendingMigrations)
            {
                Console.WriteLine($"- {migration.MigrationId}: {migration.Description}");
            }

            return 0;

        case "profile":
            if (!args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Database profiling replaces all Cycles state in the target database. Re-run with --confirm-replace against a disposable database.");
            }

            migrator.Migrate();
            var profileOptions = new SqlServerStateProfileOptions(
                SystemCount: ParseOptionalInt(args, 3, GameSeeder.CanonicalGalaxySystemCount),
                EmpireCount: ParseOptionalInt(args, 4, 4),
                HistoryTicks: ParseOptionalInt(args, 5, 0),
                Iterations: ParseOptionalInt(args, 6, 3),
                Seed: ParseOptionalInt(args, 7, 71421));
            var profile = SqlServerStateProfiler.Run(connectionString, profileOptions);
            Console.WriteLine($"SQL state profile | {profileOptions.SystemCount} systems | {profileOptions.EmpireCount} empires | {profileOptions.HistoryTicks} history ticks | {profileOptions.Iterations} iteration(s)");
            Console.WriteLine("Iteration | Records | Replace ms | Load ms | Generic update ms | Focused tick ms");
            foreach (var sample in profile.Samples)
            {
                Console.WriteLine($"{sample.Iteration} | {sample.RetainedRecords} | {sample.ReplaceMilliseconds:0.00} | {sample.LoadMilliseconds:0.00} | {sample.GenericUpdateMilliseconds:0.00} | {sample.FocusedTickMilliseconds:0.00}");
            }

            Console.WriteLine($"Average | - | {profile.AverageReplaceMilliseconds:0.00} | {profile.AverageLoadMilliseconds:0.00} | {profile.AverageGenericUpdateMilliseconds:0.00} | {profile.AverageFocusedTickMilliseconds:0.00}");
            return 0;

        default:
            PrintUsage();
            return 2;
    }
}

static int RunGalaxyCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    switch (subcommand)
    {
        case "upgrade":
        {
            if (!args.Contains("--confirm-upgrade", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Galaxy topology upgrade changes the active Cycle map in place. Review the target and re-run with --confirm-upgrade.");
            }

            var connectionString = ParseRequiredSqlServerConnectionString(args, 2);
            EnsureDatabaseSchemaCurrent(connectionString);
            var store = new SqlServerGameStateStore(connectionString, () => new GameState());
            var result = store.UpdateActiveCycleExclusively(state =>
            {
                var activeCycles = state.Cycles.Where(item => item.Status == CycleStatus.Active).ToArray();
                if (activeCycles.Length != 1)
                {
                    throw new InvalidOperationException($"Galaxy topology upgrade requires exactly one active Cycle; found {activeCycles.Length}.");
                }

                if (state.Cycles.Any(item => item.Status == CycleStatus.RecoveryRequired)
                    || state.TickLogs.Any(item => item.Status == TickLogStatus.Running))
                {
                    throw new InvalidOperationException("Galaxy topology upgrade is unavailable while a Cycle or tick attempt requires recovery.");
                }

                return GameSeeder.UpgradeGalaxyTopology(state);
            });

            if (!result.Changed)
            {
                Console.WriteLine($"Galaxy topology is already current in {store.Description}.");
                return 0;
            }

            Console.WriteLine($"Upgraded galaxy topology in {store.Description}.");
            Console.WriteLine($"Added {result.SectorsAdded} sector(s), {result.SystemsAdded} system(s), and {result.LinksAdded} route(s); removed {result.LinksRemoved} superseded route(s).");
            return 0;
        }

        case "write-seed-script":
        {
            var outputPath = Path.GetFullPath(ParseRequiredArgument(args, 2, "output SQL path"));
            var confirmOverwrite = args.Contains("--confirm-overwrite", StringComparer.OrdinalIgnoreCase);
            if (File.Exists(outputPath) && !confirmOverwrite)
            {
                throw new InvalidOperationException($"Output file '{outputPath}' already exists. Re-run with --confirm-overwrite to replace it.");
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                File.WriteAllText(tempPath, SqlServerDevelopmentSeedScript.Generate(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                File.Move(tempPath, outputPath, overwrite: confirmOverwrite);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            Console.WriteLine($"Wrote the canonical curated SQL seed to '{outputPath}'.");
            return 0;
        }

        default:
            PrintUsage();
            return 2;
    }
}

static int RunStateTransferCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    switch (subcommand)
    {
        case "validate":
        {
            var inputPath = Path.GetFullPath(ParseRequiredArgument(args, 2, "input JSON path"));
            using var input = File.OpenRead(inputPath);
            var document = GameStateTransfer.Read(input);
            Console.WriteLine($"Valid state transfer format {document.FormatVersion}: {GameStateTransfer.CountRecords(document.State)} record(s).");
            Console.WriteLine($"Exported at: {document.ExportedAt:u}");
            return 0;
        }

        case "export":
        {
            var connectionString = ParseRequiredSqlServerConnectionString(args, 2);
            var outputPath = Path.GetFullPath(ParseRequiredArgument(args, 3, "output JSON path"));
            var confirmOverwrite = args.Contains("--confirm-overwrite", StringComparer.OrdinalIgnoreCase);
            if (File.Exists(outputPath) && !confirmOverwrite)
            {
                throw new InvalidOperationException($"Output file '{outputPath}' already exists. Re-run with --confirm-overwrite to replace it.");
            }

            EnsureDatabaseSchemaCurrent(connectionString);
            var store = new SqlServerGameStateStore(connectionString, () => new GameState());
            var state = store.LoadOrCreate();
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    GameStateTransfer.Write(output, state, DateTimeOffset.UtcNow);
                }

                File.Move(tempPath, outputPath, overwrite: confirmOverwrite);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            Console.WriteLine($"Exported {GameStateTransfer.CountRecords(state)} record(s) from {store.Description} to '{outputPath}'.");
            Console.WriteLine("Sensitive export: restrict access, transfer securely, retain only as required, then delete it securely. This is not a database backup.");
            return 0;
        }

        case "convert-runtime-file":
        {
            var inputPath = Path.GetFullPath(ParseRequiredArgument(args, 2, "legacy runtime JSON path"));
            var outputPath = Path.GetFullPath(ParseRequiredArgument(args, 3, "output JSON path"));
            var confirmOverwrite = args.Contains("--confirm-overwrite", StringComparer.OrdinalIgnoreCase);
            if (File.Exists(outputPath) && !confirmOverwrite)
            {
                throw new InvalidOperationException($"Output file '{outputPath}' already exists. Re-run with --confirm-overwrite to replace it.");
            }

            GameState state;
            using (var input = File.OpenRead(inputPath))
            {
                state = GameStateTransfer.ReadLegacyRuntimeState(input);
            }

            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var tempPath = $"{outputPath}.{Guid.NewGuid():N}.tmp";
            try
            {
                using (var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
                {
                    GameStateTransfer.Write(output, state, DateTimeOffset.UtcNow);
                }

                File.Move(tempPath, outputPath, overwrite: confirmOverwrite);
            }
            finally
            {
                if (File.Exists(tempPath))
                {
                    File.Delete(tempPath);
                }
            }

            Console.WriteLine($"Converted and validated {GameStateTransfer.CountRecords(state)} record(s) from legacy runtime state to '{outputPath}'.");
            Console.WriteLine("Sensitive export: restrict access, transfer securely, retain only as required, then delete it securely. This is not a database backup.");
            return 0;
        }

        case "import":
        {
            if (!args.Contains("--confirm-import", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("State import replaces authoritative SQL state. Review the target and re-run with --confirm-import.");
            }

            var inputPath = Path.GetFullPath(ParseRequiredArgument(args, 2, "input JSON path"));
            using var input = File.OpenRead(inputPath);
            var document = GameStateTransfer.Read(input);
            CurrentRuntimeGameScope.EnsureSupportedForOperationalImport(document.State);
            var connectionString = ParseRequiredSqlServerConnectionString(args, 3);
            new SqlServerMigrator(connectionString).Migrate();
            var store = new SqlServerGameStateStore(connectionString, () => new GameState());
            var existing = store.LoadOrCreate();
            var existingRecordCount = GameStateTransfer.CountRecords(existing);
            if (existingRecordCount > 0 && !args.Contains("--confirm-replace", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Target {store.Description} contains {existingRecordCount} state record(s). Re-run with --confirm-replace only after confirming they may be destroyed.");
            }

            store.Replace(document.State);
            var reloaded = store.LoadOrCreate();
            var reloadedValidation = GameStateTransfer.Validate(reloaded);
            if (!reloadedValidation.IsValid || GameStateTransfer.CountRecords(reloaded) != GameStateTransfer.CountRecords(document.State))
            {
                throw new InvalidOperationException("SQL state did not pass post-import validation. Treat the target as requiring operator investigation.");
            }

            Console.WriteLine($"Imported and verified {GameStateTransfer.CountRecords(reloaded)} record(s) into {store.Description}.");
            Console.WriteLine("The input contains private cross-empire and identity data; retain or delete it according to the operator handling policy.");
            return 0;
        }

        default:
            PrintUsage();
            return 2;
    }
}

static void EnsureDatabaseSchemaCurrent(string connectionString)
{
    var status = new SqlServerMigrator(connectionString).GetStatus();
    if (!status.DatabaseExists || status.PendingMigrations.Count > 0)
    {
        throw new InvalidOperationException("The SQL database is missing or has pending migrations. Run 'db migrate' first.");
    }
}

static string ParseRequiredSqlServerConnectionString(string[] args, int index)
{
    if (args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
    {
        throw new InvalidOperationException("Missing SQL Server connection string. Use sqlserver:<connectionString>.");
    }

    const string prefix = "sqlserver:";
    var value = args[index].Trim();
    if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
    {
        var connectionString = value[prefix.Length..].Trim();
        return connectionString.Length > 0
            ? connectionString
            : throw new InvalidOperationException("The SQL Server store specifier contains no connection string.");
    }

    if (value.Contains('=', StringComparison.Ordinal))
    {
        return value;
    }

    throw new InvalidOperationException("Expected a SQL Server store specifier. File-backed gameplay state is no longer supported; use sqlserver:<connectionString>.");
}

static SqlServerGameStateStore CreateRequiredSqlServerStore(
    string[] args,
    int index,
    Func<GameState>? seedFactory = null) =>
    new(ParseRequiredSqlServerConnectionString(args, index), seedFactory);

static SqlServerGameStateStore CreateRequiredGameplayStore(string[] args, int index) =>
    CreateRequiredSqlServerStore(args, index, () => GameSeeder.CreateCuratedColdStart());

static string ParseRequiredArgument(string[] args, int index, string label)
{
    if (args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
    {
        throw new InvalidOperationException($"Missing {label}.");
    }

    return args[index];
}

static string ParseRequiredOption(string[] args, string optionName)
{
    var optionIndex = Array.FindIndex(args, arg => string.Equals(arg, optionName, StringComparison.OrdinalIgnoreCase));
    if (optionIndex < 0 || optionIndex + 1 >= args.Length || string.IsNullOrWhiteSpace(args[optionIndex + 1]))
    {
        throw new InvalidOperationException($"Missing {optionName} value.");
    }

    return args[optionIndex + 1];
}

static bool IsRecoverySubcommand(string? value) =>
    value is "details" or "clear" or "retry" or "abandon";

static Cycle GetDisplayCycle(GameState state) =>
    state.GetActiveCycle()
    ?? state.Cycles
        .OrderByDescending(cycle => cycle.StartAt)
        .FirstOrDefault()
    ?? throw new InvalidOperationException("No cycle exists.");

static int ParseOptionalInt(string[] args, int index, int defaultValue) =>
    args.Length > index ? int.Parse(args[index]) : defaultValue;

static Guid ParseRequiredGuid(string[] args, int index, string label)
{
    if (args.Length <= index)
    {
        throw new InvalidOperationException($"Missing {label}.");
    }

    return Guid.Parse(args[index]);
}

static string FormatCompletedAt(DateTimeOffset? completedAt) =>
    completedAt.HasValue ? completedAt.Value.ToString("u") : "not completed";

static string FormatDateTime(DateTimeOffset? value) =>
    value.HasValue ? value.Value.ToString("u") : "none";

static string FormatDiagnostic(string diagnosticLog, bool showDetails) =>
    showDetails
        ? diagnosticLog.Trim()
        : diagnosticLog
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?? diagnosticLog.Trim();

static TimeSpan GetRunningTickSuspicionThreshold()
{
    const string variableName = "CYCLES_RUNNING_TICK_SUSPICION_MINUTES";
    var configured = Environment.GetEnvironmentVariable(variableName);
    if (string.IsNullOrWhiteSpace(configured))
    {
        return OperationalDiagnosticsService.DefaultRunningTickSuspicionThreshold;
    }

    if (!double.TryParse(configured, out var minutes) || minutes <= 0)
    {
        throw new InvalidOperationException($"{variableName} must be a positive number of minutes.");
    }

    return TimeSpan.FromMinutes(minutes);
}

static string FormatDuration(TimeSpan duration) =>
    duration.TotalMinutes >= 1
        ? $"{duration.TotalMinutes:0.##} minute(s)"
        : $"{duration.TotalSeconds:0.##} second(s)";

static void PrintUsage()
{
    Console.WriteLine("Cycles CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- seed <sqlserver:connectionString> [systemCount] [empireCount] [seed] --confirm-replace");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- show <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- diagnostics <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- tick <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- balance [tickCount] [systemCount] [empireCount] [seed] [balanced|military|expansion|cautious|mixed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- balance compare [tickCount] [systemCount] [empireCount] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- cycle end <sqlserver:connectionString> [cycleId]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- cycle next <sqlserver:connectionString> [completedCycleId] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- galaxy upgrade <sqlserver:connectionString> --confirm-upgrade");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- galaxy write-seed-script <output.sql> [--confirm-overwrite]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery details <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery clear <sqlserver:connectionString> <cycleId> --operator <name> --reason <reason>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery retry <sqlserver:connectionString> <cycleId> --operator <name> --reason <reason>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery abandon <sqlserver:connectionString> <tickAttemptId> --operator <name> --reason <reason>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- move <sqlserver:connectionString> <fleetId> <targetSystemId> [--confirm-replace]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- attack <sqlserver:connectionString> <fleetId> [targetEmpireId] [--confirm-replace]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- hold <sqlserver:connectionString> <fleetId> [--confirm-replace]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db init <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db migrate <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db status <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db profile <sqlserver:connectionString> [systemCount] [empireCount] [historyTicks] [iterations] [seed] --confirm-replace");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- state export <sqlserver:connectionString> <output.json> [--confirm-overwrite]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- state convert-runtime-file <input.json> <output.json> [--confirm-overwrite]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- state validate <input.json>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- state import <input.json> <sqlserver:connectionString> --confirm-import [--confirm-replace]");
    Console.WriteLine();
    Console.WriteLine("Gameplay and operator commands require SQL Server. JSON is available only through the explicit state transfer commands.");
}
