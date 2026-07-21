using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardCommandHierarchyRegressionTests
{
    [Fact]
    public void Command_workspace_prioritises_agenda_then_calendar_before_secondary_intelligence()
    {
        var html = ReadDashboardAsset("app.html");
        var css = ReadDashboardAsset("styles.css");

        var agenda = html.IndexOf("class=\"view-card council-agenda\"", StringComparison.Ordinal);
        var calendar = html.IndexOf("id=\"orderQueueSection\"", StringComparison.Ordinal);
        var intelligence = html.IndexOf("class=\"command-rail\"", StringComparison.Ordinal);

        Assert.True(agenda >= 0);
        Assert.True(calendar > agenda);
        Assert.True(intelligence > calendar);
        Assert.Contains("min-height: 92px;", css);
        Assert.Contains("grid-template-columns: 34px 38px minmax(0, 1.25fr) minmax(130px, 0.72fr) auto;", css);
        Assert.Contains("grid-column: 5;", css);
    }

    [Fact]
    public void Parent_shell_uses_compact_labels_and_real_scheduled_turn_progress()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.DoesNotContain("<span class=\"toolbar-kicker\">Imperial command</span>", html);
        Assert.DoesNotContain("<span class=\"instrument-label\">Current Cycle</span>", html);
        Assert.DoesNotContain("<span class=\"instrument-label\">Next turn</span>", html);
        Assert.Contains("id=\"nextTurnTrack\" class=\"turn-track\" role=\"progressbar\"", html);
        Assert.Contains("replace(/^Command window open$/i, \"Commands open\")", script);
        Assert.Contains("const dueAt = Date.parse(cycle.nextTickAt);", script);
        Assert.Contains("--turn-window-progress", script);
        Assert.Contains("30_000", script);
        Assert.Contains("left: var(--turn-window-progress);", css);
        Assert.DoesNotMatch(
            new Regex(@"\.next-turn-instrument\s*\{[^}]*display:\s*none;", RegexOptions.Singleline),
            css);
    }

    [Fact]
    public void Game_context_keeps_game_navigation_together_without_competing_with_session_actions()
    {
        var html = ReadDashboardAsset("app.html");

        var contextStart = html.IndexOf("id=\"selectedGameContext\"", StringComparison.Ordinal);
        var contextEnd = html.IndexOf("id=\"turnMessage\"", contextStart, StringComparison.Ordinal);
        var context = html[contextStart..contextEnd];

        Assert.Contains("id=\"allGamesLink\"", context);
        Assert.Contains(">Games</a>", context);
        Assert.Contains("id=\"selectedGameName\"", context);
        Assert.Contains("id=\"gameSelector\"", context);
        Assert.Contains("class=\"visually-hidden\">Switch game</span>", context);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
