using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
var trustedPlayerSelectionEnabled = builder.Configuration.GetValue<bool?>("Cycles:TrustedPlayerSelection:Enabled")
    ?? builder.Environment.IsDevelopment();
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(configuredSqlConnectionString))
{
    throw new InvalidOperationException("Cycles.Api requires a Cycles SQL connection string. Configure ConnectionStrings:Cycles or CYCLES_SQL_CONNECTION_STRING.");
}
Func<GameState>? developmentSeedFactory = builder.Environment.IsDevelopment()
    ? () => GameSeeder.CreateCuratedColdStart()
    : null;

builder.Services.AddSingleton<IGameStateStore>(new SqlServerGameStateStore(configuredSqlConnectionString, developmentSeedFactory));
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
});
builder.Services.ConfigureHttpJsonOptions(options =>
{
    ApiJson.Configure(options.SerializerOptions);
});
builder.Services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
builder.Services.AddDataProtection();

ExternalAuthenticationOptions? externalAuthentication = null;
if (!builder.Environment.IsDevelopment() && !trustedPlayerSelectionEnabled)
{
    externalAuthentication = builder.Services.AddExternalCyclesAuthentication(builder.Configuration);
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        foreach (var configuredProxy in externalAuthentication.KnownProxies)
        {
            if (!IPAddress.TryParse(configuredProxy, out var proxy))
            {
                throw new InvalidOperationException($"Cycles:Authentication:KnownProxies contains invalid IP address '{configuredProxy}'.");
            }

            options.KnownProxies.Add(proxy);
        }
    });
}

var app = builder.Build();
var playgroundAccessCode = TrustedPlayerSelectionConfiguration.ResolvePlaygroundAccessCode(
    trustedPlayerSelectionEnabled,
    app.Environment.IsDevelopment(),
    Environment.GetEnvironmentVariable("CYCLES_PLAYGROUND_ACCESS_CODE"),
    builder.Configuration["Cycles:PlaygroundAccessCode"]);

if (!app.Environment.IsDevelopment() && !trustedPlayerSelectionEnabled)
{
    app.UseForwardedHeaders();
}

app.UseResponseCompression();
app.UseEdgeAssetRedirect(builder.Configuration["Cycles:EdgeAssetOrigin"]);
app.UsePlaygroundAccess(playgroundAccessCode);
app.UseMiddleware<ApiErrorMiddleware>();
if (!app.Environment.IsDevelopment() && !trustedPlayerSelectionEnabled)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.UsePrivateDashboard(app.Environment.IsDevelopment() || trustedPlayerSelectionEnabled);
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        if (!PlaygroundAccessMiddleware.IsPublicStaticAsset(context.Context.Request.Path))
        {
            return;
        }

        var isVersioned = context.Context.Request.Query.ContainsKey("v");
        context.Context.Response.Headers.CacheControl = isVersioned
            ? "public, max-age=86400, immutable"
            : "public, max-age=3600";
        context.Context.Response.Headers["Cloudflare-CDN-Cache-Control"] = isVersioned
            ? "public, max-age=604800, immutable"
            : "public, max-age=86400";
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

if (trustedPlayerSelectionEnabled)
{
    app.MapGet("/auth/trusted-players", (IGameStateStore store) =>
        TryResult(() =>
        {
            var state = store.LoadOrCreate();
            var cycle = GetActiveCycle(state);
            return TrustedPlayerSelection.List(state, cycle);
        }));

    app.MapPost("/auth/login", (LoginRequest request, HttpContext httpContext, IGameStateStore store) =>
        TryResult(() => store.Update(state => Login(state, request, httpContext, DateTimeOffset.UtcNow))));

    app.MapPost("/auth/logout", (HttpContext httpContext) =>
    {
        DevelopmentAuth.SignOut(httpContext);
        return Results.Ok(new { signedOut = true });
    });

    app.MapGet("/auth/logout", (HttpContext httpContext) =>
    {
        DevelopmentAuth.SignOut(httpContext);
        return Results.Redirect("/app.html");
    });
}
else
{
    app.MapGet("/auth/external/login", () => Results.Challenge(
        new AuthenticationProperties { RedirectUri = "/app.html" },
        [CyclesAuthenticationSchemes.OpenIdConnect]));

    app.MapGet("/auth/logout", () => Results.SignOut(
        new AuthenticationProperties { RedirectUri = "/" },
        [CyclesAuthenticationSchemes.Cookie, CyclesAuthenticationSchemes.OpenIdConnect]));

    app.MapGet("/auth/error", (string? code) => Results.Json(
        new ErrorResponse(
            code is "accessDenied" ? ApiErrorCodes.Forbidden : ApiErrorCodes.AuthenticationRequired,
            code is "accessDenied"
                ? "The identity provider denied access."
                : "External authentication could not be completed.",
            Details: null,
            TraceId: null),
        statusCode: code is "accessDenied" ? StatusCodes.Status403Forbidden : StatusCodes.Status401Unauthorized));
}

