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
Func<GameState>? developmentSeedFactory = builder.Environment.IsDevelopment()
    ? () => GameSeeder.CreateCuratedColdStart()
    : null;

builder.Services.AddSingleton(GameStateStoreFactory.Create(configuredStatePath, configuredSqlConnectionString, developmentSeedFactory));
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.MapPost("/auth/login", (LoginRequest request, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() => store.Update(state => Login(state, request, httpContext, app.Environment.IsDevelopment(), DateTimeOffset.UtcNow))));

app.MapGet("/auth/session", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var empire = actor.Empire
            ?? throw new InvalidOperationException("Admin requests must identify an empire.");
        return ToLoginResponse(state, actor.Player, empire, app.Environment.IsDevelopment());
    }));

app.MapGet("/cycles/current", (IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        return ToCycleResponse(state.GetActiveCycle() ?? throw new InvalidOperationException("No active cycle exists."));
    }));

app.MapGet("/ticks/last-summary", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var tickLog = state.TickLogs
            .Where(log => log.CycleId == cycle.CycleId)
            .OrderByDescending(log => log.TickNumber)
            .ThenByDescending(log => log.CompletedAt ?? log.StartedAt)
            .FirstOrDefault();

        return ToLastTickSummaryResponse(state, cycle, tickLog, actor, visibleSystemIds);
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

app.MapGet("/galaxy", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var domainSystems = state.Systems.Where(system => system.CycleId == cycle.CycleId).OrderBy(system => system.SystemName).ToArray();
        var systems = domainSystems.Select(ToGalaxySystemResponse).ToArray();
        var links = state.SystemLinks.Where(link => link.CycleId == cycle.CycleId).Select(ToSystemLinkResponse).ToArray();
        var presence = domainSystems.Select(system =>
        {
            var effectivePresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);
            return new SystemPresenceResponse(
                system.SystemId,
                ApiVisibility.FilterPresence(actor, visibleSystemIds, system.SystemId, effectivePresence));
        }).ToArray();

        var outposts = state.ColonialOutposts
            .Where(item => item.CycleId == cycle.CycleId)
            .Where(item => actor.IsAdmin
                           || visibleSystemIds.Contains(item.SystemId)
                           || item.EmpireId == actor.Empire?.EmpireId)
            .OrderBy(item => domainSystems.Single(system => system.SystemId == item.SystemId).SystemName)
            .ThenBy(item => state.Empires.Single(empire => empire.EmpireId == item.EmpireId).EmpireName)
            .Select(item => ToColonialOutpostResponse(state, item))
            .ToArray();

        return new GalaxyResponse(ToCycleResponse(cycle), systems, links, presence, outposts);
    }));

