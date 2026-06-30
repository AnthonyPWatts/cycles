using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);
var configuredStatePath = builder.Configuration["Cycles:StatePath"]
    ?? Environment.GetEnvironmentVariable("CYCLES_STATE_PATH")
    ?? Path.Combine(builder.Environment.ContentRootPath, "data", "cycles-state.json");
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");

builder.Services.AddSingleton(GameStateStoreFactory.Create(configuredStatePath, configuredSqlConnectionString));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/login", (LoginRequest request, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() => store.Update(state => Login(state, request, httpContext, DateTimeOffset.UtcNow))));

app.MapGet("/auth/session", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var empire = actor.Empire
            ?? throw new InvalidOperationException("Admin requests must identify an empire.");
        return ToLoginResponse(state, actor.Player, empire);
    }));

app.MapGet("/cycles/current", (IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        return state.GetActiveCycle() ?? throw new InvalidOperationException("No active cycle exists.");
    }));

app.MapGet("/ticks/last-summary", (IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var tickLog = state.TickLogs
            .Where(log => log.CycleId == cycle.CycleId)
            .OrderByDescending(log => log.TickNumber)
            .ThenByDescending(log => log.CompletedAt ?? log.StartedAt)
            .FirstOrDefault();

        return ToLastTickSummaryResponse(state, cycle, tickLog);
    }));

app.MapGet("/empire", (Guid? empireId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var targetEmpireId = DevelopmentAuth.ResolveEmpireId(state, actor, empireId);
        var empire = state.Empires
            .Where(item => item.CycleId == cycle.CycleId)
            .First(item => item.EmpireId == targetEmpireId);

        return ToEmpireResponse(state, empire);
    }));

app.MapGet("/galaxy", (IGameStateStore store) =>
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

app.MapGet("/systems/{systemId:guid}", (Guid systemId, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var system = state.Systems.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.SystemId == systemId)
            ?? throw new InvalidOperationException("System was not found in the active cycle.");

        return ToSystemDetailResponse(state, cycle, system);
    }));

app.MapGet("/fleets", (Guid? empireId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        Guid? targetEmpireId = actor.IsAdmin && !empireId.HasValue
            ? null
            : DevelopmentAuth.ResolveEmpireId(state, actor, empireId);
        return state.Fleets
            .Where(fleet => fleet.CycleId == cycle.CycleId && (!targetEmpireId.HasValue || fleet.EmpireId == targetEmpireId.Value))
            .OrderBy(fleet => fleet.FleetName)
            .Select(fleet => ToFleetResponse(state, fleet))
            .ToArray();
    }));

app.MapGet("/fleets/{fleetId:guid}", (Guid fleetId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var fleet = state.Fleets.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.FleetId == fleetId)
            ?? throw new InvalidOperationException("Fleet was not found in the active cycle.");
        if (!actor.IsAdmin && fleet.EmpireId != DevelopmentAuth.ResolveEmpireId(state, actor))
        {
            throw new ApiForbiddenException("The authenticated player cannot inspect this fleet.");
        }

        return ToFleetDetailResponse(state, cycle, fleet);
    }));

app.MapGet("/orders", (Guid? empireId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        Guid? targetEmpireId = actor.IsAdmin && !empireId.HasValue
            ? null
            : DevelopmentAuth.ResolveEmpireId(state, actor, empireId);
        return state.FleetOrders
            .Where(order => order.CycleId == cycle.CycleId)
            .Where(order => !targetEmpireId.HasValue || state.Fleets.Any(fleet => fleet.FleetId == order.FleetId && fleet.EmpireId == targetEmpireId.Value))
            .OrderBy(order => order.Status == FleetOrderStatus.Pending ? 0 : 1)
            .ThenBy(order => order.ExecuteAfterTick)
            .ThenByDescending(order => order.CreatedAt)
            .Take(50)
            .Select(order => ToOrderResponse(state, order))
            .ToArray();
    }));

