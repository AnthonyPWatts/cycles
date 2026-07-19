using Cycles.Core;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Cycles.Tests;

public sealed class ExternalAuthenticationTests
{
    [Fact]
    public void Invited_identity_is_provisioned_by_stable_issuer_and_subject()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        var options = Options(invited: "https://identity.example|subject-2");

        var player = ExternalIdentityAdmission.SignIn(
            state,
            "https://identity.example",
            "subject-2",
            "one",
            "two@example.test",
            options,
            TestState.Now);

        Assert.Equal("https://identity.example", player.ExternalIssuer);
        Assert.Equal("subject-2", player.ExternalSubject);
        Assert.NotEqual("one", player.Username);
        Assert.Equal(PlayerRole.Player, player.Role);
        Assert.Contains(state.Empires, empire => empire.PlayerId == player.PlayerId);
    }

    [Fact]
    public void Uninvited_identity_is_rejected_without_creating_player()
    {
        var state = TestState.CreateSingleEmpireState();
        var originalPlayerCount = state.Players.Count;

        var exception = Assert.Throws<ApiForbiddenException>(() => ExternalIdentityAdmission.SignIn(
            state,
            "https://identity.example",
            "not-invited",
            "outsider",
            null,
            Options(invited: "https://identity.example|subject-2"),
            TestState.Now));

        Assert.Contains("not been invited", exception.Message, StringComparison.Ordinal);
        Assert.Equal(originalPlayerCount, state.Players.Count);
    }

    [Fact]
    public void Explicit_bootstrap_identity_receives_audited_admin_role_once()
    {
        var state = TestState.CreateSingleEmpireState();
        LegacyGameFoundation.Apply(state);
        var options = Options(bootstrap: "https://identity.example|admin-subject");

        var first = ExternalIdentityAdmission.SignIn(
            state,
            "https://identity.example",
            "admin-subject",
            "administrator",
            null,
            options,
            TestState.Now);
        var second = ExternalIdentityAdmission.SignIn(
            state,
            "https://identity.example",
            "admin-subject",
            "ignored-renamed-claim",
            null,
            options,
            TestState.Now.AddMinutes(5));

        Assert.Same(first, second);
        Assert.Equal(PlayerRole.Admin, first.Role);
        var audit = Assert.Single(state.AdminRoleAuditRecords);
        Assert.Equal(AdminRoleAuditAction.Bootstrap, audit.Action);
        Assert.Null(audit.ActorPlayerId);
        Assert.Equal("configuration:test-revision", audit.Source);
    }

    [Fact]
    public void Authenticated_local_player_claim_is_used_before_development_credentials()
    {
        var state = TestState.CreateSingleEmpireState();
        var player = state.Players.Single();
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(CyclesClaimTypes.PlayerId, player.PlayerId.ToString("D"))],
                CyclesAuthenticationSchemes.Cookie))
        };

        var actor = DevelopmentAuth.RequireActor(context, state);

        Assert.Equal(player.PlayerId, actor.Player.PlayerId);
    }

    [Fact]
    public void Non_development_configuration_requires_provider_credentials_and_exact_identity_keys()
    {
        var missingCredentials = new ExternalAuthenticationOptions();
        Assert.Throws<InvalidOperationException>(missingCredentials.Validate);

        var malformedIdentity = new ExternalAuthenticationOptions
        {
            Authority = "https://identity.example",
            ClientId = "cycles",
            ClientSecret = "configured-outside-source",
            InvitedIdentities = ["email@example.test"]
        };
        var exception = Assert.Throws<InvalidOperationException>(malformedIdentity.Validate);
        Assert.Contains("issuer|subject", exception.Message, StringComparison.Ordinal);
    }

    private static ExternalAuthenticationOptions Options(string? invited = null, string? bootstrap = null) => new()
    {
        InvitedIdentities = invited is null ? [] : [invited],
        AdminBootstrapIdentities = bootstrap is null ? [] : [bootstrap],
        DeploymentRevision = "test-revision"
    };
}
