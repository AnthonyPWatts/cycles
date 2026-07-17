namespace Cycles.Tests;

public sealed class DashboardTutorialScrollContractTests
{
    [Fact]
    public void Guide_only_scrolls_when_the_target_is_not_already_usefully_visible()
    {
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            "app.js"));

        Assert.Contains("if (!tutorialTargetNeedsScroll(target))", script);
        Assert.Contains("function tutorialTargetNeedsScroll(target)", script);
        Assert.Contains("target.closest(\".app-view\")?.getBoundingClientRect()", script);
        Assert.Contains("Math.min(bounds.height, 120)", script);
        Assert.Contains("elements.tutorialPanel.getBoundingClientRect().top", script);
    }
}
