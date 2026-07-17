using Cycles.Core;
using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class TrustedPlayerSelectionTests
{
    [Fact]
    public void Selector_lists_only_active_human_accounts_in_the_match()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;

        var players = TrustedPlayerSelection.List(state, cycle);

        Assert.Equal(["Tony", "Will"], players.Select(item => item.PlayerName));
        Assert.All(players, item => Assert.Equal(MatchParticipantStatus.Active, item.ParticipantStatus));
    }

    [Fact]
    public void Selector_rejects_missing_unknown_and_ai_player_ids()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var ai = state.Players.Single(item => item.Kind == PlayerKind.AI);

        Assert.Throws<ApiForbiddenException>(() => TrustedPlayerSelection.Resolve(state, cycle, null));
        Assert.Throws<ApiForbiddenException>(() => TrustedPlayerSelection.Resolve(state, cycle, Guid.NewGuid()));
        Assert.Throws<ApiForbiddenException>(() => TrustedPlayerSelection.Resolve(state, cycle, ai.PlayerId));
    }

    [Fact]
    public void Defeated_human_remains_selectable_but_cannot_advance_the_turn()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var tony = state.Players.Single(item => item.Username == "Tony");
        var participant = state.GetParticipant(cycle.CycleId, tony.PlayerId)!;
        MatchControl.DefeatEmpire(state, participant.EmpireId, TestState.Now.AddHours(1));

        var selected = TrustedPlayerSelection.Resolve(state, cycle, tony.PlayerId);
        var listed = Assert.Single(TrustedPlayerSelection.List(state, cycle), item => item.PlayerId == tony.PlayerId);

        Assert.Equal(MatchParticipantStatus.Defeated, listed.ParticipantStatus);
        Assert.False(TrustedPlayerSelection.CanAdvanceTurn(
            selected.Player,
            selected.Participant,
            selected.Empire,
            trustedPlayerSelectionEnabled: true));
    }

    [Fact]
    public void Production_trusted_mode_requires_the_shared_access_boundary()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            TrustedPlayerSelectionConfiguration.ResolvePlaygroundAccessCode(
                trustedPlayerSelectionEnabled: true,
                isDevelopment: false,
                environmentAccessCode: null,
                configuredAccessCode: null));

        Assert.Contains("requires", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Null(TrustedPlayerSelectionConfiguration.ResolvePlaygroundAccessCode(
            trustedPlayerSelectionEnabled: true,
            isDevelopment: true,
            environmentAccessCode: null,
            configuredAccessCode: null));
    }

    [Fact]
    public void Trusted_cookie_is_protected_and_production_ignores_the_development_header()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var tony = state.Players.Single(item => item.Username == "Tony");
        var signIn = TestHttpContextFactory.CreateProduction();
        DevelopmentAuth.SignIn(signIn, tony);
        var setCookie = signIn.Response.Headers["Set-Cookie"].ToString();
        Assert.NotEmpty(setCookie);
        var cookiePair = setCookie.Split(';', 2)[0];

        var authenticated = TestHttpContextFactory.CreateProduction();
        authenticated.Request.Headers.Cookie = cookiePair;
        Assert.Equal(tony.PlayerId, DevelopmentAuth.RequireActor(authenticated, state).Player.PlayerId);

        var tampered = TestHttpContextFactory.CreateProduction();
        tampered.Request.Headers.Cookie = cookiePair[..^1] + (cookiePair[^1] == 'A' ? 'B' : 'A');
        Assert.Throws<ApiUnauthorizedException>(() => DevelopmentAuth.RequireActor(tampered, state));

        var headerOnly = TestHttpContextFactory.CreateProduction();
        headerOnly.Request.Headers[DevelopmentAuth.HeaderName] = tony.PlayerId.ToString("D");
        Assert.Throws<ApiUnauthorizedException>(() => DevelopmentAuth.RequireActor(headerOnly, state));
    }

    [Fact]
    public void Even_a_valid_trusted_cookie_cannot_authenticate_an_ai_player()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var ai = state.Players.Single(item => item.Kind == PlayerKind.AI);
        var signIn = TestHttpContextFactory.CreateProduction();
        DevelopmentAuth.SignIn(signIn, ai);
        var setCookie = signIn.Response.Headers["Set-Cookie"].ToString();
        Assert.NotEmpty(setCookie);
        var cookiePair = setCookie.Split(';', 2)[0];
        var request = TestHttpContextFactory.CreateProduction();
        request.Request.Headers.Cookie = cookiePair;

        Assert.Throws<ApiForbiddenException>(() => DevelopmentAuth.RequireActor(request, state));
    }
}