app.MapGet("/systems/{systemId:guid}", (Guid systemId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var system = state.Systems.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.SystemId == systemId)
            ?? throw new InvalidOperationException("System was not found in the active cycle.");

        return ToSystemDetailResponse(state, cycle, system, actor, visibleSystemIds);
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

        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return ToFleetDetailResponse(state, cycle, fleet, actor, visibleSystemIds);
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

app.MapPost("/orders/fleet/colonise", (ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitColonise(request, httpContext, store));

app.MapPost("/orders/fleet/cancel", (CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.Cancel(request, httpContext, store));

app.MapPost("/orders/priorities", (PriorityRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.UpdatePriorities(request, httpContext, store));

app.MapPost("/admin/tick", (HttpContext httpContext, IGameStateStore store) =>
    ApiAdminEndpoints.RunTick(httpContext, store, app.Environment.IsDevelopment()));

app.MapGet("/events/recent", (int? limit, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return state.Events
            .Where(item => item.CycleId == cycle.CycleId)
            .Where(item => ApiVisibility.CanSeeEvent(item, actor, visibleSystemIds))
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit ?? 25, 1, 100))
            .Select(ToEventResponse)
            .ToArray();
    }));

app.MapGet("/chronicle", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return state.ChronicleEntries
            .Where(entry => entry.CycleId == cycle.CycleId)
            .Where(entry => ApiVisibility.CanSeeChronicleEntry(entry, actor, visibleSystemIds))
            .OrderByDescending(entry => entry.ImportanceScore)
            .Select(ToChronicleEntryResponse)
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

static LoginResponse Login(
    GameState state,
    LoginRequest request,
    HttpContext httpContext,
    bool isDevelopment,
    DateTimeOffset now)
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
    return ToLoginResponse(state, player, empire, isDevelopment);
}

static LoginResponse ToLoginResponse(GameState state, Player player, Empire empire, bool isDevelopment) =>
    new(
        player.PlayerId,
        player.Username,
        player.Role,
        player.Role == PlayerRole.Admin || isDevelopment,
        ToEmpireResponse(state, empire));

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

    var admiral = new Admiral
    {
        CycleId = cycle.CycleId,
        EmpireId = empire.EmpireId,
        AdmiralName = $"{player.Username} Vanguard",
        ReputationScore = 0,
        Status = AdmiralStatus.Active,
        CreatedAt = now,
        UpdatedAt = now
    };
    state.Admirals.Add(admiral);

    state.Fleets.Add(new Fleet
    {
        CycleId = cycle.CycleId,
        EmpireId = empire.EmpireId,
        AdmiralId = admiral.AdmiralId,
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
        ToGalaxySystemResponse(home),
        ToEmpireResourceResponse(resources),
        ToEmpirePriorityResponse(priorities),
        state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId && fleet.Status != FleetStatus.Destroyed));
}

static FleetResponse ToFleetResponse(GameState state, Fleet fleet)
{
    var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
    var currentSystem = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
    var destination = fleet.DestinationSystemId.HasValue
        ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value)
        : null;

    return new FleetResponse(
        ToFleetDataResponse(fleet),
        empire.EmpireName,
        currentSystem.SystemName,
        destination?.SystemName,
        ToAdmiralSummary(state, fleet.AdmiralId));
}

