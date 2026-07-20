using System.Buffers.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class GalaxyMapExperienceContractTests
{
    [Fact]
    public void Authored_atlas_contains_full_resolution_masters_and_smaller_web_delivery_assets()
    {
        var atlasDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", "assets", "galaxy");
        var masters = Directory.GetFiles(atlasDirectory, "*.png");
        var deliveryAssets = Directory.GetFiles(atlasDirectory, "*.webp");

        Assert.Equal(12, masters.Length);
        Assert.Equal(12, deliveryAssets.Length);
        Assert.Contains(masters, path => Path.GetFileName(path) == "galaxy-overview.png");
        Assert.Contains(deliveryAssets, path => Path.GetFileName(path) == "galaxy-overview.webp");
        Assert.Contains(masters, path => Path.GetFileName(path) == "twin-reaches-overview.png");
        Assert.Contains(deliveryAssets, path => Path.GetFileName(path) == "twin-reaches-overview.webp");
        foreach (var master in masters)
        {
            var header = File.ReadAllBytes(master).AsSpan(0, 24);
            var dimensions = Path.GetFileName(master) switch
            {
                "twin-reaches-overview.png" => (Width: 1774, Height: 887),
                "twin-reaches-inner-reach.png" or "twin-reaches-outer-reach.png" => (Width: 1254, Height: 1254),
                _ => (Width: 2400, Height: 992)
            };
            Assert.Equal(dimensions.Width, BinaryPrimitives.ReadInt32BigEndian(header[16..20]));
            Assert.Equal(dimensions.Height, BinaryPrimitives.ReadInt32BigEndian(header[20..24]));

            var deliveryAsset = Path.ChangeExtension(master, ".webp");
            var deliveryHeader = File.ReadAllBytes(deliveryAsset).AsSpan(0, 12);
            Assert.Equal("RIFF", Encoding.ASCII.GetString(deliveryHeader[..4]));
            Assert.Equal("WEBP", Encoding.ASCII.GetString(deliveryHeader[8..12]));
            Assert.True(new FileInfo(deliveryAsset).Length < new FileInfo(master).Length);
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
        Assert.Contains("const mapAtlasesByProfileKey", script);
        Assert.Contains("\"territorial-graph-v2\": standardGalaxyAtlas", script);
        Assert.Contains("\"tutorial-foundations-v1\": twinReachesAtlas", script);
        Assert.Contains("galaxyAsset: \"/assets/galaxy/galaxy-overview.webp?v=20260718-webp-1\"", script);
        Assert.Equal(8, Regex.Matches(script, "asset: \"/assets/galaxy/sector-").Count);
        Assert.Equal(3, Regex.Matches(script, "assets/galaxy/twin-reaches-").Count);
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
        Assert.Contains("presence[state.empire.factionId]", script);
        Assert.DoesNotContain("presence[state.empire.empireId]", script);
        Assert.Contains("function activeMapAtlas", script);
        Assert.Contains("mapAtlasesByProfileKey[state.cycle?.mapProfileKey]", script);
        Assert.Contains("<image class=\"atlas-background\"", script);
        Assert.Contains("renderMapStarfield()", script);
        Assert.Contains("function mapAtlasGalaxyRoutePath", script);
        Assert.Contains("return `M ${firstPosition.x} ${firstPosition.y} L ${secondPosition.x} ${secondPosition.y}`;", script);
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

    [Fact]
    public void Galaxy_map_exposes_a_keyboard_operable_systems_and_routes_equivalent()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"systemsRoutesSection\"", html);
        Assert.Contains("id=\"systemsRoutesHeading\">Systems and routes", html);
        Assert.Contains("id=\"systemsRoutesList\"", html);
        Assert.Contains("aria-describedby=\"systemsRoutesHelp\"", html);

        var renderer = Regex.Match(
            script,
            @"function renderSystemsAndRoutes\(.*?(?=\nfunction topologyRoutesForSystem)",
            RegexOptions.Singleline).Value;
        Assert.NotEmpty(renderer);
        Assert.Contains("galaxy.systems", renderer);
        Assert.Contains("galaxy.links", renderer);
        Assert.Contains("groupSystemsBySector(galaxy.systems)", renderer);
        Assert.Contains("data-topology-system-id", renderer);
        Assert.Contains("data-topology-destination-id", renderer);
        Assert.Contains("aria-current=\\\"location\\\"", renderer);
        Assert.Contains("Known ownership", renderer);
        Assert.Contains("item.fleet.currentSystemId === system.systemId", renderer);
        Assert.DoesNotContain("Firstlight", renderer);
        Assert.DoesNotContain("Gatehouse", renderer);

        var routeBuilder = Regex.Match(
            script,
            @"function topologyRoutesForSystem\(.*?(?=\nfunction topologyKnownOwnership)",
            RegexOptions.Singleline).Value;
        Assert.NotEmpty(routeBuilder);
        Assert.Contains("galaxy.links", routeBuilder);
        Assert.Contains("travelTicks: Number(link.travelTicks)", routeBuilder);
        Assert.Contains("selectSystem(destinationButton.dataset.topologyDestinationId, { restoreTopologyFocus: true })", script);
        Assert.Contains("function focusTopologySystem", script);

        Assert.Contains(".systems-routes {", css);
        Assert.Contains(".topology-system-select[aria-current=\"location\"]", css);
        Assert.Contains(".topology-route {", css);
        Assert.Contains("min-height: 44px;", css);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
