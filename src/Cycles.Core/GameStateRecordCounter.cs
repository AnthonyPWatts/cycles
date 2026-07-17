namespace Cycles.Core;

public static class GameStateRecordCounter
{
    public static int CountCycleRecords(GameState state, Guid cycleId)
    {
        var empireIds = state.Empires
            .Where(item => item.CycleId == cycleId)
            .Select(item => item.EmpireId)
            .ToHashSet();
        var playerIds = state.Empires
            .Where(item => item.CycleId == cycleId)
            .Select(item => item.PlayerId)
            .ToHashSet();

        return state.Players.Count(item => playerIds.Contains(item.PlayerId))
               + state.AdminRoleAuditRecords.Count(item => playerIds.Contains(item.TargetPlayerId))
               + state.Cycles.Count(item => item.CycleId == cycleId)
               + state.Sectors.Count(item => item.CycleId == cycleId)
               + state.Systems.Count(item => item.CycleId == cycleId)
               + state.SystemLinks.Count(item => item.CycleId == cycleId)
               + state.Empires.Count(item => item.CycleId == cycleId)
               + state.Factions.Count(item => item.CycleId == cycleId)
               + state.MatchParticipants.Count(item => item.CycleId == cycleId)
               + state.EmpireResources.Count(item => empireIds.Contains(item.EmpireId))
               + state.EmpirePriorities.Count(item => empireIds.Contains(item.EmpireId))
               + state.EmpireMetrics.Count(item => item.CycleId == cycleId)
               + state.CycleRankings.Count(item => item.CycleId == cycleId)
               + state.CycleMajorEvents.Count(item => item.CycleId == cycleId)
               + state.SystemHistoricalSignals.Count(item => item.CycleId == cycleId)
               + state.Admirals.Count(item => item.CycleId == cycleId)
               + state.AdmiralBattleHistories.Count(item => item.CycleId == cycleId)
               + state.Fleets.Count(item => item.CycleId == cycleId)
               + state.FleetOrders.Count(item => item.CycleId == cycleId)
               + state.ShipConstructions.Count(item => item.CycleId == cycleId)
               + state.ColonialOutposts.Count(item => item.CycleId == cycleId)
               + state.DiplomaticRelationships.Count(item => item.CycleId == cycleId)
               + state.TickLogs.Count(item => item.CycleId == cycleId)
               + state.Events.Count(item => item.CycleId == cycleId)
               + state.BattleRecords.Count(item => item.CycleId == cycleId)
               + state.ChronicleEntries.Count(item => item.CycleId == cycleId);
    }
}
