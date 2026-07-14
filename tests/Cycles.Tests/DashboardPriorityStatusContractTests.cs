namespace Cycles.Tests;

public sealed class DashboardPriorityStatusContractTests
{
    [Fact]
    public void Priority_status_only_calls_attention_to_pending_changes()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var styles = ReadDashboardAsset("styles.css");

        Assert.DoesNotContain("id=\"priorityTotal\"", html);
        Assert.Contains("id=\"priorityDraftStatus\" class=\"priority-save-status\"", html);
        Assert.Contains("elements.priorityDraftStatus.hidden = !state.prioritySaving && !isDirty;", script);
        Assert.DoesNotContain("priorityTotal: document.querySelector", script);
        Assert.Contains(".priority-save-status", styles);
        Assert.DoesNotContain(".priority-total {", styles);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
