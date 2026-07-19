namespace Cycles.Core;

public static class CurrentRuntimeGameScope
{
    public static void EnsureSupportedForOperationalImport(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        var containsExactlyLegacyGame = state.Games.Count == 1
            && state.Games[0].GameId == GameFoundationConstants.LegacyGameId;
        var containsOnlyLegacyFoundationRows = state.Cycles.All(cycle =>
                cycle.GameId == GameFoundationConstants.LegacyGameId)
            && state.CycleConfigurations.All(configuration =>
                configuration.GameId == GameFoundationConstants.LegacyGameId)
            && state.GameEnrolments.All(enrolment =>
                enrolment.GameId == GameFoundationConstants.LegacyGameId)
            && state.MatchParticipants.All(participant =>
                participant.GameId == GameFoundationConstants.LegacyGameId)
            && state.GameLifecycleEvents.All(gameEvent =>
                gameEvent.GameId == GameFoundationConstants.LegacyGameId);

        if (containsExactlyLegacyGame && containsOnlyLegacyFoundationRows)
        {
            return;
        }

        throw new InvalidOperationException(
            "State transfer v6 can represent multiple Games, but this runtime can currently import only "
            + $"the deterministic legacy Game {GameFoundationConstants.LegacyGameId}. "
            + "Game-scoped API, Worker, store, and player-selection paths must be implemented before "
            + "importing an additional or non-legacy Game.");
    }
}
