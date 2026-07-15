using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardViewContractTests
{
    [Fact]
    public void Dashboard_groups_player_work_into_four_addressable_views()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Equal(4, Regex.Matches(html, "data-view-link=").Count);
        Assert.Equal(4, Regex.Matches(html, "class=\"app-view").Count);
        Assert.Contains("href=\"#command\"", html);
        Assert.Contains("href=\"#galaxy\"", html);
        Assert.Contains("href=\"#fleets\"", html);
        Assert.Contains("href=\"#history\"", html);
        Assert.DoesNotContain("class=\"side-panel\"", html);
        Assert.Contains("id=\"appHeaderControls\" class=\"app-header-controls\"", html);
        Assert.DoesNotContain("class=\"toolbar\"", html);
        Assert.DoesNotContain("class=\"view-heading", html);
        Assert.DoesNotContain("class=\"panel-heading\"", html);
        Assert.DoesNotContain("id=\"commandPulse\"", html);
        Assert.DoesNotContain("renderCommandSummary", script);
        Assert.Equal(4, Regex.Matches(html, "<h1[^>]*class=\"visually-hidden\"[^>]*tabindex=\"-1\"").Count);

        Assert.Contains("window.addEventListener(\"hashchange\"", script);
        Assert.Contains("window.history.replaceState(null, \"\", `#${selectedView}`);", script);
        Assert.Contains("link.setAttribute(\"aria-current\", \"page\");", script);
        Assert.Contains("activateView(step.view, { updateLocation: true });", script);
    }

    [Fact]
    public void Galaxy_view_keeps_the_chart_anchored_and_supports_strategic_exploration()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("id=\"systemSearchForm\"", html);
        Assert.Equal(5, Regex.Matches(html, "data-map-lens=").Count);
        Assert.Contains("id=\"mapZoomOut\"", html);
        Assert.Contains("id=\"mapResetView\"", html);
        Assert.Contains("id=\"mapZoomIn\"", html);
        Assert.Contains("aria-describedby=\"mapInteractionHint\"", html);

        Assert.Contains("elements.galaxyMap.addEventListener(\"wheel\"", script);
        Assert.Contains("function zoomMap", script);
        Assert.Contains("function renderMapInsight", script);
        Assert.Contains("selected-route", script);
        Assert.Contains("data-focus-system", script);
        Assert.Contains("data-command-fleet", script);

        Assert.Contains("grid-template-rows: auto auto minmax(0, 1fr);", css);
        Assert.Contains(".map-panel {", css);
        Assert.Contains("position: sticky;", css);
    }

    [Fact]
    public void Command_view_shows_pending_orders_without_rendering_all_history()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"orderQueueSection\"", html);
        Assert.Contains("id=\"orderHistory\"", html);
        Assert.Contains("id=\"fleetHistoryPanel\"", html);
        Assert.Contains("id=\"orderHistoryScope\"", html);
        Assert.Contains("id=\"orderHistoryStatus\"", html);
        Assert.Contains("orderHistoryLimit: 20", script);
        Assert.Contains("orders.filter(order => order.status === \"pending\")", script);
        Assert.Contains("state.orders.filter(order => order.status !== \"pending\")", script);
        Assert.Contains("state.orderHistoryLimit += 20;", script);
    }

    [Fact]
    public void History_view_separates_filterable_chronicle_and_event_records()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"historyView\"", html);
        Assert.Contains("data-history-tab=\"chronicle\"", html);
        Assert.Contains("data-history-tab=\"events\"", html);
        Assert.Contains("id=\"chronicleSearch\"", html);
        Assert.Contains("id=\"chronicleImportance\"", html);
        Assert.Contains("id=\"chronicleSort\"", html);
        Assert.Contains("id=\"eventSearch\"", html);
        Assert.Contains("id=\"eventSeverity\"", html);
        Assert.Contains("id=\"eventSort\"", html);
        Assert.Contains("Importance ${formatNumber(entry.importanceScore)}", script);
        Assert.Contains("T${entry.tickNumber ?? \"?\"}", script);
        Assert.Contains("class=\"chronicle-summary\"", script);
    }

    [Fact]
    public void Fleet_actions_use_the_roster_selection_as_their_command_context()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("data-fleet-tab=\"command\"", html);
        Assert.Contains("data-fleet-tab=\"history\"", html);
        Assert.Contains("data-fleet-action=\"move\"", html);
        Assert.Contains("data-fleet-action=\"attack\"", html);
        Assert.Contains("data-fleet-action=\"colonise\"", html);
        Assert.True(
            html.IndexOf("id=\"fleetSection\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"ordersSection\"", StringComparison.Ordinal));
        Assert.DoesNotContain("id=\"fleetSelect\"", html);
        Assert.DoesNotContain("id=\"attackFleetSelect\"", html);
        Assert.DoesNotContain("id=\"coloniseFleetSelect\"", html);
        Assert.Contains("const fleetId = state.selectedFleetId;", script);
    }

    [Fact]
    public void Day_one_guide_points_to_the_required_fleet_before_its_action_form()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("state.selectedFleetId === moveFleetId", script);
        Assert.Contains("state.selectedFleetId === colonise.fleetId", script);
        Assert.Contains("state.selectedFleetId === attack.fleetId", script);
        Assert.Contains("document.querySelector(`[data-fleet-id=\"${moveFleetId}\"]`)", script);
        Assert.Contains("document.querySelector(`[data-fleet-id=\"${colonise.fleetId}\"]`)", script);
        Assert.Contains("document.querySelector(`[data-fleet-id=\"${attack.fleetId}\"]`)", script);
    }

    [Fact]
    public void Day_one_guide_uses_typed_briefing_and_teaches_visibility_and_cycle_history()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("version: \"v2\"", script);
        Assert.Contains("getJson(\"/briefings/opening\")", script);
        Assert.DoesNotContain("event.factJson", script);
        Assert.DoesNotContain("JSON.parse(event.factJson)", script);
        Assert.Contains("id: \"visibility\"", script);
        Assert.Contains("galaxy topology and routes", script);
        Assert.Contains("id: \"cycle-history\"", script);
        Assert.Contains("an operator ends the Cycle", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
