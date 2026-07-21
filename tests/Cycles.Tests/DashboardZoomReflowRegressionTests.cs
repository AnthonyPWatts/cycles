namespace Cycles.Tests;

public sealed class DashboardZoomReflowRegressionTests
{
    [Fact]
    public void Workspace_navigation_compacts_before_the_200_percent_reflow_width()
    {
        var styles = ReadDashboardAsset("styles.css");
        var compactNavigation = CssMediaBlockContaining(
            styles,
            "@media (max-width: 767px)",
            ".view-nav small,");
        var narrowNavigation = CssMediaBlockContaining(
            styles,
            "@media (max-width: 560px)",
            "padding: 8px 2px;");

        Assert.Contains("grid-template-columns: minmax(0, 1fr);", compactNavigation);
        Assert.Contains(".view-nav small,", compactNavigation);
        Assert.Contains(".view-badge", compactNavigation);
        Assert.Contains("display: none;", compactNavigation);
        Assert.Contains("text-align: center;", compactNavigation);
        Assert.Contains("padding: 8px 2px;", narrowNavigation);
        Assert.Contains("letter-spacing: 0.04em;", narrowNavigation);
    }

    [Fact]
    public void Narrow_guide_reflows_without_a_sticky_action_block_covering_controls()
    {
        var styles = ReadDashboardAsset("styles.css");
        var narrowGuide = CssMediaBlockContaining(
            styles,
            "@media (max-width: 560px)",
            ".tutorial-panel > *");
        var shortGuide = CssMediaBlockContaining(
            styles,
            "@media (max-width: 560px) and (max-height: 500px)",
            ".tutorial-actions");

        Assert.Contains(".tutorial-panel > *", narrowGuide);
        Assert.Contains("min-width: 0;", narrowGuide);
        Assert.Contains(".tutorial-heading-row", narrowGuide);
        Assert.Contains("display: grid;", narrowGuide);
        Assert.Contains(".tutorial-heading-row > .tutorial-heading-actions", narrowGuide);
        Assert.Contains("flex-wrap: wrap;", narrowGuide);
        Assert.Contains("body.tutorial-active .tutorial-panel,", narrowGuide);
        Assert.Contains("body.tutorial-active .tutorial-panel.is-right", narrowGuide);
        Assert.Contains("max-height: min(56dvh, 460px);", narrowGuide);

        Assert.Contains("body.tutorial-active,", shortGuide);
        Assert.Contains("padding-bottom: 0;", shortGuide);
        Assert.Contains("height: 100dvh;", shortGuide);
        Assert.Contains("max-height: 100dvh;", shortGuide);
        Assert.Contains("border-radius: 0;", shortGuide);
        Assert.Contains(".tutorial-actions", shortGuide);
        Assert.Contains("position: static;", shortGuide);
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
