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

        Assert.Contains("window.addEventListener(\"hashchange\"", script);
        Assert.Contains("window.history.replaceState(null, \"\", `#${selectedView}`);", script);
        Assert.Contains("link.setAttribute(\"aria-current\", \"page\");", script);
        Assert.Contains("activateView(step.view, { updateLocation: true });", script);
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

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
