using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class GalaxyMapExperienceContractTests
{
    [Fact]
    public void Galaxy_map_exposes_named_ranges_and_orientation_recovery()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Equal(3, Regex.Matches(html, "data-map-preset=").Count);
        Assert.Contains("id=\"mapFocusHome\"", html);
        Assert.Contains("id=\"mapFocusSelected\"", html);
        Assert.Contains("id=\"mapFocusFrontier\"", html);
        Assert.Contains("id=\"mapNavigator\"", html);
        Assert.Contains("id=\"mapRecentSystems\"", html);

        Assert.Contains("function applyMapPreset", script);
        Assert.Contains("function recoverMapToSystem", script);
        Assert.Contains("function recoverMapToFrontier", script);
        Assert.Contains("function moveMapFromNavigator", script);
        Assert.Contains("function renderRecentMapSystems", script);
    }

    [Fact]
    public void Galaxy_map_can_take_over_the_viewport_and_escape_cleanly()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"mapMaximise\"", html);
        Assert.Contains("aria-pressed=\"false\"", html);
        Assert.Contains("function setMapMaximised", script);
        Assert.Contains("event.key === \"Escape\" && state.mapMaximised", script);
        Assert.Contains("document.body.classList.toggle(\"map-maximised\"", script);
        Assert.Contains(".galaxy-layout.is-maximised", css);
        Assert.Contains("position: fixed;", css);
        Assert.Contains("height: 100dvh;", css);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
