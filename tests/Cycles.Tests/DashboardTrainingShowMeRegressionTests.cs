using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardTrainingShowMeRegressionTests
{
    [Fact]
    public void Guide_explains_first_and_targets_only_after_explicit_show_me()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");
        var renderer = FunctionBody(script, "renderTrainingTutorial", "trainingTutorialCanShowTarget");

        Assert.Contains("id=\"tutorialShowMeButton\"", html);
        Assert.Contains(">Show me</button>", html);
        Assert.Contains("styles.css?v=20260721-command-focus-2", html);
        Assert.Contains("app.js?v=20260723-oidc-authority", html);
        Assert.Contains("tutorialShowMeButton: document.querySelector(\"#tutorialShowMeButton\")", script);
        Assert.Contains("elements.tutorialShowMeButton.addEventListener(\"click\", showTrainingTutorialTarget)", script);
        Assert.DoesNotContain("trainingTutorialTarget", renderer);
        Assert.DoesNotContain("applyTutorialTarget", renderer);
        Assert.DoesNotContain("activateView", renderer);
    }

    [Fact]
    public void Show_me_is_hidden_when_there_is_no_truthful_action_target()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("elements.tutorialShowMeButton.hidden = true;", script);
        Assert.Contains("elements.tutorialShowMeButton.hidden = !trainingTutorialCanShowTarget(journey, lesson, status);", script);
        Assert.Contains("status === \"active\"", script);
        Assert.Contains("!journey.coreCompleted", script);
        Assert.Contains("Boolean(lesson)", script);
        Assert.Contains("!lesson.blockedReason", script);
        Assert.Contains("normaliseTutorialStatus(lesson.entryState) !== \"waiting-for-resolution\"", script);
    }

    [Fact]
    public void Show_me_maps_each_server_lesson_to_loaded_authoritative_state()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("return { element: trainingFleetTarget(\"Home Guard\") };", script);
        Assert.Contains("return { element: document.querySelector(\"#eventsSection\") };", script);
        Assert.Contains("trainingEvidenceEvent(lesson, \"prioritiesChanged\")", script);
        Assert.Contains("return { element: trainingFleetTarget(\"Survey Wing\") };", script);
        Assert.Contains("focusElement: document.querySelector(\"#militaryWeight\")", script);
        Assert.Contains("return { element: trainingFleetTarget(\"Vanguard\") };", script);
        Assert.Contains("return { element: elements.fleets };", script);
        Assert.Contains("data-order-id=\"${order.fleetOrderId}\"", script);
        Assert.Contains("state.orderHistoryScope = \"all\";", script);
        Assert.Contains("state.orderHistoryStatus = \"all\";", script);
        Assert.Contains("state.orderHistoryLimit = Math.max(20, state.orders.length);", script);
        Assert.Contains("order.status === \"processed\"", script);
        Assert.Contains("evidenceIds.has(String(order.fleetOrderId).toLowerCase())", script);
        Assert.Contains("document.querySelector(`[data-order-id=\"${processedOrder.fleetOrderId}\"]`)", script);
    }

    [Fact]
    public void Show_me_is_presentation_only_and_releases_narrow_modal_focus()
    {
        var script = ReadDashboardAsset("app.js");
        var css = ReadDashboardAsset("styles.css");
        var handler = FunctionBody(script, "showTrainingTutorialTarget", "trainingTutorialTarget");
        var narrowDismissal = FunctionBody(script, "dismissTutorialForTarget", "syncTutorialPresentation");

        Assert.DoesNotContain("postJson", handler);
        Assert.DoesNotContain("putJson", handler);
        Assert.DoesNotContain("deleteJson", handler);
        Assert.DoesNotContain(".click(", handler);
        Assert.DoesNotContain(".submit(", handler);
        Assert.DoesNotContain("selectFleet(", handler);
        Assert.DoesNotContain("selectCommandMoveTarget", handler);
        Assert.Contains("const narrow = window.innerWidth < 1200;", handler);
        Assert.Contains("const sheetTarget = target.element === elements.prioritySection;", handler);
        Assert.Contains("const describe = !narrow && !sheetTarget;", handler);
        Assert.Contains("applyTutorialTarget(target.element, { describe });", handler);
        Assert.Contains("if (narrow || sheetTarget)", handler);
        Assert.Contains("dismissTutorialForTarget();", handler);
        Assert.Contains("focusTutorialTarget(target.focusElement ?? target.element, { describe });", handler);
        Assert.DoesNotContain("clearTutorialTarget", narrowDismissal);
        Assert.DoesNotContain("tutorialButton).focus", narrowDismissal);
        Assert.Contains("setTutorialBackgroundInert(modal)", script);
        Assert.Contains("if (describe)", script);
        Assert.Contains("if (describe && target !== tutorial.target)", script);
        Assert.Contains("describedBy.add(\"tutorialBody\")", script);
        Assert.Contains("target.setAttribute(\"tabindex\", \"-1\")", script);
        Assert.Matches(
            new Regex(
                @"@media \(max-width: 560px\)[\s\S]*?#tutorialShowMeButton:not\(\[hidden\]\) ~ #tutorialNextButton:not\(\[hidden\]\)\s*\{[^}]*grid-column:\s*1 / -1;",
                RegexOptions.Singleline),
            css);
    }

    private static string FunctionBody(string script, string functionName, string nextFunctionName)
    {
        var start = script.IndexOf($"function {functionName}(", StringComparison.Ordinal);
        var end = script.IndexOf($"function {nextFunctionName}(", start, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Function {functionName} was not found.");
        Assert.True(end > start, $"Function {nextFunctionName} was not found after {functionName}.");
        return script[start..end];
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "Dashboard",
            fileName));
}
