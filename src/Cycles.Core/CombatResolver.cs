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
        IReadOnlyCollection<Fleet> defenderFleets)
    {
        var attackerShipsBefore = attackerFleet.ShipCount;
        var defenderShipsBefore = defenderFleets.Sum(fleet => fleet.ShipCount);
        var defenderFleetShipsBefore = defenderFleets.ToDictionary(fleet => fleet.FleetId, fleet => fleet.ShipCount);
        var seed = DeterministicSeed(attackerFleet.CycleId, tickNumber, system.SystemId, attackerFleet.FleetId);
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

        var attackerLossesByFleet = ApplyLosses([attackerFleet], attackerLosses);
        var defenderLossesByFleet = ApplyLosses(defenderFleets, defenderLosses);

        var outcome = attackerFleet.Status == FleetStatus.Destroyed && defenderFleets.All(fleet => fleet.Status == FleetStatus.Destroyed)
            ? BattleOutcome.MutualDestruction
            : attackerWins ? BattleOutcome.AttackerVictory : BattleOutcome.DefenderVictory;

        var battle = new BattleRecord
        {
            CycleId = attackerFleet.CycleId,
            TickNumber = tickNumber,
            SystemId = system.SystemId,
            AttackerEmpireId = attackerFleet.EmpireId,
            DefenderEmpireId = defenderFleets.First().EmpireId,
            AttackerFleetIds = attackerFleet.FleetId.ToString(),
            DefenderFleetIds = string.Join(",", defenderFleets.Select(fleet => fleet.FleetId)),
            AttackerShipsBefore = attackerShipsBefore,
            DefenderShipsBefore = defenderShipsBefore,
            AttackerLosses = attackerLosses,
            DefenderLosses = defenderLosses,
            Outcome = outcome,
            CreatedAt = now
        };

        var fleetResults = new[]
            {
                new AdmiralFleetBattleResult(
                    attackerFleet.FleetId,
                    AdmiralBattleRole.Attacker,
                    ToAdmiralOutcome(outcome, AdmiralBattleRole.Attacker),
                    attackerShipsBefore,
                    attackerLossesByFleet[attackerFleet.FleetId])
            }
            .Concat(defenderFleets.Select(fleet => new AdmiralFleetBattleResult(
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
        var shipsBefore = fleets.Sum(fleet => fleet.ShipCount);
        var index = 0;
        var lossesByFleet = new Dictionary<Guid, int>();

        foreach (var fleet in fleets)
        {
            index++;
            var loss = index == fleets.Count
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

    private static int DeterministicSeed(Guid cycleId, int tickNumber, Guid systemId, Guid fleetId)
    {
        unchecked
        {
            var hash = 17;
            foreach (var value in cycleId.ToByteArray().Concat(systemId.ToByteArray()).Concat(fleetId.ToByteArray()))
            {
                hash = (hash * 31) + value;
            }

            return (hash * 31) + tickNumber;
        }
    }
}
