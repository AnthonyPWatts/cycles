using Cycles.Application;
using Cycles.Core;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Cycles.Tests;

public sealed class ExternalAuthenticationTests
{
    [Fact]
    public void Invited_identity_is_admitted_through_the_player_only_account_boundary()
    {
        var expected = Account("one");
        var accounts = new RecordingAccountStore(command =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Success(
                new ExternalPlayerSignInSnapshot(expected, Created: true, BootstrapAuditRecordId: null)));
        var options = Options(invited: "https://identity.example|subject-2");

        var player = ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "subject-2",
            "one",
            "two@example.test",
            options,
            TestState.Now);

        Assert.Equal(expected, player);
        var command = Assert.IsType<ExternalPlayerSignInCommand>(accounts.ExternalCommand);
        Assert.Equal("https://identity.example", command.Issuer);
        Assert.Equal("subject-2", command.Subject);
        Assert.Equal("one", command.PreferredUsername);
        Assert.Equal("two@example.test", command.Email);
        Assert.Null(command.Bootstrap);
        Assert.Equal(TestState.Now, command.SignedInAt);
    }

    [Fact]
    public void Uninvited_identity_is_rejected_without_calling_the_account_store()
    {
        var accounts = new RecordingAccountStore(_ => throw new InvalidOperationException("Must not be called."));

        var exception = Assert.Throws<ApiForbiddenException>(() => ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "not-invited",
            "outsider",
            null,
            Options(invited: "https://identity.example|subject-2"),
            TestState.Now));

        Assert.Contains("not been invited", exception.Message, StringComparison.Ordinal);
        Assert.Null(accounts.ExternalCommand);
    }

    [Fact]
    public void Explicit_bootstrap_identity_passes_an_auditable_configuration_source()
    {
        var expected = Account("administrator", role: PlayerRole.Admin);
        var accounts = new RecordingAccountStore(command =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Success(
                new ExternalPlayerSignInSnapshot(
                    expected,
                    Created: true,
                    BootstrapAuditRecordId: Guid.NewGuid())));
        var options = Options(bootstrap: "https://identity.example|admin-subject");

        var player = ExternalIdentityAdmission.SignIn(
            accounts,
            "https://identity.example",
            "admin-subject",
            "administrator",
            null,
            options,
            TestState.Now);

        Assert.Equal(PlayerRole.Admin, player.Role);
        Assert.Equal("configuration:test-revision", accounts.ExternalCommand?.Bootstrap?.Source);
    }

    [Fact]
    public void Unavailable_and_busy_account_results_are_mapped_without_leaking_storage_details()
    {
        var unavailable = new RecordingAccountStore(_ =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Unavailable());
        var busy = new RecordingAccountStore(_ =>
            new AccountCommandResult<ExternalPlayerSignInSnapshot>.Busy());
        var options = Options(invited: "https://identity.example|subject-2");

        Assert.Throws<ApiForbiddenException>(() => ExternalIdentityAdmission.SignIn(
            unavailable,
            "https://identity.example",
            "subject-2",
            null,
            null,
            options,
            TestState.Now));
        Assert.Throws<ApiStateConflictException>(() => ExternalIdentityAdmission.SignIn(
            busy,
            "https://identity.example",
            "subject-2",
            null,
            null,
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

    [Theory]
    [InlineData(" |subject")]
    [InlineData("issuer| ")]
    [InlineData("\t|subject")]
    [InlineData("issuer|\t")]
    public void Configured_identity_rejects_parts_that_are_empty_after_operator_whitespace_is_trimmed(
        string configuredIdentity)
    {
        var options = new ExternalAuthenticationOptions
        {
            Authority = "https://identity.example",
            ClientId = "cycles",
            ClientSecret = "configured-outside-source",
            InvitedIdentities = [configuredIdentity]
        };

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);
        Assert.Contains("issuer|subject", exception.Message, StringComparison.Ordinal);
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

    private static ExternalAuthenticationOptions Options(string? invited = null, string? bootstrap = null) => new()
    {
        InvitedIdentities = invited is null ? [] : [invited],
        AdminBootstrapIdentities = bootstrap is null ? [] : [bootstrap],
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
