using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardGameApiContractTests
{
    [Fact]
    public void Selected_game_requests_use_the_scoped_route_contract()
    {
        var script = ReadDashboardAsset();

        Assert.Contains("gameApi.getJson(`/dashboard/bootstrap${selectedFleetQuery}`)", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.postJson(\"/orders/move\"", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.postJson(\"/orders/recall\"", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.postJson(\"/orders/attack\"", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.postJson(\"/orders/colonise\"", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.deleteJson(`/orders/${encodeURIComponent(fleetOrderId)}`)", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.putJson(\"/priorities\"", script, StringComparison.Ordinal);
        Assert.Contains("gameApi.postJson(\"/admin/tick\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("await postJson(\"/admin/tick\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/orders/fleet/", script, StringComparison.Ordinal);
        Assert.DoesNotContain("/orders/priorities", script, StringComparison.Ordinal);
        Assert.DoesNotContain("getJson(`/fleets/${", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Initial_bootstrap_is_legacy_pinned_then_refreshes_are_game_scoped()
    {
        var refresh = ExtractFunction(
            ReadDashboardAsset(),
            "async function refresh(",
            "function viewFromHash()");

        Assert.Contains("const bootstrap = state.gameId", refresh, StringComparison.Ordinal);
        Assert.Contains("? await gameApi.getJson(`/dashboard/bootstrap${selectedFleetQuery}`)", refresh, StringComparison.Ordinal);
        Assert.Contains(": await getJson(`/dashboard/bootstrap${selectedFleetQuery}`)", refresh, StringComparison.Ordinal);
        Assert.Contains("if (!bootstrap?.gameId)", refresh, StringComparison.Ordinal);
        Assert.Contains("selectGame(bootstrapGameId);", refresh, StringComparison.Ordinal);
        Assert.Contains("bootstrapGameId !== state.gameId", refresh, StringComparison.Ordinal);
    }

    [Fact]
    public void Game_api_aborts_the_previous_generation_and_rejects_late_responses()
    {
        var client = ExtractFunction(
            ReadDashboardAsset(),
            "function createGameApi()",
            "function createGameRequestCancellation()");

        Assert.Contains("let generation = 0;", client, StringComparison.Ordinal);
        Assert.Contains("controller?.abort();", client, StringComparison.Ordinal);
        Assert.Contains("generation += 1;", client, StringComparison.Ordinal);
        Assert.Contains("controller = new AbortController();", client, StringComparison.Ordinal);
        Assert.Contains("generation === request.generation", client, StringComparison.Ordinal);
        Assert.Contains("!request.signal.aborted", client, StringComparison.Ordinal);
        Assert.Contains("if (!isCurrent(request))", client, StringComparison.Ordinal);
        Assert.Contains("throw createGameRequestCancellation();", client, StringComparison.Ordinal);
        Assert.Contains("`/games/${encodeURIComponent(request.gameId)}${path}`", client, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_network_request_flows_through_the_csrf_aware_transport()
    {
        var script = ReadDashboardAsset();
        var transport = ExtractFunction(
            script,
            "async function requestJsonCore(",
            "async function readResponse(");

        Assert.Single(Regex.Matches(script, @"\bfetch\(").Cast<Match>());
        Assert.Contains("headers[antiforgeryHeaderName] = await requireAntiforgeryToken();", transport, StringComparison.Ordinal);
        Assert.Contains("credentials: \"same-origin\"", transport, StringComparison.Ordinal);
        Assert.Contains("error.code === antiforgeryErrorCode", transport, StringComparison.Ordinal);
        Assert.Contains("clearAntiforgeryToken();", transport, StringComparison.Ordinal);
        Assert.Contains("await requireAntiforgeryToken();", transport, StringComparison.Ordinal);
        Assert.Contains("// Keep mutation controls disabled. The original request is never replayed.", transport, StringComparison.Ordinal);
        Assert.Single(Regex.Matches(transport, @"\bfetch\(").Cast<Match>());
    }

    [Fact]
    public void Trusted_login_rotates_the_request_token_before_enabling_the_session()
    {
        var login = ExtractFunction(
            ReadDashboardAsset(),
            "async function login(",
            "async function signOut()");

        var request = login.IndexOf("await postJson(\"/auth/login\"", StringComparison.Ordinal);
        var clear = login.IndexOf("clearAntiforgeryToken();", StringComparison.Ordinal);
        var reacquire = login.IndexOf("await requireAntiforgeryToken();", StringComparison.Ordinal);
        var apply = login.IndexOf("applySession(login);", StringComparison.Ordinal);

        Assert.True(request >= 0);
        Assert.True(clear > request);
        Assert.True(reacquire > clear);
        Assert.True(apply > reacquire);
        Assert.Contains("selectGame(login.gameId);", ReadDashboardAsset(), StringComparison.Ordinal);
    }

    [Fact]
    public void Boot_handles_antiforgery_initialisation_failure_with_a_visible_retry_message()
    {
        var boot = ExtractFunction(
            ReadDashboardAsset(),
            "async function boot()",
            "async function loadTrustedPlayers()");

        var tryStart = boot.IndexOf("try {", StringComparison.Ordinal);
        var tokenRequest = boot.IndexOf("await requireAntiforgeryToken();", StringComparison.Ordinal);
        var refresh = boot.IndexOf("await refresh({ applySessionFromBootstrap: true });", StringComparison.Ordinal);

        Assert.True(tryStart >= 0);
        Assert.True(tokenRequest > tryStart);
        Assert.True(refresh > tokenRequest);
        Assert.Contains("if (!antiforgeryReady)", boot, StringComparison.Ordinal);
        Assert.Contains("showLogin(\"Secure session setup failed. Refresh the page to try again.\");", boot, StringComparison.Ordinal);
        Assert.Contains("return;", boot, StringComparison.Ordinal);
    }

    [Fact]
    public void Selected_game_tick_route_uses_the_route_game_and_keeps_the_legacy_adapter()
    {
        var program = ReadApiSource("Program.cs");

        var selectedRouteStart = program.IndexOf(
            "selectedGameRoutes.MapPost(\"/admin/tick\"",
            StringComparison.Ordinal);
        var legacyRoutesStart = program.IndexOf(
            "app.MapGet(\"/dashboard/bootstrap\"",
            selectedRouteStart,
            StringComparison.Ordinal);

        Assert.True(selectedRouteStart >= 0);
        Assert.True(legacyRoutesStart > selectedRouteStart);
        var selectedRoute = program[selectedRouteStart..legacyRoutesStart]
            .Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("ApiAdminEndpoints.RunTick(", selectedRoute, StringComparison.Ordinal);
        Assert.Contains("httpContext,\n        gameId,", selectedRoute, StringComparison.Ordinal);
        Assert.Contains("app.MapPost(\"/admin/tick\"", program, StringComparison.Ordinal);
        Assert.Contains("GetLegacyGameId(httpContext, games, legacyScope)", program, StringComparison.Ordinal);
    }

    [Fact]
    public void Logout_is_a_tokenised_post_form_so_oidc_redirects_use_browser_navigation()
    {
        var signOut = ExtractFunction(
            ReadDashboardAsset(),
            "async function signOut()",
            "function applySession(");

        Assert.Contains("clearAntiforgeryToken();", signOut, StringComparison.Ordinal);
        Assert.Contains("await requireAntiforgeryToken();", signOut, StringComparison.Ordinal);
        Assert.Contains("form.method = \"post\";", signOut, StringComparison.Ordinal);
        Assert.Contains("form.action = \"/auth/logout\";", signOut, StringComparison.Ordinal);
        Assert.Contains("token.name = antiforgeryFormFieldName;", signOut, StringComparison.Ordinal);
        Assert.Contains("form.submit();", signOut, StringComparison.Ordinal);
        Assert.DoesNotContain("window.location", signOut, StringComparison.Ordinal);
        Assert.DoesNotContain("fetch(", signOut, StringComparison.Ordinal);
    }

    private static string ExtractFunction(string script, string startMarker, string endMarker)
    {
        var start = script.IndexOf(startMarker, StringComparison.Ordinal);
        var end = script.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Could not find '{startMarker}'.");
        Assert.True(end > start, $"Could not find '{endMarker}' after '{startMarker}'.");
        return script[start..end];
    }

    private static string ReadDashboardAsset() =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            "app.js"));

    private static string ReadApiSource(string path) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OnlineSource",
            "Api",
            path));
}
