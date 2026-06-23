using Cycles.Core;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var configuredStatePath = builder.Configuration["Cycles:StatePath"]
    ?? Environment.GetEnvironmentVariable("CYCLES_STATE_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "cycles-state.json");

builder.Services.AddSingleton(new FileGameStateStore(configuredStatePath));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/login", (LoginRequest request, FileGameStateStore store) =>
    TryResult(() => store.Update(state => Login(state, request, DateTimeOffset.UtcNow))));

app.MapGet("/cycles/current", (FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        return state.GetActiveCycle() ?? throw new InvalidOperationException("No active cycle exists.");
    }));

app.MapGet("/empire", (Guid? playerId, FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var empire = state.Empires
            .Where(item => item.CycleId == cycle.CycleId)
            .FirstOrDefault(item => !playerId.HasValue || item.PlayerId == playerId.Value)
            ?? throw new InvalidOperationException("No empire was found for the supplied player.");

        return ToEmpireResponse(state, empire);
    }));

app.MapGet("/galaxy", (FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var systems = state.Systems.Where(system => system.CycleId == cycle.CycleId).OrderBy(system => system.SystemName).ToArray();
        var links = state.SystemLinks.Where(link => link.CycleId == cycle.CycleId).ToArray();
        var presence = systems.Select(system =>
        {
            var effectivePresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);
            return new SystemPresenceResponse(system.SystemId, effectivePresence);
        }).ToArray();

        return new GalaxyResponse(cycle, systems, links, presence);
    }));

app.MapGet("/fleets", (Guid? empireId, FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        return state.Fleets
            .Where(fleet => fleet.CycleId == cycle.CycleId && (!empireId.HasValue || fleet.EmpireId == empireId.Value))
            .OrderBy(fleet => fleet.FleetName)
            .Select(fleet => ToFleetResponse(state, fleet))
            .ToArray();
    }));

app.MapPost("/orders/fleet/move", (MoveFleetRequest request, FileGameStateStore store) =>
    TryResult(() => store.Update(state => OrderService.SubmitMoveOrder(
        state,
        request.FleetId,
        request.TargetSystemId,
        DateTimeOffset.UtcNow))));

app.MapPost("/orders/fleet/attack", (AttackFleetRequest request, FileGameStateStore store) =>
    TryResult(() => store.Update(state => OrderService.SubmitAttackOrder(
        state,
        request.FleetId,
        request.TargetEmpireId,
        DateTimeOffset.UtcNow))));

app.MapPost("/orders/priorities", (PriorityRequest request, FileGameStateStore store) =>
    TryResult(() => store.Update(state => OrderService.UpdatePriorities(
        state,
        request.EmpireId,
        request.IndustryWeight,
        request.ResearchWeight,
        request.MilitaryWeight,
        request.ExpansionWeight,
        DateTimeOffset.UtcNow))));

app.MapGet("/events/recent", (int? limit, FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        return state.Events
            .Where(item => item.CycleId == cycle.CycleId)
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit ?? 25, 1, 100))
            .ToArray();
    }));

app.MapGet("/chronicle", (FileGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        return state.ChronicleEntries
            .Where(entry => entry.CycleId == cycle.CycleId)
            .OrderByDescending(entry => entry.ImportanceScore)
            .ToArray();
    }));

app.Run();

static IResult TryResult<T>(Func<T> action)
{
    try
    {
        return Results.Ok(action());
    }
    catch (InvalidOperationException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new ErrorResponse(ex.Message));
    }
}

static Cycle GetActiveCycle(GameState state) =>
    state.GetActiveCycle() ?? throw new InvalidOperationException("No active cycle exists.");

static LoginResponse Login(GameState state, LoginRequest request, DateTimeOffset now)
{
    var username = string.IsNullOrWhiteSpace(request.Username) ? "player-1" : request.Username.Trim();
    var player = state.Players.FirstOrDefault(item => string.Equals(item.Username, username, StringComparison.OrdinalIgnoreCase));
    if (player is null)
    {
        player = new Player
        {
            Username = username,
            Email = $"{username}@cycles.local",
            PasswordHash = "prototype",
            CreatedAt = now,
            LastLoginAt = now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
    }

    player.LastLoginAt = now;
    var cycle = GetActiveCycle(state);
    var empire = state.Empires.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.PlayerId == player.PlayerId)
        ?? AddEmpireForPlayer(state, cycle, player, request.EmpireName, now);

    return new LoginResponse(player.PlayerId, player.Username, ToEmpireResponse(state, empire));
}

