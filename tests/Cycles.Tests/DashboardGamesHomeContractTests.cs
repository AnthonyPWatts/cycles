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
