using Cycles.Application;
using Cycles.Core;
using Cycles.Infrastructure.SqlServer;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
GameProfileCatalogue.EnsureValid();
var trustedPlayerSelectionEnabled = builder.Configuration.GetValue<bool?>("Cycles:TrustedPlayerSelection:Enabled")
    ?? builder.Environment.IsDevelopment();
var configuredSqlConnectionString = builder.Configuration.GetConnectionString("Cycles")
    ?? builder.Configuration["Cycles:SqlConnectionString"]
    ?? Environment.GetEnvironmentVariable("CYCLES_SQL_CONNECTION_STRING");
if (string.IsNullOrWhiteSpace(configuredSqlConnectionString))
{
    throw new InvalidOperationException("Cycles.Api requires a Cycles SQL connection string. Configure ConnectionStrings:Cycles or CYCLES_SQL_CONNECTION_STRING.");
}
builder.Services.AddSingleton(new SqlServerGameStateStore(configuredSqlConnectionString));
builder.Services.AddSingleton<IPlayerAccountQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IGameCatalogueQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IGameAccessQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IGameCommandAccessQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ICycleViewQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ICycleCommandStore>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ICycleResolutionStore>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ILegacyRuntimeScopeQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IPlayerAccountCommandStore>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<ITrustedPlayerSelectionQuery>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<IAdminRoleCommandStore>(services => services.GetRequiredService<SqlServerGameStateStore>());
builder.Services.AddSingleton<SelectedGameRequestService>();
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
builder.Services.AddCyclesApiAntiforgery(builder.Environment);

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
app.MapGet(ApiAntiforgery.EndpointPath, (HttpContext httpContext, IAntiforgery antiforgery) =>
    Results.Ok(ApiAntiforgery.IssueToken(httpContext, antiforgery)));

if (trustedPlayerSelectionEnabled)
{
    app.MapGet("/auth/trusted-players", (
        ILegacyRuntimeScopeQuery legacyScope,
        ITrustedPlayerSelectionQuery trustedPlayers) =>
        TryResult(() => TrustedPlayerSelection.List(trustedPlayers.List(legacyScope.GetRequired()))));

    app.MapPost("/auth/login", (
        LoginRequest request,
        HttpContext httpContext,
        IPlayerAccountCommandStore accounts,
        ITrustedPlayerSelectionQuery trustedPlayers,
        ILegacyRuntimeScopeQuery legacyScope,
        IGameCommandAccessQuery gameAccess,
        ICycleViewQuery cycleView) =>
        TryResult(() => Login(
            request,
            httpContext,
            accounts,
            trustedPlayers,
            legacyScope,
            gameAccess,
            cycleView,
            DateTimeOffset.UtcNow)))
        .RequireCyclesAntiforgery();

    app.MapPost("/auth/logout", (HttpContext httpContext) =>
    {
        DevelopmentAuth.SignOut(httpContext);
        ApiAntiforgery.ExpireTokenCookie(httpContext);
        httpContext.Response.Headers.Location = "/app.html";
        return Results.StatusCode(StatusCodes.Status303SeeOther);
    }).RequireCyclesAntiforgery();
}
else
{
    app.MapGet("/auth/external/login", () => Results.Challenge(
        new AuthenticationProperties { RedirectUri = "/app.html" },
        [CyclesAuthenticationSchemes.OpenIdConnect]));

    app.MapPost("/auth/logout", (HttpContext httpContext) =>
    {
        ApiAntiforgery.ExpireTokenCookie(httpContext);
        return Results.SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            [CyclesAuthenticationSchemes.Cookie, CyclesAuthenticationSchemes.OpenIdConnect]);
    }).RequireCyclesAntiforgery();

    app.MapGet("/auth/error", (string? code) =>
    {
        var accessDenied = code is ExternalAuthenticationFailureCodes.AccessDenied;
        var temporarilyBusy = code is ExternalAuthenticationFailureCodes.TemporarilyBusy;
        return Results.Json(
            new ErrorResponse(
                accessDenied
                    ? ApiErrorCodes.Forbidden
                    : temporarilyBusy
                        ? ApiErrorCodes.StateConflict
                        : ApiErrorCodes.AuthenticationRequired,
                accessDenied
                    ? "The identity provider denied access."
                    : temporarilyBusy
                        ? "Player account sign-in is temporarily busy. Try again."
                        : "External authentication could not be completed.",
                Details: null,
                TraceId: null),
            statusCode: accessDenied
                ? StatusCodes.Status403Forbidden
                : temporarilyBusy
                    ? StatusCodes.Status409Conflict
                    : StatusCodes.Status401Unauthorized);
    });
}