static Empire AddEmpireForPlayer(GameState state, Cycle cycle, Player player, string? requestedEmpireName, DateTimeOffset now)
{
    var claimedHomeSystems = state.Empires.Where(empire => empire.CycleId == cycle.CycleId).Select(empire => empire.HomeSystemId).ToHashSet();
    var homeSystem = state.Systems
        .Where(system => system.CycleId == cycle.CycleId && !claimedHomeSystems.Contains(system.SystemId))
        .OrderByDescending(system => system.StrategicValue)
        .FirstOrDefault()
        ?? state.Systems.Where(system => system.CycleId == cycle.CycleId).OrderByDescending(system => system.StrategicValue).First();

    var empire = new Empire
    {
        CycleId = cycle.CycleId,
        PlayerId = player.PlayerId,
        EmpireName = string.IsNullOrWhiteSpace(requestedEmpireName) ? $"{player.Username} Continuance" : requestedEmpireName.Trim(),
        HomeSystemId = homeSystem.SystemId,
        CreatedAt = now,
        Status = EmpireStatus.Active
    };
    state.Empires.Add(empire);

    state.EmpireResources.Add(new EmpireResource
    {
        EmpireId = empire.EmpireId,
        Industry = 100,
        Research = 100,
        Population = 100,
        UpdatedAt = now
    });

    state.EmpirePriorities.Add(new EmpirePriority
    {
        EmpireId = empire.EmpireId,
        IndustryWeight = 30,
        ResearchWeight = 25,
        MilitaryWeight = 30,
        ExpansionWeight = 15,
        UpdatedAt = now
    });

    state.Fleets.Add(new Fleet
    {
        CycleId = cycle.CycleId,
        EmpireId = empire.EmpireId,
        FleetName = $"{empire.EmpireName} Home Fleet",
        CurrentSystemId = homeSystem.SystemId,
        ShipCount = 45,
        Status = FleetStatus.Active,
        CreatedAt = now
    });

    return empire;
}

static EmpireResponse ToEmpireResponse(GameState state, Empire empire)
{
    var home = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
    var resources = state.EmpireResources.Single(resource => resource.EmpireId == empire.EmpireId);
    var priorities = state.EmpirePriorities.Single(priority => priority.EmpireId == empire.EmpireId);

    return new EmpireResponse(
        empire.EmpireId,
        empire.PlayerId,
        empire.EmpireName,
        home,
        resources,
        priorities,
        state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId && fleet.Status != FleetStatus.Destroyed));
}

static FleetResponse ToFleetResponse(GameState state, Fleet fleet)
{
    var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
    var currentSystem = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
    var destination = fleet.DestinationSystemId.HasValue
        ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value)
        : null;

    return new FleetResponse(fleet, empire.EmpireName, currentSystem.SystemName, destination?.SystemName);
}

public sealed record LoginRequest(string Username, string? EmpireName);

public sealed record LoginResponse(Guid PlayerId, string Username, EmpireResponse Empire);

public sealed record EmpireResponse(
    Guid EmpireId,
    Guid PlayerId,
    string EmpireName,
    GalaxySystem HomeSystem,
    EmpireResource Resources,
    EmpirePriority Priorities,
    int FleetCount);

public sealed record GalaxyResponse(
    Cycle Cycle,
    IReadOnlyCollection<GalaxySystem> Systems,
    IReadOnlyCollection<SystemLink> Links,
    IReadOnlyCollection<SystemPresenceResponse> Presence);

public sealed record SystemPresenceResponse(Guid SystemId, IReadOnlyDictionary<Guid, decimal> EffectivePresence);

public sealed record FleetResponse(Fleet Fleet, string EmpireName, string CurrentSystemName, string? DestinationSystemName);

public sealed record MoveFleetRequest(Guid FleetId, Guid TargetSystemId);

public sealed record AttackFleetRequest(Guid FleetId, Guid? TargetEmpireId);

public sealed record PriorityRequest(
    Guid EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight);

public sealed record ErrorResponse(string Message);
