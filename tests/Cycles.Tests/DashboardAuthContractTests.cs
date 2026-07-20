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
        Assert.Contains("form.method = \"post\";", script);
        Assert.Contains("form.action = \"/auth/logout\";", script);
        Assert.Contains("token.name = antiforgeryFormFieldName;", script);
        Assert.DoesNotContain("window.location.assign(\"/auth/logout\");", script);
    }

    [Fact]
    public void Missing_session_prompts_for_login_instead_of_logging_in_automatically()
    {
        var script = ReadDashboardAsset("app.js");
        var bootStart = script.IndexOf("async function boot()", StringComparison.Ordinal);
        var loginStart = script.IndexOf("async function loadTrustedPlayers()", StringComparison.Ordinal);

        Assert.True(bootStart >= 0);
        Assert.True(loginStart > bootStart);

        var bootFunction = script[bootStart..loginStart];
        Assert.Contains("showLogin(\"Choose a player to continue.\");", bootFunction);
        Assert.Contains("await loadTrustedPlayers();", bootFunction);
        Assert.Contains("await refresh({ applySessionFromBootstrap: true });", bootFunction);
        Assert.DoesNotContain("/auth/session", bootFunction);
        Assert.DoesNotContain("await login(", bootFunction);
    }

    [Fact]
    public void Initial_load_and_refresh_use_one_player_bootstrap_request()
    {
        var script = ReadDashboardAsset("app.js");
        var refreshStart = script.IndexOf("async function refresh(", StringComparison.Ordinal);
        var refreshEnd = script.IndexOf("function viewFromHash()", refreshStart, StringComparison.Ordinal);

        Assert.True(refreshStart >= 0);
        Assert.True(refreshEnd > refreshStart);

        var refreshFunction = script[refreshStart..refreshEnd];
        Assert.Contains("const bootstrap = state.gameId", refreshFunction);
        Assert.Contains("gameApi.getJson(`/dashboard/bootstrap${selectedFleetQuery}`)", refreshFunction);
        Assert.Contains("getJson(`/dashboard/bootstrap${selectedFleetQuery}`)", refreshFunction);
        Assert.DoesNotContain("Promise.all", refreshFunction);
        Assert.DoesNotContain("/fleets/${state.selectedFleetId}", refreshFunction);
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
