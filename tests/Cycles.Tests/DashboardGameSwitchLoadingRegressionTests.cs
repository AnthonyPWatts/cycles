namespace Cycles.Tests;

public sealed class DashboardGameSwitchLoadingRegressionTests
{
    // Regression: ISSUE-009 — a destination Game title appeared over the previous Game's authority
    // Found by /qa on 2026-07-21
    // Report: .gstack/qa-reports/qa-report-127-0-0-1-2026-07-20-mg10.md
    [Fact]
    public void Switching_game_hides_scoped_authority_until_the_destination_bootstrap_finishes()
    {
        var script = ReadDashboardAsset();
        var navigation = ExtractFunction(
            script,
            "async function navigateFromLocation(",
            "function showGamesHome(");
        var selectedGameShell = ExtractFunction(
            script,
            "function showSelectedGameShell(",
            "function selectedGameHash(");
        var refresh = ExtractFunction(
            script,
            "async function refresh(",
            "async function loadGamesHome(");

        Assert.Contains("const loading = selection.changed || !state.cycle;", navigation);
        Assert.Contains("showSelectedGameShell(game, { loading });", navigation);
        Assert.Contains("if (loading)", navigation);
        Assert.Contains("setTurnMessage(\"\");", navigation);
        Assert.Contains("function showSelectedGameShell(item, { loading = false } = {})", selectedGameShell);
        Assert.Contains("elements.viewNav.hidden = loading;", selectedGameShell);
        Assert.Contains("elements.viewStack.hidden = loading;", selectedGameShell);
        Assert.Contains("elements.appHeaderControls.hidden = loading;", selectedGameShell);
        Assert.Contains("elements.turnProgressRibbon.hidden = loading ||", selectedGameShell);
        Assert.Contains("setTurnMessage(`Loading ${item.game.gameName}…`);", selectedGameShell);
        Assert.Contains("showSelectedGameShell(gameById(state.gameId));", refresh);
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
}
