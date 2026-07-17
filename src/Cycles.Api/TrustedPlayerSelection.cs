using Cycles.Core;

internal static class TrustedPlayerSelection
{
    public static IReadOnlyCollection<TrustedPlayerResponse> List(GameState state, Cycle cycle) =>
        state.MatchParticipants
            .Where(item => item.CycleId == cycle.CycleId)
            .Join(
                state.Players.Where(item => item.Kind == PlayerKind.Human && item.Status == PlayerStatus.Active),
                participant => participant.PlayerId,
                player => player.PlayerId,
                (participant, player) => new TrustedPlayerResponse(player.PlayerId, player.Username, participant.Status))
            .OrderBy(item => item.PlayerName)
            .ToArray();

    public static TrustedPlayerLogin Resolve(GameState state, Cycle cycle, Guid? requestedPlayerId)
    {
        if (!requestedPlayerId.HasValue)
        {
            throw new ApiForbiddenException("Choose an available player to sign in.");
        }

        var player = state.Players.SingleOrDefault(item => item.PlayerId == requestedPlayerId.Value);
        if (player is null || player.Kind != PlayerKind.Human || player.Status != PlayerStatus.Active)
        {
            throw new ApiForbiddenException("The selected player is not available for trusted sign-in.");
        }

        var participant = state.GetParticipant(cycle.CycleId, player.PlayerId)
            ?? throw new ApiForbiddenException("The selected player is not participating in the active Cycle.");
        var empire = state.Empires.Single(item => item.EmpireId == participant.EmpireId);
        return new TrustedPlayerLogin(player, participant, empire);
    }

    public static bool CanAdvanceTurn(Player player, MatchParticipant participant, Empire empire, bool trustedPlayerSelectionEnabled) =>
        player.Role == PlayerRole.Admin
        || (trustedPlayerSelectionEnabled
            && participant.Status == MatchParticipantStatus.Active
            && participant.EndedAt is null
            && empire.Status == EmpireStatus.Active);
}

internal sealed record TrustedPlayerLogin(Player Player, MatchParticipant Participant, Empire Empire);

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
