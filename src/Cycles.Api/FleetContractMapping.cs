using Cycles.Core;

internal static class FleetContractMapping
{
    public static string GetOwnerName(GameState state, Fleet fleet) =>
        state.GetFleetFaction(fleet).FactionName;
}
