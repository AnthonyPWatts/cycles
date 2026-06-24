namespace Cycles.Core;

public static class ChronicleScoring
{
    public const int ChronicleThreshold = 70;

    public static int ScoreBattle(BattleRecord battle, GalaxySystem system)
    {
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        var score = totalLosses;
        score += system.StrategicValue;
        score += system.HistoricalSignificance * 10;

        var attackerWasUnderdog = battle.AttackerShipsBefore * 2 < battle.DefenderShipsBefore;
        var defenderWasUnderdog = battle.DefenderShipsBefore * 2 < battle.AttackerShipsBefore;
        if ((attackerWasUnderdog && battle.Outcome == BattleOutcome.AttackerVictory)
            || (defenderWasUnderdog && battle.Outcome == BattleOutcome.DefenderVictory))
        {
            score += 25;
        }

        if (totalLosses >= 100)
        {
            score += 15;
        }

        return score;
    }

    public static ChronicleEntry CreateBattleEntry(
        BattleRecord battle,
        EventRecord sourceEvent,
        GalaxySystem system,
        Empire attacker,
        Empire defender,
        int importance,
        DateTimeOffset now)
    {
        var title = battle.Outcome switch
        {
            BattleOutcome.AttackerVictory => $"The Battle of {system.SystemName}",
            BattleOutcome.DefenderVictory => $"The Defence of {system.SystemName}",
            _ => $"The Ruin at {system.SystemName}"
        };

        var factualSummary =
            $"{attacker.EmpireName} and {defender.EmpireName} fought at {system.SystemName} on tick {battle.TickNumber}. " +
            $"{battle.AttackerLosses + battle.DefenderLosses} ships were destroyed.";

        return new ChronicleEntry
        {
            SourceEventId = sourceEvent.EventId,
            SourceBattleId = battle.BattleId,
            CycleId = battle.CycleId,
            SystemId = battle.SystemId,
            Title = title,
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = importance,
            FactualSummary = factualSummary,
            NarrativeText = factualSummary,
            CreatedAt = now
        };
    }
}
