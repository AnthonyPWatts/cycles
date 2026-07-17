using System.Buffers.Binary;
using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class GalaxyMapExperienceContractTests
{
    [Fact]
    public void Authored_atlas_contains_one_galaxy_and_eight_full_resolution_sector_charts()
    {
        var atlasDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", "assets", "galaxy");
        var assets = Directory.GetFiles(atlasDirectory, "*.png");

        Assert.Equal(9, assets.Length);
        Assert.Contains(assets, path => Path.GetFileName(path) == "galaxy-overview.png");
        foreach (var asset in assets)
        {
            var header = File.ReadAllBytes(asset).AsSpan(0, 24);
            Assert.Equal(2400, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal(992, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));
        }
    }

    [Fact]
    public void Galaxy_map_exposes_named_ranges_and_compact_focus_recovery()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Equal(3, Regex.Matches(html, "data-map-preset=").Count);
        Assert.Contains("id=\"mapFocusHome\"", html);
        Assert.Contains("id=\"mapFocusSelected\"", html);
        Assert.Contains("id=\"mapFocusFrontier\"", html);
        Assert.DoesNotContain("id=\"mapNavigator\"", html);
        Assert.DoesNotContain("id=\"mapNavigatorReset\"", html);
        Assert.DoesNotContain("id=\"mapRecentSystems\"", html);

        Assert.Contains("function applyMapPreset", script);
        Assert.Contains("function recoverMapToSystem", script);
        Assert.Contains("function recoverMapToFrontier", script);
        Assert.DoesNotContain("function moveMapFromNavigator", script);
        Assert.DoesNotContain("function renderRecentMapSystems", script);
        Assert.DoesNotContain("mapRecentSystemIds", script);
        Assert.Contains("function setMapRange", script);
        Assert.Contains("function mapComposition", script);
        Assert.Contains("const authoredGalaxyAtlas", script);
        Assert.Contains("galaxyAsset: \"/assets/galaxy/galaxy-overview.png?v=20260716-wide-atlas-1\"", script);
        Assert.Equal(8, Regex.Matches(script, "asset: \"/assets/galaxy/sector-").Count);
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
        Assert.Contains("id=\"mapToolbar\"", html);
        Assert.Contains("id=\"mapOwnershipStats\"", html);
        Assert.DoesNotContain("class=\"map-legend\"", html);

        Assert.Contains("function normaliseGalaxySectors", script);
        Assert.Contains("function mapSectorDisplayName", script);
        Assert.Contains("function mapSectorContext", script);
        Assert.Contains("function renderMapSectorLayer", script);
        Assert.Contains("function mapAtlasSectorPosition", script);
        Assert.Contains("function mapAtlasSystemPosition", script);
        Assert.Contains("galaxyRoutes: [", script);
        Assert.Contains("function mapAtlasSectorRoutePath", script);
        Assert.Contains("<image class=\"atlas-background\"", script);
        Assert.Contains("<path class=\"link", script);
        Assert.DoesNotContain("<line class=\"link", script);
        Assert.DoesNotContain("(firstPosition.x + secondPosition.x) / 2", script);
        Assert.Contains("selected-route", script);
        Assert.Equal(8, Regex.Matches(script, @"\n\s+routes: \[").Count);
        Assert.Contains("function focusMapOnSector", script);
        Assert.Contains("function focusRenderedMapNode", script);
        Assert.DoesNotContain("(currentIndex + direction + sectors.length) % sectors.length", script);
        Assert.DoesNotContain("const immediateSystemIds", script);
        Assert.DoesNotContain("elements.galaxyMap.addEventListener(\"pointerdown\"", script);
        Assert.DoesNotContain("mapViewBox", script);
        Assert.DoesNotContain("`Y${String(sortOrder)", script);

        Assert.Contains(".atlas-background", css);
        Assert.Contains(".atlas-route-overlay", css);
        Assert.Contains("#galaxyMap[data-map-range=\"sector\"] .system-node.is-active-sector .system-label", css);
        Assert.Contains("#galaxyMap[data-map-range=\"local\"] .system-node:not(.is-local-context)", css);
        Assert.Contains(".sector-focus", css);
        Assert.Contains(".gateway-ring", css);
        Assert.DoesNotContain(".map-navigator", css);
        Assert.DoesNotContain(".navigator-sector", css);

        var galaxyPalette = Regex.Match(css, @"\.galaxy-layout\s*\{(?<rules>.*?)\}", RegexOptions.Singleline).Groups["rules"].Value;
        Assert.Contains("--accent: #8fb4f4", galaxyPalette);
        Assert.DoesNotContain("#8fd1bd", galaxyPalette, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("#52b69a", galaxyPalette, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
