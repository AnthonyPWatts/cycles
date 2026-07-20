using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardGamesHomeContractTests
{
    [Fact]
    public void Account_shell_places_a_games_ledger_above_exactly_four_game_workspaces()
    {
        var html = ReadDashboardAsset("app.html");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"gamesHome\"", html);
        Assert.Contains("id=\"attentionGames\"", html);
        Assert.Contains("id=\"gamesEmptyState\"", html);
        Assert.Contains("id=\"trainingOffer\"", html);
        Assert.Contains("id=\"startTrainingButton\"", html);
        Assert.Contains("id=\"activeGames\"", html);
        Assert.Contains("id=\"waitingGames\"", html);
        Assert.Contains("id=\"completedGames\"", html);
        Assert.Contains("id=\"allGamesLink\"", html);
        Assert.Contains("id=\"gameSelector\"", html);
        Assert.Equal(4, Regex.Matches(html, "data-view-link=").Count);
        Assert.Contains("width: min(1120px, 100%);", css);
        Assert.Contains("body.account-active", css);
        Assert.Contains(".turn-progress-ribbon[hidden]", css);
        Assert.Contains("@media (max-width: 599px)", css);
    }

    [Fact]
    public void Account_boot_loads_session_and_catalogue_before_following_the_url()
    {
        var script = ReadDashboardAsset("app.js");
        var boot = ExtractFunction(script, "async function boot()", "async function loadTrustedPlayers()");

        var session = boot.IndexOf("getJson(\"/auth/session\")", StringComparison.Ordinal);
        var catalogue = boot.IndexOf("await loadGamesHome();", StringComparison.Ordinal);
        var navigate = boot.IndexOf("await navigateFromLocation();", StringComparison.Ordinal);

        Assert.True(session >= 0);
        Assert.True(catalogue > session);
        Assert.True(navigate > catalogue);
        Assert.Contains("const home = await getJson(\"/games\");", script);
        Assert.Contains("elements.gamesEmptyState.hidden = total !== 0;", script);
        Assert.Contains("elements.trainingOffer.hidden = !home.training;", script);
        Assert.Contains("async function startTraining()", script);
        Assert.Contains("crypto.randomUUID()", script);
        Assert.Contains("await loadGamesHome();", script);
        Assert.Contains("hideTutorialForAccount();", script);
        Assert.Contains("elements.tutorialPanel.hidden = true;", script);
    }

    [Fact]
    public void Selected_game_url_is_authoritative_and_switching_reuses_the_generation_guard()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("/^games\\/([0-9a-f-]{36})\\/(command|galaxy|fleets|history)$/", script);
        Assert.Contains("return `#/games/${encodeURIComponent(item.game.gameId)}/${selectedView}`;", script);
        Assert.Contains("const selection = selectGame(route.gameId);", script);
        Assert.Contains("if (selection.changed || !state.cycle)", script);
        Assert.Contains("controller?.abort();", script);
        Assert.Contains("generation += 1;", script);
        Assert.Contains("const changesSelectedGame = Boolean(state.gameId)", script);
        Assert.Contains("changesSelectedGame && !confirmLeavingUnsavedPriorities()", script);
        Assert.Contains("window.addEventListener(\"beforeunload\"", script);
        Assert.DoesNotContain("writeStoredValue(\"cycles.gameId\"", script);
    }

    [Fact]
    public void Games_endpoint_uses_authenticated_account_scope_and_server_ranking()
    {
        var program = ReadApiSource("Program.cs");

        var start = program.IndexOf("app.MapGet(\"/games\"", StringComparison.Ordinal);
        var end = program.IndexOf("var selectedGameRoutes", start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        var route = program[start..end];
        Assert.Contains("DevelopmentAuth.RequireAccount(httpContext, accounts)", route);
        Assert.Contains("catalogue.ListForPlayer(", route);
        Assert.Contains("GameCataloguePage.MaximumPageSize", route);
        Assert.Contains("GamesHomeProjection.Create(page, DateTimeOffset.UtcNow)", route);
        Assert.Contains("features.TrainingGames.Includes(account.PlayerId)", route);
        Assert.Contains("app.MapPost(\"/training/{tutorialKey}/attempts\"", route);
        Assert.Contains(".RequireCyclesAntiforgery();", route);
    }

    [Fact]
    public void Training_guide_uses_server_journey_resolution_and_fresh_attempt_routes()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var program = ReadApiSource("Program.cs");

        Assert.Contains("id=\"tutorialHint\"", html);
        Assert.Contains("state.tutorialJourney = isTrainingGame()", script);
        Assert.Contains("gameApi.getJson(\"/tutorial/journey\")", script);
        Assert.Contains("gameApi.postJson(\"/tutorial/resolve\", {})", script);
        Assert.Contains("gameApi.postJson(\n                \"/turns/resolve\"", script);
        Assert.Contains("expectedCurrentTickNumber: state.cycle.currentTickNumber", script);
        Assert.Contains("function selfPacedTurnControl()", script);
        Assert.Contains("Self-paced", script);
        Assert.Contains("\"/tutorial/acknowledgements\"", script);
        Assert.Contains("\"/tutorial/start-fresh\"", script);
        Assert.Contains("function renderTrainingTutorial()", script);
        Assert.Matches(
            new Regex(@"function renderTrainingTutorial\(\)\s*\{.*?updateTutorialButton\(\);", RegexOptions.Singleline),
            script);
        Assert.Contains("lesson.mechanicalEvidence.summary", script);
        Assert.Contains("selectedGameRoutes.MapGet(\"/tutorial/journey\"", program);
        Assert.Contains("selectedGameRoutes.MapPost(\"/tutorial/acknowledgements\"", program);
        Assert.Contains("selectedGameRoutes.MapPost(\"/tutorial/status\"", program);
        Assert.Contains("selectedGameRoutes.MapPost(\"/tutorial/resolve\"", program);
        Assert.Contains("selectedGameRoutes.MapPost(\"/turns/resolve\"", program);
        Assert.Contains("selectedGameRoutes.MapPost(\"/tutorial/start-fresh\"", program);
        Assert.Contains("ExplicitCycleResolutionPolicy.TutorialJourney", program);
    }

    private static string ExtractFunction(string script, string startMarker, string endMarker)
    {
        var start = script.IndexOf(startMarker, StringComparison.Ordinal);
        var end = script.IndexOf(endMarker, start, StringComparison.Ordinal);
        Assert.True(start >= 0);
        Assert.True(end > start);
        return script[start..end];
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));

    private static string ReadApiSource(string path) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "OnlineSource", "Api", path));
}
