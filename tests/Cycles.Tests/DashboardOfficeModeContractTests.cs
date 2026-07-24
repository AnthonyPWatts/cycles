namespace Cycles.Tests;

public sealed class DashboardOfficeModeContractTests
{
    [Fact]
    public void Office_mode_is_a_persisted_admin_and_local_development_presentation_toggle()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("office-mode.css?v=20260724-1", html);
        Assert.Contains(
            "id=\"officeModeButton\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "aria-pressed=\"false\"",
            html,
            StringComparison.Ordinal);
        Assert.Contains(
            "hidden>Office mode</button>",
            html,
            StringComparison.Ordinal);

        Assert.Contains(
            "const officeModeStorageKey = \"cycles.officeMode\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "return String(state.role ?? \"\").toLowerCase() === \"admin\";",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "return Boolean(state.playerId)",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "state.authenticationMode === \"developmentSelector\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "elements.officeModeButton.hidden = !isAvailable;",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "document.body.classList.toggle(\"office-mode\", active);",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "elements.officeModeButton.setAttribute(\"aria-pressed\", String(active));",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "readStoredValue(officeModeStorageKey) === \"true\"",
            script,
            StringComparison.Ordinal);
        Assert.Contains(
            "writeStoredValue(officeModeStorageKey, String(active));",
            script,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Office_mode_styles_are_scoped_and_remove_decorative_layers()
    {
        var styles = ReadDashboardAsset("office-mode.css");

        Assert.Contains(
            "body.office-mode {",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "color-scheme: light;",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.office-mode .view-nav a::before",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.office-mode .resource-card::before",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.office-mode .atlas-background",
            styles,
            StringComparison.Ordinal);
        Assert.Contains(
            "body.office-mode .starfield-layer",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\n.view-card",
            styles,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\n.topbar",
            styles,
            StringComparison.Ordinal);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
