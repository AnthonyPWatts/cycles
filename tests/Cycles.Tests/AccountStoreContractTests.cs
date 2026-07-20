using Cycles.Application;
using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class AccountStoreContractTests
{
    [Fact]
    public void External_identity_case_and_internal_whitespace_remain_exact_while_display_claims_are_normalised()
    {
        var preferredUsername = $"  {new string('u', 90)}  ";
        var email = $"  {new string('e', 270)}  ";

        var command = new ExternalPlayerSignInCommand(
            "https://Identity Provider.example/Issuer",
            "Subject With Internal Spaces",
            preferredUsername,
            email,
            new ConfiguredAdminBootstrap("  configuration:test  "),
            DateTimeOffset.UnixEpoch);

        Assert.Equal("https://Identity Provider.example/Issuer", command.Issuer);
        Assert.Equal("Subject With Internal Spaces", command.Subject);
        Assert.Equal(new string('u', 80), command.PreferredUsername);
        Assert.Equal(new string('e', 256), command.Email);
        Assert.Equal("configuration:test", command.Bootstrap!.Source);
    }

    [Theory]
    [InlineData(" issuer")]
    [InlineData("issuer ")]
    [InlineData("\tissuer")]
    [InlineData("issuer\u2003")]
    public void External_identity_rejects_issuer_edge_whitespace(string issuer)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ExternalPlayerSignInCommand(
            issuer,
            "subject",
            preferredUsername: null,
            email: null,
            bootstrap: null,
            DateTimeOffset.UnixEpoch));

        Assert.Equal("issuer", exception.ParamName);
    }

    [Theory]
    [InlineData(" subject")]
    [InlineData("subject ")]
    [InlineData("\tsubject")]
    [InlineData("subject\u2003")]
    public void External_identity_rejects_subject_edge_whitespace(string subject)
    {
        var exception = Assert.Throws<ArgumentException>(() => new ExternalPlayerSignInCommand(
            "issuer",
            subject,
            preferredUsername: null,
            email: null,
            bootstrap: null,
            DateTimeOffset.UnixEpoch));

        Assert.Equal("subject", exception.ParamName);
    }

    [Fact]
    public void Identity_lock_names_use_unambiguous_hashes_and_never_include_claims()
    {
        const string issuer = "https://private-issuer.example";
        const string subject = "private-subject-value";

        var lockName = SqlServerGameStateStore.BuildExternalIdentityLockName(issuer, subject);

        Assert.StartsWith("Cycles.Account.Identity.", lockName, StringComparison.Ordinal);
        Assert.DoesNotContain(issuer, lockName, StringComparison.Ordinal);
        Assert.DoesNotContain(subject, lockName, StringComparison.Ordinal);
        Assert.NotEqual(
            SqlServerGameStateStore.BuildExternalIdentityLockName("ab", "c"),
            SqlServerGameStateStore.BuildExternalIdentityLockName("a", "bc"));
        Assert.NotEqual(
            SqlServerGameStateStore.BuildExternalIdentityLockName("Issuer", "subject"),
            SqlServerGameStateStore.BuildExternalIdentityLockName("issuer", "subject"));
    }
}
