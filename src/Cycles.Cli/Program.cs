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
    var result = store is SqlServerGameStateStore sqlStore
        ? sqlStore.RunTick(DateTimeOffset.UtcNow)
        : store.Update(state =>
        {
            var cycle = state.GetActiveCycle()
                ?? throw new InvalidOperationException("No active cycle exists.");
            return new TickEngine().RunTick(state, cycle.CycleId, DateTimeOffset.UtcNow);
        });

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

    Console.WriteLine();
    Console.WriteLine("Fleets");
    foreach (var fleet in state.Fleets.Where(fleet => fleet.CycleId == cycle.CycleId).OrderBy(fleet => fleet.FleetName))
    {
        var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        var destination = fleet.DestinationSystemId.HasValue
            ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value).SystemName
            : null;
        Console.WriteLine($"- {fleet.FleetName} ({fleet.FleetId})");
        Console.WriteLine($"  {empire.EmpireName}; {fleet.ShipCount} ships; {fleet.Status}; at {system.SystemName}{(destination is null ? "" : $" -> {destination} on tick {fleet.ArrivalTickNumber}")}");
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

        default:
            PrintUsage();
            return 2;
    }
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
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- tick [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- cycle end [statePath] [cycleId]");
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
    Console.WriteLine();
    Console.WriteLine("Use sqlserver:<connectionString> instead of a state path to read and write SQL Server state.");
}
