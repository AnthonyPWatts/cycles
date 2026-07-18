using Cycles.Core;
using Microsoft.AspNetCore.Http;

internal sealed record DashboardBootstrapContext(
    GameState State,
    Cycle Cycle,
    DevelopmentActor Actor,
    Empire Empire,
    IReadOnlySet<Guid> VisibleSystemIds,
    IReadOnlyCollection<Fleet> Fleets,
    Fleet? SelectedFleet,
    IReadOnlyCollection<FleetOrder> Orders,
    IReadOnlyCollection<EventRecord> Events,
    IReadOnlyCollection<ChronicleEntry> ChronicleEntries,
    OpeningBriefingResponse? OpeningBriefing);

internal static class DashboardBootstrapContextFactory
{
    public static DashboardBootstrapContext Load(
        Guid? selectedFleetId,
        HttpContext httpContext,
        IGameStateStore store)
    {
        var state = store.LoadOrCreate();
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("No active cycle exists.");
        var actor = DevelopmentAuth.RequireActor(httpContext, state);
        var empireId = DevelopmentAuth.ResolveEmpireId(state, actor);
        var empire = state.Empires.Single(item => item.CycleId == cycle.CycleId && item.EmpireId == empireId);
        var visibleSystemIds = ApiVisibility.GetVisibleSystemIds(state, cycle, actor);
        var fleets = PlayerViewScope.SelectFleets(state, cycle, empireId);
        var selectedFleet = selectedFleetId.HasValue
            ? fleets.SingleOrDefault(fleet => fleet.FleetId == selectedFleetId.Value)
            : null;
        selectedFleet ??= fleets.FirstOrDefault(fleet => fleet.Status == FleetStatus.Active && fleet.ShipCount > 0)
            ?? fleets.FirstOrDefault();

        return new DashboardBootstrapContext(
            state,
            cycle,
            actor,
            empire,
            visibleSystemIds,
            fleets,
            selectedFleet,
            PlayerViewScope.SelectOrders(state, cycle, empireId),
            PlayerViewScope.SelectEvents(state, cycle, actor, visibleSystemIds, 20),
            PlayerViewScope.SelectChronicleEntries(state, cycle, actor, visibleSystemIds),
            OpeningBriefingContract.FindVisible(state, cycle, actor, visibleSystemIds));
    }
}

internal static class PlayerViewScope
{
    public static IReadOnlyCollection<Fleet> SelectFleets(GameState state, Cycle cycle, Guid? empireId) =>
        state.Fleets
            .Where(fleet => fleet.CycleId == cycle.CycleId
                            && (!empireId.HasValue || fleet.EmpireId == empireId.Value))
            .OrderBy(fleet => fleet.FleetName)
            .ToArray();

    public static IReadOnlyCollection<FleetOrder> SelectOrders(GameState state, Cycle cycle, Guid? empireId) =>
        state.FleetOrders
            .Where(order => order.CycleId == cycle.CycleId)
            .Where(order => !empireId.HasValue
                            || state.Fleets.Any(fleet => fleet.FleetId == order.FleetId
                                                        && fleet.EmpireId == empireId.Value))
            .OrderBy(order => order.Status == FleetOrderStatus.Pending ? 0 : 1)
            .ThenBy(order => order.ExecuteAfterTick)
            .ThenByDescending(order => order.CreatedAt)
            .Take(50)
            .ToArray();

    public static IReadOnlyCollection<EventRecord> SelectEvents(
        GameState state,
        Cycle cycle,
        DevelopmentActor actor,
        IReadOnlySet<Guid> visibleSystemIds,
        int limit) =>
        state.Events
            .Where(item => item.CycleId == cycle.CycleId)
            .Where(item => ApiVisibility.CanSeeEvent(item, actor, visibleSystemIds))
            .OrderByDescending(item => item.CreatedAt)
            .Take(Math.Clamp(limit, 1, 100))
            .ToArray();

    public static IReadOnlyCollection<ChronicleEntry> SelectChronicleEntries(
        GameState state,
        Cycle cycle,
        DevelopmentActor actor,
        IReadOnlySet<Guid> visibleSystemIds) =>
        state.ChronicleEntries
            .Where(entry => entry.CycleId == cycle.CycleId)
            .Where(entry => ApiVisibility.CanSeeChronicleEntry(entry, actor, visibleSystemIds))
            .OrderByDescending(entry => entry.ImportanceScore)
            .ToArray();
}
