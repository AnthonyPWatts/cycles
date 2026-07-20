namespace Cycles.Tests;

public sealed class ApiAuthenticationBoundaryTests
{
    [Fact]
    public void Trusted_login_uses_scoped_reads_and_issues_the_cookie_only_after_the_response_exists()
    {
        var program = ReadApiSource("Program.cs");
        var methodStart = program.IndexOf("static LoginResponse Login(", StringComparison.Ordinal);
        Assert.True(methodStart >= 0);
        var methodEnd = program.IndexOf(
            "static LoginResponse QueryLoginResponse(",
            methodStart,
            StringComparison.Ordinal);

        Assert.True(methodEnd > methodStart);
        var method = program[methodStart..methodEnd];
        var legacyScope = method.IndexOf("legacyScope.GetRequired()", StringComparison.Ordinal);
        var listedPlayer = method.IndexOf("RequireListedPlayerId", StringComparison.Ordinal);
        var actorQuery = method.IndexOf("gameAccess.Get(playerId, scope)", StringComparison.Ordinal);
        var recordLogin = method.IndexOf("accounts.RecordLogin", StringComparison.Ordinal);
        var response = method.IndexOf("QueryLoginResponse", StringComparison.Ordinal);
        var cookie = method.IndexOf("DevelopmentAuth.SignIn", StringComparison.Ordinal);

        Assert.True(legacyScope >= 0);
        Assert.True(listedPlayer > legacyScope);
        Assert.True(actorQuery > listedPlayer);
        Assert.True(recordLogin > actorQuery);
        Assert.True(response > recordLogin);
        Assert.True(cookie > response);
        Assert.DoesNotContain("RepairLegacyStartingAdmiralName", method, StringComparison.Ordinal);
        Assert.DoesNotContain("IGameStateStore", method, StringComparison.Ordinal);
    }

    [Fact]
    public void Account_session_resolves_the_player_without_requiring_a_game_or_cycle()
    {
        var program = ReadApiSource("Program.cs");
        var routeStart = program.IndexOf("app.MapGet(\"/auth/session\"", StringComparison.Ordinal);
        var routeEnd = program.IndexOf(
            "app.MapGet(\"/dashboard/bootstrap\"",
            routeStart,
            StringComparison.Ordinal);

        Assert.True(routeStart >= 0);
        Assert.True(routeEnd > routeStart);
        var route = program[routeStart..routeEnd];

        Assert.Contains("IPlayerAccountQuery accounts", route, StringComparison.Ordinal);
        Assert.Contains("DevelopmentAuth.RequireAccount", route, StringComparison.Ordinal);
        Assert.Contains("AccountSessionResponse", route, StringComparison.Ordinal);
        Assert.DoesNotContain("ILegacyRuntimeScopeQuery", route, StringComparison.Ordinal);
        Assert.DoesNotContain("IGameCommandAccessQuery", route, StringComparison.Ordinal);
        Assert.DoesNotContain("ICycleViewQuery", route, StringComparison.Ordinal);
        Assert.DoesNotContain("IGameStateStore", route, StringComparison.Ordinal);
    }

    [Fact]
    public void Oidc_events_preserve_transient_account_contention_through_the_safe_error_endpoint()
    {
        var authentication = ReadApiSource("ExternalAuthentication.cs");
        var program = ReadApiSource("Program.cs");

        Assert.Contains("catch (ApiStateConflictException exception)", authentication, StringComparison.Ordinal);
        Assert.Contains("MarkTemporarilyBusy(context.HttpContext)", authentication, StringComparison.Ordinal);
        Assert.Contains("ResolveRemoteFailure(context.HttpContext)", authentication, StringComparison.Ordinal);
        Assert.Contains("ExternalAuthenticationFailureCodes.TemporarilyBusy", program, StringComparison.Ordinal);
        Assert.Contains("ApiErrorCodes.StateConflict", program, StringComparison.Ordinal);
        Assert.Contains("StatusCodes.Status409Conflict", program, StringComparison.Ordinal);
    }

    private static string ReadApiSource(string path) =>
        File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "OnlineSource",
            "Api",
            path));
}
