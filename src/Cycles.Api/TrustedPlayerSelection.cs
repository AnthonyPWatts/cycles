using Cycles.Application;
using Cycles.Core;

internal static class TrustedPlayerSelection
{
    public static IReadOnlyCollection<TrustedPlayerResponse> List(
        IEnumerable<TrustedPlayerSelectionSnapshot> players) =>
        players
            .Select(player => new TrustedPlayerResponse(
                player.PlayerId,
                player.Username,
                player.ParticipantStatus))
            .OrderBy(item => item.PlayerName)
            .ToArray();

    public static Guid RequirePlayerId(Guid? requestedPlayerId)
    {
        if (!requestedPlayerId.HasValue)
        {
            throw new ApiForbiddenException("Choose an available player to sign in.");
        }

        return requestedPlayerId.Value;
    }

    public static Guid RequireListedPlayerId(
        Guid? requestedPlayerId,
        IEnumerable<TrustedPlayerSelectionSnapshot> listedPlayers)
    {
        ArgumentNullException.ThrowIfNull(listedPlayers);

        var playerId = RequirePlayerId(requestedPlayerId);
        if (!listedPlayers.Any(player => player.PlayerId == playerId))
        {
            throw new ApiForbiddenException("Choose an available player to sign in.");
        }

        return playerId;
    }

    public static bool CanAdvanceTurn(Player player, MatchParticipant participant, Empire empire, bool trustedPlayerSelectionEnabled) =>
        player.Role == PlayerRole.Admin
        || (trustedPlayerSelectionEnabled
            && participant.Status == MatchParticipantStatus.Active
            && participant.EndedAt is null
            && empire.Status == EmpireStatus.Active);
}

internal static class TrustedPlayerSelectionConfiguration
{
    public static string? ResolvePlaygroundAccessCode(
        bool trustedPlayerSelectionEnabled,
        bool isDevelopment,
        string? environmentAccessCode,
        string? configuredAccessCode)
    {
        var accessCode = environmentAccessCode ?? configuredAccessCode;
        if (trustedPlayerSelectionEnabled && !isDevelopment && string.IsNullOrWhiteSpace(accessCode))
        {
            throw new InvalidOperationException(
                "Trusted player selection outside Development requires Cycles:PlaygroundAccessCode or CYCLES_PLAYGROUND_ACCESS_CODE.");
        }

        return accessCode;
    }
}
