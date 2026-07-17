using Cycles.Core;

internal static class FleetContractMapping
{
    public static string GetOwnerName(GameState state, Fleet fleet) =>
        state.Factions.Single(item => item.FactionId == fleet.FactionId).FactionName;
}
