using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

var command = args.ElementAtOrDefault(0)?.ToLowerInvariant() ?? "show";

try
{
    if (command == "db")
    {
        return RunDatabaseCommand(args);
    }

    if (command == "recovery")
    {
        return RunRecoveryCommand(args);
    }

    if (command == "cycle")
    {
        return RunCycleCommand(args);
    }

    if (command == "balance")
    {
        return RunBalanceScenario(args);
    }

    var statePath = args.ElementAtOrDefault(1) ?? Path.Combine("data", "cycles-state.json");
    var store = GameStateStoreFactory.Create(statePath);

    switch (command)
    {
        case "seed":
            Seed(args, store);
            break;
        case "tick":
            Tick(store);
            break;
        case "show":
            Show(store);
            break;
        case "diagnostics":
            ShowDiagnostics(store);
            break;
        case "move":
            SubmitMove(args, store);
            break;
        case "attack":
            SubmitAttack(args, store);
            break;
        case "hold":
            SubmitHold(args, store);
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
    var systemCount = ParseOptionalInt(args, 2, 24);
    var empireCount = ParseOptionalInt(args, 3, 4);
    var seed = ParseOptionalInt(args, 4, 71421);
    var state = GameSeeder.CreateDefault(systemCount, empireCount, seed);

    store.Replace(state);
    Console.WriteLine($"Seeded {store.Description}");
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
        var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        var destination = fleet.DestinationSystemId.HasValue
            ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value).SystemName
            : null;
        var admiral = fleet.AdmiralId.HasValue
            ? state.Admirals.SingleOrDefault(item => item.AdmiralId == fleet.AdmiralId.Value)
            : null;
        Console.WriteLine($"- {fleet.FleetName} ({fleet.FleetId})");
        Console.WriteLine($"  {empire.EmpireName}; {fleet.ShipCount} ships; {fleet.Status}; at {system.SystemName}{(destination is null ? "" : $" -> {destination} on tick {fleet.ArrivalTickNumber}")}");
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

static void ShowDiagnostics(IGameStateStore store)
{
    var diagnostics = OperationalDiagnosticsService.Create(store.LoadOrCreate(), DateTimeOffset.UtcNow);

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
                var statePath = args.ElementAtOrDefault(2) ?? Path.Combine("data", "cycles-state.json");
                var requestedCycleId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
                var store = GameStateStoreFactory.Create(statePath);
                var rankings = store.Update(state =>
                {
                    var cycleId = requestedCycleId
                        ?? state.GetActiveCycle()?.CycleId
                        ?? throw new InvalidOperationException("No active cycle exists.");
                    return CycleEndService.CompleteCycle(state, cycleId, DateTimeOffset.UtcNow);
                });

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
                var statePath = args.ElementAtOrDefault(2) ?? Path.Combine("data", "cycles-state.json");
                var requestedCycleId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
                var seed = args.Length > 4 ? int.Parse(args[4]) : (int?)null;
                var store = GameStateStoreFactory.Create(statePath);
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
    var options = new BalanceScenarioOptions(
        TickCount: ParseOptionalInt(args, 1, 48),
        SystemCount: ParseOptionalInt(args, 2, 24),
        EmpireCount: ParseOptionalInt(args, 3, 4),
        Seed: ParseOptionalInt(args, 4, 71421));
    var result = BalanceScenarioRunner.Run(options);

    Console.WriteLine($"Balance scenario | seed {options.Seed} | {options.TickCount} ticks | {options.SystemCount} systems | {options.EmpireCount} empires");
    Console.WriteLine($"Rendezvous: {result.RendezvousSystem}");
    Console.WriteLine($"Orders: {result.OrdersProcessed}; battles: {result.Battles}; colonies: {result.ColonialOutposts}; Chronicle entries: {result.ChronicleEntries}");
    Console.WriteLine($"Completed ship construction: {result.CompletedShipConstructions}; doctrine unlocks: {result.DoctrineUnlocks}; map-control gap: {result.MapControlGap:0.##} points");
    Console.WriteLine($"Retained records: {result.RetainedRecords:N0}");
    Console.WriteLine($"Timing: order planning {result.OrderPlanningMilliseconds:0.00} ms; tick processing {result.TickProcessingMilliseconds:0.00} ms");
    if (result.StopReason is not null)
    {
        Console.WriteLine($"Partial run: {result.StopReason}");
    }
    Console.WriteLine();
    Console.WriteLine("Empire | Ships | Growth | Map | Colonies | Industry | Research | Population | W-L");
    foreach (var empire in result.Empires)
    {
        Console.WriteLine($"{empire.EmpireName} | {empire.ActiveShips} | {empire.ShipGrowthFactor:0.00}x | {empire.MapControlPercent:0.00}% | {empire.ColonialOutposts} | {empire.Industry:0.00} | {empire.Research:0.00} | {empire.Population:0.00} | {empire.BattlesWon}-{empire.BattlesLost}");
    }

    return 0;
}

static int RunRecoveryCommand(string[] args)
{
    var subcommand = args.ElementAtOrDefault(1)?.ToLowerInvariant();
    if (!IsRecoverySubcommand(subcommand))
    {
        var statePath = args.ElementAtOrDefault(1) ?? Path.Combine("data", "cycles-state.json");
        ShowRecovery(GameStateStoreFactory.Create(statePath), showDetails: false);
        return 0;
    }

    switch (subcommand)
    {
        case "details":
            {
                var statePath = args.ElementAtOrDefault(2) ?? Path.Combine("data", "cycles-state.json");
                ShowRecovery(GameStateStoreFactory.Create(statePath), showDetails: true);
                return 0;
            }

        case "clear":
            {
                var store = GameStateStoreFactory.Create(ParseRequiredArgument(args, 2, "state path"));
                var cycleId = ParseRequiredGuid(args, 3, "cycle id");
                var operatorName = ParseRequiredOption(args, "--operator");
                var reason = ParseRequiredOption(args, "--reason");
                var recoveryEvent = store.Update(state => RecoveryService.ClearRecovery(state, cycleId, operatorName, reason, DateTimeOffset.UtcNow));

                Console.WriteLine($"Recovery cleared for cycle {cycleId}.");
                Console.WriteLine($"Audit event: {recoveryEvent.EventId}");
                return 0;
            }

        case "retry":
            {
                var store = GameStateStoreFactory.Create(ParseRequiredArgument(args, 2, "state path"));
                var cycleId = ParseRequiredGuid(args, 3, "cycle id");
                var operatorName = ParseRequiredOption(args, "--operator");
                var reason = ParseRequiredOption(args, "--reason");
                var now = DateTimeOffset.UtcNow;
                var result = store.Update(state =>
                {
                    RecoveryService.ClearRecovery(state, cycleId, operatorName, reason, now);
                    return new TickEngine().RunTick(state, cycleId, now);
                });

                Console.WriteLine($"Retry tick {result.TickNumber}: {result.Status}");
                Console.WriteLine($"Orders: {result.OrdersProcessed}; events: {result.EventsCreated}; battles: {result.BattlesCreated}; Chronicle entries: {result.ChronicleEntriesCreated}");
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
    var order = store.Update(state => OrderService.SubmitMoveOrder(state, fleetId, targetSystemId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Move order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitAttack(string[] args, IGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var targetEmpireId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
    var order = store.Update(state => OrderService.SubmitAttackOrder(state, fleetId, targetEmpireId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Attack order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitHold(string[] args, IGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var order = store.Update(state => OrderService.SubmitHoldOrder(state, fleetId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Hold order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
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
                SystemCount: ParseOptionalInt(args, 3, 24),
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

static string ParseRequiredSqlServerConnectionString(string[] args, int index)
{
    if (args.Length <= index || string.IsNullOrWhiteSpace(args[index]))
    {
        throw new InvalidOperationException("Missing SQL Server connection string. Use sqlserver:<connectionString>.");
    }

    return GameStateStoreFactory.TryParseSqlServerSpecifier(args[index], out var parsedConnectionString)
        ? parsedConnectionString
        : args[index];
}

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
    value is "details" or "clear" or "retry";

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

static void PrintUsage()
{
    Console.WriteLine("Cycles CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- seed [statePath] [systemCount] [empireCount] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- show [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- diagnostics [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- tick [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- balance [tickCount] [systemCount] [empireCount] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- cycle end [statePath] [cycleId]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- cycle next [statePath] [completedCycleId] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery details [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery clear <statePath> <cycleId> --operator <name> --reason <reason>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- recovery retry <statePath> <cycleId> --operator <name> --reason <reason>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- move [statePath] <fleetId> <targetSystemId>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- attack [statePath] <fleetId> [targetEmpireId]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- hold [statePath] <fleetId>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db init <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db migrate <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db status <sqlserver:connectionString>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- db profile <sqlserver:connectionString> [systemCount] [empireCount] [historyTicks] [iterations] [seed] --confirm-replace");
    Console.WriteLine();
    Console.WriteLine("Use sqlserver:<connectionString> instead of a state path to read and write SQL Server state.");
}
