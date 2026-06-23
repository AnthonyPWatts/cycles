using Cycles.Core;

var command = args.ElementAtOrDefault(0)?.ToLowerInvariant() ?? "show";
var statePath = args.ElementAtOrDefault(1) ?? Path.Combine("data", "cycles-state.json");
var store = new FileGameStateStore(statePath);

try
{
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

static void Seed(string[] args, FileGameStateStore store)
{
    var systemCount = ParseOptionalInt(args, 2, 24);
    var empireCount = ParseOptionalInt(args, 3, 4);
    var seed = ParseOptionalInt(args, 4, 71421);
    var state = GameSeeder.CreateDefault(systemCount, empireCount, seed);

    store.Replace(state);
    Console.WriteLine($"Seeded {store.StatePath}");
    Console.WriteLine($"Cycle: {state.GetActiveCycle()?.Name}");
    Console.WriteLine($"Systems: {state.Systems.Count}; empires: {state.Empires.Count}; fleets: {state.Fleets.Count}");
}

static void Tick(FileGameStateStore store)
{
    var result = store.Update(state =>
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        return new TickEngine().RunTick(state, cycle.CycleId, DateTimeOffset.UtcNow);
    });

    Console.WriteLine($"Tick {result.TickNumber}: {result.Status}");
    Console.WriteLine($"Orders: {result.OrdersProcessed}; events: {result.EventsCreated}; battles: {result.BattlesCreated}; Chronicle entries: {result.ChronicleEntriesCreated}");
}

static void Show(FileGameStateStore store)
{
    var state = store.LoadOrCreate();
    var cycle = state.GetActiveCycle()
        ?? throw new InvalidOperationException("No active cycle exists.");

    Console.WriteLine($"{cycle.Name} | tick {cycle.CurrentTickNumber} | {cycle.Status}");
    Console.WriteLine();
    Console.WriteLine("Empires");
    foreach (var empire in state.Empires.Where(empire => empire.CycleId == cycle.CycleId).OrderBy(empire => empire.EmpireName))
    {
        var home = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
        var resources = state.EmpireResources.Single(resource => resource.EmpireId == empire.EmpireId);
        var fleetCount = state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId && fleet.Status != FleetStatus.Destroyed);
        Console.WriteLine($"- {empire.EmpireName} ({empire.EmpireId})");
        Console.WriteLine($"  Home: {home.SystemName}; fleets: {fleetCount}");
        Console.WriteLine($"  Resources: industry {resources.Industry:0.##}, research {resources.Research:0.##}, population {resources.Population:0.##}");
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
}

static void SubmitMove(string[] args, FileGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var targetSystemId = ParseRequiredGuid(args, 3, "target system id");
    var order = store.Update(state => OrderService.SubmitMoveOrder(state, fleetId, targetSystemId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Move order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitAttack(string[] args, FileGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var targetEmpireId = args.Length > 3 ? Guid.Parse(args[3]) : (Guid?)null;
    var order = store.Update(state => OrderService.SubmitAttackOrder(state, fleetId, targetEmpireId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Attack order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

static void SubmitHold(string[] args, FileGameStateStore store)
{
    var fleetId = ParseRequiredGuid(args, 2, "fleet id");
    var order = store.Update(state => OrderService.SubmitHoldOrder(state, fleetId, DateTimeOffset.UtcNow));
    Console.WriteLine($"Hold order queued for tick {order.ExecuteAfterTick}: {order.FleetOrderId}");
}

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

static void PrintUsage()
{
    Console.WriteLine("Cycles CLI");
    Console.WriteLine("Usage:");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- seed [statePath] [systemCount] [empireCount] [seed]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- show [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- tick [statePath]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- move [statePath] <fleetId> <targetSystemId>");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- attack [statePath] <fleetId> [targetEmpireId]");
    Console.WriteLine("  dotnet run --project src/Cycles.Cli -- hold [statePath] <fleetId>");
}
