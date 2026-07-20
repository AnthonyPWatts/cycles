using Cycles.Application;
using Cycles.Core;
using Microsoft.AspNetCore.Http;

namespace Cycles.Tests;

public sealed class TrustedPlayerSelectionTests
{
    [Fact]
    public void Selector_maps_and_orders_scoped_account_snapshots()
    {
        var players = TrustedPlayerSelection.List(
        [
            new TrustedPlayerSelectionSnapshot(Guid.NewGuid(), "Will", MatchParticipantStatus.Active),
            new TrustedPlayerSelectionSnapshot(Guid.NewGuid(), "Tony", MatchParticipantStatus.Defeated)
        ]);

        Assert.Equal(["Tony", "Will"], players.Select(item => item.PlayerName));
        Assert.Equal(
            [MatchParticipantStatus.Defeated, MatchParticipantStatus.Active],
            players.Select(item => item.ParticipantStatus));
    }

    [Fact]
    public void Selector_rejects_a_missing_player_id()
    {
        Assert.Throws<ApiForbiddenException>(() => TrustedPlayerSelection.RequirePlayerId(null));
    }

    [Fact]
    public void Trusted_login_accepts_only_an_identifier_returned_by_the_bounded_selector()
    {
        var listedPlayerId = Guid.NewGuid();
        var listedPlayers = new[]
        {
            new TrustedPlayerSelectionSnapshot(
                listedPlayerId,
                "Listed player",
                MatchParticipantStatus.Active)
        };

        Assert.Equal(
            listedPlayerId,
            TrustedPlayerSelection.RequireListedPlayerId(listedPlayerId, listedPlayers));
        Assert.Throws<ApiForbiddenException>(() =>
            TrustedPlayerSelection.RequireListedPlayerId(Guid.NewGuid(), listedPlayers));
        Assert.Throws<ApiForbiddenException>(() =>
            TrustedPlayerSelection.RequireListedPlayerId(null, listedPlayers));
    }

    [Fact]
    public void Defeated_human_remains_selectable_but_cannot_advance_the_turn()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var tony = state.Players.Single(item => item.Username == "Tony");
        var participant = state.GetParticipant(cycle.CycleId, tony.PlayerId)!;
        MatchControl.DefeatEmpire(state, participant.EmpireId, TestState.Now.AddHours(1));

        var empire = state.Empires.Single(item => item.EmpireId == participant.EmpireId);
        var listed = Assert.Single(TrustedPlayerSelection.List(
        [
            new TrustedPlayerSelectionSnapshot(
                tony.PlayerId,
                tony.Username,
                participant.Status)
        ]));

        Assert.Equal(MatchParticipantStatus.Defeated, listed.ParticipantStatus);
        Assert.False(TrustedPlayerSelection.CanAdvanceTurn(
            tony,
            participant,
            empire,
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
