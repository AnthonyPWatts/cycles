using Cycles.Core;

public static class ApiVisibility
{
    public static HashSet<Guid> GetVisibleSystemIds(GameState state, Cycle cycle, DevelopmentActor actor)
    {
        if (actor.IsAdmin)
        {
            return state.Systems
                .Where(system => system.CycleId == cycle.CycleId)
                .Select(system => system.SystemId)
                .ToHashSet();
        }

        var empireId = actor.Empire?.EmpireId
            ?? throw new ApiForbiddenException("The authenticated player has no empire in the active cycle.");

        return state.Fleets
            .Where(fleet => fleet.CycleId == cycle.CycleId
                            && fleet.EmpireId == empireId
                            && fleet.Status == FleetStatus.Active
                            && fleet.ShipCount > 0)
            .Select(fleet => fleet.CurrentSystemId)
            .ToHashSet();
    }

    public static bool CanSeeSystemDetails(DevelopmentActor actor, IReadOnlySet<Guid> visibleSystemIds, Guid systemId) =>
        actor.IsAdmin || visibleSystemIds.Contains(systemId);

    public static IReadOnlyDictionary<Guid, decimal> FilterPresence(
        DevelopmentActor actor,
        IReadOnlySet<Guid> visibleSystemIds,
        Guid systemId,
        IReadOnlyDictionary<Guid, decimal> effectivePresence) =>
        CanSeeSystemDetails(actor, visibleSystemIds, systemId)
            ? effectivePresence
            : new Dictionary<Guid, decimal>();

    public static bool CanSeeEvent(EventRecord item, DevelopmentActor actor, IReadOnlySet<Guid> visibleSystemIds)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (actor.Empire is not null && item.EmpireId == actor.Empire.EmpireId)
        {
            return true;
        }

        return item.SystemId.HasValue && visibleSystemIds.Contains(item.SystemId.Value);
    }

    public static bool CanSeeBattle(BattleRecord item, DevelopmentActor actor, IReadOnlySet<Guid> visibleSystemIds)
    {
        if (actor.IsAdmin)
        {
            return true;
        }

        if (actor.Empire is not null
            && (item.AttackerEmpireId == actor.Empire.EmpireId || item.DefenderEmpireId == actor.Empire.EmpireId))
        {
            return true;
        }

        return visibleSystemIds.Contains(item.SystemId);
    }

    public static bool CanSeeChronicleEntry(ChronicleEntry item, DevelopmentActor actor, IReadOnlySet<Guid> visibleSystemIds) =>
        actor.IsAdmin || !item.SystemId.HasValue || visibleSystemIds.Contains(item.SystemId.Value);
}