app.MapGet("/auth/session", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var empire = actor.Empire
            ?? throw new InvalidOperationException("Admin requests must identify an empire.");
        return ToLoginResponse(state, actor.Player, empire, trustedPlayerSelectionEnabled);
    }));

app.MapGet("/cycles/current", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        _ = DevelopmentAuth.RequireActor(httpContext, state);
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
        var domainLinks = state.SystemLinks.Where(link => link.CycleId == cycle.CycleId).ToArray();
        var domainSectors = state.Sectors.Where(sector => sector.CycleId == cycle.CycleId).ToArray();
        var systemsById = domainSystems.ToDictionary(system => system.SystemId);
        var gatewaySystemIds = new HashSet<Guid>();
        var adjacentSectorIds = domainSectors.ToDictionary(sector => sector.SectorId, _ => new HashSet<Guid>());
        var sectorSortOrders = domainSectors.ToDictionary(sector => sector.SectorId, sector => sector.SortOrder);
        foreach (var link in domainLinks)
        {
            if (!systemsById.TryGetValue(link.SystemAId, out var systemA)
                || !systemsById.TryGetValue(link.SystemBId, out var systemB)
                || systemA.SectorId == systemB.SectorId)
            {
                continue;
            }

            gatewaySystemIds.Add(systemA.SystemId);
            gatewaySystemIds.Add(systemB.SystemId);
            if (adjacentSectorIds.TryGetValue(systemA.SectorId, out var systemAAdjacentSectors)
                && adjacentSectorIds.TryGetValue(systemB.SectorId, out var systemBAdjacentSectors))
            {
                systemAAdjacentSectors.Add(systemB.SectorId);
                systemBAdjacentSectors.Add(systemA.SectorId);
            }
        }

        var systems = domainSystems.Select(system => ToGalaxySystemResponse(system, gatewaySystemIds.Contains(system.SystemId))).ToArray();
        var links = domainLinks.Select(ToSystemLinkResponse).ToArray();
        var sectors = domainSectors
            .OrderBy(sector => sector.SortOrder)
            .ThenBy(sector => sector.SectorName)
            .Select(sector => new GalaxySectorResponse(
                sector.SectorId,
                sector.CycleId,
                sector.SectorName,
                sector.CentreX,
                sector.CentreY,
                sector.SortOrder,
                domainSystems.Count(system => system.SectorId == sector.SectorId),
                gatewaySystemIds
                    .Where(systemId => systemsById[systemId].SectorId == sector.SectorId)
                    .OrderBy(systemId => systemsById[systemId].SystemName)
                    .ToArray(),
                adjacentSectorIds[sector.SectorId]
                    .OrderBy(sectorId => sectorSortOrders[sectorId])
                    .ToArray()))
            .ToArray();
        var presence = domainSystems.Select(system =>
        {
            var effectivePresence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);
            return new SystemPresenceResponse(
                system.SystemId,
                ApiVisibility.FilterPresence(actor, visibleSystemIds, system.SystemId, effectivePresence));
        }).ToArray();
        var factions = state.Factions
            .Where(item => item.CycleId == cycle.CycleId)
            .OrderBy(item => item.FactionName)
            .Select(item => new FactionResponse(item.FactionId, item.EmpireId, item.FactionName, item.Kind, item.Status))
            .ToArray();

        var outposts = state.ColonialOutposts
            .Where(item => item.CycleId == cycle.CycleId)
            .Where(item => actor.IsAdmin
                           || visibleSystemIds.Contains(item.SystemId)
                           || item.EmpireId == actor.Empire?.EmpireId)
            .OrderBy(item => domainSystems.Single(system => system.SystemId == item.SystemId).SystemName)
            .ThenBy(item => state.Empires.Single(empire => empire.EmpireId == item.EmpireId).EmpireName)
            .Select(item => ToColonialOutpostResponse(state, item))
            .ToArray();

        return new GalaxyResponse(ToCycleResponse(cycle), sectors, systems, links, presence, factions, outposts);
    }));