static FleetDetailResponse ToFleetDetailResponse(
    GameState state,
    Cycle cycle,
    Fleet fleet,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds)
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

    var activeFleetsInSystem = ApiVisibility.CanSeeSystemDetails(actor, visibleSystemIds, fleet.CurrentSystemId)
        ? state.Fleets
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
                    item.Status,
                    ToAdmiralSummary(state, item.AdmiralId));
            })
            .ToArray()
        : [];

    return new FleetDetailResponse(
        fleet.FleetId,
        fleet.CycleId,
        fleet.EmpireId,
        fleet.FleetName,
        empire.EmpireName,
        fleet.ShipCount,
        fleet.Status,
        ToAdmiralSummary(state, fleet.AdmiralId),
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

static CycleResponse ToCycleResponse(Cycle cycle) =>
    new(
        cycle.CycleId,
        cycle.Name,
        cycle.StartAt,
        cycle.EndAt,
        cycle.TickLengthMinutes,
        cycle.CurrentTickNumber,
        cycle.Status,
        cycle.CreatedAt);

static GalaxySystemResponse ToGalaxySystemResponse(GalaxySystem system) =>
    new(
        system.SystemId,
        system.CycleId,
        system.SystemName,
        system.X,
        system.Y,
        system.IndustryOutput,
        system.ResearchOutput,
        system.PopulationOutput,
        system.StrategicValue,
        system.HistoricalSignificance,
        system.CreatedAt);

static SystemLinkResponse ToSystemLinkResponse(SystemLink link) =>
    new(link.SystemLinkId, link.CycleId, link.SystemAId, link.SystemBId, link.Distance, link.TravelTicks);

static EmpireResourceResponse ToEmpireResourceResponse(EmpireResource resources) =>
    new(
        resources.EmpireResourceId,
        resources.EmpireId,
        resources.Industry,
        resources.Research,
        resources.Population,
        resources.LastGeneratedIndustry,
        resources.LastGeneratedResearch,
        resources.LastGeneratedPopulation,
        resources.LastSpentIndustry,
        resources.LastSpentResearch,
        resources.LastSpentPopulation,
        resources.UpdatedAt);

static EmpirePriorityResponse ToEmpirePriorityResponse(EmpirePriority priorities) =>
    new(
        priorities.EmpirePriorityId,
        priorities.EmpireId,
        priorities.IndustryWeight,
        priorities.ResearchWeight,
        priorities.MilitaryWeight,
        priorities.ExpansionWeight,
        priorities.UpdatedAt);

static FleetDataResponse ToFleetDataResponse(Fleet fleet) =>
    new(
        fleet.FleetId,
        fleet.CycleId,
        fleet.EmpireId,
        fleet.AdmiralId,
        fleet.FleetName,
        fleet.CurrentSystemId,
        fleet.DestinationSystemId,
        fleet.ArrivalTickNumber,
        fleet.ShipCount,
        fleet.Status,
        fleet.CreatedAt);

static EventResponse ToEventResponse(EventRecord item) =>
    new(
        item.EventId,
        item.CycleId,
        item.TickNumber,
        item.EventType,
        item.SystemId,
        item.EmpireId,
        item.Severity,
        item.FactJson,
        item.DisplayText,
        item.CreatedAt);

static BattleResponse ToBattleResponse(BattleRecord item) =>
    new(
        item.BattleId,
        item.CycleId,
        item.TickNumber,
        item.SystemId,
        item.AttackerEmpireId,
        item.DefenderEmpireId,
        item.AttackerFleetIds,
        item.DefenderFleetIds,
        item.AttackerShipsBefore,
        item.DefenderShipsBefore,
        item.AttackerLosses,
        item.DefenderLosses,
        item.Outcome,
        item.FactJson,
        item.CreatedAt);

static ChronicleEntryResponse ToChronicleEntryResponse(ChronicleEntry item) =>
    new(
        item.ChronicleEntryId,
        item.SourceEventId,
        item.SourceBattleId,
        item.CycleId,
        item.SystemId,
        item.Title,
        item.EntryType,
        item.ImportanceScore,
        item.FactualSummary,
        item.NarrativeText,
        item.NarrativeStatus,
        item.NarrativeContextJson,
        item.NarrativeGeneratedAt,
        item.NarrativeFailureReason,
        item.CreatedAt);

static SystemDetailResponse ToSystemDetailResponse(
    GameState state,
    Cycle cycle,
    GalaxySystem system,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds)
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

    var canSeeDetails = ApiVisibility.CanSeeSystemDetails(actor, visibleSystemIds, system.SystemId);
    var presence = canSeeDetails
        ? InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId)
        : new Dictionary<Guid, decimal>();
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

    var activeFleets = canSeeDetails
        ? state.Fleets
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
                    item.Status,
                    ToAdmiralSummary(state, item.AdmiralId));
            })
            .ToArray()
        : [];

    var outposts = state.ColonialOutposts
        .Where(item => item.CycleId == cycle.CycleId && item.SystemId == system.SystemId)
        .Where(item => canSeeDetails || item.EmpireId == actor.Empire?.EmpireId)
        .OrderBy(item => state.Empires.Single(empire => empire.EmpireId == item.EmpireId).EmpireName)
        .Select(item => ToColonialOutpostResponse(state, item))
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
        linkedSystems,
        outposts);
}

