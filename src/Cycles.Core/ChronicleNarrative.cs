namespace Cycles.Core;

public sealed record ChronicleBattleNarrativeSource(
    Guid SourceEventId,
    Guid SourceBattleId,
    Guid CycleId,
    Guid SystemId,
    string SystemName,
    int SystemStrategicValue,
    int SystemHistoricalSignificance,
    Guid AttackerEmpireId,
    string AttackerEmpireName,
    Guid DefenderEmpireId,
    string DefenderEmpireName,
    int TickNumber,
    int AttackerShipsBefore,
    int DefenderShipsBefore,
    int AttackerLosses,
    int DefenderLosses,
    BattleOutcome Outcome,
    int ImportanceScore)
{
    public int TotalLosses => AttackerLosses + DefenderLosses;

    public bool AttackerWasUnderdog => AttackerShipsBefore * 2 < DefenderShipsBefore;

    public bool DefenderWasUnderdog => DefenderShipsBefore * 2 < AttackerShipsBefore;

    public static ChronicleBattleNarrativeSource FromBattle(
        BattleRecord battle,
        EventRecord sourceEvent,
        GalaxySystem system,
        Empire attacker,
        Empire defender,
        int importance) =>
        new(
            sourceEvent.EventId,
            battle.BattleId,
            battle.CycleId,
            battle.SystemId,
            system.SystemName,
            system.StrategicValue,
            system.HistoricalSignificance,
            attacker.EmpireId,
            attacker.EmpireName,
            defender.EmpireId,
            defender.EmpireName,
            battle.TickNumber,
            battle.AttackerShipsBefore,
            battle.DefenderShipsBefore,
            battle.AttackerLosses,
            battle.DefenderLosses,
            battle.Outcome,
            importance);
}

public static class ChronicleRequiredFactValidator
{
    public static ChronicleFactValidationResult ValidateBattleReport(ChronicleBattleNarrativeSource source, string generatedText)
    {
        var missingFacts = new List<string>();

        RequireContains(generatedText, source.AttackerEmpireName, "attacker empire", missingFacts);
        RequireContains(generatedText, source.DefenderEmpireName, "defender empire", missingFacts);
        RequireContains(generatedText, source.SystemName, "system", missingFacts);
        RequireContains(generatedText, $"tick {source.TickNumber}", "tick number", missingFacts);
        RequireContains(generatedText, source.AttackerLosses.ToString(), "attacker losses", missingFacts);
        RequireContains(generatedText, source.DefenderLosses.ToString(), "defender losses", missingFacts);
        RequireContains(generatedText, $"{source.TotalLosses} ships", "total losses", missingFacts);
        RequireContains(generatedText, source.ImportanceScore.ToString(), "importance score", missingFacts);
        RequireContains(generatedText, FormatOutcome(source.Outcome), "battle outcome", missingFacts);

        return new ChronicleFactValidationResult(missingFacts);
    }

    public static string FormatOutcome(BattleOutcome outcome) =>
        outcome switch
        {
            BattleOutcome.AttackerVictory => "attacker victory",
            BattleOutcome.DefenderVictory => "defender victory",
            _ => "mutual destruction"
        };

    private static void RequireContains(string text, string expected, string label, List<string> missingFacts)
    {
        if (text.Contains(expected, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        missingFacts.Add(label);
    }
}

public sealed record ChronicleFactValidationResult(IReadOnlyList<string> MissingFacts)
{
    public bool IsValid => MissingFacts.Count == 0;

    public void ThrowIfInvalid()
    {
        if (IsValid)
        {
            return;
        }

        throw new InvalidOperationException($"Generated Chronicle battle report missed required fact(s): {string.Join(", ", MissingFacts)}.");
    }
}
