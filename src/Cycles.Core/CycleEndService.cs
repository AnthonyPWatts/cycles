using System.Text.Json;

namespace Cycles.Core;

public static class CycleEndService
{
    public static IReadOnlyList<CycleRanking> CompleteCycle(
        GameState state,
        Guid cycleId,
        DateTimeOffset cutoffAt)
    {
        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == cycleId)
            ?? throw new InvalidOperationException("Cycle was not found.");

        if (cycle.Status != CycleStatus.Active)
        {
            throw new InvalidOperationException("Only active cycles can be completed.");
        }

        var metrics = EmpireMetricCalculator.CreateTickMetrics(state, cycleId, cycle.CurrentTickNumber, cutoffAt);
        if (metrics.Count == 0)
        {
            throw new InvalidOperationException("Cycle cannot be completed without active empires.");
        }

        var rankings = metrics
            .OrderBy(metric => metric.Rank)
            .Select(metric => new CycleRanking
            {
                CycleId = cycleId,
                EmpireId = metric.EmpireId,
                Rank = metric.Rank,
                IsWinner = metric.IsWinner,
                MapControlPercent = metric.MapControlPercent,
                TotalEffectivePresence = metric.TotalEffectivePresence,
                ActiveShipCount = metric.ActiveShipCount,
                CutoffTickNumber = cycle.CurrentTickNumber,
                CutoffAt = cutoffAt
            })
            .ToArray();

        state.CycleRankings.RemoveAll(ranking => ranking.CycleId == cycleId);
        state.CycleRankings.AddRange(rankings);
        cycle.Status = CycleStatus.Completed;

        var winner = rankings.Single(ranking => ranking.IsWinner);
        var winnerEmpire = state.Empires.Single(empire => empire.EmpireId == winner.EmpireId);
        state.Events.Add(new EventRecord
        {
            CycleId = cycleId,
            TickNumber = cycle.CurrentTickNumber,
            EventType = EventType.CycleCompleted,
            EmpireId = winner.EmpireId,
            Severity = EventSeverity.Historic,
            DisplayText = $"{cycle.Name} ended at tick {cycle.CurrentTickNumber}. {winnerEmpire.EmpireName} won with {winner.MapControlPercent:0.##}% map control.",
            FactJson = JsonSerializer.Serialize(new
            {
                cycleId,
                cutoffTickNumber = cycle.CurrentTickNumber,
                cutoffAt,
                winnerEmpireId = winner.EmpireId,
                rankings = rankings.Select(ranking => new
                {
                    ranking.EmpireId,
                    ranking.Rank,
                    ranking.IsWinner,
                    ranking.MapControlPercent,
                    ranking.TotalEffectivePresence,
                    ranking.ActiveShipCount
                })
            }, GameStateJson.Options),
            CreatedAt = cutoffAt
        });

        return rankings;
    }
}
