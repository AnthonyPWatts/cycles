using Cycles.Core;

namespace Cycles.Application;

public interface IPlayerAccountCommandStore
{
    AccountCommandResult<ExternalPlayerSignInSnapshot> SignInExternal(
        ExternalPlayerSignInCommand command);

    AccountCommandResult<PlayerAccountSnapshot> RecordLogin(
        RecordPlayerLoginCommand command);
}

public interface ITrustedPlayerSelectionQuery
{
    IReadOnlyList<TrustedPlayerSelectionSnapshot> List(GameCycleScope scope);
}

public sealed record ExternalPlayerSignInCommand
{
    public ExternalPlayerSignInCommand(
        string issuer,
        string subject,
        string? preferredUsername,
        string? email,
        ConfiguredAdminBootstrap? bootstrap,
        DateTimeOffset signedInAt)
    {
        Issuer = ValidateIdentityPart(issuer, nameof(issuer));
        Subject = ValidateIdentityPart(subject, nameof(subject));
        PreferredUsername = NormaliseOptional(preferredUsername, 80);
        Email = NormaliseOptional(email, 256);
        Bootstrap = bootstrap;
        SignedInAt = signedInAt;
    }

    public string Issuer { get; }

    public string Subject { get; }

    public string? PreferredUsername { get; }

    public string? Email { get; }

    public ConfiguredAdminBootstrap? Bootstrap { get; }

    public DateTimeOffset SignedInAt { get; }

    private static string ValidateIdentityPart(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException(
                "External identity values cannot be empty.",
                parameterName);
        }

        if (value.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value.Length,
                "External identity values must be 256 characters or fewer.");
        }

        if (char.IsWhiteSpace(value[0]) || char.IsWhiteSpace(value[^1]))
        {
            throw new ArgumentException(
                "External identity values cannot start or end with whitespace.",
                parameterName);
        }

        // OIDC issuer and subject values are stable, case-sensitive identifiers.
        // Preserve accepted values exactly; in particular, do not normalise case
        // or internal whitespace in the provider's claim.
        return value;
    }

    private static string? NormaliseOptional(string? value, int maximumLength)
    {
        var normalised = value?.Trim();
        if (string.IsNullOrEmpty(normalised))
        {
            return null;
        }

        return normalised[..Math.Min(normalised.Length, maximumLength)];
    }
}

public sealed record ConfiguredAdminBootstrap
{
    public ConfiguredAdminBootstrap(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new ArgumentException(
                "Configured administrator bootstrap requires a source.",
                nameof(source));
        }

        var normalised = source.Trim();
        if (normalised.Length > 256)
        {
            throw new ArgumentOutOfRangeException(
                nameof(source),
                normalised.Length,
                "Configured administrator bootstrap sources must be 256 characters or fewer.");
        }

        Source = normalised;
    }

    public string Source { get; }
}

public sealed record RecordPlayerLoginCommand
{
    public RecordPlayerLoginCommand(Guid playerId, DateTimeOffset signedInAt)
    {
        if (playerId == Guid.Empty)
        {
            throw new ArgumentException(
                "Player identifier cannot be empty.",
                nameof(playerId));
        }

        PlayerId = playerId;
        SignedInAt = signedInAt;
    }

    public Guid PlayerId { get; }

    public DateTimeOffset SignedInAt { get; }
}

public sealed record ExternalPlayerSignInSnapshot(
    PlayerAccountSnapshot Player,
    bool Created,
    Guid? BootstrapAuditRecordId);

public sealed record TrustedPlayerSelectionSnapshot(
    Guid PlayerId,
    string Username,
    MatchParticipantStatus ParticipantStatus)
{
    public const int MaximumResults = 100;
}

public abstract record AccountCommandResult<T>
{
    private AccountCommandResult()
    {
    }

    public sealed record Success(T Value) : AccountCommandResult<T>;

    public sealed record Unavailable() : AccountCommandResult<T>;

    public sealed record Busy() : AccountCommandResult<T>;
}
