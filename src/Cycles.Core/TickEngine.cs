using System.Text.Json;

namespace Cycles.Core;

public sealed class TickEngine
{
    private readonly Func<GameState, Guid, int, GameState> createWorkingState;
    private readonly bool rollbackSharedAppends;

    public TickEngine()
        : this(
            static (state, cycleId, tickNumber) => state.CreateTickWorkingCopy(cycleId, tickNumber),
            rollbackSharedAppends: true)
    {
    }

    internal TickEngine(
        Func<GameState, Guid, int, GameState> createWorkingState,
        bool rollbackSharedAppends)
    {
        this.createWorkingState = createWorkingState;
        this.rollbackSharedAppends = rollbackSharedAppends;
    }

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

        if (cycle.TurnStage != TurnResolutionStage.CommandOpen)
        {
            throw new InvalidOperationException($"The Cycle cannot start a tick while it is {cycle.TurnStage}.");
        }

        var nextTick = cycle.CurrentTickNumber + 1;
        var appendCounts = AppendOnlyCounts.Capture(state);
        var working = createWorkingState(state, cycleId, nextTick);

        try
        {
            var result = ProcessTick(working, cycleId, nextTick, now);
            state.ReplaceWith(working);
            return result;
        }
        catch (Exception ex)
        {
            if (rollbackSharedAppends)
            {
                appendCounts.RollBack(state);
            }

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
            cycle.TurnStage = working.Cycles.Single(item => item.CycleId == cycleId).TurnStage;
            return new TickResult(nextTick, TickLogStatus.Failed, 0, 0, 0, 0);
        }
    }

    private readonly record struct AppendOnlyCounts(
        int ColonialOutposts,
        int EmpireDoctrineUnlocks,
        int AdmiralBattleHistories,
        int TickLogs,
        int Events,
        int BattleRecords,
        int BattleFleetParticipants,
        int ChronicleEntries)
    {
        public static AppendOnlyCounts Capture(GameState state) => new(
            state.ColonialOutposts.Count,
            state.EmpireDoctrineUnlocks.Count,
            state.AdmiralBattleHistories.Count,
            state.TickLogs.Count,
            state.Events.Count,
            state.BattleRecords.Count,
            state.BattleFleetParticipants.Count,
            state.ChronicleEntries.Count);

        public void RollBack(GameState state)
        {
            TrimToCount(state.ColonialOutposts, ColonialOutposts);
            TrimToCount(state.EmpireDoctrineUnlocks, EmpireDoctrineUnlocks);
            TrimToCount(state.AdmiralBattleHistories, AdmiralBattleHistories);
            TrimToCount(state.TickLogs, TickLogs);
            TrimToCount(state.Events, Events);
            TrimToCount(state.BattleRecords, BattleRecords);
            TrimToCount(state.BattleFleetParticipants, BattleFleetParticipants);
            TrimToCount(state.ChronicleEntries, ChronicleEntries);
        }

        private static void TrimToCount<T>(List<T> items, int count)
        {
            if (items.Count > count)
            {
                items.RemoveRange(count, items.Count - count);
            }
        }
    }

    private static TickResult ProcessTick(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var cycle = state.Cycles.Single(item => item.CycleId == cycleId);
        var eventsBefore = state.Events.Count;
        var battlesBefore = state.BattleRecords.Count;
        var chroniclesBefore = state.ChronicleEntries.Count;
        var log = new TickLog
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            StartedAt = now,
            Status = TickLogStatus.Running
        };
        state.TickLogs.Add(log);

        var ledger = TurnLedgerSealer.Seal(state, cycleId, tickNumber, now);
        cycle.TurnStage = TurnResolutionStage.Resolving;

        // Resources are calculated from the sealed phase-start world. Current income
        // is then available to mandatory economy and new programme spending.
        InfluenceCalculator.GenerateResources(state, cycleId, tickNumber, now);
        EconomyProcessor.CompleteShipConstruction(state, cycleId, tickNumber, now);
        EconomyProcessor.ApplyPrioritySpending(state, cycleId, tickNumber, now);

        // Recall intentions reverse existing journeys before passive arrivals. New
        // movement then dispatches from the resulting world state. All order batches
        // use ledger order (fleet ID, then order ID), never submission time.
        foreach (var order in ledger.OrdersFor(FleetOrderType.RecallFleet))
        {
            ProcessRecallOrder(state, order, tickNumber, now);
        }

        ProcessArrivals(state, cycleId, tickNumber, now);
        foreach (var order in ledger.OrdersFor(FleetOrderType.MoveFleet))
        {
            ProcessMoveOrder(state, order, tickNumber, now);
        }

        foreach (var order in ledger.OrdersFor(FleetOrderType.Hold))
        {
            ProcessHoldOrder(state, order, tickNumber, now);
        }

        ProcessAttackOrders(state, ledger.OrdersFor(FleetOrderType.Attack).ToArray(), tickNumber, now);

        foreach (var order in ledger.OrdersFor(FleetOrderType.Colonise))
        {
            ProcessColoniseOrder(state, order, tickNumber, now);
        }

        foreach (var order in ledger.Orders.Where(order => !Enum.IsDefined(order.OrderType)))
        {
            RejectOrder(state, order, tickNumber, now, "Unsupported order type.");
        }

        state.EmpireMetrics.RemoveAll(metric => metric.CycleId == cycleId && metric.TickNumber == tickNumber);
        state.EmpireMetrics.AddRange(EmpireMetricCalculator.CreateTickMetrics(state, cycleId, tickNumber, now));

        // Progression is published for the next command window after derived state
        // for this tick has been calculated.
        EconomyProcessor.ApplyResearchUnlocks(state, cycleId, tickNumber, now);
        cycle.TurnStage = TurnResolutionStage.Publishing;
        cycle.CurrentTickNumber = tickNumber;
        log.Status = TickLogStatus.Completed;
        log.CompletedAt = now;
        log.DiagnosticLog = $"Sealed and processed {ledger.Orders.Count} order(s) through deterministic resolution phases.";
        cycle.TurnStage = TurnResolutionStage.CommandOpen;

        return new TickResult(
            tickNumber,
            TickLogStatus.Completed,
            ledger.Orders.Count,
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
            .OrderBy(fleet => fleet.FleetId)
            .ToList();

        foreach (var fleet in arrivingFleets)
        {
            var wasReturning = fleet.DestinationSystemId == fleet.CurrentSystemId;
            fleet.CurrentSystemId = fleet.DestinationSystemId!.Value;
            fleet.DestinationSystemId = null;
            fleet.DepartureTickNumber = null;
            fleet.ArrivalTickNumber = null;
            fleet.Status = FleetStatus.Active;

            var destination = state.Systems.Single(system => system.SystemId == fleet.CurrentSystemId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = wasReturning ? EventType.FleetReturned : EventType.FleetArrived,
                SystemId = destination.SystemId,
                EmpireId = fleet.EmpireId,
                Severity = EventSeverity.Normal,
                DisplayText = wasReturning
                    ? $"{fleet.FleetName} returned to {destination.SystemName}."
                    : $"{fleet.FleetName} arrived at {destination.SystemName}.",
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

        var journey = MoveJourneyTiming.Project(link.TravelTicks, tickNumber);
        var origin = state.Systems.Single(system => system.SystemId == fleet.CurrentSystemId);
        if (journey.TravelTicks == 1)
        {
            fleet.CurrentSystemId = target.SystemId;
            fleet.Status = FleetStatus.Active;
            fleet.DestinationSystemId = null;
            fleet.DepartureTickNumber = null;
            fleet.ArrivalTickNumber = null;
        }
        else
        {
            fleet.Status = FleetStatus.InTransit;
            fleet.DestinationSystemId = target.SystemId;
            fleet.DepartureTickNumber = journey.DispatchTickNumber;
            fleet.ArrivalTickNumber = journey.ArrivalTickNumber;
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
            FactionId = fleet.FactionId,
            Severity = EventSeverity.Normal,
            DisplayText = $"{fleet.FleetName} moved from {origin.SystemName} to {target.SystemName}{inTransitText}.",
            FactJson = JsonSerializer.Serialize(new
            {
                fleetId = fleet.FleetId,
                originSystemId = origin.SystemId,
                targetSystemId = target.SystemId,
                travelTicks = journey.TravelTicks,
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

        if (order.CommandSource != FleetOrderCommandSource.Human)
        {
            return;
        }

        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.FleetHeld,
            SystemId = system.SystemId,
            EmpireId = fleet.EmpireId,
            FactionId = fleet.FactionId,
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

    private static void ProcessAttackOrders(
        GameState state,
        IReadOnlyCollection<FleetOrder> orders,
        int tickNumber,
        DateTimeOffset now)
    {
        var intents = new List<AttackIntent>();
        foreach (var order in orders)
        {
            var attackerFleet = TryGetActiveFleet(state, order.FleetId);
            if (attackerFleet is null)
            {
                RejectOrder(state, order, tickNumber, now, "Attacking fleet is not active.");
                continue;
            }

            attackerFleet.FactionId = state.GetFactionId(attackerFleet);
            var requestedFactionId = order.TargetFactionId
                ?? (order.TargetEmpireId.HasValue ? state.GetEmpireFaction(order.TargetEmpireId.Value).FactionId : null);
            var defenderFleets = state.Fleets
                .Where(fleet => fleet.CycleId == order.CycleId
                                && fleet.Status == FleetStatus.Active
                                && fleet.CurrentSystemId == attackerFleet.CurrentSystemId
                                && state.GetFactionId(fleet) != attackerFleet.FactionId
                                && (!requestedFactionId.HasValue || state.GetFactionId(fleet) == requestedFactionId.Value))
                .OrderBy(fleet => fleet.FleetId)
                .ToArray();

            if (defenderFleets.Length == 0)
            {
                RejectOrder(state, order, tickNumber, now, "No hostile active fleet is present in the system.");
                continue;
            }

            var defenderFactionId = requestedFactionId
                ?? defenderFleets
                    .GroupBy(state.GetFactionId)
                    .OrderByDescending(group => group.Sum(fleet => fleet.ShipCount))
                    .ThenBy(group => group.Key)
                    .First()
                    .Key;
            var defenderFleetIds = defenderFleets
                .Where(fleet => state.GetFactionId(fleet) == defenderFactionId)
                .Select(fleet => fleet.FleetId)
                .ToArray();
            intents.Add(new AttackIntent(
                order,
                attackerFleet.FleetId,
                attackerFleet.CurrentSystemId,
                attackerFleet.FactionId,
                defenderFactionId,
                defenderFleetIds));
        }

        foreach (var group in intents
                     .GroupBy(intent => CombatGroupKey.Create(
                         intent.SystemId,
                         intent.AttackerFactionId,
                         intent.DefenderFactionId))
                     .OrderBy(group => group.Key.SystemId)
                     .ThenBy(group => group.Key.FirstFactionId)
                     .ThenBy(group => group.Key.SecondFactionId))
        {
            ProcessAttackGroup(state, group.Key, group.ToArray(), tickNumber, now);
        }
    }

    private static void ProcessRecallOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        var fleet = state.Fleets.SingleOrDefault(item => item.FleetId == order.FleetId);
        if (fleet is null
            || fleet.Status != FleetStatus.InTransit
            || !fleet.DestinationSystemId.HasValue
            || !fleet.DepartureTickNumber.HasValue
            || !fleet.ArrivalTickNumber.HasValue
            || fleet.DestinationSystemId == fleet.CurrentSystemId)
        {
            RejectOrder(state, order, tickNumber, now, "Fleet is not on an outbound journey.");
            return;
        }

        var origin = state.Systems.Single(system => system.SystemId == fleet.CurrentSystemId);
        var outwardDestination = state.Systems.Single(system => system.SystemId == fleet.DestinationSystemId.Value);
        var elapsedOutboundTicks = Math.Max(1, tickNumber - fleet.DepartureTickNumber.Value);
        var returnArrivalTick = tickNumber + elapsedOutboundTicks - 1;

        if (elapsedOutboundTicks <= 1)
        {
            fleet.Status = FleetStatus.Active;
            fleet.DestinationSystemId = null;
            fleet.DepartureTickNumber = null;
            fleet.ArrivalTickNumber = null;
        }
        else
        {
            fleet.DestinationSystemId = origin.SystemId;
            fleet.DepartureTickNumber = tickNumber;
            fleet.ArrivalTickNumber = returnArrivalTick;
        }

        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = tickNumber;

        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.FleetRecalled,
            SystemId = origin.SystemId,
            EmpireId = fleet.EmpireId,
            FactionId = fleet.FactionId,
            Severity = EventSeverity.Normal,
            DisplayText = elapsedOutboundTicks <= 1
                ? $"{fleet.FleetName} reversed course and returned to {origin.SystemName}."
                : $"{fleet.FleetName} reversed course from {outwardDestination.SystemName} and will return to {origin.SystemName} on tick {returnArrivalTick}.",
            FactJson = JsonSerializer.Serialize(new
            {
                fleetId = fleet.FleetId,
                originSystemId = origin.SystemId,
                outwardDestinationSystemId = outwardDestination.SystemId,
                elapsedOutboundTicks,
                returnArrivalTick
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static void ProcessAttackGroup(
        GameState state,
        CombatGroupKey key,
        IReadOnlyCollection<AttackIntent> intents,
        int tickNumber,
        DateTimeOffset now)
    {
        var attackingFactionIds = intents.Select(intent => intent.AttackerFactionId).Distinct().ToArray();
        var attackerFactionId = attackingFactionIds.Length == 1
            ? attackingFactionIds[0]
            : key.FirstFactionId;
        var defenderFactionId = attackerFactionId == key.FirstFactionId
            ? key.SecondFactionId
            : key.FirstFactionId;
        var attackerFleetIds = intents
            .Where(intent => intent.AttackerFactionId == attackerFactionId)
            .Select(intent => intent.AttackerFleetId)
            .Distinct()
            .Order()
            .ToArray();
        var defenderFleetIds = intents
            .Where(intent => intent.DefenderFactionId == defenderFactionId)
            .SelectMany(intent => intent.DefenderFleetIds)
            .Concat(intents
                .Where(intent => intent.AttackerFactionId == defenderFactionId)
                .Select(intent => intent.AttackerFleetId))
            .Distinct()
            .Order()
            .ToArray();
        var attackerFleets = attackerFleetIds.Select(id => state.Fleets.Single(fleet => fleet.FleetId == id)).ToArray();
        var defenderFleets = defenderFleetIds.Select(id => state.Fleets.Single(fleet => fleet.FleetId == id)).ToArray();

        foreach (var fleet in attackerFleets)
        {
            fleet.FactionId = attackerFactionId;
        }

        foreach (var fleet in defenderFleets)
        {
            fleet.FactionId = defenderFactionId;
        }

        var system = state.Systems.Single(item => item.SystemId == key.SystemId);
        var battle = CombatResolver.Resolve(state, tickNumber, now, system, attackerFleets, defenderFleets);
        var attackerFaction = state.Factions.Single(item => item.FactionId == attackerFactionId);
        var defenderFaction = state.Factions.Single(item => item.FactionId == defenderFactionId);
        foreach (var aggression in intents
                     .Select(intent => (intent.AttackerFactionId, intent.DefenderFactionId))
                     .Distinct())
        {
            var aggressor = state.Factions.Single(item => item.FactionId == aggression.AttackerFactionId);
            var target = state.Factions.Single(item => item.FactionId == aggression.DefenderFactionId);
            if (aggressor.EmpireId.HasValue && target.EmpireId.HasValue)
            {
                DiplomacyService.RecordAggression(
                    state,
                    intents.First().Order.CycleId,
                    tickNumber,
                    aggressor.EmpireId.Value,
                    target.EmpireId.Value,
                    system,
                    now);
            }
        }

        foreach (var intent in intents)
        {
            intent.Order.Status = FleetOrderStatus.Processed;
            intent.Order.ProcessedTick = tickNumber;
        }

        var attacker = attackerFaction.EmpireId.HasValue
            ? state.Empires.Single(empire => empire.EmpireId == attackerFaction.EmpireId.Value)
            : new Empire { EmpireId = Guid.Empty, EmpireName = attackerFaction.FactionName };
        var defender = defenderFaction.EmpireId.HasValue
            ? state.Empires.Single(empire => empire.EmpireId == defenderFaction.EmpireId.Value)
            : new Empire { EmpireId = Guid.Empty, EmpireName = defenderFaction.FactionName };
        var totalLosses = battle.AttackerLosses + battle.DefenderLosses;
        var severity = totalLosses >= 80 ? EventSeverity.Historic : totalLosses >= 30 ? EventSeverity.High : EventSeverity.Normal;
        var outcomeText = battle.Outcome == BattleOutcome.AttackerVictory
            ? $"{attacker.EmpireName} forced {defender.EmpireName} back"
            : battle.Outcome == BattleOutcome.DefenderVictory
                ? $"{defender.EmpireName} held against {attacker.EmpireName}"
                : $"{attacker.EmpireName} and {defender.EmpireName} shattered each other";

        var battleEvent = new EventRecord
        {
            CycleId = intents.First().Order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.CombatResolved,
            SystemId = system.SystemId,
            EmpireId = attackerFaction.EmpireId,
            FactionId = attackerFaction.FactionId,
            Severity = severity,
            DisplayText = $"{outcomeText} at {system.SystemName}; {totalLosses} ships were destroyed.",
            FactJson = battle.FactJson,
            CreatedAt = now
        };
        state.Events.Add(battleEvent);

        var admiralHistories = state.AdmiralBattleHistories
            .Where(history => history.BattleId == battle.BattleId)
            .ToArray();
        var importance = ChronicleScoring.ScoreBattle(battle, system, admiralHistories);
        if (importance >= ChronicleScoring.ChronicleThreshold)
        {
            var chronicle = ChronicleScoring.CreateBattleEntry(
                battle,
                battleEvent,
                system,
                attacker,
                defender,
                importance,
                now,
                state.Admirals,
                admiralHistories);
            state.ChronicleEntries.Add(chronicle);
            state.Events.Add(new EventRecord
            {
                CycleId = intents.First().Order.CycleId,
                TickNumber = tickNumber,
                EventType = EventType.ChronicleCreated,
                SystemId = system.SystemId,
                EmpireId = attackerFaction.EmpireId,
                FactionId = attackerFaction.FactionId,
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

    private sealed record AttackIntent(
        FleetOrder Order,
        Guid AttackerFleetId,
        Guid SystemId,
        Guid AttackerFactionId,
        Guid DefenderFactionId,
        IReadOnlyCollection<Guid> DefenderFleetIds);

    private readonly record struct CombatGroupKey(
        Guid SystemId,
        Guid FirstFactionId,
        Guid SecondFactionId)
    {
        public static CombatGroupKey Create(Guid systemId, Guid attackerFactionId, Guid defenderFactionId) =>
            attackerFactionId.CompareTo(defenderFactionId) <= 0
                ? new CombatGroupKey(systemId, attackerFactionId, defenderFactionId)
                : new CombatGroupKey(systemId, defenderFactionId, attackerFactionId);
    }

    private static void ProcessColoniseOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now)
    {
        var fleet = TryGetActiveFleet(state, order.FleetId);
        if (fleet is null)
        {
            RejectOrder(state, order, tickNumber, now, "Colonising fleet is not active.");
            return;
        }

        if (!order.TargetSystemId.HasValue || fleet.CurrentSystemId != order.TargetSystemId.Value)
        {
            RejectOrder(state, order, tickNumber, now, "Colonising fleet is no longer present in the target system.");
            return;
        }

        var empire = state.Empires.Single(item => item.EmpireId == fleet.EmpireId);
        if (fleet.CurrentSystemId == empire.HomeSystemId)
        {
            RejectOrder(state, order, tickNumber, now, "An empire cannot colonise its home system.");
            return;
        }

        if (state.ColonialOutposts.Any(item => item.CycleId == order.CycleId
                                                && item.EmpireId == fleet.EmpireId
                                                && item.SystemId == fleet.CurrentSystemId))
        {
            RejectOrder(state, order, tickNumber, now, "The empire already has a colonial outpost in this system.");
            return;
        }

        if (!OrderService.HasLeadingPresence(state, order.CycleId, fleet.CurrentSystemId, fleet.EmpireId))
        {
            RejectOrder(state, order, tickNumber, now, "Colonisation requires the empire to retain the leading influence in the system.");
            return;
        }

        var resources = state.EmpireResources.Single(item => item.EmpireId == fleet.EmpireId);
        if (resources.Population < OrderService.ColonisationPopulationCost)
        {
            RejectOrder(state, order, tickNumber, now, $"Colonisation requires {OrderService.ColonisationPopulationCost:0.##} population.");
            return;
        }

        resources.Population -= OrderService.ColonisationPopulationCost;
        resources.LastSpentPopulation += OrderService.ColonisationPopulationCost;
        resources.UpdatedAt = now;

        var outpost = new ColonialOutpost
        {
            CycleId = order.CycleId,
            EmpireId = fleet.EmpireId,
            SystemId = fleet.CurrentSystemId,
            EstablishedTick = tickNumber,
            CreatedAt = now
        };
        state.ColonialOutposts.Add(outpost);

        order.Status = FleetOrderStatus.Processed;
        order.ProcessedTick = tickNumber;

        var system = state.Systems.Single(item => item.SystemId == fleet.CurrentSystemId);
        state.Events.Add(new EventRecord
        {
            CycleId = order.CycleId,
            TickNumber = tickNumber,
            EventType = EventType.ColonialOutpostEstablished,
            SystemId = system.SystemId,
            EmpireId = fleet.EmpireId,
            FactionId = fleet.FactionId,
            Severity = EventSeverity.Normal,
            DisplayText = $"{empire.EmpireName} established a colonial outpost at {system.SystemName}.",
            FactJson = JsonSerializer.Serialize(new
            {
                outpost.ColonialOutpostId,
                empire.EmpireId,
                fleet.FleetId,
                system.SystemId,
                populationCost = OrderService.ColonisationPopulationCost,
                localPresence = InfluenceCalculator.ColonialOutpostPresence
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static Fleet? TryGetActiveFleet(GameState state, Guid fleetId) =>
        state.Fleets.SingleOrDefault(fleet => fleet.FleetId == fleetId
                                             && fleet.Status == FleetStatus.Active
                                             && fleet.ShipCount > 0);

    private static void RejectOrder(GameState state, FleetOrder order, int tickNumber, DateTimeOffset now, string reason)
        => OrderService.RejectOrder(state, order, tickNumber, now, reason);
}

public sealed record TickResult(
    int TickNumber,
    TickLogStatus Status,
    int OrdersProcessed,
    int EventsCreated,
    int BattlesCreated,
    int ChronicleEntriesCreated);
