namespace Cycles.Tests;

public sealed class DashboardTutorialResponsiveRegressionTests
{
    [Fact]
    public void Tablet_uses_a_right_drawer_and_mobile_uses_a_bottom_sheet()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("@media (min-width: 768px) and (max-width: 1199px)", css);
        Assert.Contains("width: min(400px, calc(100vw - 48px));", css);
        Assert.Contains("max-height: 100dvh;", css);
        Assert.Contains("border-radius: 10px 0 0 10px;", css);
        Assert.Contains("body.tutorial-active .tutorial-heading-row", css);
        Assert.Contains("flex-wrap: wrap;", css);
        Assert.Contains("@media (max-width: 767px)", css);
        Assert.Contains("border-radius: 10px 10px 0 0;", css);
        Assert.Contains("calc(min(42dvh, 360px) + var(--turn-ribbon-height))", css);
        Assert.Contains("scroll-margin-block: 130px 45dvh;", css);
        Assert.Contains("window.innerWidth <= 767", script);
        Assert.DoesNotContain("@media (min-width: 901px) and (max-width: 1199px)", css);
        Assert.DoesNotContain("padding-bottom: min(48dvh, 380px)", css);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
