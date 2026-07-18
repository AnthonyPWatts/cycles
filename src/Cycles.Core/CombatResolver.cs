using System.Text.Json;

namespace Cycles.Core;

public static class CombatResolver
{
    public static BattleRecord Resolve(
        GameState state,
        int tickNumber,
        DateTimeOffset now,
        GalaxySystem system,
        Fleet attackerFleet,
        IReadOnlyCollection<Fleet> defenderFleets) =>
        Resolve(state, tickNumber, now, system, [attackerFleet], defenderFleets);

    public static BattleRecord Resolve(
        GameState state,
        int tickNumber,
        DateTimeOffset now,
        GalaxySystem system,
        IReadOnlyCollection<Fleet> attackerFleets,
        IReadOnlyCollection<Fleet> defenderFleets)
    {
        if (attackerFleets.Count == 0 || defenderFleets.Count == 0)
        {
            throw new ArgumentException("Combat requires at least one fleet on each side.");
        }

        var orderedAttackers = attackerFleets.OrderBy(fleet => fleet.FleetId).ToArray();
        var orderedDefenders = defenderFleets.OrderBy(fleet => fleet.FleetId).ToArray();
        var attackerShipsBefore = orderedAttackers.Sum(fleet => fleet.ShipCount);
        var defenderShipsBefore = defenderFleets.Sum(fleet => fleet.ShipCount);
        var attackerFleetShipsBefore = orderedAttackers.ToDictionary(fleet => fleet.FleetId, fleet => fleet.ShipCount);
        var defenderFleetShipsBefore = orderedDefenders.ToDictionary(fleet => fleet.FleetId, fleet => fleet.ShipCount);
        var seed = DeterministicSeed(orderedAttackers[0].CycleId, tickNumber, system.SystemId, orderedAttackers);
        var random = new Random(seed);

        var attackerWinProbability = attackerShipsBefore / (double)(attackerShipsBefore + defenderShipsBefore);
        var attackerWins = random.NextDouble() < attackerWinProbability;
        var loserLossRate = 0.55m + (random.Next(0, 26) / 100m);
        var winnerLossRate = 0.10m + (random.Next(0, 21) / 100m);

        var attackerLosses = attackerWins
            ? CalculateLosses(attackerShipsBefore, winnerLossRate)
            : CalculateLosses(attackerShipsBefore, loserLossRate);
        var defenderLosses = attackerWins
            ? CalculateLosses(defenderShipsBefore, loserLossRate)
            : CalculateLosses(defenderShipsBefore, winnerLossRate);

        var attackerLossesByFleet = ApplyLosses(orderedAttackers, attackerLosses);
        var defenderLossesByFleet = ApplyLosses(orderedDefenders, defenderLosses);

        var outcome = orderedAttackers.All(fleet => fleet.Status == FleetStatus.Destroyed)
                      && orderedDefenders.All(fleet => fleet.Status == FleetStatus.Destroyed)
            ? BattleOutcome.MutualDestruction
            : attackerWins ? BattleOutcome.AttackerVictory : BattleOutcome.DefenderVictory;
        var attackerFaction = state.Factions.Single(item => item.FactionId == state.GetFactionId(orderedAttackers[0]));
        var defenderFaction = state.Factions.Single(item => item.FactionId == state.GetFactionId(orderedDefenders[0]));

        var battle = new BattleRecord
        {
            CycleId = orderedAttackers[0].CycleId,
            TickNumber = tickNumber,
            SystemId = system.SystemId,
            AttackerEmpireId = attackerFaction.EmpireId ?? Guid.Empty,
            DefenderEmpireId = defenderFaction.EmpireId ?? Guid.Empty,
            AttackerFactionId = attackerFaction.FactionId,
            DefenderFactionId = defenderFaction.FactionId,
            AttackerFleetIds = string.Join(",", orderedAttackers.Select(fleet => fleet.FleetId)),
            DefenderFleetIds = string.Join(",", orderedDefenders.Select(fleet => fleet.FleetId)),
            AttackerShipsBefore = attackerShipsBefore,
            DefenderShipsBefore = defenderShipsBefore,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            Outcome = outcome,
            CreatedAt = now
        };

        var fleetResults = orderedAttackers
            .Select(fleet => new AdmiralFleetBattleResult(
                fleet.FleetId,
                AdmiralBattleRole.Attacker,
                ToAdmiralOutcome(outcome, AdmiralBattleRole.Attacker),
                attackerFleetShipsBefore[fleet.FleetId],
                attackerLossesByFleet[fleet.FleetId]))
            .Concat(orderedDefenders.Select(fleet => new AdmiralFleetBattleResult(
                fleet.FleetId,
                AdmiralBattleRole.Defender,
                ToAdmiralOutcome(outcome, AdmiralBattleRole.Defender),
                defenderFleetShipsBefore[fleet.FleetId],
                defenderLossesByFleet[fleet.FleetId])))
            .ToArray();

        var admiralHistories = AdmiralService.ApplyBattleHistory(state, battle, now, fleetResults);

        battle.FactJson = JsonSerializer.Serialize(new
        {
            battleId = battle.BattleId,
            battle.CycleId,
            battle.TickNumber,
            battle.SystemId,
            battle.AttackerEmpireId,
            battle.DefenderEmpireId,
            battle.AttackerFactionId,
            battle.DefenderFactionId,
            attackerShipsBefore,
            defenderShipsBefore,
            attackerLosses,
            defenderLosses,
            outcome,
            fleetResults,
            admiralBattleHistoryIds = admiralHistories.Select(history => history.AdmiralBattleHistoryId)
        }, GameStateJson.Options);

        state.BattleRecords.Add(battle);
        return battle;
    }

