namespace Cycles.Core;

public static class EmpireMetricCalculator
{
    public static IReadOnlyList<EmpireMetric> CreateTickMetrics(
        GameState state,
        Guid cycleId,
        int tickNumber,
        DateTimeOffset now)
    {
        var activeEmpires = state.Empires
            .Where(empire => empire.CycleId == cycleId && empire.Status == EmpireStatus.Active)
            .ToList();

        if (activeEmpires.Count == 0)
        {
            return [];
        }

        var totalSystems = state.Systems.Count(system => system.CycleId == cycleId);
        var controlShares = activeEmpires.ToDictionary(empire => empire.EmpireId, _ => 0m);
        var totalPresence = activeEmpires.ToDictionary(empire => empire.EmpireId, _ => 0m);

        foreach (var system in state.Systems.Where(system => system.CycleId == cycleId))
        {
            var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycleId, system.SystemId);
            if (presence.Count == 0)
            {
                continue;
            }

            var systemTotalPresence = presence.Values.Sum();
            foreach (var (factionId, effectivePresence) in presence)
            {
                var empireId = state.GetEmpireIdForFaction(factionId);
                if (!empireId.HasValue)
                {
                    continue;
                }

                if (!controlShares.ContainsKey(empireId.Value))
                {
                    continue;
                }

                controlShares[empireId.Value] += effectivePresence / systemTotalPresence;
                totalPresence[empireId.Value] += effectivePresence;
            }
        }

        var activeShipCounts = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.Status == FleetStatus.Active
                            && fleet.ShipCount > 0)
            .GroupBy(fleet => fleet.FactionId)
            .Select(group => new
            {
                EmpireId = state.GetEmpireIdForFaction(group.Key),
                ShipCount = group.Sum(fleet => fleet.ShipCount)
            })
            .Where(item => item.EmpireId.HasValue)
            .ToDictionary(item => item.EmpireId!.Value, item => item.ShipCount);

        var standings = activeEmpires
            .Select(empire => new
            {
                empire.EmpireId,
                MapControlPercent = totalSystems == 0
                    ? 0m
                    : controlShares[empire.EmpireId] / totalSystems * 100m,
                TotalEffectivePresence = totalPresence[empire.EmpireId],
                ActiveShipCount = activeShipCounts.GetValueOrDefault(empire.EmpireId)
            })
            .OrderByDescending(metric => metric.MapControlPercent)
            .ThenByDescending(metric => metric.TotalEffectivePresence)
            .ThenByDescending(metric => metric.ActiveShipCount)
            .ThenBy(metric => metric.EmpireId)
            .ToList();

        return standings
            .Select((metric, index) => new EmpireMetric
            {
                CycleId = cycleId,
                EmpireId = metric.EmpireId,
                TickNumber = tickNumber,
                Rank = index + 1,
                IsWinner = index == 0,
                MapControlPercent = decimal.Round(metric.MapControlPercent, 6),
                TotalEffectivePresence = decimal.Round(metric.TotalEffectivePresence, 2),
                ActiveShipCount = metric.ActiveShipCount,
                CreatedAt = now
            })
            .ToList();
    }
}