static ColonialOutpostResponse ToColonialOutpostResponse(GameState state, ColonialOutpost outpost)
{
    var empire = state.Empires.Single(item => item.EmpireId == outpost.EmpireId);
    var isProjectingPresence = state.Fleets.Any(item => item.CycleId == outpost.CycleId
                                                        && item.EmpireId == outpost.EmpireId
                                                        && item.CurrentSystemId == outpost.SystemId
                                                        && item.Status == FleetStatus.Active
                                                        && item.ShipCount > 0);
    return new ColonialOutpostResponse(
        outpost.ColonialOutpostId,
        outpost.SystemId,
        outpost.EmpireId,
        empire.EmpireName,
        outpost.EstablishedTick,
        isProjectingPresence);
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

static AdmiralSummaryResponse? ToAdmiralSummary(GameState state, Guid? admiralId)
{
    if (!admiralId.HasValue)
    {
        return null;
    }

    var admiral = state.Admirals.SingleOrDefault(item => item.AdmiralId == admiralId.Value);
    return admiral is null
        ? null
        : new AdmiralSummaryResponse(admiral.AdmiralId, admiral.AdmiralName, admiral.ReputationScore, admiral.Status);
}

static LastTickSummaryResponse ToLastTickSummaryResponse(
    GameState state,
    Cycle cycle,
    TickLog? tickLog,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds)
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
        .Where(item => ApiVisibility.CanSeeEvent(item, actor, visibleSystemIds))
        .OrderBy(item => item.CreatedAt)
        .ToArray();

    var battles = state.BattleRecords
        .Where(item => item.CycleId == cycle.CycleId && item.TickNumber == tickLog.TickNumber)
        .Where(item => ApiVisibility.CanSeeBattle(item, actor, visibleSystemIds))
        .OrderBy(item => item.CreatedAt)
        .ToArray();

    var eventIds = events.Select(item => item.EventId).ToHashSet();
    var battleIds = battles.Select(item => item.BattleId).ToHashSet();
    var chronicleEntries = state.ChronicleEntries
        .Where(entry => entry.CycleId == cycle.CycleId
                        && ApiVisibility.CanSeeChronicleEntry(entry, actor, visibleSystemIds)
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
        events.Select(ToEventResponse).ToArray(),
        battles.Select(ToBattleResponse).ToArray(),
        chronicleEntries.Select(ToChronicleEntryResponse).ToArray());
}

public sealed record LoginRequest(string Username, string? EmpireName, bool IsAdmin = false);

public sealed record LoginResponse(
    Guid PlayerId,
    string Username,
    PlayerRole Role,
    bool CanAdvanceTurn,
    EmpireResponse Empire);

public sealed record EmpireResponse(
    Guid EmpireId,
    Guid PlayerId,
    string EmpireName,
    GalaxySystemResponse HomeSystem,
    EmpireResourceResponse Resources,
    EmpirePriorityResponse Priorities,
    int FleetCount);

public sealed record GalaxyResponse(
    CycleResponse Cycle,
    IReadOnlyCollection<GalaxySystemResponse> Systems,
    IReadOnlyCollection<SystemLinkResponse> Links,
    IReadOnlyCollection<SystemPresenceResponse> Presence,
    IReadOnlyCollection<ColonialOutpostResponse> ColonialOutposts);

public sealed record SystemPresenceResponse(Guid SystemId, IReadOnlyDictionary<Guid, decimal> EffectivePresence);

public sealed record FleetResponse(
    FleetDataResponse Fleet,
    string EmpireName,
    string CurrentSystemName,
    string? DestinationSystemName,
    AdmiralSummaryResponse? Admiral);

public sealed record FleetDetailResponse(
    Guid FleetId,
    Guid CycleId,
    Guid EmpireId,
    string FleetName,
    string EmpireName,
    int ShipCount,
    FleetStatus Status,
    AdmiralSummaryResponse? Admiral,
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
    IReadOnlyCollection<SystemSummaryResponse> LinkedSystems,
    IReadOnlyCollection<ColonialOutpostResponse> ColonialOutposts);

public sealed record ColonialOutpostResponse(
    Guid ColonialOutpostId,
    Guid SystemId,
    Guid EmpireId,
    string EmpireName,
    int EstablishedTick,
    bool IsProjectingPresence);

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
    FleetStatus Status,
    AdmiralSummaryResponse? Admiral);