app.MapPost("/orders/fleet/move", (MoveFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitMove(request, httpContext, store));

app.MapPost("/orders/fleet/attack", (AttackFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitAttack(request, httpContext, store));

app.MapPost("/orders/fleet/cancel", (CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.Cancel(request, httpContext, store));

app.MapPost("/orders/priorities", (PriorityRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.UpdatePriorities(request, httpContext, store));

app.MapGet("/events/recent", (int? limit, IGameStateStore store) =>
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

app.MapGet("/chronicle", (IGameStateStore store) =>
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
    catch (ApiUnauthorizedException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status401Unauthorized);
    }
    catch (ApiForbiddenException ex)
    {
        return Results.Json(new ErrorResponse(ex.Message), statusCode: StatusCodes.Status403Forbidden);
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

static LoginResponse Login(GameState state, LoginRequest request, HttpContext httpContext, DateTimeOffset now)
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
            Role = request.IsAdmin ? PlayerRole.Admin : PlayerRole.Player,
            CreatedAt = now,
            LastLoginAt = now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
    }
    else if (request.IsAdmin)
    {
        player.Role = PlayerRole.Admin;
    }

    player.LastLoginAt = now;
    var cycle = GetActiveCycle(state);
    var empire = state.Empires.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.PlayerId == player.PlayerId)
        ?? AddEmpireForPlayer(state, cycle, player, request.EmpireName, now);

    DevelopmentAuth.SignIn(httpContext, player);
    return ToLoginResponse(state, player, empire);
}

static LoginResponse ToLoginResponse(GameState state, Player player, Empire empire) =>
    new(player.PlayerId, player.Username, player.Role, ToEmpireResponse(state, empire));

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

static FleetDetailResponse ToFleetDetailResponse(GameState state, Cycle cycle, Fleet fleet)
{
    var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
    var currentSystem = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
    var destination = fleet.DestinationSystemId.HasValue
        ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value)
        : null;

    var systemsById = state.Systems
        .Where(system => system.CycleId == cycle.CycleId)
        .ToDictionary(system => system.SystemId);

    var linkedSystems = state.SystemLinks
        .Where(link => link.CycleId == cycle.CycleId && (link.SystemAId == currentSystem.SystemId || link.SystemBId == currentSystem.SystemId))
        .Select(link => systemsById[link.SystemAId == currentSystem.SystemId ? link.SystemBId : link.SystemAId])
        .OrderBy(system => system.SystemName)
        .Select(ToSystemSummaryResponse)
        .ToArray();

    var orders = state.FleetOrders
        .Where(order => order.CycleId == cycle.CycleId && order.FleetId == fleet.FleetId)
        .OrderBy(order => order.Status == FleetOrderStatus.Pending ? 0 : 1)
        .ThenBy(order => order.ExecuteAfterTick)
        .ThenByDescending(order => order.CreatedAt)
        .Take(12)
        .Select(order => ToOrderResponse(state, order))
        .ToArray();

    var activeFleetsInSystem = state.Fleets
        .Where(item => item.CycleId == cycle.CycleId
                       && item.FleetId != fleet.FleetId
                       && item.CurrentSystemId == fleet.CurrentSystemId
                       && item.Status == FleetStatus.Active)
        .OrderBy(item => state.Empires.Single(empireItem => empireItem.EmpireId == item.EmpireId).EmpireName)
        .ThenBy(item => item.FleetName)
        .Select(item =>
        {
            var otherEmpire = state.Empires.Single(empireItem => empireItem.EmpireId == item.EmpireId);
            return new FleetAtSystemResponse(
                item.FleetId,
                item.FleetName,
                item.EmpireId,
                otherEmpire.EmpireName,
                item.ShipCount,
                item.Status);
        })
        .ToArray();

    return new FleetDetailResponse(
        fleet.FleetId,
        fleet.CycleId,
        fleet.EmpireId,
        fleet.FleetName,
        empire.EmpireName,
        fleet.ShipCount,
        fleet.Status,
        ToSystemSummaryResponse(currentSystem),
        destination is null ? null : ToSystemSummaryResponse(destination),
        fleet.ArrivalTickNumber,
        linkedSystems,
        orders,
        activeFleetsInSystem);
}

static SystemSummaryResponse ToSystemSummaryResponse(GalaxySystem system) =>
    new(
        system.SystemId,
        system.SystemName,
        system.X,
        system.Y,
        system.StrategicValue,
        system.HistoricalSignificance);

static SystemDetailResponse ToSystemDetailResponse(GameState state, Cycle cycle, GalaxySystem system)
{
    var systemsById = state.Systems
        .Where(item => item.CycleId == cycle.CycleId)
        .ToDictionary(item => item.SystemId);

    var linkedSystems = state.SystemLinks
        .Where(link => link.CycleId == cycle.CycleId && (link.SystemAId == system.SystemId || link.SystemBId == system.SystemId))
        .Select(link => systemsById[link.SystemAId == system.SystemId ? link.SystemBId : link.SystemAId])
        .OrderBy(item => item.SystemName)
        .Select(ToSystemSummaryResponse)
        .ToArray();

    var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);
    var totalPresence = presence.Values.Sum();
    var influence = presence
        .OrderByDescending(item => item.Value)
        .Select(item =>
        {
            var empire = state.Empires.Single(empireItem => empireItem.EmpireId == item.Key);
            var share = totalPresence == 0 ? 0 : decimal.Round(item.Value / totalPresence * 100, 2);
            return new SystemInfluenceResponse(empire.EmpireId, empire.EmpireName, item.Value, share);
        })
        .ToArray();

    var activeFleets = state.Fleets
        .Where(item => item.CycleId == cycle.CycleId
                       && item.CurrentSystemId == system.SystemId
                       && item.Status == FleetStatus.Active)
        .OrderBy(item => state.Empires.Single(empireItem => empireItem.EmpireId == item.EmpireId).EmpireName)
        .ThenBy(item => item.FleetName)
        .Select(item =>
        {
            var empire = state.Empires.Single(empireItem => empireItem.EmpireId == item.EmpireId);
            return new FleetAtSystemResponse(
                item.FleetId,
                item.FleetName,
                item.EmpireId,
                empire.EmpireName,
                item.ShipCount,
                item.Status);
        })
        .ToArray();

    return new SystemDetailResponse(
        system.SystemId,
        system.SystemName,
        system.X,
        system.Y,
        system.IndustryOutput,
        system.ResearchOutput,
        system.PopulationOutput,
        system.StrategicValue,
        system.HistoricalSignificance,
        influence,
        activeFleets,
        linkedSystems);
}

static FleetOrderResponse ToOrderResponse(GameState state, FleetOrder order)
{
    var fleet = state.Fleets.SingleOrDefault(item => item.FleetId == order.FleetId);
    var targetSystem = order.TargetSystemId.HasValue
        ? state.Systems.SingleOrDefault(item => item.SystemId == order.TargetSystemId.Value)
        : null;
    var targetEmpire = order.TargetEmpireId.HasValue
        ? state.Empires.SingleOrDefault(item => item.EmpireId == order.TargetEmpireId.Value)
        : null;

    return new FleetOrderResponse(
        order.FleetOrderId,
        order.OrderType,
        order.Status,
        order.SubmitTick,
        order.ExecuteAfterTick,
        order.ProcessedTick,
        order.RejectionReason,
        fleet?.FleetName ?? "Unknown fleet",
        targetSystem?.SystemName,
        targetEmpire?.EmpireName);
}

static LastTickSummaryResponse ToLastTickSummaryResponse(GameState state, Cycle cycle, TickLog? tickLog)
{
    if (tickLog is null)
    {
        return new LastTickSummaryResponse(
            cycle.CycleId,
            null,
            null,
            null,
            null,
            null,
            0,
            0,
            0,
            [],
            [],
            []);
    }

    var events = state.Events
        .Where(item => item.CycleId == cycle.CycleId && item.TickNumber == tickLog.TickNumber)
        .OrderBy(item => item.CreatedAt)
        .ToArray();

    var battles = state.BattleRecords
        .Where(item => item.CycleId == cycle.CycleId && item.TickNumber == tickLog.TickNumber)
        .OrderBy(item => item.CreatedAt)
        .ToArray();

    var eventIds = events.Select(item => item.EventId).ToHashSet();
    var battleIds = battles.Select(item => item.BattleId).ToHashSet();
    var chronicleEntries = state.ChronicleEntries
        .Where(entry => entry.CycleId == cycle.CycleId
                        && ((entry.SourceEventId.HasValue && eventIds.Contains(entry.SourceEventId.Value))
                            || (entry.SourceBattleId.HasValue && battleIds.Contains(entry.SourceBattleId.Value))))
        .OrderByDescending(entry => entry.ImportanceScore)
        .ToArray();

    return new LastTickSummaryResponse(
        cycle.CycleId,
        tickLog.TickNumber,
        tickLog.Status,
        tickLog.StartedAt,
        tickLog.CompletedAt,
        tickLog.DiagnosticLog,
        events.Length,
        battles.Length,
        chronicleEntries.Length,
        events,
        battles,
        chronicleEntries);
}

public sealed record LoginRequest(string Username, string? EmpireName, bool IsAdmin = false);

public sealed record LoginResponse(Guid PlayerId, string Username, PlayerRole Role, EmpireResponse Empire);

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

public sealed record FleetDetailResponse(
    Guid FleetId,
    Guid CycleId,
    Guid EmpireId,
    string FleetName,
    string EmpireName,
    int ShipCount,
    FleetStatus Status,
    SystemSummaryResponse CurrentSystem,
    SystemSummaryResponse? DestinationSystem,
    int? ArrivalTickNumber,
    IReadOnlyCollection<SystemSummaryResponse> LinkedSystems,
    IReadOnlyCollection<FleetOrderResponse> Orders,
    IReadOnlyCollection<FleetAtSystemResponse> ActiveFleetsInSystem);

public sealed record SystemSummaryResponse(
    Guid SystemId,
    string SystemName,
    int X,
    int Y,
    int StrategicValue,
    int HistoricalSignificance);

public sealed record SystemDetailResponse(
    Guid SystemId,
    string SystemName,
    int X,
    int Y,
    decimal IndustryOutput,
    decimal ResearchOutput,
    decimal PopulationOutput,
    int StrategicValue,
    int HistoricalSignificance,
    IReadOnlyCollection<SystemInfluenceResponse> Influence,
    IReadOnlyCollection<FleetAtSystemResponse> ActiveFleets,
    IReadOnlyCollection<SystemSummaryResponse> LinkedSystems);

public sealed record SystemInfluenceResponse(
    Guid EmpireId,
    string EmpireName,
    decimal EffectivePresence,
    decimal InfluencePercent);

public sealed record FleetAtSystemResponse(
    Guid FleetId,
    string FleetName,
    Guid EmpireId,
    string EmpireName,
    int ShipCount,
    FleetStatus Status);

public sealed record FleetOrderResponse(
    Guid FleetOrderId,
    FleetOrderType OrderType,
    FleetOrderStatus Status,
    int SubmitTick,
    int ExecuteAfterTick,
    int? ProcessedTick,
    string? RejectionReason,
    string FleetName,
    string? TargetSystemName,
    string? TargetEmpireName);

public sealed record LastTickSummaryResponse(
    Guid CycleId,
    int? TickNumber,
    TickLogStatus? Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? DiagnosticLog,
    int EventsCreated,
    int BattlesCreated,
    int ChronicleEntriesCreated,
    IReadOnlyCollection<EventRecord> Events,
    IReadOnlyCollection<BattleRecord> Battles,
    IReadOnlyCollection<ChronicleEntry> ChronicleEntries);

public sealed record MoveFleetRequest(Guid FleetId, Guid TargetSystemId);

public sealed record AttackFleetRequest(Guid FleetId, Guid? TargetEmpireId);

public sealed record CancelFleetOrderRequest(Guid FleetOrderId);

public sealed record PriorityRequest(
    Guid? EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight);

public sealed record ErrorResponse(string Message);
