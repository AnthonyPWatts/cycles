using System.Text.Json;

namespace Cycles.Core;

public static class AdmiralService
{
    public const int LegendaryReputationThreshold = 100;

    public static IReadOnlyList<AdmiralBattleHistory> ApplyBattleHistory(
        GameState state,
        BattleRecord battle,
        DateTimeOffset now,
        IReadOnlyCollection<AdmiralFleetBattleResult> fleetResults)
    {
        var histories = new List<AdmiralBattleHistory>();
        var system = state.Systems.Single(item => item.SystemId == battle.SystemId);

        foreach (var result in fleetResults)
        {
            var fleet = state.Fleets.Single(item => item.FleetId == result.FleetId);
            if (!fleet.AdmiralId.HasValue)
            {
                continue;
            }

            var admiral = state.Admirals.SingleOrDefault(item => item.AdmiralId == fleet.AdmiralId.Value);
            if (admiral is null || !CanCommand(admiral.Status))
            {
                continue;
            }

            var reputationChange = CalculateReputationChange(battle, result, fleet);
            var isFamousAssociation = IsFamousSystemAssociation(battle, result, fleet);
            admiral.ReputationScore += reputationChange;
            admiral.Status = ResolveStatus(admiral, fleet);
            admiral.UpdatedAt = now;

            var history = new AdmiralBattleHistory
            {
                CycleId = battle.CycleId,
                AdmiralId = admiral.AdmiralId,
                BattleId = battle.BattleId,
                SystemId = battle.SystemId,
                FleetId = fleet.FleetId,
                Role = result.Role,
                Outcome = result.Outcome,
                ShipsCommandedBefore = result.ShipsCommandedBefore,
                ShipsLost = result.ShipsLost,
                ReputationChange = reputationChange,
                ReputationScoreAfter = admiral.ReputationScore,
                AdmiralStatusAfter = admiral.Status,
                IsFamousSystemAssociation = isFamousAssociation,
                CreatedAt = now
            };

            state.AdmiralBattleHistories.Add(history);
            histories.Add(history);
            state.Events.Add(CreateBattleEvent(battle, system, admiral, history, now));
        }

        return histories;
    }

    private static bool CanCommand(AdmiralStatus status) =>
        status is AdmiralStatus.Active or AdmiralStatus.Legendary;

    private static int CalculateReputationChange(BattleRecord battle, AdmiralFleetBattleResult result, Fleet fleet)
    {
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        var change = 5 + Math.Min(20, totalLosses / 10);
        change += result.Outcome switch
        {
            AdmiralBattleOutcome.Victory => 20,
            AdmiralBattleOutcome.Defeat => 8,
            AdmiralBattleOutcome.MutualDestruction => 12,
            _ => 0
        };

        if (WonAsUnderdog(battle, result))
        {
            change += 15;
        }

        if (fleet.Status == FleetStatus.Destroyed && result.ShipsCommandedBefore > 0)
        {
            change += 10;
        }

        return change;
    }

    private static bool IsFamousSystemAssociation(BattleRecord battle, AdmiralFleetBattleResult result, Fleet fleet)
    {
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        return totalLosses >= 50
               || WonAsUnderdog(battle, result)
               || (result.Outcome == AdmiralBattleOutcome.Defeat && fleet.Status == FleetStatus.Destroyed);
    }

    private static bool WonAsUnderdog(BattleRecord battle, AdmiralFleetBattleResult result) =>
        result.Outcome == AdmiralBattleOutcome.Victory
        && (result.Role == AdmiralBattleRole.Attacker
            ? battle.AttackerShipsBefore * 2 < battle.DefenderShipsBefore
            : battle.DefenderShipsBefore * 2 < battle.AttackerShipsBefore);

    private static AdmiralStatus ResolveStatus(Admiral admiral, Fleet fleet)
    {
        if (fleet.Status == FleetStatus.Destroyed)
        {
            return AdmiralStatus.Killed;
        }

        return admiral.ReputationScore >= LegendaryReputationThreshold
            ? AdmiralStatus.Legendary
            : admiral.Status;
    }

    private static EventRecord CreateBattleEvent(
        BattleRecord battle,
        GalaxySystem system,
        Admiral admiral,
        AdmiralBattleHistory history,
        DateTimeOffset now)
    {
        var severity = history.IsFamousSystemAssociation || history.AdmiralStatusAfter is AdmiralStatus.Killed or AdmiralStatus.Legendary
            ? EventSeverity.Historic
            : history.ReputationChange >= 30 ? EventSeverity.High : EventSeverity.Low;

        return new EventRecord
        {
            CycleId = battle.CycleId,
            TickNumber = battle.TickNumber,
            EventType = EventType.AdmiralBattleReported,
            SystemId = battle.SystemId,
            EmpireId = admiral.EmpireId,
            Severity = severity,
            DisplayText = $"{admiral.AdmiralName} gained {history.ReputationChange} reputation after {FormatOutcome(history.Outcome)} at {system.SystemName}.",
            FactJson = JsonSerializer.Serialize(new
            {
                admiralBattleHistoryId = history.AdmiralBattleHistoryId,
                admiral.AdmiralId,
                admiral.AdmiralName,
                battle.BattleId,
                battle.SystemId,
                history.FleetId,
                history.Role,
                history.Outcome,
                history.ShipsCommandedBefore,
                history.ShipsLost,
                history.ReputationChange,
                history.ReputationScoreAfter,
                history.AdmiralStatusAfter,
                history.IsFamousSystemAssociation
            }, GameStateJson.Options),
            CreatedAt = now
        };
    }

    private static string FormatOutcome(AdmiralBattleOutcome outcome) =>
        outcome switch
        {
            AdmiralBattleOutcome.Victory => "a victory",
            AdmiralBattleOutcome.Defeat => "a defeat",
            AdmiralBattleOutcome.MutualDestruction => "mutual destruction",
            _ => outcome.ToString()
        };
}

public sealed record AdmiralFleetBattleResult(
    Guid FleetId,
    AdmiralBattleRole Role,
    AdmiralBattleOutcome Outcome,
    int ShipsCommandedBefore,
    int ShipsLost);