public sealed record AdmiralSummaryResponse(
    Guid AdmiralId,
    string AdmiralName,
    int ReputationScore,
    AdmiralStatus Status);

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
    IReadOnlyCollection<EventResponse> Events,
    IReadOnlyCollection<BattleResponse> Battles,
    IReadOnlyCollection<ChronicleEntryResponse> ChronicleEntries);

public sealed record CycleResponse(
    Guid CycleId,
    string Name,
    DateTimeOffset StartAt,
    DateTimeOffset EndAt,
    int TickLengthMinutes,
    int CurrentTickNumber,
    CycleStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GalaxySystemResponse(
    Guid SystemId,
    Guid CycleId,
    string SystemName,
    int X,
    int Y,
    decimal IndustryOutput,
    decimal ResearchOutput,
    decimal PopulationOutput,
    int StrategicValue,
    int HistoricalSignificance,
    DateTimeOffset CreatedAt);

public sealed record SystemLinkResponse(
    Guid SystemLinkId,
    Guid CycleId,
    Guid SystemAId,
    Guid SystemBId,
    decimal Distance,
    int TravelTicks);

public sealed record EmpireResourceResponse(
    Guid EmpireResourceId,
    Guid EmpireId,
    decimal Industry,
    decimal Research,
    decimal Population,
    decimal LastGeneratedIndustry,
    decimal LastGeneratedResearch,
    decimal LastGeneratedPopulation,
    decimal LastSpentIndustry,
    decimal LastSpentResearch,
    decimal LastSpentPopulation,
    DateTimeOffset UpdatedAt);

public sealed record EmpirePriorityResponse(
    Guid EmpirePriorityId,
    Guid EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight,
    DateTimeOffset UpdatedAt);

public sealed record FleetDataResponse(
    Guid FleetId,
    Guid CycleId,
    Guid EmpireId,
    Guid? AdmiralId,
    string FleetName,
    Guid CurrentSystemId,
    Guid? DestinationSystemId,
    int? ArrivalTickNumber,
    int ShipCount,
    FleetStatus Status,
    DateTimeOffset CreatedAt);

public sealed record EventResponse(
    Guid EventId,
    Guid CycleId,
    int TickNumber,
    EventType EventType,
    Guid? SystemId,
    Guid? EmpireId,
    EventSeverity Severity,
    string FactJson,
    string DisplayText,
    DateTimeOffset CreatedAt);

public sealed record BattleResponse(
    Guid BattleId,
    Guid CycleId,
    int TickNumber,
    Guid SystemId,
    Guid AttackerEmpireId,
    Guid DefenderEmpireId,
    string AttackerFleetIds,
    string DefenderFleetIds,
    int AttackerShipsBefore,
    int DefenderShipsBefore,
    int AttackerLosses,
    int DefenderLosses,
    BattleOutcome Outcome,
    string FactJson,
    DateTimeOffset CreatedAt);

public sealed record ChronicleEntryResponse(
    Guid ChronicleEntryId,
    Guid? SourceEventId,
    Guid? SourceBattleId,
    Guid CycleId,
    Guid? SystemId,
    string Title,
    ChronicleEntryType EntryType,
    int ImportanceScore,
    string FactualSummary,
    string NarrativeText,
    NarrativeGenerationStatus NarrativeStatus,
    string NarrativeContextJson,
    DateTimeOffset? NarrativeGeneratedAt,
    string? NarrativeFailureReason,
    DateTimeOffset CreatedAt);

public sealed record MoveFleetRequest(Guid FleetId, Guid TargetSystemId);

public sealed record AttackFleetRequest(Guid FleetId, Guid? TargetEmpireId);

public sealed record ColoniseFleetRequest(Guid FleetId);

public sealed record CancelFleetOrderRequest(Guid FleetOrderId);

public sealed record PriorityRequest(
    Guid? EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight);

public sealed record ErrorResponse(string Message);
