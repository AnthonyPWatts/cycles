namespace Cycles.Tests;

public sealed class DashboardTutorialModalRegressionTests
{
    [Fact]
    public void Narrow_guide_is_modal_and_contains_keyboard_focus()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");

        Assert.Contains("window.innerWidth < 1200", script);
        Assert.Contains("modal ? \"dialog\" : \"complementary\"", script);
        Assert.Contains("setAttribute(\"aria-modal\", \"true\")", script);
        Assert.Contains("removeAttribute(\"aria-modal\")", script);
        Assert.Contains("setTutorialBackgroundInert(modal)", script);
        Assert.Contains("element.inert = true", script);
        Assert.Contains("function focusTutorialClose()", script);
        Assert.Contains("elements.tutorialCloseButton.focus({ preventScroll: true })", script);
        Assert.Contains("if (tutorial.modal) {", script);
        Assert.Contains("if (event.key === \"Tab\")", script);
        Assert.Contains("function containTutorialFocus(event)", script);
        Assert.Contains("event.shiftKey && active === first", script);
        Assert.Contains("!event.shiftKey && active === last", script);
        Assert.Contains("elements.tutorialCloseButton.addEventListener(\"click\", closeTutorialPanel)", script);
        Assert.Contains("event.key === \"Escape\"", script);
        Assert.Contains("closeTutorialPanel();", script);
        Assert.Contains("(tutorial.returnFocus ?? elements.tutorialButton).focus()", script);
        Assert.Contains("body.tutorial-modal::before", css);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
