using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cycles.Core;

public sealed record GameStateTransferDocument(
    int FormatVersion,
    DateTimeOffset ExportedAt,
    GameState State);

public sealed record GameStateValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public static class GameStateTransfer
{
    public const int CurrentFormatVersion = 1;

    private static readonly JsonSerializerOptions TransferJsonOptions = CreateJsonOptions();
    private static readonly PropertyInfo[] PersistedCollections = typeof(GameState)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.PropertyType.IsGenericType
                           && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
        .ToArray();

    public static void Write(Stream destination, GameState state, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(destination);
        var validation = Validate(state);
        if (!validation.IsValid)
        {
            throw InvalidState(validation);
        }

        JsonSerializer.Serialize(
            destination,
            new GameStateTransferDocument(CurrentFormatVersion, exportedAt, state),
            TransferJsonOptions);
    }

    public static GameStateTransferDocument Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(source);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The state transfer file is not valid JSON: {exception.Message}", exception);
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The state transfer file must contain a JSON object.");
            }

            var version = RequireProperty(root, "formatVersion");
            if (version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var formatVersion))
            {
                throw new InvalidOperationException("The state transfer formatVersion must be an integer.");
            }

            if (formatVersion != CurrentFormatVersion)
            {
                throw new InvalidOperationException($"Unsupported state transfer formatVersion {formatVersion}; this CLI supports version {CurrentFormatVersion}.");
            }

            _ = RequireProperty(root, "exportedAt");
            var stateElement = RequireProperty(root, "state");
            if (stateElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The state transfer state property must contain an object.");
            }

            foreach (var property in PersistedCollections)
            {
                var jsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                var collection = RequireProperty(stateElement, jsonName);
                if (collection.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"The state transfer state.{jsonName} property must contain an array.");
                }
            }

            GameStateTransferDocument document;
            try
            {
                document = root.Deserialize<GameStateTransferDocument>(TransferJsonOptions)
                    ?? throw new InvalidOperationException("The state transfer document could not be deserialised.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The state transfer document has an invalid value: {exception.Message}", exception);
            }

            StrategicPriorityPolicy.Normalize(document.State);
            var validation = Validate(document.State);
            if (!validation.IsValid)
            {
                throw InvalidState(validation);
            }

            return document;
        }
    }

    public static GameStateValidationResult Validate(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var errors = new List<string>();

        foreach (var property in PersistedCollections)
        {
            if (property.GetValue(state) is null)
            {
                errors.Add($"state.{JsonNamingPolicy.CamelCase.ConvertName(property.Name)} is missing.");
            }
        }

        if (errors.Count > 0)
        {
            return new GameStateValidationResult(errors);
        }

        ValidateIdentifiers(state, errors);
        ValidatePlayers(state, errors);
        ValidateCyclesAndReferences(state, errors);
        ValidateTickAndRecoveryState(state, errors);
        ValidateEmbeddedJson(state, errors);
        return new GameStateValidationResult(errors);
    }

    public static int CountRecords(GameState state) =>
        PersistedCollections.Sum(property => ((System.Collections.ICollection)property.GetValue(state)!).Count);

    private static void ValidateIdentifiers(GameState state, List<string> errors)
    {
        Unique(state.Players, item => item.PlayerId, "players", errors);
        Unique(state.AdminRoleAuditRecords, item => item.AdminRoleAuditRecordId, "adminRoleAuditRecords", errors);
        Unique(state.Cycles, item => item.CycleId, "cycles", errors);
        Unique(state.Empires, item => item.EmpireId, "empires", errors);
        Unique(state.EmpireResources, item => item.EmpireResourceId, "empireResources", errors);
        Unique(state.EmpirePriorities, item => item.EmpirePriorityId, "empirePriorities", errors);
        Unique(state.EmpireMetrics, item => item.EmpireMetricId, "empireMetrics", errors);
        Unique(state.CycleRankings, item => item.CycleRankingId, "cycleRankings", errors);
        Unique(state.CycleMajorEvents, item => item.CycleMajorEventId, "cycleMajorEvents", errors);
        Unique(state.SystemHistoricalSignals, item => item.SystemHistoricalSignalId, "systemHistoricalSignals", errors);
        Unique(state.ColonialOutposts, item => item.ColonialOutpostId, "colonialOutposts", errors);
        Unique(state.DiplomaticRelationships, item => item.DiplomaticRelationshipId, "diplomaticRelationships", errors);
        Unique(state.Admirals, item => item.AdmiralId, "admirals", errors);
        Unique(state.AdmiralBattleHistories, item => item.AdmiralBattleHistoryId, "admiralBattleHistories", errors);
        Unique(state.Systems, item => item.SystemId, "systems", errors);
        Unique(state.SystemLinks, item => item.SystemLinkId, "systemLinks", errors);
        Unique(state.Fleets, item => item.FleetId, "fleets", errors);
        Unique(state.FleetOrders, item => item.FleetOrderId, "fleetOrders", errors);
        Unique(state.ShipConstructions, item => item.ShipConstructionId, "shipConstructions", errors);
        Unique(state.TickLogs, item => item.TickLogId, "tickLogs", errors);
        Unique(state.Events, item => item.EventId, "events", errors);
        Unique(state.BattleRecords, item => item.BattleId, "battleRecords", errors);
        Unique(state.ChronicleEntries, item => item.ChronicleEntryId, "chronicleEntries", errors);
    }

    private static void ValidatePlayers(GameState state, List<string> errors)
    {
        foreach (var player in state.Players)
        {
            Required(player.Username, $"Player {player.PlayerId} has no username.", errors);
            var hasIssuer = !string.IsNullOrWhiteSpace(player.ExternalIssuer);
            var hasSubject = !string.IsNullOrWhiteSpace(player.ExternalSubject);
            if (hasIssuer != hasSubject)
            {
                errors.Add($"Player {player.PlayerId} must have both external issuer and subject, or neither.");
            }
        }

        foreach (var duplicate in state.Players
                     .Where(player => !string.IsNullOrWhiteSpace(player.ExternalIssuer))
                     .GroupBy(player => (player.ExternalIssuer, player.ExternalSubject))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"External identity '{duplicate.Key.ExternalIssuer}|{duplicate.Key.ExternalSubject}' maps to more than one player.");
        }

        var playerIds = state.Players.Select(item => item.PlayerId).ToHashSet();
        foreach (var audit in state.AdminRoleAuditRecords)
        {
            Reference(playerIds, audit.TargetPlayerId, $"Admin role audit {audit.AdminRoleAuditRecordId} target player", errors);
            if (audit.ActorPlayerId.HasValue)
            {
                Reference(playerIds, audit.ActorPlayerId.Value, $"Admin role audit {audit.AdminRoleAuditRecordId} actor player", errors);
            }

            Required(audit.Reason, $"Admin role audit {audit.AdminRoleAuditRecordId} has no reason.", errors);
            Required(audit.Source, $"Admin role audit {audit.AdminRoleAuditRecordId} has no source.", errors);
            if (audit.Severity != EventSeverity.High)
            {
                errors.Add($"Admin role audit {audit.AdminRoleAuditRecordId} must retain High severity.");
            }
        }
    }

    private static void ValidateCyclesAndReferences(GameState state, List<string> errors)
    {
        if (state.Cycles.Count == 0)
        {
            errors.Add("At least one Cycle is required.");
            return;
        }

        var operationalCycles = state.Cycles.Count(cycle => cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired);
        if (operationalCycles > 1)
        {
            errors.Add("Only one Active or RecoveryRequired Cycle may exist.");
        }

        foreach (var cycle in state.Cycles)
        {
            Required(cycle.Name, $"Cycle {cycle.CycleId} has no name.", errors);
            if (cycle.CurrentTickNumber < 0)
            {
                errors.Add($"Cycle {cycle.CycleId} has a negative current tick.");
            }

            if (cycle.TickLengthMinutes <= 0 || cycle.EndAt <= cycle.StartAt)
            {
                errors.Add($"Cycle {cycle.CycleId} has invalid scheduling boundaries.");
            }
        }

        var playerIds = state.Players.Select(item => item.PlayerId).ToHashSet();
        var cycleIds = state.Cycles.Select(item => item.CycleId).ToHashSet();
        var empireIds = state.Empires.Select(item => item.EmpireId).ToHashSet();
        var systemsById = state.Systems.GroupBy(item => item.SystemId).ToDictionary(group => group.Key, group => group.First());
        var fleetsById = state.Fleets.GroupBy(item => item.FleetId).ToDictionary(group => group.Key, group => group.First());
        var admiralsById = state.Admirals.GroupBy(item => item.AdmiralId).ToDictionary(group => group.Key, group => group.First());
        var battleIds = state.BattleRecords.Select(item => item.BattleId).ToHashSet();
        var eventIds = state.Events.Select(item => item.EventId).ToHashSet();

        foreach (var system in state.Systems)
        {
            Reference(cycleIds, system.CycleId, $"System {system.SystemId} Cycle", errors);
            Required(system.SystemName, $"System {system.SystemId} has no name.", errors);
        }

        foreach (var empire in state.Empires)
        {
            Reference(cycleIds, empire.CycleId, $"Empire {empire.EmpireId} Cycle", errors);
            Reference(playerIds, empire.PlayerId, $"Empire {empire.EmpireId} player", errors);
            Reference(systemsById.Keys, empire.HomeSystemId, $"Empire {empire.EmpireId} home system", errors);
            if (systemsById.TryGetValue(empire.HomeSystemId, out var home) && home.CycleId != empire.CycleId)
            {
                errors.Add($"Empire {empire.EmpireId} home system belongs to another Cycle.");
            }

            Required(empire.EmpireName, $"Empire {empire.EmpireId} has no name.", errors);
        }

        foreach (var duplicate in state.Empires.GroupBy(item => (item.CycleId, item.PlayerId)).Where(group => group.Count() > 1))
        {
            errors.Add($"Player {duplicate.Key.PlayerId} has more than one empire in Cycle {duplicate.Key.CycleId}.");
        }

        ValidateEmpireOwnedCollections(state, cycleIds, empireIds, systemsById.Keys, battleIds, errors);

        foreach (var link in state.SystemLinks)
        {
            Reference(cycleIds, link.CycleId, $"System link {link.SystemLinkId} Cycle", errors);
            Reference(systemsById.Keys, link.SystemAId, $"System link {link.SystemLinkId} first system", errors);
            Reference(systemsById.Keys, link.SystemBId, $"System link {link.SystemLinkId} second system", errors);
            if (link.SystemAId == link.SystemBId || link.TravelTicks <= 0)
            {
                errors.Add($"System link {link.SystemLinkId} has invalid endpoints or travel time.");
            }

            if (systemsById.TryGetValue(link.SystemAId, out var first)
                && systemsById.TryGetValue(link.SystemBId, out var second)
                && (first.CycleId != link.CycleId || second.CycleId != link.CycleId))
            {
                errors.Add($"System link {link.SystemLinkId} crosses Cycle boundaries.");
            }
        }

        foreach (var admiral in state.Admirals)
        {
            Reference(cycleIds, admiral.CycleId, $"Admiral {admiral.AdmiralId} Cycle", errors);
            Reference(empireIds, admiral.EmpireId, $"Admiral {admiral.AdmiralId} empire", errors);
            if (state.Empires.FirstOrDefault(item => item.EmpireId == admiral.EmpireId) is { } empire
                && empire.CycleId != admiral.CycleId)
            {
                errors.Add($"Admiral {admiral.AdmiralId} and its empire belong to different Cycles.");
            }
            Required(admiral.AdmiralName, $"Admiral {admiral.AdmiralId} has no name.", errors);
        }

        foreach (var fleet in state.Fleets)
        {
            Reference(cycleIds, fleet.CycleId, $"Fleet {fleet.FleetId} Cycle", errors);
            Reference(empireIds, fleet.EmpireId, $"Fleet {fleet.FleetId} empire", errors);
            Reference(systemsById.Keys, fleet.CurrentSystemId, $"Fleet {fleet.FleetId} current system", errors);
            if (fleet.DestinationSystemId.HasValue)
            {
                Reference(systemsById.Keys, fleet.DestinationSystemId.Value, $"Fleet {fleet.FleetId} destination system", errors);
            }

            if (fleet.AdmiralId.HasValue)
            {
                Reference(admiralsById.Keys, fleet.AdmiralId.Value, $"Fleet {fleet.FleetId} admiral", errors);
                if (admiralsById.TryGetValue(fleet.AdmiralId.Value, out var admiral) && admiral.EmpireId != fleet.EmpireId)
                {
                    errors.Add($"Fleet {fleet.FleetId} is assigned to another empire's admiral.");
                }
            }

            if (state.Empires.FirstOrDefault(item => item.EmpireId == fleet.EmpireId) is { } fleetEmpire
                && fleetEmpire.CycleId != fleet.CycleId)
            {
                errors.Add($"Fleet {fleet.FleetId} and its empire belong to different Cycles.");
            }

            if (systemsById.TryGetValue(fleet.CurrentSystemId, out var fleetSystem)
                && fleetSystem.CycleId != fleet.CycleId)
            {
                errors.Add($"Fleet {fleet.FleetId} and its current system belong to different Cycles.");
            }

            Required(fleet.FleetName, $"Fleet {fleet.FleetId} has no name.", errors);
            if (fleet.ShipCount < 0)
            {
                errors.Add($"Fleet {fleet.FleetId} has a negative ship count.");
            }
        }

        foreach (var order in state.FleetOrders)
        {
            Reference(cycleIds, order.CycleId, $"Fleet order {order.FleetOrderId} Cycle", errors);
            Reference(fleetsById.Keys, order.FleetId, $"Fleet order {order.FleetOrderId} fleet", errors);
            if (order.TargetSystemId.HasValue)
            {
                Reference(systemsById.Keys, order.TargetSystemId.Value, $"Fleet order {order.FleetOrderId} target system", errors);
            }

            if (order.TargetEmpireId.HasValue)
            {
                Reference(empireIds, order.TargetEmpireId.Value, $"Fleet order {order.FleetOrderId} target empire", errors);
            }

            if (fleetsById.TryGetValue(order.FleetId, out var orderedFleet) && orderedFleet.CycleId != order.CycleId)
            {
                errors.Add($"Fleet order {order.FleetOrderId} and its fleet belong to different Cycles.");
            }

            if (order.SubmitTick < 0 || order.ExecuteAfterTick <= order.SubmitTick)
            {
                errors.Add($"Fleet order {order.FleetOrderId} has invalid tick scheduling.");
            }
        }

        foreach (var history in state.AdmiralBattleHistories)
        {
            Reference(cycleIds, history.CycleId, $"Admiral battle history {history.AdmiralBattleHistoryId} Cycle", errors);
            Reference(admiralsById.Keys, history.AdmiralId, $"Admiral battle history {history.AdmiralBattleHistoryId} admiral", errors);
            Reference(battleIds, history.BattleId, $"Admiral battle history {history.AdmiralBattleHistoryId} battle", errors);
            Reference(systemsById.Keys, history.SystemId, $"Admiral battle history {history.AdmiralBattleHistoryId} system", errors);
            Reference(fleetsById.Keys, history.FleetId, $"Admiral battle history {history.AdmiralBattleHistoryId} fleet", errors);
            if (state.BattleRecords.FirstOrDefault(item => item.BattleId == history.BattleId) is { } historyBattle
                && historyBattle.CycleId != history.CycleId)
            {
                errors.Add($"Admiral battle history {history.AdmiralBattleHistoryId} and its battle belong to different Cycles.");
            }
        }

        foreach (var item in state.Events)
        {
            Reference(cycleIds, item.CycleId, $"Event {item.EventId} Cycle", errors);
            OptionalReference(systemsById.Keys, item.SystemId, $"Event {item.EventId} system", errors);
            OptionalReference(empireIds, item.EmpireId, $"Event {item.EventId} empire", errors);
            if (item.SystemId.HasValue && systemsById.TryGetValue(item.SystemId.Value, out var eventSystem) && eventSystem.CycleId != item.CycleId)
            {
                errors.Add($"Event {item.EventId} and its system belong to different Cycles.");
            }
        }

        foreach (var battle in state.BattleRecords)
        {
            Reference(cycleIds, battle.CycleId, $"Battle {battle.BattleId} Cycle", errors);
            Reference(systemsById.Keys, battle.SystemId, $"Battle {battle.BattleId} system", errors);
            Reference(empireIds, battle.AttackerEmpireId, $"Battle {battle.BattleId} attacker", errors);
            Reference(empireIds, battle.DefenderEmpireId, $"Battle {battle.BattleId} defender", errors);
            ValidateFleetIdList(battle.AttackerFleetIds, fleetsById.Keys, $"Battle {battle.BattleId} attacker fleets", errors);
            ValidateFleetIdList(battle.DefenderFleetIds, fleetsById.Keys, $"Battle {battle.BattleId} defender fleets", errors);
            if (systemsById.TryGetValue(battle.SystemId, out var battleSystem) && battleSystem.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its system belong to different Cycles.");
            }

            if (battle.AttackerLosses < 0 || battle.DefenderLosses < 0
                || battle.AttackerLosses > battle.AttackerShipsBefore
                || battle.DefenderLosses > battle.DefenderShipsBefore)
            {
                errors.Add($"Battle {battle.BattleId} has invalid retained ship losses.");
            }
        }

        foreach (var entry in state.ChronicleEntries)
        {
            Reference(cycleIds, entry.CycleId, $"Chronicle entry {entry.ChronicleEntryId} Cycle", errors);
            OptionalReference(systemsById.Keys, entry.SystemId, $"Chronicle entry {entry.ChronicleEntryId} system", errors);
            OptionalReference(eventIds, entry.SourceEventId, $"Chronicle entry {entry.ChronicleEntryId} source event", errors);
            OptionalReference(battleIds, entry.SourceBattleId, $"Chronicle entry {entry.ChronicleEntryId} source battle", errors);
            Required(entry.Title, $"Chronicle entry {entry.ChronicleEntryId} has no title.", errors);
            if (entry.SourceEventId.HasValue && state.Events.FirstOrDefault(item => item.EventId == entry.SourceEventId.Value) is { } sourceEvent && sourceEvent.CycleId != entry.CycleId)
            {
                errors.Add($"Chronicle entry {entry.ChronicleEntryId} and its source event belong to different Cycles.");
            }

            if (entry.SourceBattleId.HasValue && state.BattleRecords.FirstOrDefault(item => item.BattleId == entry.SourceBattleId.Value) is { } sourceBattle && sourceBattle.CycleId != entry.CycleId)
            {
                errors.Add($"Chronicle entry {entry.ChronicleEntryId} and its source battle belong to different Cycles.");
            }
        }
    }

    private static void ValidateEmpireOwnedCollections(
        GameState state,
        HashSet<Guid> cycleIds,
        HashSet<Guid> empireIds,
        ICollection<Guid> systemIds,
        HashSet<Guid> battleIds,
        List<string> errors)
    {
        foreach (var resource in state.EmpireResources)
        {
            Reference(empireIds, resource.EmpireId, $"Empire resource {resource.EmpireResourceId} empire", errors);
        }

        foreach (var priority in state.EmpirePriorities)
        {
            Reference(empireIds, priority.EmpireId, $"Empire priority {priority.EmpirePriorityId} empire", errors);
            if (priority.IndustryWeight < 0 || priority.ResearchWeight < 0 || priority.MilitaryWeight < 0 || priority.ExpansionWeight < 0)
            {
                errors.Add($"Empire priority {priority.EmpirePriorityId} weights cannot be negative.");
            }

            if ((long)priority.IndustryWeight + priority.ResearchWeight + priority.MilitaryWeight + priority.ExpansionWeight != 100)
            {
                errors.Add($"Empire priority {priority.EmpirePriorityId} weights must total 100.");
            }

            if (priority.IndustryWeight != 0 || priority.ResearchWeight != 0)
            {
                errors.Add($"Empire priority {priority.EmpirePriorityId} Development and Innovation weights must be zero.");
            }
        }

        foreach (var duplicate in state.EmpireResources.GroupBy(item => item.EmpireId).Where(group => group.Count() > 1))
        {
            errors.Add($"Empire {duplicate.Key} has more than one resource record.");
        }

        foreach (var duplicate in state.EmpirePriorities.GroupBy(item => item.EmpireId).Where(group => group.Count() > 1))
        {
            errors.Add($"Empire {duplicate.Key} has more than one priority record.");
        }

        foreach (var empireId in empireIds)
        {
            if (state.EmpireResources.Count(item => item.EmpireId == empireId) != 1)
            {
                errors.Add($"Empire {empireId} must have exactly one resource record.");
            }

            if (state.EmpirePriorities.Count(item => item.EmpireId == empireId) != 1)
            {
                errors.Add($"Empire {empireId} must have exactly one priority record.");
            }
        }

        foreach (var metric in state.EmpireMetrics)
        {
            Reference(cycleIds, metric.CycleId, $"Empire metric {metric.EmpireMetricId} Cycle", errors);
            Reference(empireIds, metric.EmpireId, $"Empire metric {metric.EmpireMetricId} empire", errors);
            EnsureEmpireCycle(state, metric.EmpireId, metric.CycleId, $"Empire metric {metric.EmpireMetricId}", errors);
        }

        foreach (var ranking in state.CycleRankings)
        {
            Reference(cycleIds, ranking.CycleId, $"Cycle ranking {ranking.CycleRankingId} Cycle", errors);
            Reference(empireIds, ranking.EmpireId, $"Cycle ranking {ranking.CycleRankingId} empire", errors);
            EnsureEmpireCycle(state, ranking.EmpireId, ranking.CycleId, $"Cycle ranking {ranking.CycleRankingId}", errors);
        }

        foreach (var item in state.CycleMajorEvents)
        {
            Reference(cycleIds, item.CycleId, $"Cycle major event {item.CycleMajorEventId} Cycle", errors);
            OptionalReference(systemIds, item.SystemId, $"Cycle major event {item.CycleMajorEventId} system", errors);
            OptionalReference(battleIds, item.SourceBattleId, $"Cycle major event {item.CycleMajorEventId} source battle", errors);
        }

        foreach (var signal in state.SystemHistoricalSignals)
        {
            Reference(cycleIds, signal.CycleId, $"System historical signal {signal.SystemHistoricalSignalId} Cycle", errors);
            Reference(systemIds, signal.SystemId, $"System historical signal {signal.SystemHistoricalSignalId} system", errors);
            OptionalReference(battleIds, signal.SourceBattleId, $"System historical signal {signal.SystemHistoricalSignalId} source battle", errors);
        }

        foreach (var outpost in state.ColonialOutposts)
        {
            Reference(cycleIds, outpost.CycleId, $"Colonial outpost {outpost.ColonialOutpostId} Cycle", errors);
            Reference(empireIds, outpost.EmpireId, $"Colonial outpost {outpost.ColonialOutpostId} empire", errors);
            Reference(systemIds, outpost.SystemId, $"Colonial outpost {outpost.ColonialOutpostId} system", errors);
            EnsureEmpireCycle(state, outpost.EmpireId, outpost.CycleId, $"Colonial outpost {outpost.ColonialOutpostId}", errors);
        }

        foreach (var relationship in state.DiplomaticRelationships)
        {
            Reference(cycleIds, relationship.CycleId, $"Diplomatic relationship {relationship.DiplomaticRelationshipId} Cycle", errors);
            Reference(empireIds, relationship.FirstEmpireId, $"Diplomatic relationship {relationship.DiplomaticRelationshipId} first empire", errors);
            Reference(empireIds, relationship.SecondEmpireId, $"Diplomatic relationship {relationship.DiplomaticRelationshipId} second empire", errors);
            if (relationship.FirstEmpireId == relationship.SecondEmpireId)
            {
                errors.Add($"Diplomatic relationship {relationship.DiplomaticRelationshipId} cannot relate an empire to itself.");
            }
            EnsureEmpireCycle(state, relationship.FirstEmpireId, relationship.CycleId, $"Diplomatic relationship {relationship.DiplomaticRelationshipId}", errors);
            EnsureEmpireCycle(state, relationship.SecondEmpireId, relationship.CycleId, $"Diplomatic relationship {relationship.DiplomaticRelationshipId}", errors);
        }

        foreach (var construction in state.ShipConstructions)
        {
            Reference(cycleIds, construction.CycleId, $"Ship construction {construction.ShipConstructionId} Cycle", errors);
            Reference(empireIds, construction.EmpireId, $"Ship construction {construction.ShipConstructionId} empire", errors);
            EnsureEmpireCycle(state, construction.EmpireId, construction.CycleId, $"Ship construction {construction.ShipConstructionId}", errors);
        }
    }

    private static void EnsureEmpireCycle(GameState state, Guid empireId, Guid cycleId, string label, List<string> errors)
    {
        if (state.Empires.FirstOrDefault(item => item.EmpireId == empireId) is { } empire && empire.CycleId != cycleId)
        {
            errors.Add($"{label} and its empire belong to different Cycles.");
        }
    }

    private static void ValidateTickAndRecoveryState(GameState state, List<string> errors)
    {
        foreach (var log in state.TickLogs)
        {
            var cycle = state.Cycles.FirstOrDefault(item => item.CycleId == log.CycleId);
            if (cycle is null)
            {
                errors.Add($"Tick log {log.TickLogId} references missing Cycle {log.CycleId}.");
                continue;
            }

            if (log.TickNumber <= 0 || log.TickNumber > cycle.CurrentTickNumber + 1)
            {
                errors.Add($"Tick log {log.TickLogId} has a tick number outside Cycle {cycle.CycleId}'s retained range.");
            }

            if (log.Status == TickLogStatus.Running && log.CompletedAt.HasValue)
            {
                errors.Add($"Running tick log {log.TickLogId} must not have a completion time.");
            }

            if (log.Status != TickLogStatus.Running && !log.CompletedAt.HasValue)
            {
                errors.Add($"Finished tick log {log.TickLogId} must have a completion time.");
            }

            if (log.CompletedAt < log.StartedAt)
            {
                errors.Add($"Tick log {log.TickLogId} completes before it starts.");
            }
        }

        foreach (var duplicate in state.TickLogs
                     .Where(log => log.Status == TickLogStatus.Completed)
                     .GroupBy(log => (log.CycleId, log.TickNumber))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Cycle {duplicate.Key.CycleId} has more than one completed attempt for tick {duplicate.Key.TickNumber}.");
        }

        foreach (var cycle in state.Cycles)
        {
            var unresolved = state.TickLogs.Any(log => log.CycleId == cycle.CycleId
                                                       && log.TickNumber == cycle.CurrentTickNumber + 1
                                                       && log.Status is TickLogStatus.Running or TickLogStatus.Failed);
            if (cycle.Status == CycleStatus.RecoveryRequired && !unresolved)
            {
                errors.Add($"RecoveryRequired Cycle {cycle.CycleId} has no unresolved next-tick attempt.");
            }

            if (cycle.Status == CycleStatus.Active && unresolved)
            {
                errors.Add($"Active Cycle {cycle.CycleId} has an unresolved next-tick attempt and must require recovery.");
            }
        }
    }

    private static void ValidateEmbeddedJson(GameState state, List<string> errors)
    {
        foreach (var (value, label) in state.Events.Select(item => (item.FactJson, $"Event {item.EventId} fact JSON"))
                     .Concat(state.BattleRecords.Select(item => (item.FactJson, $"Battle {item.BattleId} fact JSON")))
                     .Concat(state.CycleMajorEvents.Select(item => (item.FactJson, $"Cycle major event {item.CycleMajorEventId} fact JSON")))
                     .Concat(state.SystemHistoricalSignals.Select(item => (item.FactJson, $"System historical signal {item.SystemHistoricalSignalId} fact JSON")))
                     .Concat(state.ChronicleEntries.Select(item => (item.NarrativeContextJson, $"Chronicle entry {item.ChronicleEntryId} narrative context JSON"))))
        {
            try
            {
                using var _ = JsonDocument.Parse(value);
            }
            catch (Exception exception) when (exception is JsonException or ArgumentException)
            {
                errors.Add($"{label} is invalid: {exception.Message}");
            }
        }
    }

    private static void Unique<T>(IEnumerable<T> items, Func<T, Guid> id, string collection, List<string> errors)
    {
        foreach (var item in items)
        {
            if (id(item) == Guid.Empty)
            {
                errors.Add($"state.{collection} contains an empty identifier.");
            }
        }

        foreach (var duplicate in items.GroupBy(id).Where(group => group.Key != Guid.Empty && group.Count() > 1))
        {
            errors.Add($"state.{collection} contains duplicate identifier {duplicate.Key}.");
        }
    }

    private static void Reference(IEnumerable<Guid> ids, Guid id, string label, List<string> errors)
    {
        if (id == Guid.Empty || !ids.Contains(id))
        {
            errors.Add($"{label} {id} does not exist.");
        }
    }

    private static void OptionalReference(IEnumerable<Guid> ids, Guid? id, string label, List<string> errors)
    {
        if (id.HasValue)
        {
            Reference(ids, id.Value, label, errors);
        }
    }

    private static void Required(string value, string error, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(error);
        }
    }

    private static void ValidateFleetIdList(string value, IEnumerable<Guid> fleetIds, string label, List<string> errors)
    {
        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            errors.Add($"{label} is empty.");
            return;
        }

        foreach (var part in parts)
        {
            if (!Guid.TryParse(part, out var fleetId))
            {
                errors.Add($"{label} contains invalid fleet identifier '{part}'.");
            }
            else
            {
                Reference(fleetIds, fleetId, label, errors);
            }
        }
    }

    private static JsonElement RequireProperty(JsonElement parent, string name) =>
        parent.TryGetProperty(name, out var value)
            ? value
            : throw new InvalidOperationException($"The state transfer document is missing required property '{name}'.");

    private static InvalidOperationException InvalidState(GameStateValidationResult validation) =>
        new($"The state transfer is invalid:{Environment.NewLine}- {string.Join($"{Environment.NewLine}- ", validation.Errors)}");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(GameStateJson.Options);
        options.Converters.Clear();
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false));
        return options;
    }
}