app.MapGet("/systems/{systemId:guid}", (Guid systemId, HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var system = state.Systems.SingleOrDefault(item => item.CycleId == cycle.CycleId && item.SystemId == systemId)
            ?? throw new ApiNotFoundException("System was not found in the active cycle.");

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
            ?? throw new ApiNotFoundException("Fleet was not found in the active cycle.");
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

app.MapPost("/orders/fleet/recall", (RecallFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitRecall(request, httpContext, store));

app.MapPost("/orders/fleet/attack", (AttackFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitAttack(request, httpContext, store));

app.MapPost("/orders/fleet/colonise", (ColoniseFleetRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.SubmitColonise(request, httpContext, store));

app.MapPost("/orders/fleet/cancel", (CancelFleetOrderRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.Cancel(request, httpContext, store));

app.MapPost("/orders/priorities", (PriorityRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiOrderEndpoints.UpdatePriorities(request, httpContext, store));

app.MapPost("/admin/tick", (HttpContext httpContext, IGameStateStore store) =>
    ApiAdminEndpoints.RunTick(httpContext, store, trustedPlayerSelectionEnabled));

app.MapPost("/admin/players/{targetPlayerId:guid}/roles/admin", (Guid targetPlayerId, AdminRoleChangeRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiAdminRoleEndpoints.Grant(targetPlayerId, request, httpContext, store));

app.MapDelete("/admin/players/{targetPlayerId:guid}/roles/admin", (Guid targetPlayerId, [FromBody] AdminRoleChangeRequest request, HttpContext httpContext, IGameStateStore store) =>
    ApiAdminRoleEndpoints.Revoke(targetPlayerId, request, httpContext, store));

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

app.MapGet("/briefings/opening", (HttpContext httpContext, IGameStateStore store) =>
    ApiEndpointResults.TryJson(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return OpeningBriefingContract.FindVisible(state, cycle, actor, visibleSystemIds);
    }));

app.MapGet("/chronicle", (HttpContext httpContext, IGameStateStore store) =>
    TryResult(() =>
    {
        var state = store.LoadOrCreate();
        var cycle = GetActiveCycle(state);
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var eventTicksById = state.Events
            .Where(item => item.CycleId == cycle.CycleId)
            .ToDictionary(item => item.EventId, item => item.TickNumber);
        var battleTicksById = state.BattleRecords
            .Where(item => item.CycleId == cycle.CycleId)
            .ToDictionary(item => item.BattleId, item => item.TickNumber);
        return state.ChronicleEntries
            .Where(entry => entry.CycleId == cycle.CycleId)
            .Where(entry => ApiVisibility.CanSeeChronicleEntry(entry, actor, visibleSystemIds))
            .OrderByDescending(entry => entry.ImportanceScore)
            .Select(entry => ToChronicleEntryResponse(entry, eventTicksById, battleTicksById))
            .ToArray();
    }));

app.Run();

static IResult TryResult<T>(Func<T> action)
{
    try
    {
        return Results.Ok(action());
    }
    catch (Exception ex) when (ApiErrorResponses.IsHandled(ex))
    {
        return ApiErrorResponses.ToResult(ex);
    }
}

static Cycle GetActiveCycle(GameState state) =>
    state.GetActiveCycle() ?? throw new InvalidOperationException("No active cycle exists.");

static LoginResponse Login(
    GameState state,
    LoginRequest request,
    HttpContext httpContext,
    DateTimeOffset now)
{
    var cycle = GetActiveCycle(state);
    var selection = TrustedPlayerSelection.Resolve(state, cycle, request.PlayerId);
    var player = selection.Player;
    var participant = selection.Participant;
    var empire = selection.Empire;
    player.LastLoginAt = now;
    PlayerProvisioning.RepairLegacyStartingAdmiralName(state, empire, player, now);

    DevelopmentAuth.SignIn(httpContext, player);
    return ToLoginResponse(state, player, empire, trustedPlayerSelectionEnabled: true);
}

static LoginResponse ToLoginResponse(GameState state, Player player, Empire empire, bool trustedPlayerSelectionEnabled)
{
    var participant = state.GetParticipant(empire.CycleId, player.PlayerId)
        ?? throw new InvalidOperationException("The player is not participating in the Empire's Cycle.");
    return
    new(
        player.PlayerId,
        player.Username,
        player.Role,
        TrustedPlayerSelection.CanAdvanceTurn(player, participant, empire, trustedPlayerSelectionEnabled),
        ToEmpireResponse(state, empire));
}

static EmpireResponse ToEmpireResponse(GameState state, Empire empire)
{
    var home = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
    var systemsById = state.Systems
        .Where(system => system.CycleId == empire.CycleId)
        .ToDictionary(system => system.SystemId);
    var homeIsGateway = state.SystemLinks.Any(link =>
        link.CycleId == empire.CycleId
        && (link.SystemAId == home.SystemId || link.SystemBId == home.SystemId)
        && systemsById.TryGetValue(link.SystemAId == home.SystemId ? link.SystemBId : link.SystemAId, out var destination)
        && destination.SectorId != home.SectorId);
    var resources = state.EmpireResources.Single(resource => resource.EmpireId == empire.EmpireId);
    var priorities = state.EmpirePriorities.Single(priority => priority.EmpireId == empire.EmpireId);

    return new EmpireResponse(
        empire.EmpireId,
        state.GetEmpireFaction(empire.EmpireId).FactionId,
        empire.PlayerId,
        empire.EmpireName,
        ToGalaxySystemResponse(home, homeIsGateway),
        ToEmpireResourceResponse(resources),
        ToEmpirePriorityResponse(priorities),
        state.Fleets.Count(fleet => fleet.EmpireId == empire.EmpireId && fleet.Status != FleetStatus.Destroyed));
}

static FleetResponse ToFleetResponse(GameState state, Fleet fleet)
{
    var currentSystem = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
    var destination = fleet.DestinationSystemId.HasValue
        ? state.Systems.Single(item => item.SystemId == fleet.DestinationSystemId.Value)
        : null;

    return new FleetResponse(
        ToFleetDataResponse(fleet),
        FleetContractMapping.GetOwnerName(state, fleet),
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
            .OrderBy(item => state.Factions.Single(factionItem => factionItem.FactionId == item.FactionId).FactionName)
            .ThenBy(item => item.FleetName)
            .Select(item =>
            {
                var otherFaction = state.Factions.Single(factionItem => factionItem.FactionId == item.FactionId);
                return new FleetAtSystemResponse(
                    item.FleetId,
                    item.FleetName,
                    otherFaction.EmpireId,
                    otherFaction.FactionId,
                    otherFaction.FactionName,
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
        fleet.FactionId,
        fleet.FleetName,
        FleetContractMapping.GetOwnerName(state, fleet),
        fleet.ShipCount,
        fleet.Status,
        ToAdmiralSummary(state, fleet.AdmiralId),
        ToSystemSummaryResponse(currentSystem),
        destination is null ? null : ToSystemSummaryResponse(destination),
        fleet.DepartureTickNumber,
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
        cycle.TurnStage,
        cycle.Status,
        cycle.CreatedAt);

static GalaxySystemResponse ToGalaxySystemResponse(GalaxySystem system, bool isGateway = false) =>
    new(
        system.SystemId,
        system.CycleId,
        system.SystemName,
        system.X,
        system.Y,
        system.SectorId,
        isGateway,
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
        fleet.FactionId,
        fleet.AdmiralId,
        fleet.FleetName,
        fleet.CurrentSystemId,
        fleet.DestinationSystemId,
        fleet.DepartureTickNumber,
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
        item.CreatedAt);

static ChronicleEntryResponse ToChronicleEntryResponse(
    ChronicleEntry item,
    IReadOnlyDictionary<Guid, int> eventTicksById,
    IReadOnlyDictionary<Guid, int> battleTicksById) =>
    new(
        item.ChronicleEntryId,
        item.SourceEventId,
        item.SourceBattleId,
        item.CycleId,
        item.SystemId,
        item.Title,
        item.SourceBattleId is Guid battleId && battleTicksById.TryGetValue(battleId, out var battleTick)
            ? battleTick
            : item.SourceEventId is Guid eventId && eventTicksById.TryGetValue(eventId, out var eventTick)
                ? eventTick
                : null,
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
            var faction = state.Factions.Single(factionItem => factionItem.FactionId == item.Key);
            var share = totalPresence == 0 ? 0 : decimal.Round(item.Value / totalPresence * 100, 2);
            return new SystemInfluenceResponse(faction.EmpireId, faction.FactionId, faction.FactionName, item.Value, share);
        })
        .ToArray();

    var activeFleets = canSeeDetails
        ? state.Fleets
            .Where(item => item.CycleId == cycle.CycleId
                           && item.CurrentSystemId == system.SystemId
                           && item.Status == FleetStatus.Active)
            .OrderBy(item => state.Factions.Single(factionItem => factionItem.FactionId == item.FactionId).FactionName)
            .ThenBy(item => item.FleetName)
            .Select(item =>
            {
                var faction = state.Factions.Single(factionItem => factionItem.FactionId == item.FactionId);
                return new FleetAtSystemResponse(
                    item.FleetId,
                    item.FleetName,
                    faction.EmpireId,
                    faction.FactionId,
                    faction.FactionName,
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
    var targetFaction = order.TargetFactionId.HasValue
        ? state.Factions.SingleOrDefault(item => item.FactionId == order.TargetFactionId.Value)
        : null;

    return new FleetOrderResponse(
        order.FleetOrderId,
        order.FleetId,
        order.OrderType,
        order.Status,
        order.CommandSource,
        order.SubmitTick,
        order.ExecuteAfterTick,
        order.ProcessedTick,
        order.SealedTick,
        order.SealedAt,
        order.RejectionReason,
        order.SupersededByOrderId,
        order.TargetSystemId,
        order.TargetEmpireId,
        order.TargetFactionId,
        fleet?.FleetName ?? "Unknown fleet",
        targetSystem?.SystemName,
        targetFaction?.FactionName ?? targetEmpire?.EmpireName);
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
    var eventTicksById = events.ToDictionary(item => item.EventId, item => item.TickNumber);
    var battleTicksById = battles.ToDictionary(item => item.BattleId, item => item.TickNumber);
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
        chronicleEntries.Select(entry => ToChronicleEntryResponse(entry, eventTicksById, battleTicksById)).ToArray());
}

public sealed record LoginRequest(
    string? Username = null,
    string? EmpireName = null,
    bool IsAdmin = false,
    Guid? PlayerId = null);

public sealed record TrustedPlayerResponse(
    Guid PlayerId,
    string PlayerName,
    MatchParticipantStatus ParticipantStatus);

public sealed record LoginResponse(
    Guid PlayerId,
    string Username,
    PlayerRole Role,
    bool CanAdvanceTurn,
    EmpireResponse Empire);

public sealed record EmpireResponse(
    Guid EmpireId,
    Guid FactionId,
    Guid PlayerId,
    string EmpireName,
    GalaxySystemResponse HomeSystem,
    EmpireResourceResponse Resources,
    EmpirePriorityResponse Priorities,
    int FleetCount);

public sealed record GalaxyResponse(
    CycleResponse Cycle,
    IReadOnlyCollection<GalaxySectorResponse> Sectors,
    IReadOnlyCollection<GalaxySystemResponse> Systems,
    IReadOnlyCollection<SystemLinkResponse> Links,
    IReadOnlyCollection<SystemPresenceResponse> Presence,
    IReadOnlyCollection<FactionResponse> Factions,
    IReadOnlyCollection<ColonialOutpostResponse> ColonialOutposts);

public sealed record SystemPresenceResponse(Guid SystemId, IReadOnlyDictionary<Guid, decimal> EffectivePresence);

public sealed record FactionResponse(
    Guid FactionId,
    Guid? EmpireId,
    string FactionName,
    FactionKind Kind,
    FactionStatus Status);

public sealed record GalaxySectorResponse(
    Guid SectorId,
    Guid CycleId,
    string SectorName,
    int CentreX,
    int CentreY,
    int SortOrder,
    int SystemCount,
    IReadOnlyCollection<Guid> GatewaySystemIds,
    IReadOnlyCollection<Guid> AdjacentSectorIds);

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
    Guid FactionId,
    string FleetName,
    string EmpireName,
    int ShipCount,
    FleetStatus Status,
    AdmiralSummaryResponse? Admiral,
    SystemSummaryResponse CurrentSystem,
    SystemSummaryResponse? DestinationSystem,
    int? DepartureTickNumber,
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
    Guid? EmpireId,
    Guid FactionId,
    string FactionName,
    decimal EffectivePresence,
    decimal InfluencePercent);

public sealed record FleetAtSystemResponse(
    Guid FleetId,
    string FleetName,
    Guid? EmpireId,
    Guid FactionId,
    string FactionName,
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
    Guid FleetId,
    FleetOrderType OrderType,
    FleetOrderStatus Status,
    FleetOrderCommandSource CommandSource,
    int SubmitTick,
    int ExecuteAfterTick,
    int? ProcessedTick,
    int? SealedTick,
    DateTimeOffset? SealedAt,
    string? RejectionReason,
    Guid? SupersededByOrderId,
    Guid? TargetSystemId,
    Guid? TargetEmpireId,
    Guid? TargetFactionId,
    string FleetName,
    string? TargetSystemName,
    string? TargetFactionName);

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
    TurnResolutionStage TurnStage,
    CycleStatus Status,
    DateTimeOffset CreatedAt);

public sealed record GalaxySystemResponse(
    Guid SystemId,
    Guid CycleId,
    string SystemName,
    int X,
    int Y,
    Guid SectorId,
    bool IsGateway,
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
    Guid FactionId,
    Guid? AdmiralId,
    string FleetName,
    Guid CurrentSystemId,
    Guid? DestinationSystemId,
    int? DepartureTickNumber,
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
    DateTimeOffset CreatedAt);

public sealed record ChronicleEntryResponse(
    Guid ChronicleEntryId,
    Guid? SourceEventId,
    Guid? SourceBattleId,
    Guid CycleId,
    Guid? SystemId,
    string Title,
    int? TickNumber,
    ChronicleEntryType EntryType,
    int ImportanceScore,
    string FactualSummary,
    string NarrativeText,
    NarrativeGenerationStatus NarrativeStatus,
    string NarrativeContextJson,
    DateTimeOffset? NarrativeGeneratedAt,
    string? NarrativeFailureReason,
    DateTimeOffset CreatedAt);

public sealed record MoveFleetRequest(Guid FleetId, Guid TargetSystemId, Guid? ReplacesOrderId = null);

public sealed record RecallFleetRequest(Guid FleetId);

public sealed record AttackFleetRequest(
    Guid FleetId,
    Guid? TargetEmpireId,
    Guid? ReplacesOrderId = null,
    Guid? TargetFactionId = null);

public sealed record ColoniseFleetRequest(Guid FleetId, Guid? ReplacesOrderId = null);

public sealed record CancelFleetOrderRequest(Guid FleetOrderId);

public sealed record PriorityRequest(
    Guid? EmpireId,
    int IndustryWeight,
    int ResearchWeight,
    int MilitaryWeight,
    int ExpansionWeight);
