using System.Text.Json;

namespace Cycles.Core;

public static class ChronicleScoring
{
    public const int ChronicleThreshold = 70;

    public static int ScoreBattle(
        BattleRecord battle,
        GalaxySystem system,
        IReadOnlyCollection<AdmiralBattleHistory>? admiralHistories = null)
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

        if (admiralHistories is { Count: > 0 })
        {
            score += Math.Min(30, admiralHistories.Sum(history => Math.Max(0, history.ReputationChange)) / 2);
            score += admiralHistories.Count(history => history.IsFamousSystemAssociation) * 15;

            if (admiralHistories.Any(history => history.AdmiralStatusAfter == AdmiralStatus.Killed))
            {
                score += 15;
            }

            if (admiralHistories.Any(history => history.AdmiralStatusAfter == AdmiralStatus.Legendary))
            {
                score += 10;
            }
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
        DateTimeOffset now,
        IReadOnlyCollection<Admiral>? admirals = null,
        IReadOnlyCollection<AdmiralBattleHistory>? admiralHistories = null)
    {
        var source = ChronicleBattleNarrativeSource.FromBattle(
            battle,
            sourceEvent,
            system,
            attacker,
            defender,
            importance,
            admirals,
            admiralHistories);
        return CreateBattleEntry(source, now);
    }

    public static ChronicleEntry CreateBattleEntry(ChronicleBattleNarrativeSource source, DateTimeOffset now)
    {
        var title = source.Outcome switch
        {
            BattleOutcome.AttackerVictory => $"The Battle of {source.SystemName}",
            BattleOutcome.DefenderVictory => $"The Defence of {source.SystemName}",
            _ => $"The Ruin at {source.SystemName}"
        };

        var factualSummary =
            $"{source.AttackerEmpireName} and {source.DefenderEmpireName} fought at {source.SystemName} on tick {source.TickNumber}. " +
            $"Outcome: {ChronicleRequiredFactValidator.FormatOutcome(source.Outcome)}. " +
            $"{source.TotalLosses} ships were destroyed.";
        var narrativeText = CreateBattleReport(source);
        ChronicleRequiredFactValidator.ValidateBattleReport(source, narrativeText).ThrowIfInvalid();

        return new ChronicleEntry
        {
            SourceEventId = source.SourceEventId,
            SourceBattleId = source.SourceBattleId,
            CycleId = source.CycleId,
            SystemId = source.SystemId,
            Title = title,
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = source.ImportanceScore,
            FactualSummary = factualSummary,
            NarrativeText = narrativeText,
            NarrativeStatus = NarrativeGenerationStatus.Generated,
            NarrativeContextJson = JsonSerializer.Serialize(source, GameStateJson.Options),
            NarrativeGeneratedAt = now,
            NarrativeFailureReason = null,
            CreatedAt = now
        };
    }

    private static string CreateBattleReport(ChronicleBattleNarrativeSource source)
    {
        var outcomeText = source.Outcome switch
        {
            BattleOutcome.AttackerVictory =>
                $"{source.AttackerEmpireName} carried the attack and forced {source.DefenderEmpireName} back from {source.SystemName}.",
            BattleOutcome.DefenderVictory =>
                $"{source.DefenderEmpireName} held {source.SystemName} against {source.AttackerEmpireName}'s assault.",
            _ =>
                $"{source.AttackerEmpireName} and {source.DefenderEmpireName} shattered each other at {source.SystemName}."
        };

        var requiredFacts =
            $"The tick {source.TickNumber} record names {source.AttackerEmpireName} and {source.DefenderEmpireName}, " +
            $"with {source.AttackerLosses} {source.AttackerEmpireName} losses, {source.DefenderLosses} {source.DefenderEmpireName} losses, " +
            $"{source.TotalLosses} ships destroyed, and a recorded outcome of {ChronicleRequiredFactValidator.FormatOutcome(source.Outcome)}.";

        var admiralText = CreateAdmiralSentence(source);
        var significance = CreateSignificanceSentence(source);
        return $"{outcomeText} {requiredFacts} {admiralText}{significance}";
    }

    private static string CreateAdmiralSentence(ChronicleBattleNarrativeSource source)
    {
        if (source.AdmiralFacts.Count == 0)
        {
            return "";
        }

        var highlight = source.AdmiralFacts
            .OrderByDescending(fact => fact.IsFamousSystemAssociation)
            .ThenByDescending(fact => fact.StatusAfter is AdmiralStatus.Legendary or AdmiralStatus.Killed)
            .ThenByDescending(fact => fact.ReputationChange)
            .First();
        var outcomeText = highlight.Outcome switch
        {
            AdmiralBattleOutcome.Victory => "victory",
            AdmiralBattleOutcome.Defeat => "defeat",
            AdmiralBattleOutcome.MutualDestruction => "mutual destruction",
            _ => highlight.Outcome.ToString()
        };
        var statusText = highlight.StatusAfter switch
        {
            AdmiralStatus.Legendary => " and reached legendary status",
            AdmiralStatus.Killed => " and was killed",
            _ => ""
        };

        return $"{highlight.AdmiralName} entered the Chronicle context with a {outcomeText}, gaining {highlight.ReputationChange} reputation{statusText}. ";
    }

    private static string CreateSignificanceSentence(ChronicleBattleNarrativeSource source)
    {
        if (source.AttackerWasUnderdog && source.Outcome == BattleOutcome.AttackerVictory)
        {
            return $"{source.AttackerEmpireName} entered outnumbered {source.AttackerShipsBefore} to {source.DefenderShipsBefore}, lifting the Chronicle importance to {source.ImportanceScore}.";
        }

        if (source.DefenderWasUnderdog && source.Outcome == BattleOutcome.DefenderVictory)
        {
            return $"{source.DefenderEmpireName} held while outnumbered {source.DefenderShipsBefore} to {source.AttackerShipsBefore}, lifting the Chronicle importance to {source.ImportanceScore}.";
        }

        if (source.TotalLosses >= 100)
        {
            return $"The scale of the losses made {source.SystemName} a major battle site with Chronicle importance {source.ImportanceScore}.";
        }

        if (source.SystemHistoricalSignificance > 0)
        {
            return $"{source.SystemName}'s existing historical significance of {source.SystemHistoricalSignificance} made the battle part of a longer record with Chronicle importance {source.ImportanceScore}.";
        }

        return $"The Chronicle marked it at importance {source.ImportanceScore}, anchored by {source.SystemName}'s strategic value of {source.SystemStrategicValue}.";
    }
}
