using System.Text.Json;

namespace Cycles.Core;

public sealed class TickEngine
{
    public TickResult RunTick(GameState state, Guid cycleId, DateTimeOffset now)
    {
        if (state.TickLogs.Any(log => log.CycleId == cycleId && log.Status == TickLogStatus.Running))
        {
            throw new InvalidOperationException("A tick is already running for this cycle.");
        }

        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == cycleId)
            ?? throw new InvalidOperationException("Cycle was not found.");

        if (cycle.Status != CycleStatus.Active)
        {
            throw new InvalidOperationException("Only active cycles can process ticks.");
        }

        var nextTick = cycle.CurrentTickNumber + 1;
        var working = state.DeepClone();

        try
        {
            var result = ProcessTick(working, cycleId, nextTick, now);
            state.ReplaceWith(working);
            return result;
        }
        catch (Exception ex)
        {
            state.TickLogs.Add(new TickLog
            {
                CycleId = cycleId,
                TickNumber = nextTick,
                StartedAt = now,
                CompletedAt = DateTimeOffset.UtcNow,
                Status = TickLogStatus.Failed,
                DiagnosticLog = ex.ToString()
            });

            cycle.Status = CycleStatus.RecoveryRequired;
            return new TickResult(nextTick, TickLogStatus.Failed, 0, 0, 0, 0);
        }
    }

    private static TickResult ProcessTick(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var cycle = state.Cycles.Single(item => item.CycleId == cycleId);
        var eventsBefore = state.Events.Count;
        var battlesBefore = state.BattleRecords.Count;
        var chroniclesBefore = state.ChronicleEntries.Count;
        var processedOrders = 0;

        var log = new TickLog
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            StartedAt = now,
            Status = TickLogStatus.Running
        };
        state.TickLogs.Add(log);

        ProcessArrivals(state, cycleId, tickNumber, now);
        InfluenceCalculator.GenerateResources(state, cycleId, tickNumber, now);

        var dueOrders = state.FleetOrders
            .Where(order => order.CycleId == cycleId
                            && order.Status == FleetOrderStatus.Pending
                            && order.ExecuteAfterTick <= tickNumber)
            .OrderBy(order => order.SubmitTick)
            .ThenBy(order => order.CreatedAt)
            .ToList();

        foreach (var order in dueOrders)
        {
            ProcessOrder(state, order, tickNumber, now);
            processedOrders++;
        }

        cycle.CurrentTickNumber = tickNumber;
        log.Status = TickLogStatus.Completed;
        log.CompletedAt = now;
        log.DiagnosticLog = $"Processed {processedOrders} order(s).";

        return new TickResult(
            tickNumber,
            TickLogStatus.Completed,
            processedOrders,
            state.Events.Count - eventsBefore,
            state.BattleRecords.Count - battlesBefore,
            state.ChronicleEntries.Count - chroniclesBefore);
    }

    private static void ProcessArrivals(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var arrivingFleets = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.Status == FleetStatus.InTransit
                            && fleet.ArrivalTickNumber <= tickNumber
                            && fleet.DestinationSystemId.HasValue)
            .ToList();

        foreach (var fleet in arrivingFleets)
        {
            fleet.CurrentSystemId = fleet.DestinationSystemId!.Value;
            fleet.DestinationSystemId = null;
            fleet.ArrivalTickNumber = null;
            fleet.Status = FleetStatus.Active;

            var destination = state.Systems.Single(system => system.SystemId == fleet.CurrentSystemId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.FleetArrived,
                SystemId = destination.SystemId,
                EmpireId = fleet.EmpireId,
                Severity = EventSeverity.Normal,
                DisplayText = $"{fleet.FleetName} arrived at {destination.SystemName}.",
                FactJson = JsonSerializer.Serialize(new
                {
                    fleetId = fleet.FleetId,
                    systemId = destination.SystemId,
                    tickNumber
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    private static void ProcessOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        switch (order.OrderType)
        {
            case FleetOrderType.MoveFleet:
                ProcessMoveOrder(state, order, tickNumber, now);
                break;
            case FleetOrderType.Hold:
                ProcessHoldOrder(state, order, tickNumber, now);
                break;
            case FleetOrderType.Attack:
                ProcessAttackOrder(state, order, tickNumber, now);
                break;
            default:
                RejectOrder(state, order, tickNumber, now, "Unsupported order type.");
                break;
        }
    }

    private static void ProcessMoveOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        var fleet = TryGetActiveFleet(state, order.FleetId);
        if (fleet is null)
        {
            RejectOrder(state, order, tickNumber, now, "Fleet is not active.");
            return;
        }

        if (!order.TargetSystemId.HasValue)
        {
            RejectOrder(state, order, tickNumber, now, "Move orders require a target system.");
            return;
        }

        var target = state.Systems.SingleOrDefault(system => system.SystemId == order.TargetSystemId.Value);
        if (target is null)
        {
            RejectOrder(state, order, tickNumber, now, "Target system does not exist.");
            return;
        }

        var link = state.SystemLinks.SingleOrDefault(systemLink => systemLink.CycleId == order.CycleId
                                                                  && systemLink.Connects(fleet.CurrentSystemId, target.SystemId));
        if (link is null)
        {
            RejectOrder(state, order, tickNumber, now, "Fleet can only move along linked systems.");
            return;
        }

        var origin = state.Systems.Single(system => system.SystemId == fleet.CurrentSystemId);
        if (link.TravelTicks <= 1)
        {
            fleet.CurrentSystemId = target.SystemId;
            fleet.Status = FleetStatus.Active;
            fleet.DestinationSystemId = null;
            fleet.ArrivalTickNumber = null;
        }
        else
        {
            fleet.Status = FleetStatus.InTransit;
            fleet.DestinationSystemId = target.SystemId;
            fleet.ArrivalTickNumber = tickNumber + link.TravelTicks - 1;
        }

        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = tickNumber;

        var inTransitText = fleet.Status == FleetStatus.InTransit
            ? $" and will arrive on tick {fleet.ArrivalTickNumber}"
            : "";

        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.FleetMoved,
            SystemId = target.SystemId,
            EmpireId = fleet.EmpireId,
            Severity = EventSeverity.Normal,
            DisplayText = $"{fleet.FleetName} moved from {origin.SystemName} to {target.SystemName}{inTransitText}.",
            FactJson = JsonSerializer.Serialize(new
            {
                fleetId = fleet.FleetId,
                originSystemId = origin.SystemId,
                targetSystemId = target.SystemId,
                travelTicks = link.TravelTicks,
                arrivalTick = fleet.ArrivalTickNumber
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static void ProcessHoldOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        var fleet = TryGetActiveFleet(state, order.FleetId);
        if (fleet is null)
        {
            RejectOrder(state, order, tickNumber, now, "Fleet is not active.");
            return;
        }

        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = tickNumber;

        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.FleetHeld,
            SystemId = system.SystemId,
            EmpireId = fleet.EmpireId,
            Severity = EventSeverity.Low,
            DisplayText = $"{fleet.FleetName} held position at {system.SystemName}.",
            FactJson = JsonSerializer.Serialize(new
            {
                fleetId = fleet.FleetId,
                systemId = system.SystemId
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static void ProcessAttackOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        var attackerFleet = TryGetActiveFleet(state, order.FleetId);
        if (attackerFleet is null)
        {
            RejectOrder(state, order, tickNumber, now, "Attacking fleet is not active.");
            return;
        }

        var defenderFleets = state.Fleets
            .Where(fleet => fleet.CycleId == order.CycleId
                            && fleet.Status == FleetStatus.Active
                            && fleet.CurrentSystemId == attackerFleet.CurrentSystemId
                            && fleet.EmpireId != attackerFleet.EmpireId
                            && (!order.TargetEmpireId.HasValue || fleet.EmpireId == order.TargetEmpireId.Value))
            .ToList();

        if (defenderFleets.Count == 0)
        {
            RejectOrder(state, order, tickNumber, now, "No hostile active fleet is present in the system.");
            return;
        }

        var defenderEmpireId = order.TargetEmpireId
            ?? defenderFleets
                .GroupBy(fleet => fleet.EmpireId)
                .OrderByDescending(group => group.Sum(fleet => fleet.ShipCount))
                .First()
                .Key;

        defenderFleets = defenderFleets.Where(fleet => fleet.EmpireId == defenderEmpireId).ToList();
        var system = state.Systems.Single(item => item.SystemId == attackerFleet.CurrentSystemId);
        var battle = CombatResolver.Resolve(state, tickNumber, now, system, attackerFleet, defenderFleets);

        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = tickNumber;

        var attacker = state.Empires.Single(empire => empire.EmpireId == battle.AttackerEmpireId);
        var defender = state.Empires.Single(empire => empire.EmpireId == battle.DefenderEmpireId);
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        var severity = totalLosses >= 80 ? EventSeverity.Historic : totalLosses >= 30 ? EventSeverity.High : EventSeverity.Normal;
        var outcomeText = battle.Outcome == BattleOutcome.AttackerVictory
            ? $"{attacker.EmpireName} forced {defender.EmpireName} back"
            : battle.Outcome == BattleOutcome.DefenderVictory
                ? $"{defender.EmpireName} held against {attacker.EmpireName}"
                : $"{attacker.EmpireName} and {defender.EmpireName} shattered each other";

        var battleEvent = new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.CombatResolved,
            SystemId = system.SystemId,
            EmpireId = attacker.EmpireId,
            Severity = severity,
            DisplayText = $"{outcomeText} at {system.SystemName}; {totalLosses} ships were destroyed.",
            FactJson = battle.FactJson,
            CreatedAt = now
        };
        state.Events.Add(battleEvent);

        var importance = ChronicleScoring.ScoreBattle(battle, system);
        if (importance >= ChronicleScoring.ChronicleThreshold)
        {
            var chronicle = ChronicleScoring.CreateBattleEntry(battle, battleEvent, system, attacker, defender, importance, now);
            state.ChronicleEntries.Add(chronicle);
            state.Events.Add(new EventRecord
            {
                CycleId = order.CycleId,
                TickNumber = tickNumber,
                EventType = EventType.ChronicleCreated,
                SystemId = system.SystemId,
                Severity = EventSeverity.Historic,
                DisplayText = $"The Chronicle preserved '{chronicle.Title}'.",
                FactJson = JsonSerializer.Serialize(new
                {
                    chronicleEntryId = chronicle.ChronicleEntryId,
                    sourceBattleId = battle.BattleId,
                    importance
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    private static Fleet? TryGetActiveFleet(GameState state, Guid fleetId) =>
        state.Fleets.SingleOrDefault(fleet => fleet.FleetId == fleetId
                                             && fleet.Status == FleetStatus.Active
                                             && fleet.ShipCount > 0);

    private static void RejectOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now, string reason)
    {
        order.Status = FleetOrderStatus.Rejected;
        order.ProcessedTick = tickNumber;
        order.RejectionReason = reason;

        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.OrderRejected,
            EmpireId = state.Fleets.SingleOrDefault(fleet => fleet.FleetId == order.FleetId)?.EmpireId,
            Severity = EventSeverity.Low,
            DisplayText = $"Order {order.FleetOrderId} was rejected: {reason}",
            FactJson = JsonSerializer.Serialize(new
            {
                orderId = order.FleetOrderId,
                orderType = order.OrderType,
                reason
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }
}

public static class InfluenceCalculator
{
    public const int HomeSystemMinimumPresence = 10;

    public static IReadOnlyDictionary<Guid, decimal> CalculateEffectivePresence(
        GameState state,
        Guid cycleId,
        Guid systemId)
    {
        var presence = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.Status == FleetStatus.Active
                            && fleet.CurrentSystemId == systemId
                            && fleet.ShipCount > 0)
            .GroupBy(fleet => fleet.EmpireId)
            .ToDictionary(group => group.Key, group => (decimal)group.Sum(fleet => fleet.ShipCount));

        var homeEmpire = state.Empires.SingleOrDefault(empire => empire.CycleId == cycleId && empire.HomeSystemId == systemId);
        if (homeEmpire is not null)
        {
            presence[homeEmpire.EmpireId] = presence.TryGetValue(homeEmpire.EmpireId, out var currentPresence)
                ? Math.Max(currentPresence, HomeSystemMinimumPresence)
                : HomeSystemMinimumPresence;
        }

        return presence;
    }

    public static void GenerateResources(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var generatedByEmpire = new Dictionary<Guid, ResourceDelta>();

        foreach (var system in state.Systems.Where(system => system.CycleId == cycleId))
        {
            var presence = CalculateEffectivePresence(state, cycleId, system.SystemId);
            if (presence.Count == 0)
            {
                continue;
            }

            var totalPresence = presence.Values.Sum();
            foreach (var (empireId, effectivePresence) in presence)
            {
                var share = effectivePresence / totalPresence;
                var delta = new ResourceDelta(
                    decimal.Round(system.IndustryOutput * share, 2),
                    decimal.Round(system.ResearchOutput * share, 2),
                    decimal.Round(system.PopulationOutput * share, 2));

                if (!generatedByEmpire.TryAdd(empireId, delta))
                {
                    generatedByEmpire[empireId] = generatedByEmpire[empireId] + delta;
                }
            }
        }

        foreach (var (empireId, delta) in generatedByEmpire)
        {
            var resources = state.EmpireResources.Single(resource => resource.EmpireId == empireId);
            resources.Industry += delta.Industry;
            resources.Research += delta.Research;
            resources.Population += delta.Population;
            resources.UpdatedAt = now;

            var empire = state.Empires.Single(item => item.EmpireId == empireId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.ResourcesGenerated,
                EmpireId = empireId,
                Severity = EventSeverity.Low,
                DisplayText = $"{empire.EmpireName} gained {delta.Industry:0.##} industry, {delta.Research:0.##} research, and {delta.Population:0.##} population.",
                FactJson = JsonSerializer.Serialize(new
                {
                    empireId,
                    delta.Industry,
                    delta.Research,
                    delta.Population
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }
}

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

        ApplyLosses([attackerFleet], attackerLosses);
        ApplyLosses(defenderFleets, defenderLosses);

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
            outcome
        }, GameStateJson.Options);

        state.BattleRecords.Add(battle);
        return battle;
    }

    private static int CalculateLosses(int shipsBefore, decimal lossRate) =>
        Math.Min(shipsBefore, Math.Max(1, (int)Math.Round(shipsBefore * lossRate, MidpointRounding.AwayFromZero)));

    private static void ApplyLosses(IReadOnlyCollection<Fleet> fleets, int totalLosses)
    {
        var remainingLosses = totalLosses;
        var shipsBefore = fleets.Sum(fleet => fleet.ShipCount);
        var index = 0;

        foreach (var fleet in fleets)
        {
            index++;
            var loss = index == fleets.Count
                ? remainingLosses
                : Math.Min(fleet.ShipCount, (int)Math.Round(totalLosses * (fleet.ShipCount / (double)shipsBefore), MidpointRounding.AwayFromZero));

            remainingLosses -= loss;
            fleet.ShipCount = Math.Max(0, fleet.ShipCount - loss);
            if (fleet.ShipCount == 0)
            {
                fleet.Status = FleetStatus.Destroyed;
            }
        }
    }

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

public readonly record struct ResourceDelta(decimal Industry, decimal Research, decimal Population)
{
    public static ResourceDelta operator +(ResourceDelta first, ResourceDelta second) =>
        new(first.Industry + second.Industry, first.Research + second.Research, first.Population + second.Population);
}

public sealed record TickResult(
    int TickNumber,
    TickLogStatus Status,
    int OrdersProcessed,
    int EventsCreated,
    int BattlesCreated,
    int ChronicleEntriesCreated);
