namespace Cycles.Tests;

public sealed class DashboardCommandOverviewContractTests
{
    [Fact]
    public void Command_overview_keeps_resources_and_linked_priorities_visible_together()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"resourcesSection\" class=\"view-card summary command-resources\"", html);
        Assert.Contains("id=\"homeSystemName\"", html);
        Assert.Contains("id=\"prioritySection\" class=\"view-card priority-console\"", html);
        Assert.Contains("data-priority-key=\"industryWeight\" type=\"range\"", html);
        Assert.Contains("data-priority-key=\"researchWeight\" type=\"range\"", html);
        Assert.Contains("data-priority-key=\"militaryWeight\" type=\"range\"", html);
        Assert.Contains("data-priority-key=\"expansionWeight\" type=\"range\"", html);
        Assert.Contains("id=\"priorityResetButton\"", html);
        Assert.Contains("id=\"prioritySaveButton\"", html);
        Assert.Contains("id=\"priorityDraftStatus\"", html);
        Assert.Contains("class=\"priority-channel-name\"", html);
        Assert.Contains("class=\"priority-saved-marker\"", html);
        Assert.DoesNotContain("id=\"priorityHint\"", html);
        Assert.DoesNotContain("<dialog", html);
        Assert.DoesNotContain("id=\"adjustPrioritiesButton\"", html);

        Assert.Contains("elements.homeSystemName.textContent = empire.homeSystem.systemName;", script);
        Assert.DoesNotContain("resource-home", script);
        Assert.Contains("rebalancePriorityDraft(input.dataset.priorityKey", script);
        Assert.Contains("const exactValue = remaining * weight / weightTotal;", script);
        Assert.Contains("sliderShell.classList.toggle(\"has-saved-marker\", isChanged);", script);
        Assert.Contains("elements.priorityDraftStatus.textContent = state.prioritySaving ? \"Saving\" : isDirty ? \"Unsaved\" : \"Saved\";", script);
        Assert.Contains("elements.prioritySaveButton.disabled = !isDirty || total !== 100 || state.prioritySaving;", script);
        Assert.Contains("elements.priorityResetButton.disabled = !isDirty || state.prioritySaving;", script);
        Assert.Contains("target: () => document.querySelector(\"#prioritySection\")", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
