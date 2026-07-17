using System.Text.Json;

namespace Cycles.Core;

public static class CycleEndService
{
    private const decimal MajorBattleSelectionShare = 0.10m;

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
        state.SystemHistoricalSignals.RemoveAll(signal => signal.CycleId == cycleId);
        var historicalSignals = ApplyHistoricalSystemSignals(state, cycleId, cutoffAt);
        state.SystemHistoricalSignals.AddRange(historicalSignals);
        var majorEvents = SelectMajorBattleEvents(state, cycleId, cutoffAt);
        state.CycleMajorEvents.RemoveAll(item => item.CycleId == cycleId);
        state.CycleMajorEvents.AddRange(majorEvents);
        MatchControl.CompleteCycle(state, cycleId, cutoffAt);

        var winner = rankings.Single(ranking => ranking.IsWinner);
        var winnerEmpire = state.Empires.Single(empire => empire.EmpireId == winner.EmpireId);
        state.Events.Add(new EventRecord
        {
            CycleId = cycleId,
            TickNumber = cycle.CurrentTickNumber,
            EventType = EventType.CycleCompleted,
            EmpireId = winner.EmpireId,
            FactionId = state.GetEmpireFaction(winner.EmpireId).FactionId,
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
                }),
                historicalSignals = historicalSignals.Select(signal => new
                {
                    signal.SystemHistoricalSignalId,
                    signal.SystemId,
                    signal.SignalType,
                    signal.SourceBattleId,
                    signal.BattleCount,
                    signal.TotalLosses,
                    signal.LargestBattleLosses,
                    signal.HostedCycleLargestBattle,
                    signal.HistoricalSignificanceIncrease,
                    signal.HistoricalSignificanceAfter,
                    signal.Summary
                }),
                majorEvents = majorEvents.Select(item => new
                {
                    item.CycleMajorEventId,
                    item.SourceBattleId,
                    item.SystemId,
                    item.EventType,
                    item.TickNumber,
                    item.SelectionRank,
                    item.ImportanceScore,
                    item.TotalLosses,
                    item.Summary
                })
            }, GameStateJson.Options),
            CreatedAt = cutoffAt
        });

        return rankings;
    }

    private static IReadOnlyList<SystemHistoricalSignal> ApplyHistoricalSystemSignals(
        GameState state,
        Guid cycleId,
        DateTimeOffset createdAt)
    {
        var battles = state.BattleRecords
            .Where(battle => battle.CycleId == cycleId)
            .ToArray();
        if (battles.Length == 0)
        {
            return [];
        }

        var largestLosses = battles.Max(TotalLosses);
        var largestBattleSystemIds = battles
            .Where(battle => TotalLosses(battle) == largestLosses)
            .Select(battle => battle.SystemId)
            .ToHashSet();

        return battles
            .GroupBy(battle => battle.SystemId)
            .OrderBy(group => state.Systems.Single(system => system.SystemId == group.Key).SystemName)
            .Select(group =>
            {
                var system = state.Systems.Single(item => item.CycleId == cycleId && item.SystemId == group.Key);
                var orderedBattles = group
                    .OrderByDescending(TotalLosses)
                    .ThenBy(battle => battle.TickNumber)
                    .ThenBy(battle => battle.BattleId)
                    .ToArray();
                var largestLocalBattle = orderedBattles[0];
                var battleCount = orderedBattles.Length;
                var totalLosses = orderedBattles.Sum(TotalLosses);
                var largestBattleLosses = TotalLosses(largestLocalBattle);
                var hostedLargestBattle = largestBattleSystemIds.Contains(system.SystemId);
                var increase = battleCount + (hostedLargestBattle ? 1 : 0);
                system.HistoricalSignificance += increase;

                return new SystemHistoricalSignal
                {
                    CycleId = cycleId,
                    SystemId = system.SystemId,
                    SignalType = SystemHistoricalSignalType.BattleActivity,
                    SourceBattleId = largestLocalBattle.BattleId,
                    BattleCount = battleCount,
                    TotalLosses = totalLosses,
                    LargestBattleLosses = largestBattleLosses,
                    HostedCycleLargestBattle = hostedLargestBattle,
                    HistoricalSignificanceIncrease = increase,
                    HistoricalSignificanceAfter = system.HistoricalSignificance,
                    Summary = CreateSystemSignalSummary(system, battleCount, totalLosses, increase, hostedLargestBattle),
                    FactJson = JsonSerializer.Serialize(new
                    {
                        cycleId,
                        system.SystemId,
                        battleCount,
                        totalLosses,
                        largestBattleId = largestLocalBattle.BattleId,
                        largestBattleLosses,
                        hostedCycleLargestBattle = hostedLargestBattle,
                        historicalSignificanceIncrease = increase,
                        historicalSignificanceAfter = system.HistoricalSignificance
                    }, GameStateJson.Options),
                    CreatedAt = createdAt
                };
            })
            .ToArray();
    }

    private static int TotalLosses(BattleRecord battle) =>
        battle.AttackerLosses + battle.DefenderLosses;

    private static IReadOnlyList<CycleMajorEvent> SelectMajorBattleEvents(
        GameState state,
        Guid cycleId,
        DateTimeOffset cutoffAt)
    {
        var battles = state.BattleRecords
            .Where(battle => battle.CycleId == cycleId)
            .ToArray();
        if (battles.Length == 0)
        {
            return [];
        }

        var selectedCount = Math.Max(1, (int)Math.Ceiling(battles.Length * MajorBattleSelectionShare));
        return battles
            .OrderByDescending(TotalLosses)
            .ThenBy(battle => battle.TickNumber)
            .ThenBy(battle => battle.BattleId)
            .Take(selectedCount)
            .Select((battle, index) => CreateMajorBattleEvent(state, battle, index + 1, cutoffAt))
            .ToArray();
    }

    private static CycleMajorEvent CreateMajorBattleEvent(
        GameState state,
        BattleRecord battle,
        int selectionRank,
        DateTimeOffset cutoffAt)
    {
        var system = state.Systems.Single(item => item.SystemId == battle.SystemId);
        var attackerFaction = battle.AttackerFactionId != Guid.Empty
            ? state.Factions.Single(item => item.FactionId == battle.AttackerFactionId)
            : state.GetEmpireFaction(battle.AttackerEmpireId);
        var defenderFaction = battle.DefenderFactionId != Guid.Empty
            ? state.Factions.Single(item => item.FactionId == battle.DefenderFactionId)
            : state.GetEmpireFaction(battle.DefenderEmpireId);
        var attacker = attackerFaction.EmpireId.HasValue
            ? state.Empires.Single(item => item.EmpireId == attackerFaction.EmpireId.Value)
            : new Empire { EmpireId = Guid.Empty, EmpireName = attackerFaction.FactionName };
        var defender = defenderFaction.EmpireId.HasValue
            ? state.Empires.Single(item => item.EmpireId == defenderFaction.EmpireId.Value)
            : new Empire { EmpireId = Guid.Empty, EmpireName = defenderFaction.FactionName };
        var admiralHistories = state.AdmiralBattleHistories
            .Where(history => history.BattleId == battle.BattleId)
            .ToArray();
        var totalLosses = TotalLosses(battle);

        return new CycleMajorEvent
        {
            CycleId = battle.CycleId,
            SourceBattleId = battle.BattleId,
            SystemId = battle.SystemId,
            EventType = CycleMajorEventType.Battle,
            TickNumber = battle.TickNumber,
            SelectionRank = selectionRank,
            ImportanceScore = ChronicleScoring.ScoreBattle(battle, system, admiralHistories),
            TotalLosses = totalLosses,
            Summary = CreateBattleSummary(battle, system, attacker, defender, totalLosses),
            FactJson = JsonSerializer.Serialize(new
            {
                battle.BattleId,
                battle.TickNumber,
                battle.SystemId,
                battle.AttackerEmpireId,
                battle.DefenderEmpireId,
                battle.AttackerFactionId,
                battle.DefenderFactionId,
                battle.AttackerShipsBefore,
                battle.DefenderShipsBefore,
                battle.AttackerLosses,
                battle.DefenderLosses,
                battle.Outcome,
                totalLosses
            }, GameStateJson.Options),
            CreatedAt = cutoffAt
        };
    }

    private static string CreateBattleSummary(
        BattleRecord battle,
        GalaxySystem system,
        Empire attacker,
        Empire defender,
        int totalLosses)
    {
        var outcome = battle.Outcome switch
        {
            BattleOutcome.AttackerVictory => $"{attacker.EmpireName} defeated {defender.EmpireName}",
            BattleOutcome.DefenderVictory => $"{defender.EmpireName} held against {attacker.EmpireName}",
            BattleOutcome.MutualDestruction => $"{attacker.EmpireName} and {defender.EmpireName} destroyed each other",
            _ => "The battle was resolved"
        };

        return $"Battle at {system.SystemName}: {outcome} with {totalLosses} ship losses.";
    }

    private static string CreateSystemSignalSummary(
        GalaxySystem system,
        int battleCount,
        int totalLosses,
        int historicalSignificanceIncrease,
        bool hostedCycleLargestBattle)
    {
        var battleText = battleCount == 1 ? "1 battle" : $"{battleCount} battles";
        var largestBattleText = hostedCycleLargestBattle
            ? " including one of the Cycle's largest battles"
            : "";

        return $"{system.SystemName} recorded {battleText}{largestBattleText}, {totalLosses} total ship losses, and gained {historicalSignificanceIncrease} historical significance.";
    }
}
