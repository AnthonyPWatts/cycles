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

    [Fact]
    public void Galaxy_map_uses_sector_semantics_at_each_named_range()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("Find a system or sector", html);
        Assert.Contains("class=\"legend-sector\"", html);
        Assert.Contains("class=\"legend-gateway\"", html);

        Assert.Contains("function normaliseGalaxySectors", script);
        Assert.Contains("function mapSectorDisplayName", script);
        Assert.Contains("function mapSectorContext", script);
        Assert.Contains("function renderMapSectorLayer", script);
        Assert.Contains("function mapSectorEnvelopePath", script);
        Assert.Contains("function focusMapOnSector", script);
        Assert.Contains("function syncMapSectorContextToCamera", script);
        Assert.Contains("function focusRenderedMapNode", script);
        Assert.Contains("ArrowRight", script);
        Assert.Contains("is-adjacent-gateway", script);

        Assert.Contains("#galaxyMap[data-map-range=\"galaxy\"] .route-segment.is-local-route", css);
        Assert.Contains("#galaxyMap[data-map-range=\"sector\"] .system-node.is-active-sector .system-label", css);
        Assert.Contains(".sector-hull", css);
        Assert.Contains(".gateway-ring", css);
        Assert.Contains(".navigator-sector", css);

        var galaxyPalette = Regex.Match(css, @"\.galaxy-layout\s*\{(?<rules>.*?)\}", RegexOptions.Singleline).Groups["rules"].Value;
        Assert.Contains("--accent: #8fb4f4", galaxyPalette);
        Assert.DoesNotContain("#8fd1bd", galaxyPalette, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#52b69a", galaxyPalette, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
