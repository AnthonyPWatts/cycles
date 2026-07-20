namespace Cycles.Tests;

public sealed class DashboardTrainingOnlyGuideRegressionTests
{
    [Fact]
    public void Guide_is_exposed_only_for_server_backed_training_journeys()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"tutorialButton\"", html);
        Assert.Contains("title=\"Core foundations\" hidden", html);
        Assert.Contains("const trainingJourneyAvailable = isTrainingGame() && Boolean(state.tutorialJourney);", script);
        Assert.Contains("elements.tutorialButton.hidden = !trainingJourneyAvailable;", script);
        Assert.Contains("function disableStandardTutorial()", script);
        Assert.Contains("renderTrainingTutorial();", script);
        Assert.DoesNotContain("cycles.tutorial.${tutorial.version}", script);
        Assert.DoesNotContain("Complete the three Day 1 commitments", script);
    }

    [Fact]
    public void Training_interactions_never_dispatch_to_the_legacy_renderer()
    {
        var script = ReadDashboardAsset("app.js");
        var styles = ReadDashboardAsset("styles.css");

        Assert.DoesNotContain("function syncTutorialDisplay()", script);
        Assert.DoesNotContain("syncTutorialDisplay();", script);
        Assert.DoesNotContain("id=\"tutorialBackButton\"", ReadDashboardAsset("app.html"));
        Assert.Matches(
            new System.Text.RegularExpressions.Regex(
                @"@media \(max-width: 560px\)[\s\S]*?\.tutorial-actions\s*\{[^}]*grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\);",
                System.Text.RegularExpressions.RegexOptions.Singleline),
            styles);
        Assert.Contains("resetTutorialContext();", script);
        Assert.Contains("elements.tutorialButton.hidden = true;", script);
        var selectGameStart = script.IndexOf("function selectGame(gameId)", StringComparison.Ordinal);
        var selectGameEnd = script.IndexOf("function clearSelectedGame()", selectGameStart, StringComparison.Ordinal);
        var selectGame = script[selectGameStart..selectGameEnd];
        Assert.True(
            selectGame.IndexOf("state.gameId = selection.gameId;", StringComparison.Ordinal)
            < selectGame.IndexOf("resetTutorialContext();", StringComparison.Ordinal));
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
