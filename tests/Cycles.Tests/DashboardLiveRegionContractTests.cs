using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class DashboardLiveRegionContractTests
{
    private static readonly string[] MixedPurposeRegionIds =
    [
        "loginMessage",
        "gamesHomeMessage",
        "turnMessage",
        "priorityMessage",
        "orderMessage"
    ];

    [Fact]
    public void Mixed_purpose_live_regions_start_with_polite_status_semantics()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        foreach (var id in MixedPurposeRegionIds)
        {
            Assert.Matches(
                new Regex($"id=\"{id}\"[^>]*role=\"status\""),
                html);
        }

        Assert.Contains("data-tutorial-message role=\"status\"", script);
    }

    [Fact]
    public void Shared_live_region_writer_sets_urgency_before_replacing_text()
    {
        var script = ReadDashboardAsset("app.js");
        var helperStart = script.IndexOf("function setLiveMessage(", StringComparison.Ordinal);
        var helperEnd = script.IndexOf("function setMessage(", helperStart, StringComparison.Ordinal);

        Assert.True(helperStart >= 0);
        Assert.True(helperEnd > helperStart);

        var helper = script[helperStart..helperEnd];
        var roleWrite = helper.IndexOf("element.setAttribute(\"role\", error ? \"alert\" : \"status\");", StringComparison.Ordinal);
        var textWrite = helper.IndexOf("element.textContent = message;", StringComparison.Ordinal);

        Assert.True(roleWrite >= 0);
        Assert.True(textWrite > roleWrite);
        Assert.Contains("setLiveMessage(elements.loginMessage, message, { error });", script);
        Assert.Matches(new Regex(@"setLiveMessage\(\s*elements\.gamesHomeMessage,"), script);
        Assert.Contains("setLiveMessage(message, `Preparing ${offer.displayName}…`);", script);
        Assert.Contains("setLiveMessage(elements.orderMessage, message, options);", script);
        Assert.Contains("setLiveMessage(elements.priorityMessage, message, options);", script);
        Assert.Contains("setLiveMessage(elements.turnMessage, message, options);", script);

        foreach (var id in MixedPurposeRegionIds)
        {
            Assert.DoesNotContain($"elements.{id}.textContent", script);
        }
    }

    [Fact]
    public void Failures_are_assertive_and_subsequent_progress_or_success_is_polite()
    {
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("showLogin(error.message, { error: true });", script);
        Assert.Contains("setLiveMessage(message, error.message, { error: true });", script);
        Assert.Contains("setPriorityMessage(\"Priorities must total 100.\", { error: true });", script);
        Assert.Contains("setTurnMessage(error.message, { error: true });", script);
        Assert.Contains("setMessage(error.message, { error: true });", script);
        Assert.DoesNotMatch(
            new Regex(@"(?:showLogin|setMessage|setPriorityMessage|setTurnMessage)\(error\.message\);"),
            script);

        Assert.Contains("setLiveMessage(elements.loginMessage, \"Signing in...\");", script);
        Assert.Contains("setLiveMessage(message, `Preparing ${offer.displayName}…`);", script);
        Assert.Contains("setPriorityMessage(\"Priorities saved for the next tick.\");", script);
        Assert.Contains("setMessage(replacement.replacesOrderId ? \"Move order replaced.\" : \"Move order queued.\");", script);
        Assert.Contains("setTurnMessage(`Published T${result.tickNumber}", script);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
