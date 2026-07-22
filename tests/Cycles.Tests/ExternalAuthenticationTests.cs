using Cycles.Application;
using Cycles.Core;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Cycles.Tests;

public sealed class ExternalAuthenticationTests
{
    private const string ProxySecret = "test-proxy-secret-that-is-at-least-32-characters";

    [Fact]
    public void Invited_identity_is_admitted_through_the_player_only_account_boundary()
    {
        var expected = Account("one");
        var accounts = new RecordingAccountStore(command =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Success(
                new ExternalPlayerSignInSnapshot(expected, Bound: true, BootstrapAuditRecordId: null)));
        var options = Options(expected.PlayerId, "two@example.test");

        var player = ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "subject-2",
            "two@example.test",
            emailVerified: true,
            options,
            TestState.Now);

        Assert.Equal(expected, player);
        var command = Assert.IsType<ExternalPlayerSignInCommand>(accounts.ExternalCommand);
        Assert.Equal("https://identity.example", command.Issuer);
        Assert.Equal("subject-2", command.Subject);
        Assert.Equal(expected.PlayerId, command.Binding!.PlayerId);
        Assert.Equal("two@example.test", command.Binding.VerifiedEmail);
        Assert.Null(command.Binding.Bootstrap);
        Assert.Equal(TestState.Now, command.SignedInAt);
    }

    [Fact]
    public void Uninvited_identity_reaches_the_store_without_a_binding_so_existing_identities_can_sign_in()
    {
        var accounts = new RecordingAccountStore(_ =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable());

        var exception = Assert.Throws<ApiForbiddenException>(() => ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "not-invited",
            "two@example.test",
            emailVerified: false,
            Options(Guid.NewGuid(), "two@example.test"),
            TestState.Now));

        Assert.Contains("not available", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(accounts.ExternalCommand);
        Assert.Null(accounts.ExternalCommand.Binding);
    }

    [Fact]
    public void Explicit_bootstrap_identity_passes_an_auditable_configuration_source()
    {
        var expected = Account("administrator", role: PlayerRole.Admin);
        var accounts = new RecordingAccountStore(command =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Success(
                new ExternalPlayerSignInSnapshot(
                    expected,
                    Bound: true,
                    BootstrapAuditRecordId: Guid.NewGuid())));
        var options = Options(expected.PlayerId, "administrator@example.test", bootstrapAdmin: true);

        var player = ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "admin-subject",
            "administrator@example.test",
            emailVerified: true,
            options,
            TestState.Now);

        Assert.Equal(PlayerRole.Admin, player.Role);
        Assert.Equal("configuration:test-revision", accounts.ExternalCommand?.Binding?.Bootstrap?.Source);
    }

    [Fact]
    public void Unavailable_and_busy_account_results_are_mapped_without_leaking_storage_details()
    {
        var unavailable = new RecordingAccountStore(_ =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable());
        var busy = new RecordingAccountStore(_ =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy());
        var options = Options(Guid.NewGuid(), "two@example.test");

        Assert.Throws<ApiForbiddenException>(() => ExternalIdentityAdmission.SignIn(
            unavailable,
            "https://identity.example",
            "subject-2",
            "two@example.test",
            emailVerified: true,
            options,
            TestState.Now));
        Assert.Throws<ApiStateConflictException>(() => ExternalIdentityAdmission.SignIn(
            busy,
            "https://identity.example",
            "subject-2",
            "two@example.test",
            emailVerified: true,
            options,
            TestState.Now));
    }

    [Fact]
    public void Authenticated_local_player_claim_is_resolved_without_game_membership()
    {
        var player = Account("new-player");
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(CyclesClaimTypes.PlayerId, player.PlayerId.ToString("D"))],
                CyclesAuthenticationSchemes.Cookie))
        };

        var account = DevelopmentAuth.RequireAccount(
            context,
            new InMemoryAccountQuery(player));

        Assert.Equal(player.PlayerId, account.PlayerId);
    }

    [Fact]
    public void Oidc_configuration_requires_provider_credentials_and_valid_invitations()
    {
        var missingCredentials = new ExternalAuthenticationOptions();
        Assert.Throws<InvalidOperationException>(missingCredentials.Validate);

        var malformedIdentity = new ExternalAuthenticationOptions
        {
            Authority = "https://identity.example",
            ClientId = "cycles",
            ClientSecret = "configured-outside-source",
            CanonicalHost = "cycles.example.test",
            ProxySecret = ProxySecret,
            Invitations = [new ExternalAuthenticationInvitation { Email = "email@example.test" }]
        };
        var exception = Assert.Throws<InvalidOperationException>(malformedIdentity.Validate);
        Assert.Contains("PlayerId", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Invitation_configuration_rejects_duplicate_emails_case_insensitively()
    {
        var options = new ExternalAuthenticationOptions
        {
            Authority = "https://identity.example",
            ClientId = "cycles",
            ClientSecret = "configured-outside-source",
            CanonicalHost = "cycles.example.test",
            ProxySecret = ProxySecret,
            Invitations =
            [
                new ExternalAuthenticationInvitation
                {
                    PlayerId = Guid.NewGuid(),
                    Email = "person@example.test"
                },
                new ExternalAuthenticationInvitation
                {
                    PlayerId = Guid.NewGuid(),
                    Email = "PERSON@example.test"
                }
            ]
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("duplicate email", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Transient_account_contention_keeps_a_safe_retryable_remote_failure_category()
    {
        var context = new DefaultHttpContext();

        Assert.Equal(
            ExternalAuthenticationFailureCodes.ExternalAuthenticationFailed,
            ExternalAuthenticationFailureCodes.ResolveRemoteFailure(context));

        ExternalAuthenticationFailureCodes.MarkTemporarilyBusy(context);

        Assert.Equal(
            ExternalAuthenticationFailureCodes.TemporarilyBusy,
            ExternalAuthenticationFailureCodes.ResolveRemoteFailure(context));

        ExternalAuthenticationFailureCodes.MarkNotAdmitted(context);

        Assert.Equal(
            ExternalAuthenticationFailureCodes.NotAdmitted,
            ExternalAuthenticationFailureCodes.ResolveRemoteFailure(context));
    }

    private static PlayerAccountSnapshot Account(
        string username,
        PlayerRole role = PlayerRole.Player) =>
        new(
            Guid.NewGuid(),
            username,
            PlayerKind.Human,
            role,
            PlayerStatus.Active,
            TestState.Now,
            TestState.Now);

    private static ExternalAuthenticationOptions Options(
        Guid playerId,
        string email,
        bool bootstrapAdmin = false) => new()
    {
        Invitations =
        [
            new ExternalAuthenticationInvitation
            {
                PlayerId = playerId,
                Email = email,
                BootstrapAdmin = bootstrapAdmin
            }
        ],
        CanonicalHost = "cycles.example.test",
        ProxySecret = ProxySecret,
        DeploymentRevision = "test-revision"
    };

    private sealed class InMemoryAccountQuery(params PlayerAccountSnapshot[] players) : IPlayerAccountQuery
    {
        public PlayerAccountSnapshot? Get(Guid playerId) =>
            players.SingleOrDefault(player => player.PlayerId == playerId);
    }

    private sealed class RecordingAccountStore(
        Func<ExternalPlayerSignInCommand, AccountCommandResult<ExternalPlayerSignInSnapshot>> signIn)
        : IPlayerAccountCommandStore
    {
        public ExternalPlayerSignInCommand? ExternalCommand { get; private set; }

        public AccountCommandResult<ExternalPlayerSignInSnapshot> SignInExternal(
            ExternalPlayerSignInCommand command)
        {
            ExternalCommand = command;
            return signIn(command);
        }

        public AccountCommandResult<PlayerAccountSnapshot> RecordLogin(RecordPlayerLoginCommand command) =>
            throw new NotSupportedException();
    }
}
