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
            $"Outcome: {FormatOutcome(battle.Outcome)}. " +
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
            NarrativeText = CreateBattleReport(battle, system, attacker, defender, importance),
            CreatedAt = now
        };
    }

    private static string CreateBattleReport(
        BattleRecord battle,
        GalaxySystem system,
        Empire attacker,
        Empire defender,
        int importance)
    {
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        var outcomeText = battle.Outcome switch
        {
            BattleOutcome.AttackerVictory =>
                $"{attacker.EmpireName} carried the attack and forced {defender.EmpireName} back from {system.SystemName}.",
            BattleOutcome.DefenderVictory =>
                $"{defender.EmpireName} held {system.SystemName} against {attacker.EmpireName}'s assault.",
            _ =>
                $"{attacker.EmpireName} and {defender.EmpireName} shattered each other at {system.SystemName}."
        };

        var requiredFacts =
            $"The tick {battle.TickNumber} record names {attacker.EmpireName} and {defender.EmpireName}, " +
            $"with {battle.AttackerLosses} {attacker.EmpireName} losses, {battle.DefenderLosses} {defender.EmpireName} losses, " +
            $"and {totalLosses} ships destroyed.";

        var significance = CreateSignificanceSentence(battle, system, attacker, defender, importance, totalLosses);
        return $"{outcomeText} {requiredFacts} {significance}";
    }

    private static string CreateSignificanceSentence(
        BattleRecord battle,
        GalaxySystem system,
        Empire attacker,
        Empire defender,
        int importance,
        int totalLosses)
    {
        var attackerWasUnderdog = battle.AttackerShipsBefore * 2 < battle.DefenderShipsBefore;
        var defenderWasUnderdog = battle.DefenderShipsBefore * 2 < battle.AttackerShipsBefore;
        if (attackerWasUnderdog && battle.Outcome == BattleOutcome.AttackerVictory)
        {
            return $"{attacker.EmpireName} entered outnumbered {battle.AttackerShipsBefore} to {battle.DefenderShipsBefore}, lifting the Chronicle importance to {importance}.";
        }

        if (defenderWasUnderdog && battle.Outcome == BattleOutcome.DefenderVictory)
        {
            return $"{defender.EmpireName} held while outnumbered {battle.DefenderShipsBefore} to {battle.AttackerShipsBefore}, lifting the Chronicle importance to {importance}.";
        }

        if (totalLosses >= 100)
        {
            return $"The scale of the losses made {system.SystemName} a major battle site with Chronicle importance {importance}.";
        }

        if (system.HistoricalSignificance > 0)
        {
            return $"{system.SystemName}'s existing historical significance of {system.HistoricalSignificance} made the battle part of a longer record.";
        }

        return $"The Chronicle marked it at importance {importance}, anchored by {system.SystemName}'s strategic value of {system.StrategicValue}.";
    }

    private static string FormatOutcome(BattleOutcome outcome) =>
        outcome switch
        {
            BattleOutcome.AttackerVictory => "attacker victory",
            BattleOutcome.DefenderVictory => "defender victory",
            _ => "mutual destruction"
        };
}