    private static int CalculateLosses(int shipsBefore, decimal lossRate) =>
        Math.Min(shipsBefore, Math.Max(1, (int)Math.Round(shipsBefore * lossRate, MidpointRounding.AwayFromZero)));

    private static Dictionary<Guid, int> ApplyLosses(IReadOnlyCollection<Fleet> fleets, int totalLosses)
    {
        var remainingLosses = totalLosses;
        var orderedFleets = fleets.OrderBy(fleet => fleet.FleetId).ToArray();
        var shipsBefore = orderedFleets.Sum(fleet => fleet.ShipCount);
        var index = 0;
        var lossesByFleet = new Dictionary<Guid, int>();

        foreach (var fleet in orderedFleets)
        {
            index++;
            var loss = index == orderedFleets.Length
                ? remainingLosses
                : Math.Min(fleet.ShipCount, (int)Math.Round(totalLosses * (fleet.ShipCount / (double)shipsBefore), MidpointRounding.AwayFromZero));

            remainingLosses -= loss;
            lossesByFleet[fleet.FleetId] = loss;
            fleet.ShipCount = Math.Max(0, fleet.ShipCount - loss);
            if (fleet.ShipCount == 0)
            {
                fleet.Status = FleetStatus.Destroyed;
            }
        }

        return lossesByFleet;
    }

    private static AdmiralBattleOutcome ToAdmiralOutcome(BattleOutcome battleOutcome, AdmiralBattleRole role) =>
        battleOutcome switch
        {
            BattleOutcome.MutualDestruction => AdmiralBattleOutcome.MutualDestruction,
            BattleOutcome.AttackerVictory => role == AdmiralBattleRole.Attacker ? AdmiralBattleOutcome.Victory : AdmiralBattleOutcome.Defeat,
            BattleOutcome.DefenderVictory => role == AdmiralBattleRole.Defender ? AdmiralBattleOutcome.Victory : AdmiralBattleOutcome.Defeat,
            _ => AdmiralBattleOutcome.Defeat
        };

    private static int DeterministicSeed(
        Guid cycleId,
        int tickNumber,
        Guid systemId,
        IReadOnlyCollection<Fleet> attackerFleets)
    {
        unchecked
        {
            var hash = 17;
            var identifierBytes = cycleId.ToByteArray()
                .Concat(systemId.ToByteArray())
                .Concat(attackerFleets.OrderBy(fleet => fleet.FleetId).SelectMany(fleet => fleet.FleetId.ToByteArray()));
            foreach (var value in identifierBytes)
            {
                hash = (hash * 31) + value;
            }

            return (hash * 31) + tickNumber;
        }
    }
}
