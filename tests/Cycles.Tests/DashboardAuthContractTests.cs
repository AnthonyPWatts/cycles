using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class DashboardAuthContractTests
{
    [Fact]
    public void Dashboard_has_distinct_logged_out_and_authenticated_states()
    {
        var html = ReadDashboardAsset("app.html");
        var script = ReadDashboardAsset("app.js");

        Assert.Contains("id=\"loginMessage\"", html);
        Assert.Contains("id=\"sessionSummary\"", html);
        Assert.Contains("id=\"signOutButton\"", html);
        Assert.Contains("id=\"appShell\" class=\"app-shell\" hidden", html);
        Assert.Contains("elements.loginForm.hidden = true;", script);
        Assert.Contains("elements.sessionSummary.hidden = false;", script);
        Assert.Contains("elements.appShell.hidden = false;", script);
        Assert.Contains("elements.loginForm.hidden = false;", script);
        Assert.Contains("elements.sessionSummary.hidden = true;", script);
        Assert.Contains("elements.appShell.hidden = true;", script);
        Assert.Contains("await postJson(\"/auth/logout\", {});", script);
    }

    [Fact]
    public void Missing_session_prompts_for_login_instead_of_logging_in_automatically()
    {
        var script = ReadDashboardAsset("app.js");
        var bootStart = script.IndexOf("async function boot()", StringComparison.Ordinal);
        var loginStart = script.IndexOf("async function login(username)", StringComparison.Ordinal);

        Assert.True(bootStart >= 0);
        Assert.True(loginStart > bootStart);

        var bootFunction = script[bootStart..loginStart];
        Assert.Contains("showLogin(\"Enter your player name to continue.\");", bootFunction);
        Assert.DoesNotContain("await login(", bootFunction);
    }

    [Fact]
    public void Sign_out_expires_the_development_session_cookie()
    {
        var context = new DefaultHttpContext();

        DevelopmentAuth.SignOut(context);

        var setCookie = Assert.Single(context.Response.Headers.SetCookie);
        Assert.StartsWith($"{DevelopmentAuth.CookieName}=;", setCookie);
        Assert.Contains("expires=", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("path=/", setCookie, StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadDashboardAsset(string fileName) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Dashboard", fileName));
}
