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
        Assert.Contains("href=\"#chronicle\"", html);
        Assert.DoesNotContain("class=\"side-panel\"", html);

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
        Assert.Contains("orderHistoryLimit: 20", script);
        Assert.Contains("orders.filter(order => order.status === \"pending\")", script);
        Assert.Contains(".filter(order => order.status !== \"pending\")", script);
        Assert.Contains("state.orderHistoryLimit += 20;", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
