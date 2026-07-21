namespace Cycles.Tests;

public sealed class DashboardTrainingMapReflowRegressionTests
{
    // Regression: ISSUE-010 — the pinned Training rail made Galaxy controls overlap the system inspector
    // Found by /qa on 2026-07-21
    // Report: .gstack/qa-reports/qa-report-127-0-0-1-2026-07-20-mg10.md
    [Fact]
    public void Training_rail_compacts_the_Galaxy_toolbar_before_the_map_columns_overlap()
    {
        var styles = ReadDashboardAsset("styles.css");
        var markup = ReadDashboardAsset("app.html");
        var compactTrainingMap = CssMediaBlockContaining(
            styles,
            "@media (min-width: 1200px) and (max-width: 1340px)",
            "body.tutorial-active .map-toolbar");

        Assert.Contains("body.tutorial-active .map-toolbar,", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-stage", compactTrainingMap);
        Assert.Contains("min-width: 0;", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-toolbar-primary", compactTrainingMap);
        Assert.Contains("grid-template-columns: minmax(0, 1fr) auto;", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-search", compactTrainingMap);
        Assert.Contains("grid-column: 1 / -1;", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-ranges", compactTrainingMap);
        Assert.Contains("justify-self: stretch;", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-maximise-label", compactTrainingMap);
        Assert.Contains("display: none;", compactTrainingMap);
        Assert.Contains("body.tutorial-active .map-toolbar-context", compactTrainingMap);
        Assert.Contains("styles.css?v=20260721-training-map-reflow-1", markup);
    }

    private static string CssMediaBlockContaining(string styles, string mediaQuery, string needle)
    {
        var searchIndex = 0;
        while (true)
        {
            var start = styles.IndexOf(mediaQuery, searchIndex, StringComparison.Ordinal);
            Assert.True(start >= 0, $"CSS media query '{mediaQuery}' was not found.");
            var openingBrace = styles.IndexOf('{', start);
            Assert.True(openingBrace >= 0, $"CSS media query '{mediaQuery}' has no opening brace.");

            var depth = 0;
            for (var index = openingBrace; index < styles.Length; index++)
            {
                depth += styles[index] switch
                {
                    '{' => 1,
                    '}' => -1,
                    _ => 0
                };
                if (depth != 0)
                {
                    continue;
                }

                var block = styles[start..(index + 1)];
                if (block.Contains(needle, StringComparison.Ordinal))
                {
                    return block;
                }

                searchIndex = index + 1;
                break;
            }
        }
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