app.MapGet("/auth/session", (
    HttpContext httpContext,
    IPlayerAccountQuery accounts) =>
    TryResult(() =>
    {
        var account = DevelopmentAuth.RequireAccount(httpContext, accounts);
        return new AccountSessionResponse(
            account.PlayerId,
            account.Username,
            account.Role);
    }));

var selectedGameRoutes = app.MapGroup("/games/{gameId:guid}");

selectedGameRoutes.MapGet("/dashboard/bootstrap", (
    Guid gameId,
    Guid? selectedFleetId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    GetDashboardBootstrap(gameId, selectedFleetId, httpContext, games, trustedPlayerSelectionEnabled));
selectedGameRoutes.MapGet("/cycles/current", (Guid gameId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetCurrentCycle(gameId, httpContext, games));
selectedGameRoutes.MapGet("/ticks/last-summary", (Guid gameId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetLastTickSummary(gameId, httpContext, games));
selectedGameRoutes.MapGet("/empire", (Guid gameId, Guid? empireId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetEmpire(gameId, empireId, httpContext, games));
selectedGameRoutes.MapGet("/galaxy", (Guid gameId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetGalaxy(gameId, httpContext, games));
selectedGameRoutes.MapGet("/systems/{systemId:guid}", (Guid gameId, Guid systemId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetSystem(gameId, systemId, httpContext, games));
selectedGameRoutes.MapGet("/fleets", (Guid gameId, Guid? empireId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetFleets(gameId, empireId, httpContext, games));
selectedGameRoutes.MapGet("/fleets/{fleetId:guid}", (Guid gameId, Guid fleetId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetFleet(gameId, fleetId, httpContext, games));
selectedGameRoutes.MapGet("/orders", (Guid gameId, Guid? empireId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetOrders(gameId, empireId, httpContext, games));
selectedGameRoutes.MapGet("/events/recent", (Guid gameId, int? limit, HttpContext httpContext, SelectedGameRequestService games) =>
    GetRecentEvents(gameId, limit, httpContext, games));
selectedGameRoutes.MapGet("/briefings/opening", (Guid gameId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetOpeningBriefing(gameId, httpContext, games));
selectedGameRoutes.MapGet("/chronicle", (Guid gameId, HttpContext httpContext, SelectedGameRequestService games) =>
    GetChronicle(gameId, httpContext, games));
selectedGameRoutes.MapPost("/orders/move", (Guid gameId, MoveFleetRequest request, HttpContext httpContext, SelectedGameRequestService games) =>
    ApiOrderEndpoints.SubmitMove(request, httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapPost("/orders/recall", (Guid gameId, RecallFleetRequest request, HttpContext httpContext, SelectedGameRequestService games) =>
    ApiOrderEndpoints.SubmitRecall(request, httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapPost("/orders/attack", (Guid gameId, AttackFleetRequest request, HttpContext httpContext, SelectedGameRequestService games) =>
    ApiOrderEndpoints.SubmitAttack(request, httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapPost("/orders/colonise", (Guid gameId, ColoniseFleetRequest request, HttpContext httpContext, SelectedGameRequestService games) =>
    ApiOrderEndpoints.SubmitColonise(request, httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapDelete("/orders/{fleetOrderId:guid}", (
    Guid gameId,
    Guid fleetOrderId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    ApiOrderEndpoints.Cancel(new CancelFleetOrderRequest(fleetOrderId), httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapPut("/priorities", (Guid gameId, PriorityRequest request, HttpContext httpContext, SelectedGameRequestService games) =>
    ApiOrderEndpoints.UpdatePriorities(request, httpContext, gameId, games))
    .RequireCyclesAntiforgery();
selectedGameRoutes.MapPost("/admin/tick", (
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games,
    ICycleResolutionStore resolutions) =>
    ApiAdminEndpoints.RunTick(
        httpContext,
        gameId,
        games,
        resolutions,
        trustedPlayerSelectionEnabled))
    .RequireCyclesAntiforgery();

app.MapGet("/dashboard/bootstrap", (
    Guid? selectedFleetId,
    HttpContext httpContext,
    SelectedGameRequestService games,
    ILegacyRuntimeScopeQuery legacyScope) =>
    GetDashboardBootstrap(
        GetLegacyGameId(httpContext, games, legacyScope),
        selectedFleetId,
        httpContext,
        games,
        trustedPlayerSelectionEnabled));
app.MapGet("/cycles/current", (HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetCurrentCycle(GetLegacyGameId(httpContext, games, legacyScope), httpContext, games));
app.MapGet("/ticks/last-summary", (HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetLastTickSummary(GetLegacyGameId(httpContext, games, legacyScope), httpContext, games));
app.MapGet("/empire", (Guid? empireId, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetEmpire(GetLegacyGameId(httpContext, games, legacyScope), empireId, httpContext, games));
app.MapGet("/galaxy", (HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetGalaxy(GetLegacyGameId(httpContext, games, legacyScope), httpContext, games));
app.MapGet("/systems/{systemId:guid}", (Guid systemId, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetSystem(GetLegacyGameId(httpContext, games, legacyScope), systemId, httpContext, games));
app.MapGet("/fleets", (Guid? empireId, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetFleets(GetLegacyGameId(httpContext, games, legacyScope), empireId, httpContext, games));
app.MapGet("/fleets/{fleetId:guid}", (Guid fleetId, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetFleet(GetLegacyGameId(httpContext, games, legacyScope), fleetId, httpContext, games));
app.MapGet("/orders", (Guid? empireId, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetOrders(GetLegacyGameId(httpContext, games, legacyScope), empireId, httpContext, games));
app.MapPost("/orders/fleet/move", (MoveFleetRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.SubmitMove(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();
app.MapPost("/orders/fleet/recall", (RecallFleetRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.SubmitRecall(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();
app.MapPost("/orders/fleet/attack", (AttackFleetRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.SubmitAttack(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();
app.MapPost("/orders/fleet/colonise", (ColoniseFleetRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.SubmitColonise(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();
app.MapPost("/orders/fleet/cancel", (CancelFleetOrderRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.Cancel(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();
app.MapPost("/orders/priorities", (PriorityRequest request, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    ApiOrderEndpoints.UpdatePriorities(request, httpContext, GetLegacyGameId(httpContext, games, legacyScope), games))
    .RequireCyclesAntiforgery();

app.MapPost("/admin/tick", (
    HttpContext httpContext,
    SelectedGameRequestService games,
    ICycleResolutionStore resolutions,
    ILegacyRuntimeScopeQuery legacyScope) =>
    ApiAdminEndpoints.RunTick(
        httpContext,
        GetLegacyGameId(httpContext, games, legacyScope),
        games,
        resolutions,
        trustedPlayerSelectionEnabled))
    .RequireCyclesAntiforgery();

app.MapPost("/admin/players/{targetPlayerId:guid}/roles/admin", (
    Guid targetPlayerId,
    AdminRoleChangeRequest request,
    HttpContext httpContext,
    IPlayerAccountQuery accounts,
    IAdminRoleCommandStore roleCommands) =>
    ApiAdminRoleEndpoints.Grant(targetPlayerId, request, httpContext, accounts, roleCommands))
    .RequireCyclesAntiforgery();

app.MapDelete("/admin/players/{targetPlayerId:guid}/roles/admin", (
    Guid targetPlayerId,
    [FromBody] AdminRoleChangeRequest request,
    HttpContext httpContext,
    IPlayerAccountQuery accounts,
    IAdminRoleCommandStore roleCommands) =>
    ApiAdminRoleEndpoints.Revoke(targetPlayerId, request, httpContext, accounts, roleCommands))
    .RequireCyclesAntiforgery();

app.MapGet("/events/recent", (int? limit, HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetRecentEvents(GetLegacyGameId(httpContext, games, legacyScope), limit, httpContext, games));
app.MapGet("/briefings/opening", (HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetOpeningBriefing(GetLegacyGameId(httpContext, games, legacyScope), httpContext, games));
app.MapGet("/chronicle", (HttpContext httpContext, SelectedGameRequestService games, ILegacyRuntimeScopeQuery legacyScope) =>
    GetChronicle(GetLegacyGameId(httpContext, games, legacyScope), httpContext, games));

app.Run();

static Guid GetLegacyGameId(
    HttpContext httpContext,
    SelectedGameRequestService games,
    ILegacyRuntimeScopeQuery legacyScope)
{
    _ = games.RequireAccount(httpContext);
    return legacyScope.GetRequired().GameId;
}

static IResult GetDashboardBootstrap(
    Guid gameId,
    Guid? selectedFleetId,
    HttpContext httpContext,
    SelectedGameRequestService games,
    bool trustedPlayerSelectionEnabled) =>
    TryResult(() => ToDashboardBootstrapResponse(
        gameId,
        DashboardBootstrapContextFactory.Load(selectedFleetId, httpContext, gameId, games),
        trustedPlayerSelectionEnabled));

static IResult GetCurrentCycle(
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
        ToCycleResponse(GetCycle(state, context))));

static IResult GetLastTickSummary(
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var tickLog = state.TickLogs
            .Where(log => log.CycleId == cycle.CycleId)
            .OrderByDescending(log => log.TickNumber)
            .ThenByDescending(log => log.CompletedAt ?? log.StartedAt)
            .FirstOrDefault();

        return ToLastTickSummaryResponse(state, cycle, tickLog, actor, visibleSystemIds);
    }));

static IResult GetEmpire(
    Guid gameId,
    Guid? empireId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var targetEmpireId = DevelopmentAuth.ResolveEmpireId(state, actor, context, empireId);
        var empire = state.Empires.Single(item =>
            item.CycleId == cycle.CycleId
            && item.EmpireId == targetEmpireId);

        return ToEmpireResponse(state, empire);
    }));

static IResult GetGalaxy(
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return ToGalaxyResponse(state, cycle, actor, visibleSystemIds);
    }));

static IResult GetSystem(
    Guid gameId,
    Guid systemId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var system = state.Systems.SingleOrDefault(item =>
                item.CycleId == context.CycleId
                && item.SystemId == systemId)
            ?? throw new ApiNotFoundException("System was not found in the selected Cycle.");

        return ToSystemDetailResponse(state, cycle, system, actor, visibleSystemIds);
    }));

static IResult GetFleets(
    Guid gameId,
    Guid? empireId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        Guid? targetEmpireId = actor.IsAdmin && !empireId.HasValue
            ? null
            : DevelopmentAuth.ResolveEmpireId(state, actor, context, empireId);
        return ToFleetResponses(state, cycle, targetEmpireId);
    }));

static IResult GetFleet(
    Guid gameId,
    Guid fleetId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var fleet = state.Fleets.SingleOrDefault(item =>
                item.CycleId == context.CycleId
                && item.FleetId == fleetId)
            ?? throw new ApiNotFoundException("Fleet was not found in the selected Cycle.");
        if (!actor.IsAdmin && fleet.EmpireId != context.EmpireId)
        {
            throw new ApiForbiddenException("The authenticated player cannot inspect this fleet.");
        }

        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return ToFleetDetailResponse(state, cycle, fleet, actor, visibleSystemIds);
    }));

static IResult GetOrders(
    Guid gameId,
    Guid? empireId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        Guid? targetEmpireId = actor.IsAdmin && !empireId.HasValue
            ? null
            : DevelopmentAuth.ResolveEmpireId(state, actor, context, empireId);
        return ToOrderResponses(state, cycle, targetEmpireId);
    }));

static IResult GetRecentEvents(
    Guid gameId,
    int? limit,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return ToEventResponses(state, cycle, actor, visibleSystemIds, limit ?? 25);
    }));

static IResult GetOpeningBriefing(
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    ApiEndpointResults.TryJson(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return OpeningBriefingContract.FindVisible(state, cycle, actor, visibleSystemIds);
    }));

static IResult GetChronicle(
    Guid gameId,
    HttpContext httpContext,
    SelectedGameRequestService games) =>
    TryResult(() => games.Query(httpContext, gameId, (state, context) =>
    {
        var cycle = GetCycle(state, context);
        var actor = DevelopmentAuth.RequireActor(state, context);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        return ToChronicleEntryResponses(state, cycle, actor, visibleSystemIds);
    }));

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

static Cycle GetCycle(GameState state, GameCommandContext context) =>
    state.Cycles.SingleOrDefault(item => item.CycleId == context.CycleId)
        ?? throw new InvalidOperationException("The selected Cycle is unavailable.");

static LoginResponse Login(
    LoginRequest request,
    HttpContext httpContext,
    IPlayerAccountCommandStore accounts,
    ITrustedPlayerSelectionQuery trustedPlayers,
    ILegacyRuntimeScopeQuery legacyScope,
    IGameCommandAccessQuery gameAccess,
    ICycleViewQuery cycleView,
    DateTimeOffset now)
{
    var scope = legacyScope.GetRequired();
    var playerId = TrustedPlayerSelection.RequireListedPlayerId(
        request.PlayerId,
        trustedPlayers.List(scope));
    var context = gameAccess.Get(playerId, scope)
        ?? throw new ApiForbiddenException("The selected player is not available in the current Cycle.");
    var account = accounts.RecordLogin(new RecordPlayerLoginCommand(playerId, now)) switch
    {
        AccountCommandResult<PlayerAccountSnapshot>.Success success => success.Value,
        AccountCommandResult<PlayerAccountSnapshot>.Unavailable =>
            throw new ApiForbiddenException("The selected player is not available for trusted sign-in."),
        AccountCommandResult<PlayerAccountSnapshot>.Busy =>
            throw new ApiStateConflictException("Player account sign-in is temporarily busy. Try again."),
        _ => throw new InvalidOperationException("The player account store returned an unsupported login result.")
    };
    var response = QueryLoginResponse(context, cycleView, trustedPlayerSelectionEnabled: true);

    // Do not issue a durable browser identity unless every authoritative read
    // needed to construct the legacy response has succeeded.
    DevelopmentAuth.SignIn(httpContext, account.PlayerId);
    ApiAntiforgery.ExpireTokenCookie(httpContext);
    return response;
}

static LoginResponse QueryLoginResponse(
    GameCommandContext context,
    ICycleViewQuery cycleView,
    bool trustedPlayerSelectionEnabled)
{
    var result = cycleView.Query(context, state =>
    {
        var actor = DevelopmentAuth.RequireActor(state, context);
        var empire = actor.Empire
            ?? throw new InvalidOperationException("The scoped player has no empire in the current Cycle.");
        return ToLoginResponse(
            context.GameAccess.GameId,
            state,
            actor.Player,
            empire,
            trustedPlayerSelectionEnabled);
    });
    return result switch
    {
        ScopedQueryResult<LoginResponse>.Success success => success.Value,
        ScopedQueryResult<LoginResponse>.Unavailable =>
            throw new ApiForbiddenException("The current Cycle is not available to the authenticated player."),
        _ => throw new InvalidOperationException("The Cycle view returned an unsupported query result.")
    };
}

static LoginResponse ToLoginResponse(
    Guid gameId,
    GameState state,
    Player player,
    Empire empire,
    bool trustedPlayerSelectionEnabled)
{
    var participant = state.GetParticipant(empire.CycleId, player.PlayerId)
        ?? throw new InvalidOperationException("The player is not participating in the Empire's Cycle.");
    return
    new(
        gameId,
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

static DashboardBootstrapResponse ToDashboardBootstrapResponse(
    Guid gameId,
    DashboardBootstrapContext context,
    bool trustedPlayerSelectionEnabled)
{
    var login = ToLoginResponse(
        gameId,
        context.State,
        context.Actor.Player,
        context.Empire,
        trustedPlayerSelectionEnabled);

    return new DashboardBootstrapResponse(
        gameId,
        new DashboardSessionResponse(login.PlayerId, login.Username, login.Role, login.CanAdvanceTurn),
        ToCycleResponse(context.Cycle),
        login.Empire,
        ToGalaxyResponse(context.State, context.Cycle, context.Actor, context.VisibleSystemIds),
        context.Fleets.Select(fleet => ToFleetResponse(context.State, fleet)).ToArray(),
        context.SelectedFleet is null
            ? null
            : ToFleetDetailResponse(
                context.State,
                context.Cycle,
                context.SelectedFleet,
                context.Actor,
                context.VisibleSystemIds),
        context.Orders.Select(order => ToOrderResponse(context.State, order)).ToArray(),
        context.Events.Select(ToEventResponse).ToArray(),
        ToChronicleEntryResponsesFromEntries(context.State, context.Cycle, context.ChronicleEntries),
        context.OpeningBriefing,
        TurnResolutionPresentationContract.Create(context.State, context.Cycle, context.Empire));
}

static GalaxyResponse ToGalaxyResponse(
    GameState state,
    Cycle cycle,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds)
{
    var domainSystems = state.Systems
        .Where(system => system.CycleId == cycle.CycleId)
        .OrderBy(system => system.SystemName)
        .ToArray();
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

    var systems = domainSystems
        .Select(system => ToGalaxySystemResponse(system, gatewaySystemIds.Contains(system.SystemId)))
        .ToArray();
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
}

static IReadOnlyCollection<FleetResponse> ToFleetResponses(GameState state, Cycle cycle, Guid? targetEmpireId) =>
    PlayerViewScope.SelectFleets(state, cycle, targetEmpireId)
        .Select(fleet => ToFleetResponse(state, fleet))
        .ToArray();

static IReadOnlyCollection<FleetOrderResponse> ToOrderResponses(GameState state, Cycle cycle, Guid? targetEmpireId) =>
    PlayerViewScope.SelectOrders(state, cycle, targetEmpireId)
        .Select(order => ToOrderResponse(state, order))
        .ToArray();

static IReadOnlyCollection<EventResponse> ToEventResponses(
    GameState state,
    Cycle cycle,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds,
    int limit) =>
    PlayerViewScope.SelectEvents(state, cycle, actor, visibleSystemIds, limit)
        .Select(ToEventResponse)
        .ToArray();

static IReadOnlyCollection<ChronicleEntryResponse> ToChronicleEntryResponses(
    GameState state,
    Cycle cycle,
    DevelopmentActor actor,
    IReadOnlySet<Guid> visibleSystemIds) =>
    ToChronicleEntryResponsesFromEntries(
        state,
        cycle,
        PlayerViewScope.SelectChronicleEntries(state, cycle, actor, visibleSystemIds));

static IReadOnlyCollection<ChronicleEntryResponse> ToChronicleEntryResponsesFromEntries(
    GameState state,
    Cycle cycle,
    IReadOnlyCollection<ChronicleEntry> entries)
{
    var eventTicksById = state.Events
        .Where(item => item.CycleId == cycle.CycleId)
        .ToDictionary(item => item.EventId, item => item.TickNumber);
    var battleTicksById = state.BattleRecords
        .Where(item => item.CycleId == cycle.CycleId)
        .ToDictionary(item => item.BattleId, item => item.TickNumber);

    return entries
        .Select(entry => ToChronicleEntryResponse(entry, eventTicksById, battleTicksById))
        .ToArray();
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
        MoveJourneyPresentationContract.CreateLegalDestinations(state, cycle, fleet),
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

static EventResponse ToEventResponse(EventRecord item)
{
    var phase = TurnResolutionPresentationContract.GetEventPhase(item.EventType);
    return new(
        item.EventId,
        item.CycleId,
        item.TickNumber,
        item.EventType,
        item.SystemId,
        item.EmpireId,
        item.Severity,
        item.DisplayText,
        item.CreatedAt,
        phase.Phase,
        phase.Order);
}

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
        targetFaction?.FactionName ?? targetEmpire?.EmpireName,
        MoveJourneyPresentationContract.CreateOrderProjection(state, order));
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
    Guid GameId,
    Guid PlayerId,
    string Username,
    PlayerRole Role,
    bool CanAdvanceTurn,
    EmpireResponse Empire);

public sealed record AccountSessionResponse(
    Guid PlayerId,
    string Username,
    PlayerRole Role);

public sealed record DashboardSessionResponse(
    Guid PlayerId,
    string Username,
    PlayerRole Role,
    bool CanAdvanceTurn);

public sealed record DashboardBootstrapResponse(
    Guid GameId,
    DashboardSessionResponse Session,
    CycleResponse Cycle,
    EmpireResponse Empire,
    GalaxyResponse Galaxy,
    IReadOnlyCollection<FleetResponse> Fleets,
    FleetDetailResponse? SelectedFleet,
    IReadOnlyCollection<FleetOrderResponse> Orders,
    IReadOnlyCollection<EventResponse> Events,
    IReadOnlyCollection<ChronicleEntryResponse> Chronicle,
    OpeningBriefingResponse? OpeningBriefing,
    TurnResolutionPresentationResponse TurnResolution);

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
    IReadOnlyCollection<LegalMoveDestinationResponse> LegalMoveDestinations,
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
    string? TargetFactionName,
    MoveJourneyProjectionResponse? MoveJourneyProjection);

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
    DateTimeOffset CreatedAt,
    TurnResolutionPhase? ResolutionPhase,
    int? ResolutionPhaseOrder);

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
