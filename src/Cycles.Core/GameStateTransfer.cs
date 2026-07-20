using System.Reflection;
using System.Security.Cryptography;
using System.Text;
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
    public const int CurrentFormatVersion = 7;

    private static readonly JsonSerializerOptions TransferJsonOptions = CreateJsonOptions();
    private static readonly PropertyInfo[] PersistedCollections = typeof(GameState)
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.PropertyType.IsGenericType
                           && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>))
        .ToArray();

    public static void Write(Stream destination, GameState state, DateTimeOffset exportedAt)
    {
        ArgumentNullException.ThrowIfNull(destination);
        UpgradeLegacyOwnershipModel(state);
        UpgradeTransitTiming(state);
        UpgradeDoctrineUnlocks(state);
        EnrichLegacyGameFoundationForWrite(state);
        CycleScheduling.NormalizePersistedSchedule(state);
        UpgradeLegacyMatchParticipantGameScope(state);
        BattleFleetParticipantCompatibility.UpgradeLegacyMembership(state);
        BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
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

            if (formatVersion is < 1 or > CurrentFormatVersion)
            {
                throw new InvalidOperationException($"Unsupported state transfer formatVersion {formatVersion}; this CLI supports versions 1 through {CurrentFormatVersion}.");
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
                if ((formatVersion == 1
                     && property.Name == nameof(GameState.Sectors)
                     && !stateElement.TryGetProperty(jsonName, out _))
                    || (formatVersion < 3
                        && property.Name is nameof(GameState.Factions) or nameof(GameState.MatchParticipants)
                        && !stateElement.TryGetProperty(jsonName, out _))
                    || (formatVersion < 4
                        && property.Name == nameof(GameState.EmpireDoctrineUnlocks)
                        && !stateElement.TryGetProperty(jsonName, out _))
                    || (formatVersion < 5
                        && IsGameFoundationCollection(property.Name)
                        && !stateElement.TryGetProperty(jsonName, out _))
                    || (formatVersion < 6
                        && property.Name == nameof(GameState.BattleFleetParticipants)
                        && !stateElement.TryGetProperty(jsonName, out _)))
                {
                    continue;
                }

                var collection = RequireProperty(stateElement, jsonName);
                if (collection.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"The state transfer state.{jsonName} property must contain an array.");
                }
            }

            if (formatVersion >= 7)
            {
                foreach (var configuration in RequireProperty(stateElement, "cycleConfigurations").EnumerateArray())
                {
                    _ = RequireProperty(configuration, "schedulingMode");
                }

                foreach (var cycle in RequireProperty(stateElement, "cycles").EnumerateArray())
                {
                    _ = RequireProperty(cycle, "schedulingMode");
                    _ = RequireProperty(cycle, "nextTickAt");
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

            UpgradeLegacyOwnershipModel(document.State);
            UpgradeTransitTiming(document.State);
            if (formatVersion < 4)
            {
                UpgradeDoctrineUnlocks(document.State);
            }
            if (formatVersion < 5)
            {
                LegacyGameFoundation.Apply(document.State);
            }
            if (formatVersion < 6)
            {
                UpgradeLegacyMatchParticipantGameScope(document.State);
                BattleFleetParticipantCompatibility.UpgradeLegacyMembership(document.State);
                BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(document.State);
            }
            if (formatVersion < 7)
            {
                CycleScheduling.NormalizePersistedSchedule(
                    document.State,
                    upgradeLegacyFormat: true);
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

    public static GameState ReadLegacyRuntimeState(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        JsonDocument json;
        try
        {
            json = JsonDocument.Parse(source);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException($"The legacy runtime state file is not valid JSON: {exception.Message}", exception);
        }

        using (json)
        {
            var root = json.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException("The legacy runtime state file must contain a JSON object.");
            }

            foreach (var property in PersistedCollections)
            {
                var jsonName = JsonNamingPolicy.CamelCase.ConvertName(property.Name);
                if ((property.Name == nameof(GameState.Sectors)
                     || property.Name == nameof(GameState.Factions)
                     || property.Name == nameof(GameState.MatchParticipants)
                     || property.Name == nameof(GameState.EmpireDoctrineUnlocks)
                     || property.Name == nameof(GameState.BattleFleetParticipants)
                     || IsGameFoundationCollection(property.Name))
                    && !root.TryGetProperty(jsonName, out _))
                {
                    continue;
                }

                var collection = RequireProperty(root, jsonName);
                if (collection.ValueKind != JsonValueKind.Array)
                {
                    throw new InvalidOperationException($"The legacy runtime state {jsonName} property must contain an array.");
                }
            }

            GameState state;
            try
            {
                state = root.Deserialize<GameState>(GameStateJson.Options)
                    ?? throw new InvalidOperationException("The legacy runtime state could not be deserialised.");
            }
            catch (JsonException exception)
            {
                throw new InvalidOperationException($"The legacy runtime state has an invalid value: {exception.Message}", exception);
            }

            UpgradeLegacyOwnershipModel(state);
            UpgradeTransitTiming(state);
            UpgradeDoctrineUnlocks(state);
            LegacyGameFoundation.Apply(state);
            UpgradeLegacyMatchParticipantGameScope(state);
            BattleFleetParticipantCompatibility.UpgradeLegacyMembership(state);
            BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
            CycleScheduling.NormalizePersistedSchedule(state, upgradeLegacyFormat: true);
            StrategicPriorityPolicy.Normalize(state);
            var validation = Validate(state);
            if (!validation.IsValid)
            {
                throw InvalidState(validation);
            }

            return state;
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
        ValidateGameFoundation(state, errors);
        ValidateCyclesAndReferences(state, errors);
        ValidateTickAndRecoveryState(state, errors);
        ValidateEmbeddedJson(state, errors);
        return new GameStateValidationResult(errors);
    }

    private static void UpgradeLegacyOwnershipModel(GameState state)
    {
        foreach (var empire in state.Empires)
        {
            if (!state.Factions.Any(item => item.EmpireId == empire.EmpireId))
            {
                state.Factions.Add(new Faction
                {
                    FactionId = empire.EmpireId,
                    CycleId = empire.CycleId,
                    EmpireId = empire.EmpireId,
                    FactionName = empire.EmpireName,
                    Kind = FactionKind.Empire,
                    Status = empire.Status == EmpireStatus.Active ? FactionStatus.Active : FactionStatus.Defeated,
                    CreatedAt = empire.CreatedAt
                });
            }

            if (!state.MatchParticipants.Any(item => item.CycleId == empire.CycleId && item.PlayerId == empire.PlayerId))
            {
                var cycle = state.Cycles.Single(item => item.CycleId == empire.CycleId);
                var status = empire.Status == EmpireStatus.Defeated
                    ? MatchParticipantStatus.Defeated
                    : cycle.Status == CycleStatus.Completed
                        ? MatchParticipantStatus.Completed
                        : MatchParticipantStatus.Active;
                state.MatchParticipants.Add(new MatchParticipant
                {
                    MatchParticipantId = CreateLegacyParticipantId(empire.CycleId, empire.PlayerId),
                    GameId = cycle.GameId ?? Guid.Empty,
                    CycleId = empire.CycleId,
                    PlayerId = empire.PlayerId,
                    EmpireId = empire.EmpireId,
                    Status = status,
                    JoinedAt = empire.CreatedAt,
                    EndedAt = status == MatchParticipantStatus.Active
                        ? null
                        : cycle.Status == CycleStatus.Completed
                            ? cycle.EndAt
                            : empire.CreatedAt
                });
            }
        }

        foreach (var fleet in state.Fleets.Where(item => item.FactionId == Guid.Empty && item.EmpireId != Guid.Empty))
        {
            fleet.FactionId = fleet.EmpireId;
        }

        foreach (var order in state.FleetOrders.Where(item => !item.TargetFactionId.HasValue && item.TargetEmpireId.HasValue))
        {
            order.TargetFactionId = order.TargetEmpireId;
        }

        foreach (var item in state.Events.Where(item => !item.FactionId.HasValue && item.EmpireId.HasValue))
        {
            item.FactionId = item.EmpireId;
        }

        foreach (var battle in state.BattleRecords)
        {
            battle.AttackerFactionId = battle.AttackerFactionId == Guid.Empty ? battle.AttackerEmpireId : battle.AttackerFactionId;
            battle.DefenderFactionId = battle.DefenderFactionId == Guid.Empty ? battle.DefenderEmpireId : battle.DefenderFactionId;
        }
    }

    private static void UpgradeTransitTiming(GameState state)
    {
        foreach (var fleet in state.Fleets)
        {
            if (fleet is not
                {
                    Status: FleetStatus.InTransit,
                    DepartureTickNumber: null,
                    DestinationSystemId: { } destinationSystemId,
                    ArrivalTickNumber: { } arrivalTickNumber
                })
            {
                continue;
            }

            var link = state.SystemLinks.SingleOrDefault(item => item.CycleId == fleet.CycleId
                                                                 && item.Connects(fleet.CurrentSystemId, destinationSystemId));
            if (link is not null)
            {
                fleet.DepartureTickNumber = arrivalTickNumber - link.TravelTicks + 1;
            }
        }
    }

    private static Guid CreateLegacyParticipantId(Guid cycleId, Guid playerId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"cycles-participant:{cycleId:N}:{playerId:N}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void UpgradeDoctrineUnlocks(GameState state)
    {
        foreach (var item in state.Events
                     .Where(item => item.EventType == EventType.DoctrineUnlocked && item.EmpireId.HasValue)
                     .OrderBy(item => item.TickNumber)
                     .ThenBy(item => item.CreatedAt)
                     .ThenBy(item => item.EventId))
        {
            var doctrineKey = ReadDoctrineKey(item.FactJson);
            var empireId = item.EmpireId.GetValueOrDefault();
            if (string.IsNullOrWhiteSpace(doctrineKey)
                || state.EmpireDoctrineUnlocks.Any(unlock => unlock.CycleId == item.CycleId
                                                             && unlock.EmpireId == empireId
                                                             && string.Equals(
                                                                 unlock.DoctrineKey,
                                                                 doctrineKey,
                                                                 StringComparison.Ordinal)))
            {
                continue;
            }

            state.EmpireDoctrineUnlocks.Add(new EmpireDoctrineUnlock
            {
                EmpireDoctrineUnlockId = CreateLegacyDoctrineUnlockId(item.CycleId, empireId, doctrineKey),
                CycleId = item.CycleId,
                EmpireId = empireId,
                DoctrineKey = doctrineKey,
                UnlockedTickNumber = item.TickNumber,
                UnlockedAt = item.CreatedAt
            });
        }
    }

    private static void EnrichLegacyGameFoundationForWrite(GameState state)
    {
        if (state.Cycles.Count == 0)
        {
            return;
        }

        var isUnadaptedLegacyState = state.Games.Count == 0;
        var isIncompleteLegacyFoundation = state.Games.Count == 1
            && state.Games[0].GameId == GameFoundationConstants.LegacyGameId
            && (state.Cycles.Any(cycle =>
                    cycle.GameId is null
                    || cycle.CycleConfigurationId is null
                    || !state.CycleConfigurations.Any(configuration =>
                        configuration.CycleConfigurationId == cycle.CycleConfigurationId.Value))
                || state.MatchParticipants.Any(participant =>
                    participant.GameId == Guid.Empty
                    || !state.GameEnrolments.Any(enrolment =>
                        enrolment.GameId == GameFoundationConstants.LegacyGameId
                        && enrolment.PlayerId == participant.PlayerId))
                || state.GameLifecycleEvents.All(gameEvent =>
                    gameEvent.GameLifecycleEventId != GameFoundationConstants.LegacyLifecycleEventId));

        if (isUnadaptedLegacyState || isIncompleteLegacyFoundation)
        {
            LegacyGameFoundation.Apply(state);
        }
    }

    private static void UpgradeLegacyMatchParticipantGameScope(GameState state)
    {
        var cyclesById = state.Cycles
            .GroupBy(item => item.CycleId)
            .ToDictionary(group => group.Key, group => group.First());
        foreach (var participant in state.MatchParticipants.Where(item => item.GameId == Guid.Empty))
        {
            if (!cyclesById.TryGetValue(participant.CycleId, out var cycle)
                || !cycle.GameId.HasValue)
            {
                throw new InvalidOperationException(
                    $"Match participant {participant.MatchParticipantId} has no legacy Game scope to adapt.");
            }

            participant.GameId = cycle.GameId.Value;
        }
    }

    private static bool IsGameFoundationCollection(string propertyName) => propertyName is
        nameof(GameState.Games)
        or nameof(GameState.CycleConfigurations)
        or nameof(GameState.GameEnrolments)
        or nameof(GameState.GameLifecycleEvents);

    private static string? ReadDoctrineKey(string factJson)
    {
        try
        {
            using var document = JsonDocument.Parse(factJson);
            return document.RootElement.TryGetProperty("doctrine", out var doctrine)
                   && doctrine.ValueKind == JsonValueKind.String
                ? doctrine.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Guid CreateLegacyDoctrineUnlockId(Guid cycleId, Guid empireId, string doctrineKey)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(
            $"cycles-doctrine-unlock:{cycleId:N}:{empireId:N}:{doctrineKey}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    public static int CountRecords(GameState state) =>
        PersistedCollections.Sum(property => ((System.Collections.ICollection)property.GetValue(state)!).Count);

    private static void ValidateIdentifiers(GameState state, List<string> errors)
    {
        Unique(state.Players, item => item.PlayerId, "players", errors);
        Unique(state.AdminRoleAuditRecords, item => item.AdminRoleAuditRecordId, "adminRoleAuditRecords", errors);
        Unique(state.Games, item => item.GameId, "games", errors);
        Unique(state.CycleConfigurations, item => item.CycleConfigurationId, "cycleConfigurations", errors);
        Unique(state.GameEnrolments, item => item.GameEnrolmentId, "gameEnrolments", errors);
        Unique(state.GameLifecycleEvents, item => item.GameLifecycleEventId, "gameLifecycleEvents", errors);
        Unique(state.Cycles, item => item.CycleId, "cycles", errors);
        Unique(state.Empires, item => item.EmpireId, "empires", errors);
        Unique(state.Factions, item => item.FactionId, "factions", errors);
        Unique(state.MatchParticipants, item => item.MatchParticipantId, "matchParticipants", errors);
        Unique(state.EmpireResources, item => item.EmpireResourceId, "empireResources", errors);
        Unique(state.EmpireDoctrineUnlocks, item => item.EmpireDoctrineUnlockId, "empireDoctrineUnlocks", errors);
        Unique(state.EmpirePriorities, item => item.EmpirePriorityId, "empirePriorities", errors);
        Unique(state.EmpireMetrics, item => item.EmpireMetricId, "empireMetrics", errors);
        Unique(state.CycleRankings, item => item.CycleRankingId, "cycleRankings", errors);
        Unique(state.CycleMajorEvents, item => item.CycleMajorEventId, "cycleMajorEvents", errors);
        Unique(state.SystemHistoricalSignals, item => item.SystemHistoricalSignalId, "systemHistoricalSignals", errors);
        Unique(state.ColonialOutposts, item => item.ColonialOutpostId, "colonialOutposts", errors);
        Unique(state.DiplomaticRelationships, item => item.DiplomaticRelationshipId, "diplomaticRelationships", errors);
        Unique(state.Admirals, item => item.AdmiralId, "admirals", errors);
        Unique(state.AdmiralBattleHistories, item => item.AdmiralBattleHistoryId, "admiralBattleHistories", errors);
        Unique(state.Sectors, item => item.SectorId, "sectors", errors);
        Unique(state.Systems, item => item.SystemId, "systems", errors);
        Unique(state.SystemLinks, item => item.SystemLinkId, "systemLinks", errors);
        Unique(state.Fleets, item => item.FleetId, "fleets", errors);
        Unique(state.FleetOrders, item => item.FleetOrderId, "fleetOrders", errors);
        Unique(state.ShipConstructions, item => item.ShipConstructionId, "shipConstructions", errors);
        Unique(state.TickLogs, item => item.TickLogId, "tickLogs", errors);
        Unique(state.Events, item => item.EventId, "events", errors);
        Unique(state.BattleRecords, item => item.BattleId, "battleRecords", errors);
        foreach (var item in state.BattleFleetParticipants)
        {
            if (item.BattleId == Guid.Empty || item.CycleId == Guid.Empty || item.FleetId == Guid.Empty)
            {
                errors.Add("state.battleFleetParticipants contains an empty identifier.");
            }
        }
        foreach (var duplicate in state.BattleFleetParticipants
                     .GroupBy(item => (item.BattleId, item.FleetId))
                     .Where(group => group.Key.BattleId != Guid.Empty
                                     && group.Key.FleetId != Guid.Empty
                                     && group.Count() > 1))
        {
            errors.Add(
                $"state.battleFleetParticipants contains duplicate Battle/Fleet membership {duplicate.Key.BattleId}/{duplicate.Key.FleetId}.");
        }
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

    private static void ValidateGameFoundation(GameState state, List<string> errors)
    {
        if (state.Games.Count == 0)
        {
            errors.Add("At least one Game is required.");
        }

        var playerIds = state.Players.Select(item => item.PlayerId).ToHashSet();
        var gamesById = state.Games
            .GroupBy(item => item.GameId)
            .ToDictionary(group => group.Key, group => group.First());
        var configurationsById = state.CycleConfigurations
            .GroupBy(item => item.CycleConfigurationId)
            .ToDictionary(group => group.Key, group => group.First());
        var cyclesById = state.Cycles
            .GroupBy(item => item.CycleId)
            .ToDictionary(group => group.Key, group => group.First());

        foreach (var game in state.Games)
        {
            Required(game.Name, $"Game {game.GameId} has no name.", errors);
            Required(game.GamePolicyKey, $"Game {game.GameId} has no policy key.", errors);
            if (game.GamePolicyVersion <= 0)
            {
                errors.Add($"Game {game.GameId} has an invalid policy version.");
            }

            ValidateContentHash(
                game.GamePolicyContentHash,
                $"Game {game.GameId} policy content hash",
                errors);
            OptionalReference(playerIds, game.CreatedByPlayerId, $"Game {game.GameId} creator", errors);
            if (game.PolicyProvenanceStatus == ProvenanceStatus.Verified)
            {
                Required(game.GamePolicyContentHash, $"Verified Game {game.GameId} has no policy content hash.", errors);
            }
            else if (game.CreationSource != GameCreationSource.LegacyImport)
            {
                errors.Add($"Game {game.GameId} uses legacy-unverified policy provenance without a legacy import source.");
            }

            ValidateGameLifecycle(game, state.Cycles, errors);
        }

        foreach (var duplicate in state.CycleConfigurations
                     .GroupBy(item => (item.GameId, item.SequenceNumber))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Game {duplicate.Key.GameId} has more than one Cycle configuration at sequence {duplicate.Key.SequenceNumber}.");
        }

        foreach (var configuration in state.CycleConfigurations)
        {
            Reference(gamesById.Keys, configuration.GameId, $"Cycle configuration {configuration.CycleConfigurationId} Game", errors);
            if (configuration.SequenceNumber <= 0)
            {
                errors.Add($"Cycle configuration {configuration.CycleConfigurationId} has an invalid sequence number.");
            }

            OptionalPositive(
                configuration.TickLengthMinutes,
                $"Cycle configuration {configuration.CycleConfigurationId} tick length",
                errors);
            ValidateHumanSeatBounds(configuration, errors);
            if (configuration.ScheduledStartAt.HasValue
                && configuration.ScheduledEndAt.HasValue
                && configuration.ScheduledEndAt.Value <= configuration.ScheduledStartAt.Value)
            {
                errors.Add($"Cycle configuration {configuration.CycleConfigurationId} scheduled end must follow its scheduled start.");
            }
            ValidateConfigurationStatusTimestamps(configuration, errors);
            ValidateProfileProvenance(configuration, gamesById, errors);

            var materializedCycles = state.Cycles.Count(cycle =>
                cycle.CycleConfigurationId == configuration.CycleConfigurationId);
            if (configuration.Status == CycleConfigurationStatus.Materialized)
            {
                if (materializedCycles != 1)
                {
                    errors.Add($"Materialized Cycle configuration {configuration.CycleConfigurationId} must belong to exactly one Cycle.");
                }
            }
            else if (materializedCycles != 0)
            {
                errors.Add($"Cycle configuration {configuration.CycleConfigurationId} is referenced by a Cycle while its status is {configuration.Status}.");
            }
        }

        foreach (var duplicate in state.Cycles
                     .Where(item => item.CycleConfigurationId.HasValue)
                     .GroupBy(item => item.CycleConfigurationId!.Value)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Cycle configuration {duplicate.Key} materializes more than one Cycle.");
        }

        foreach (var cycle in state.Cycles)
        {
            if (!cycle.GameId.HasValue)
            {
                errors.Add($"Cycle {cycle.CycleId} has no Game.");
            }
            else
            {
                Reference(gamesById.Keys, cycle.GameId.Value, $"Cycle {cycle.CycleId} Game", errors);
            }

            if (!cycle.CycleConfigurationId.HasValue)
            {
                errors.Add($"Cycle {cycle.CycleId} has no Cycle configuration.");
            }
            else
            {
                Reference(
                    configurationsById.Keys,
                    cycle.CycleConfigurationId.Value,
                    $"Cycle {cycle.CycleId} configuration",
                    errors);
                if (configurationsById.TryGetValue(cycle.CycleConfigurationId.Value, out var configuration))
                {
                    if (cycle.GameId != configuration.GameId)
                    {
                        errors.Add($"Cycle {cycle.CycleId} and its configuration belong to different Games.");
                    }
                    if (configuration.Status != CycleConfigurationStatus.Materialized)
                    {
                        errors.Add($"Cycle {cycle.CycleId} references a configuration that is not materialized.");
                    }
                    ValidateCycleProvenance(cycle, configuration, errors);
                }
            }

            if (cycle.PreviousCycleId.HasValue)
            {
                Reference(cyclesById.Keys, cycle.PreviousCycleId.Value, $"Cycle {cycle.CycleId} predecessor", errors);
                if (cycle.PreviousCycleId == cycle.CycleId)
                {
                    errors.Add($"Cycle {cycle.CycleId} cannot be its own predecessor.");
                }
                else if (cyclesById.TryGetValue(cycle.PreviousCycleId.Value, out var previous)
                         && previous.GameId != cycle.GameId)
                {
                    errors.Add($"Cycle {cycle.CycleId} and its predecessor belong to different Games.");
                }
            }
        }

        ValidateCycleLineage(state.Cycles, cyclesById, configurationsById, errors);

        foreach (var duplicate in state.GameEnrolments
                     .GroupBy(item => (item.GameId, item.PlayerId))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Player {duplicate.Key.PlayerId} has more than one enrolment in Game {duplicate.Key.GameId}.");
        }

        foreach (var enrolment in state.GameEnrolments)
        {
            Reference(gamesById.Keys, enrolment.GameId, $"Game enrolment {enrolment.GameEnrolmentId} Game", errors);
            Reference(playerIds, enrolment.PlayerId, $"Game enrolment {enrolment.GameEnrolmentId} player", errors);
            if (enrolment.StatusChangedAt < enrolment.EnrolledAt)
            {
                errors.Add($"Game enrolment {enrolment.GameEnrolmentId} changes status before enrolment.");
            }

            var shouldHaveEnded = enrolment.Status is GameEnrolmentStatus.Completed or GameEnrolmentStatus.Withdrawn;
            if (shouldHaveEnded != enrolment.EndedAt.HasValue)
            {
                errors.Add($"Game enrolment {enrolment.GameEnrolmentId} status and end timestamp disagree.");
            }
        }

        foreach (var participant in state.MatchParticipants)
        {
            Reference(gamesById.Keys, participant.GameId, $"Match participant {participant.MatchParticipantId} Game", errors);
            var cycle = cyclesById.GetValueOrDefault(participant.CycleId);
            if (cycle?.GameId is { } cycleGameId && cycleGameId != participant.GameId)
            {
                errors.Add(
                    $"Match participant {participant.MatchParticipantId} and Cycle {participant.CycleId} belong to different Games.");
            }
            if (!state.GameEnrolments.Any(enrolment => enrolment.GameId == participant.GameId
                                                          && enrolment.PlayerId == participant.PlayerId))
            {
                errors.Add(
                    $"Match participant {participant.MatchParticipantId} has no enrolment in Game {participant.GameId}.");
            }
        }

        foreach (var gameEvent in state.GameLifecycleEvents)
        {
            Reference(gamesById.Keys, gameEvent.GameId, $"Game lifecycle event {gameEvent.GameLifecycleEventId} Game", errors);
            OptionalReference(playerIds, gameEvent.SubjectPlayerId, $"Game lifecycle event {gameEvent.GameLifecycleEventId} subject player", errors);
            OptionalReference(playerIds, gameEvent.ActorPlayerId, $"Game lifecycle event {gameEvent.GameLifecycleEventId} actor player", errors);
            if (!Enum.IsDefined(typeof(GameLifecycleEventType), gameEvent.Type))
            {
                errors.Add($"Game lifecycle event {gameEvent.GameLifecycleEventId} has an invalid event type '{gameEvent.Type}'.");
            }
            ValidateLifecycleStatus(gameEvent.FromStatus, gameEvent.GameLifecycleEventId, "from", errors);
            ValidateLifecycleStatus(gameEvent.ToStatus, gameEvent.GameLifecycleEventId, "to", errors);
        }
    }

    private static void ValidateGameLifecycle(
        Game game,
        IReadOnlyCollection<Cycle> cycles,
        List<string> errors)
    {
        var terminalTimestampsAgree = game.Status switch
        {
            GameLifecycleStatus.Completed => game.CompletedAt.HasValue
                && !game.CancelledAt.HasValue
                && !game.TerminatedAt.HasValue,
            GameLifecycleStatus.Cancelled => !game.CompletedAt.HasValue
                && game.CancelledAt.HasValue
                && !game.TerminatedAt.HasValue,
            GameLifecycleStatus.Terminated => !game.CompletedAt.HasValue
                && !game.CancelledAt.HasValue
                && game.TerminatedAt.HasValue,
            GameLifecycleStatus.Forming
                or GameLifecycleStatus.Starting
                or GameLifecycleStatus.Active
                or GameLifecycleStatus.Intermission => !game.CompletedAt.HasValue
                    && !game.CancelledAt.HasValue
                    && !game.TerminatedAt.HasValue,
            _ => false
        };
        if (!terminalTimestampsAgree)
        {
            errors.Add($"Game {game.GameId} status and terminal timestamps disagree.");
        }

        if (game.Status is not (GameLifecycleStatus.Forming
            or GameLifecycleStatus.Starting
            or GameLifecycleStatus.Cancelled)
            && !game.FirstStartedAt.HasValue)
        {
            errors.Add($"Game {game.GameId} status {game.Status} requires a first-start timestamp.");
        }

        var operationalCycleCount = cycles.Count(cycle =>
            cycle.GameId == game.GameId
            && cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired);
        if (game.Status == GameLifecycleStatus.Active && operationalCycleCount != 1)
        {
            errors.Add($"Active Game {game.GameId} must have exactly one Active or RecoveryRequired Cycle.");
        }
        else if (game.Status != GameLifecycleStatus.Active && operationalCycleCount != 0)
        {
            errors.Add($"Game {game.GameId} cannot retain an Active or RecoveryRequired Cycle while its status is {game.Status}.");
        }
    }

    private static void ValidateHumanSeatBounds(CycleConfiguration configuration, List<string> errors)
    {
        var bothAbsent = !configuration.MinimumHumanSeats.HasValue
            && !configuration.MaximumHumanSeats.HasValue;
        var validBounds = configuration.MinimumHumanSeats is > 0
            && configuration.MaximumHumanSeats.HasValue
            && configuration.MaximumHumanSeats.Value >= configuration.MinimumHumanSeats.Value;
        if (!bothAbsent && !validBounds)
        {
            errors.Add(
                $"Cycle configuration {configuration.CycleConfigurationId} human-seat bounds must both be absent or form a positive minimum/maximum range.");
        }
    }

    private static void ValidateConfigurationStatusTimestamps(
        CycleConfiguration configuration,
        List<string> errors)
    {
        var timestampsAgree = configuration.Status switch
        {
            CycleConfigurationStatus.Draft => !configuration.LockedAt.HasValue
                && !configuration.MaterializedAt.HasValue
                && !configuration.CancelledAt.HasValue,
            CycleConfigurationStatus.Locked => configuration.LockedAt.HasValue
                && !configuration.MaterializedAt.HasValue
                && !configuration.CancelledAt.HasValue,
            CycleConfigurationStatus.Materialized => configuration.LockedAt.HasValue
                && configuration.MaterializedAt.HasValue
                && !configuration.CancelledAt.HasValue,
            CycleConfigurationStatus.Cancelled => !configuration.MaterializedAt.HasValue
                && configuration.CancelledAt.HasValue,
            _ => false
        };
        if (!timestampsAgree)
        {
            errors.Add(
                $"Cycle configuration {configuration.CycleConfigurationId} status and lock, materialization or cancellation timestamps disagree.");
        }
    }

    private static void ValidateCycleLineage(
        IReadOnlyCollection<Cycle> cycles,
        IReadOnlyDictionary<Guid, Cycle> cyclesById,
        IReadOnlyDictionary<Guid, CycleConfiguration> configurationsById,
        List<string> errors)
    {
        foreach (var fork in cycles
                     .Where(cycle => cycle.PreviousCycleId.HasValue)
                     .GroupBy(cycle => cycle.PreviousCycleId!.Value)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Cycle {fork.Key} has more than one direct successor.");
        }

        foreach (var cycle in cycles.Where(item => item.PreviousCycleId.HasValue))
        {
            if (!cyclesById.TryGetValue(cycle.PreviousCycleId!.Value, out var previous)
                || !cycle.CycleConfigurationId.HasValue
                || !previous.CycleConfigurationId.HasValue
                || !configurationsById.TryGetValue(cycle.CycleConfigurationId.Value, out var configuration)
                || !configurationsById.TryGetValue(previous.CycleConfigurationId.Value, out var previousConfiguration))
            {
                continue;
            }

            if (previousConfiguration.SequenceNumber >= configuration.SequenceNumber)
            {
                errors.Add(
                    $"Cycle {cycle.CycleId} must have a higher configuration sequence than predecessor {previous.CycleId}.");
            }
        }

        var visitStates = new Dictionary<Guid, byte>();
        var cycleReported = false;
        foreach (var cycle in cycles)
        {
            Visit(cycle.CycleId);
        }

        void Visit(Guid cycleId)
        {
            if (visitStates.TryGetValue(cycleId, out var visitState))
            {
                if (visitState == 1 && !cycleReported)
                {
                    errors.Add($"Cycle lineage contains a predecessor cycle involving Cycle {cycleId}.");
                    cycleReported = true;
                }
                return;
            }

            visitStates[cycleId] = 1;
            if (cyclesById.TryGetValue(cycleId, out var cycle)
                && cycle.PreviousCycleId.HasValue
                && cyclesById.ContainsKey(cycle.PreviousCycleId.Value))
            {
                Visit(cycle.PreviousCycleId.Value);
            }
            visitStates[cycleId] = 2;
        }
    }

    private static void ValidateProfileProvenance(
        CycleConfiguration configuration,
        IReadOnlyDictionary<Guid, Game> gamesById,
        List<string> errors)
    {
        Required(configuration.MapProfileKey, $"Cycle configuration {configuration.CycleConfigurationId} has no map profile key.", errors);
        Required(configuration.ScenarioProfileKey, $"Cycle configuration {configuration.CycleConfigurationId} has no scenario profile key.", errors);
        Required(configuration.CyclePolicyKey, $"Cycle configuration {configuration.CycleConfigurationId} has no Cycle policy key.", errors);
        ValidateContentHash(
            configuration.MapProfileContentHash,
            $"Cycle configuration {configuration.CycleConfigurationId} map profile content hash",
            errors);
        ValidateContentHash(
            configuration.ScenarioProfileContentHash,
            $"Cycle configuration {configuration.CycleConfigurationId} scenario profile content hash",
            errors);
        ValidateContentHash(
            configuration.CyclePolicyContentHash,
            $"Cycle configuration {configuration.CycleConfigurationId} Cycle policy content hash",
            errors);
        OptionalPositive(configuration.MapProfileVersion, $"Cycle configuration {configuration.CycleConfigurationId} map profile version", errors);
        OptionalPositive(configuration.ScenarioProfileVersion, $"Cycle configuration {configuration.CycleConfigurationId} scenario profile version", errors);
        if (configuration.CyclePolicyVersion <= 0)
        {
            errors.Add($"Cycle configuration {configuration.CycleConfigurationId} has an invalid Cycle policy version.");
        }

        if (configuration.ProvenanceStatus == ProvenanceStatus.Verified)
        {
            if (!configuration.MapProfileVersion.HasValue || !configuration.ScenarioProfileVersion.HasValue)
            {
                errors.Add($"Verified Cycle configuration {configuration.CycleConfigurationId} must retain map and scenario profile versions.");
            }
            Required(configuration.MapProfileContentHash, $"Verified Cycle configuration {configuration.CycleConfigurationId} has no map profile content hash.", errors);
            Required(configuration.ScenarioProfileContentHash, $"Verified Cycle configuration {configuration.CycleConfigurationId} has no scenario profile content hash.", errors);
            Required(configuration.CyclePolicyContentHash, $"Verified Cycle configuration {configuration.CycleConfigurationId} has no Cycle policy content hash.", errors);
        }
        else if (gamesById.TryGetValue(configuration.GameId, out var game)
                 && game.CreationSource != GameCreationSource.LegacyImport)
        {
            errors.Add($"Cycle configuration {configuration.CycleConfigurationId} uses legacy-unverified provenance outside a legacy Game.");
        }
    }

    private static void ValidateCycleProvenance(
        Cycle cycle,
        CycleConfiguration configuration,
        List<string> errors)
    {
        ValidateContentHash(cycle.MapProfileContentHash, $"Cycle {cycle.CycleId} map profile content hash", errors);
        ValidateContentHash(cycle.ScenarioProfileContentHash, $"Cycle {cycle.CycleId} scenario profile content hash", errors);
        ValidateContentHash(cycle.CyclePolicyContentHash, $"Cycle {cycle.CycleId} Cycle policy content hash", errors);

        if (!cycle.ProfileProvenanceStatus.HasValue)
        {
            errors.Add($"Cycle {cycle.CycleId} has no profile provenance status.");
            return;
        }

        if (cycle.ProfileProvenanceStatus.Value != configuration.ProvenanceStatus
            || !string.Equals(cycle.MapProfileKey, configuration.MapProfileKey, StringComparison.Ordinal)
            || cycle.MapProfileVersion != configuration.MapProfileVersion
            || !string.Equals(cycle.MapProfileContentHash, configuration.MapProfileContentHash, StringComparison.Ordinal)
            || cycle.MapSeed != configuration.MapSeed
            || !string.Equals(cycle.ScenarioProfileKey, configuration.ScenarioProfileKey, StringComparison.Ordinal)
            || cycle.ScenarioProfileVersion != configuration.ScenarioProfileVersion
            || !string.Equals(cycle.ScenarioProfileContentHash, configuration.ScenarioProfileContentHash, StringComparison.Ordinal)
            || cycle.ScenarioSeed != configuration.ScenarioSeed
            || !string.Equals(cycle.CyclePolicyKey, configuration.CyclePolicyKey, StringComparison.Ordinal)
            || cycle.CyclePolicyVersion != configuration.CyclePolicyVersion
            || !string.Equals(cycle.CyclePolicyContentHash, configuration.CyclePolicyContentHash, StringComparison.Ordinal)
            || cycle.SchedulingMode != configuration.SchedulingMode
            || configuration.TickLengthMinutes.HasValue && configuration.TickLengthMinutes.Value != cycle.TickLengthMinutes
            || configuration.ScheduledStartAt.HasValue && configuration.ScheduledStartAt.Value != cycle.StartAt
            || (cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired
                && configuration.ScheduledEndAt.HasValue
                && configuration.ScheduledEndAt.Value != cycle.EndAt))
        {
            errors.Add($"Cycle {cycle.CycleId} provenance does not match its materialized configuration.");
        }
    }

    private static void ValidateLifecycleStatus(string? value, Guid eventId, string label, List<string> errors)
    {
        if (!string.IsNullOrWhiteSpace(value)
            && !Enum.GetNames<GameLifecycleStatus>().Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            errors.Add($"Game lifecycle event {eventId} has an invalid {label} status '{value}'.");
        }
    }

    private static void ValidateContentHash(string? value, string label, List<string> errors)
    {
        if (value is not null
            && (value.Length != 64 || value.Any(character => !char.IsAsciiHexDigit(character))))
        {
            errors.Add($"{label} must contain exactly 64 hexadecimal characters when present.");
        }
    }

    private static void OptionalPositive(int? value, string label, List<string> errors)
    {
        if (value.HasValue && value.Value <= 0)
        {
            errors.Add($"{label} must be positive when present.");
        }
    }

    private static void ValidateCyclesAndReferences(GameState state, List<string> errors)
    {
        if (state.Cycles.Count == 0)
        {
            errors.Add("At least one Cycle is required.");
            return;
        }

        foreach (var operationalGame in state.Cycles
                     .Where(cycle => cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired)
                     .Where(cycle => cycle.GameId.HasValue)
                     .GroupBy(cycle => cycle.GameId!.Value)
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Game {operationalGame.Key} has more than one Active or RecoveryRequired Cycle.");
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

            if (cycle.Status == CycleStatus.Active && cycle.TurnStage != TurnResolutionStage.CommandOpen)
            {
                errors.Add($"Active Cycle {cycle.CycleId} must have an open command window in committed state.");
            }

            if (cycle.NextTickAt.HasValue
                && (cycle.Status != CycleStatus.Active
                    || cycle.SchedulingMode != CycleSchedulingMode.Scheduled))
            {
                errors.Add($"Cycle {cycle.CycleId} has a next tick timestamp outside active scheduled play.");
            }
            else if (cycle.Status == CycleStatus.Active
                     && cycle.SchedulingMode == CycleSchedulingMode.Scheduled
                     && !cycle.NextTickAt.HasValue)
            {
                errors.Add($"Active scheduled Cycle {cycle.CycleId} has no next tick timestamp.");
            }
        }

        var playerIds = state.Players.Select(item => item.PlayerId).ToHashSet();
        var cycleIds = state.Cycles.Select(item => item.CycleId).ToHashSet();
        var empireIds = state.Empires.Select(item => item.EmpireId).ToHashSet();
        var factionIds = state.Factions.Select(item => item.FactionId).ToHashSet();
        var sectorsById = state.Sectors.GroupBy(item => item.SectorId).ToDictionary(group => group.Key, group => group.First());
        var systemsById = state.Systems.GroupBy(item => item.SystemId).ToDictionary(group => group.Key, group => group.First());
        var fleetsById = state.Fleets.GroupBy(item => item.FleetId).ToDictionary(group => group.Key, group => group.First());
        var fleetOrdersById = state.FleetOrders.GroupBy(item => item.FleetOrderId).ToDictionary(group => group.Key, group => group.First());
        var admiralsById = state.Admirals.GroupBy(item => item.AdmiralId).ToDictionary(group => group.Key, group => group.First());
        var battleIds = state.BattleRecords.Select(item => item.BattleId).ToHashSet();
        var eventIds = state.Events.Select(item => item.EventId).ToHashSet();

        foreach (var cycle in state.Cycles)
        {
            OptionalReference(playerIds, cycle.CreatedByPlayerId, $"Cycle {cycle.CycleId} creator", errors);
            var empireCount = state.Empires.Count(item => item.CycleId == cycle.CycleId);
            if (empireCount > MatchControl.MaximumEmpireCount)
            {
                errors.Add($"Cycle {cycle.CycleId} has {empireCount} Empires; at most {MatchControl.MaximumEmpireCount} are supported.");
            }
        }

        foreach (var sector in state.Sectors)
        {
            Reference(cycleIds, sector.CycleId, $"Sector {sector.SectorId} Cycle", errors);
            Required(sector.SectorName, $"Sector {sector.SectorId} has no name.", errors);
            if (sector.SortOrder < 0)
            {
                errors.Add($"Sector {sector.SectorId} has a negative sort order.");
            }
        }

        foreach (var duplicate in state.Sectors.GroupBy(item => (item.CycleId, item.SortOrder)).Where(group => group.Count() > 1))
        {
            errors.Add($"Cycle {duplicate.Key.CycleId} has more than one sector at sort order {duplicate.Key.SortOrder}.");
        }

        foreach (var cycleSectors in state.Sectors.GroupBy(item => item.CycleId))
        {
            foreach (var duplicate in cycleSectors
                         .GroupBy(item => item.SectorName.Trim(), StringComparer.OrdinalIgnoreCase)
                         .Where(group => group.Count() > 1))
            {
                errors.Add($"Cycle {cycleSectors.Key} has more than one sector named '{duplicate.Key}'.");
            }
        }

        foreach (var system in state.Systems)
        {
            Reference(cycleIds, system.CycleId, $"System {system.SystemId} Cycle", errors);
            Required(system.SystemName, $"System {system.SystemId} has no name.", errors);
            var cycleHasSectors = state.Sectors.Any(item => item.CycleId == system.CycleId);
            if (cycleHasSectors)
            {
                Reference(sectorsById.Keys, system.SectorId, $"System {system.SystemId} sector", errors);
                if (sectorsById.TryGetValue(system.SectorId, out var sector) && sector.CycleId != system.CycleId)
                {
                    errors.Add($"System {system.SystemId} and its sector belong to different Cycles.");
                }
            }
            else if (system.SectorId != Guid.Empty)
            {
                errors.Add($"System {system.SystemId} references a sector but its Cycle has no sectors.");
            }
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

        foreach (var faction in state.Factions)
        {
            Reference(cycleIds, faction.CycleId, $"Faction {faction.FactionId} Cycle", errors);
            Required(faction.FactionName, $"Faction {faction.FactionId} has no name.", errors);
            if (faction.Kind == FactionKind.Empire && !faction.EmpireId.HasValue)
            {
                errors.Add($"Empire faction {faction.FactionId} has no Empire.");
            }
            else if (faction.Kind == FactionKind.Neutral && faction.EmpireId.HasValue)
            {
                errors.Add($"Neutral faction {faction.FactionId} cannot own an Empire.");
            }

            OptionalReference(empireIds, faction.EmpireId, $"Faction {faction.FactionId} empire", errors);
            if (faction.EmpireId.HasValue
                && state.Empires.FirstOrDefault(item => item.EmpireId == faction.EmpireId.Value) is { } factionEmpire
                && factionEmpire.CycleId != faction.CycleId)
            {
                errors.Add($"Faction {faction.FactionId} and its Empire belong to different Cycles.");
            }
        }

        foreach (var empire in state.Empires)
        {
            if (state.Factions.Count(item => item.EmpireId == empire.EmpireId && item.Kind == FactionKind.Empire) != 1)
            {
                errors.Add($"Empire {empire.EmpireId} must have exactly one Empire faction.");
            }
        }

        foreach (var participant in state.MatchParticipants)
        {
            Reference(cycleIds, participant.CycleId, $"Match participant {participant.MatchParticipantId} Cycle", errors);
            Reference(playerIds, participant.PlayerId, $"Match participant {participant.MatchParticipantId} player", errors);
            Reference(empireIds, participant.EmpireId, $"Match participant {participant.MatchParticipantId} Empire", errors);
            var participantEmpire = state.Empires.FirstOrDefault(item => item.EmpireId == participant.EmpireId);
            if (participantEmpire is not null && participantEmpire.CycleId != participant.CycleId)
            {
                errors.Add($"Match participant {participant.MatchParticipantId} and its Empire belong to different Cycles.");
            }
            else if (participantEmpire is not null && participantEmpire.PlayerId != participant.PlayerId)
            {
                errors.Add($"Match participant {participant.MatchParticipantId} does not match its Empire's player ownership.");
            }

            if ((participant.Status == MatchParticipantStatus.Active) != (participant.EndedAt is null))
            {
                errors.Add($"Match participant {participant.MatchParticipantId} active status and end timestamp disagree.");
            }
        }

        foreach (var duplicate in state.MatchParticipants.GroupBy(item => (item.CycleId, item.PlayerId)).Where(group => group.Count() > 1))
        {
            errors.Add($"Player {duplicate.Key.PlayerId} has more than one participant record in Cycle {duplicate.Key.CycleId}.");
        }

        foreach (var duplicate in state.MatchParticipants.Where(item => item.EndedAt is null).GroupBy(item => item.EmpireId).Where(group => group.Count() > 1))
        {
            errors.Add($"Empire {duplicate.Key} has shared current control.");
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
            Reference(factionIds, fleet.FactionId, $"Fleet {fleet.FleetId} faction", errors);
            if (fleet.EmpireId != Guid.Empty)
            {
                Reference(empireIds, fleet.EmpireId, $"Fleet {fleet.FleetId} empire", errors);
            }
            Reference(systemsById.Keys, fleet.CurrentSystemId, $"Fleet {fleet.FleetId} current system", errors);
            if (fleet.DestinationSystemId.HasValue)
            {
                Reference(systemsById.Keys, fleet.DestinationSystemId.Value, $"Fleet {fleet.FleetId} destination system", errors);
                if (systemsById.TryGetValue(fleet.DestinationSystemId.Value, out var destinationSystem)
                    && destinationSystem.CycleId != fleet.CycleId)
                {
                    errors.Add($"Fleet {fleet.FleetId} and its destination system belong to different Cycles.");
                }
            }

            if (fleet.Status == FleetStatus.InTransit)
            {
                if (!fleet.DestinationSystemId.HasValue)
                {
                    errors.Add($"In-transit fleet {fleet.FleetId} has no destination system.");
                }
                if (!fleet.DepartureTickNumber.HasValue)
                {
                    errors.Add($"In-transit fleet {fleet.FleetId} has no departure tick.");
                }
                if (!fleet.ArrivalTickNumber.HasValue)
                {
                    errors.Add($"In-transit fleet {fleet.FleetId} has no arrival tick.");
                }
                if (fleet.DepartureTickNumber > fleet.ArrivalTickNumber)
                {
                    errors.Add($"In-transit fleet {fleet.FleetId} departs after its arrival tick.");
                }
            }
            else if (fleet.DestinationSystemId.HasValue || fleet.DepartureTickNumber.HasValue || fleet.ArrivalTickNumber.HasValue)
            {
                errors.Add($"Fleet {fleet.FleetId} has transit timing while its status is {fleet.Status}.");
            }

            if (fleet.AdmiralId.HasValue)
            {
                Reference(admiralsById.Keys, fleet.AdmiralId.Value, $"Fleet {fleet.FleetId} admiral", errors);
                if (admiralsById.TryGetValue(fleet.AdmiralId.Value, out var admiral))
                {
                    if (admiral.CycleId != fleet.CycleId)
                    {
                        errors.Add($"Fleet {fleet.FleetId} and its admiral belong to different Cycles.");
                    }
                    else if (admiral.EmpireId != fleet.EmpireId)
                    {
                        errors.Add($"Fleet {fleet.FleetId} is assigned to another empire's admiral.");
                    }
                }
            }

            if (state.Empires.FirstOrDefault(item => item.EmpireId == fleet.EmpireId) is { } fleetEmpire
                && fleetEmpire.CycleId != fleet.CycleId)
            {
                errors.Add($"Fleet {fleet.FleetId} and its empire belong to different Cycles.");
            }

            if (state.Factions.FirstOrDefault(item => item.FactionId == fleet.FactionId) is { } fleetFaction
                && (fleetFaction.CycleId != fleet.CycleId || fleetFaction.EmpireId.GetValueOrDefault() != fleet.EmpireId))
            {
                errors.Add($"Fleet {fleet.FleetId} ownership is inconsistent with its faction.");
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
                if (systemsById.TryGetValue(order.TargetSystemId.Value, out var targetSystem)
                    && targetSystem.CycleId != order.CycleId)
                {
                    errors.Add($"Fleet order {order.FleetOrderId} and its target system belong to different Cycles.");
                }
            }

            if (order.TargetEmpireId.HasValue)
            {
                Reference(empireIds, order.TargetEmpireId.Value, $"Fleet order {order.FleetOrderId} target empire", errors);
                if (state.Empires.FirstOrDefault(item => item.EmpireId == order.TargetEmpireId.Value) is { } targetEmpire
                    && targetEmpire.CycleId != order.CycleId)
                {
                    errors.Add($"Fleet order {order.FleetOrderId} and its target empire belong to different Cycles.");
                }
            }

            if (order.TargetFactionId.HasValue)
            {
                Reference(factionIds, order.TargetFactionId.Value, $"Fleet order {order.FleetOrderId} target faction", errors);
                if (state.Factions.FirstOrDefault(item => item.FactionId == order.TargetFactionId.Value) is { } targetFaction
                    && targetFaction.CycleId != order.CycleId)
                {
                    errors.Add($"Fleet order {order.FleetOrderId} and its target faction belong to different Cycles.");
                }
            }

            if (order.SupersededByOrderId.HasValue)
            {
                Reference(
                    fleetOrdersById.Keys,
                    order.SupersededByOrderId.Value,
                    $"Fleet order {order.FleetOrderId} replacement order",
                    errors);
            }

            if (order.Status == FleetOrderStatus.Superseded && !order.SupersededByOrderId.HasValue)
            {
                errors.Add($"Superseded fleet order {order.FleetOrderId} has no replacement order.");
            }

            if (order.Status != FleetOrderStatus.Superseded && order.SupersededByOrderId.HasValue)
            {
                errors.Add($"Fleet order {order.FleetOrderId} links to a replacement without being superseded.");
            }

            if (order.SupersededByOrderId.HasValue
                && fleetOrdersById.TryGetValue(order.SupersededByOrderId.Value, out var replacement)
                && (replacement.CycleId != order.CycleId || replacement.FleetId != order.FleetId))
            {
                errors.Add($"Fleet order {order.FleetOrderId} and its replacement must belong to the same fleet and Cycle.");
            }

            if (fleetsById.TryGetValue(order.FleetId, out var orderedFleet) && orderedFleet.CycleId != order.CycleId)
            {
                errors.Add($"Fleet order {order.FleetOrderId} and its fleet belong to different Cycles.");
            }

            if (order.SubmitTick < 0 || order.ExecuteAfterTick <= order.SubmitTick)
            {
                errors.Add($"Fleet order {order.FleetOrderId} has invalid tick scheduling.");
            }

            if (order.SealedTick.HasValue != order.SealedAt.HasValue)
            {
                errors.Add($"Fleet order {order.FleetOrderId} must record its sealed tick and timestamp together.");
            }

            if (order.SealedTick.HasValue && order.SealedTick.Value < order.ExecuteAfterTick)
            {
                errors.Add($"Fleet order {order.FleetOrderId} was sealed before its execution tick.");
            }

            if (order.CommandSource != FleetOrderCommandSource.Human && !order.SealedTick.HasValue)
            {
                errors.Add($"Generated fleet order {order.FleetOrderId} is not part of a sealed turn ledger.");
            }
        }

        foreach (var duplicate in state.FleetOrders
                     .Where(item => item.Status == FleetOrderStatus.Pending)
                     .GroupBy(item => (item.CycleId, item.FleetId, item.ExecuteAfterTick))
                     .Where(group => group.Count() > 1))
        {
            errors.Add(
                $"Fleet {duplicate.Key.FleetId} has more than one pending order for tick {duplicate.Key.ExecuteAfterTick} in Cycle {duplicate.Key.CycleId}.");
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
            if (admiralsById.TryGetValue(history.AdmiralId, out var historyAdmiral)
                && historyAdmiral.CycleId != history.CycleId)
            {
                errors.Add($"Admiral battle history {history.AdmiralBattleHistoryId} and its admiral belong to different Cycles.");
            }
            if (systemsById.TryGetValue(history.SystemId, out var historySystem)
                && historySystem.CycleId != history.CycleId)
            {
                errors.Add($"Admiral battle history {history.AdmiralBattleHistoryId} and its system belong to different Cycles.");
            }
            if (fleetsById.TryGetValue(history.FleetId, out var historyFleet)
                && historyFleet.CycleId != history.CycleId)
            {
                errors.Add($"Admiral battle history {history.AdmiralBattleHistoryId} and its fleet belong to different Cycles.");
            }
        }

        foreach (var item in state.Events)
        {
            Reference(cycleIds, item.CycleId, $"Event {item.EventId} Cycle", errors);
            OptionalReference(systemsById.Keys, item.SystemId, $"Event {item.EventId} system", errors);
            OptionalReference(empireIds, item.EmpireId, $"Event {item.EventId} empire", errors);
            OptionalReference(factionIds, item.FactionId, $"Event {item.EventId} faction", errors);
            if (item.SystemId.HasValue && systemsById.TryGetValue(item.SystemId.Value, out var eventSystem) && eventSystem.CycleId != item.CycleId)
            {
                errors.Add($"Event {item.EventId} and its system belong to different Cycles.");
            }
            if (item.FactionId.HasValue
                && state.Factions.FirstOrDefault(faction => faction.FactionId == item.FactionId.Value) is { } eventFaction
                && eventFaction.CycleId != item.CycleId)
            {
                errors.Add($"Event {item.EventId} and its faction belong to different Cycles.");
            }
            if (item.EmpireId.HasValue
                && state.Empires.FirstOrDefault(empire => empire.EmpireId == item.EmpireId.Value) is { } eventEmpire
                && eventEmpire.CycleId != item.CycleId)
            {
                errors.Add($"Event {item.EventId} and its empire belong to different Cycles.");
            }
        }

        foreach (var battle in state.BattleRecords)
        {
            Reference(cycleIds, battle.CycleId, $"Battle {battle.BattleId} Cycle", errors);
            Reference(systemsById.Keys, battle.SystemId, $"Battle {battle.BattleId} system", errors);
            if (battle.AttackerEmpireId != Guid.Empty)
            {
                Reference(empireIds, battle.AttackerEmpireId, $"Battle {battle.BattleId} attacker", errors);
            }
            if (battle.DefenderEmpireId != Guid.Empty)
            {
                Reference(empireIds, battle.DefenderEmpireId, $"Battle {battle.BattleId} defender", errors);
            }
            Reference(factionIds, battle.AttackerFactionId, $"Battle {battle.BattleId} attacker faction", errors);
            Reference(factionIds, battle.DefenderFactionId, $"Battle {battle.BattleId} defender faction", errors);
            ValidateBattleFleetMembership(state, battle, errors);
            if (systemsById.TryGetValue(battle.SystemId, out var battleSystem) && battleSystem.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its system belong to different Cycles.");
            }
            if (state.Factions.FirstOrDefault(item => item.FactionId == battle.AttackerFactionId) is { } attackerFaction
                && attackerFaction.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its attacker faction belong to different Cycles.");
            }
            if (state.Factions.FirstOrDefault(item => item.FactionId == battle.DefenderFactionId) is { } defenderFaction
                && defenderFaction.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its defender faction belong to different Cycles.");
            }
            if (state.Empires.FirstOrDefault(item => item.EmpireId == battle.AttackerEmpireId) is { } attackerEmpire
                && attackerEmpire.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its attacker empire belong to different Cycles.");
            }
            if (state.Empires.FirstOrDefault(item => item.EmpireId == battle.DefenderEmpireId) is { } defenderEmpire
                && defenderEmpire.CycleId != battle.CycleId)
            {
                errors.Add($"Battle {battle.BattleId} and its defender empire belong to different Cycles.");
            }

            if (battle.AttackerLosses < 0 || battle.DefenderLosses < 0
                || battle.AttackerLosses > battle.AttackerShipsBefore
                || battle.DefenderLosses > battle.DefenderShipsBefore)
            {
                errors.Add($"Battle {battle.BattleId} has invalid retained ship losses.");
            }
        }

        foreach (var participant in state.BattleFleetParticipants)
        {
            Reference(battleIds, participant.BattleId, $"Battle fleet participant {participant.FleetId} Battle", errors);
            Reference(cycleIds, participant.CycleId, $"Battle fleet participant {participant.BattleId}/{participant.FleetId} Cycle", errors);
            Reference(fleetsById.Keys, participant.FleetId, $"Battle fleet participant {participant.BattleId} fleet", errors);
            if (!Enum.IsDefined(participant.Side))
            {
                errors.Add(
                    $"Battle fleet participant {participant.BattleId}/{participant.FleetId} has invalid side '{participant.Side}'.");
            }
            if (state.BattleRecords.FirstOrDefault(item => item.BattleId == participant.BattleId) is { } participantBattle
                && participantBattle.CycleId != participant.CycleId)
            {
                errors.Add(
                    $"Battle {participant.BattleId} and fleet {participant.FleetId} membership belong to different Cycles.");
            }
            if (fleetsById.TryGetValue(participant.FleetId, out var participantFleet)
                && participantFleet.CycleId != participant.CycleId)
            {
                errors.Add(
                    $"Battle fleet participant {participant.BattleId}/{participant.FleetId} and its Fleet belong to different Cycles.");
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
            if (entry.SystemId.HasValue
                && systemsById.TryGetValue(entry.SystemId.Value, out var chronicleSystem)
                && chronicleSystem.CycleId != entry.CycleId)
            {
                errors.Add($"Chronicle entry {entry.ChronicleEntryId} and its system belong to different Cycles.");
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

        foreach (var unlock in state.EmpireDoctrineUnlocks)
        {
            Reference(cycleIds, unlock.CycleId, $"Empire doctrine unlock {unlock.EmpireDoctrineUnlockId} Cycle", errors);
            Reference(empireIds, unlock.EmpireId, $"Empire doctrine unlock {unlock.EmpireDoctrineUnlockId} empire", errors);
            EnsureEmpireCycle(
                state,
                unlock.EmpireId,
                unlock.CycleId,
                $"Empire doctrine unlock {unlock.EmpireDoctrineUnlockId}",
                errors);
            Required(unlock.DoctrineKey, $"Empire doctrine unlock {unlock.EmpireDoctrineUnlockId} has no doctrine key.", errors);
            if (unlock.UnlockedTickNumber < 0)
            {
                errors.Add($"Empire doctrine unlock {unlock.EmpireDoctrineUnlockId} tick cannot be negative.");
            }
        }

        foreach (var duplicate in state.EmpireDoctrineUnlocks
                     .GroupBy(item => new
                     {
                         item.CycleId,
                         item.EmpireId,
                         DoctrineKey = item.DoctrineKey.Trim().ToUpperInvariant()
                     })
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Empire {duplicate.Key.EmpireId} has more than one {duplicate.First().DoctrineKey} doctrine unlock in Cycle {duplicate.Key.CycleId}.");
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
            EnsureSystemCycle(state, item.SystemId, item.CycleId, $"Cycle major event {item.CycleMajorEventId}", errors);
            EnsureBattleCycle(state, item.SourceBattleId, item.CycleId, $"Cycle major event {item.CycleMajorEventId}", errors);
        }

        foreach (var signal in state.SystemHistoricalSignals)
        {
            Reference(cycleIds, signal.CycleId, $"System historical signal {signal.SystemHistoricalSignalId} Cycle", errors);
            Reference(systemIds, signal.SystemId, $"System historical signal {signal.SystemHistoricalSignalId} system", errors);
            OptionalReference(battleIds, signal.SourceBattleId, $"System historical signal {signal.SystemHistoricalSignalId} source battle", errors);
            EnsureSystemCycle(state, signal.SystemId, signal.CycleId, $"System historical signal {signal.SystemHistoricalSignalId}", errors);
            EnsureBattleCycle(state, signal.SourceBattleId, signal.CycleId, $"System historical signal {signal.SystemHistoricalSignalId}", errors);
        }

        foreach (var outpost in state.ColonialOutposts)
        {
            Reference(cycleIds, outpost.CycleId, $"Colonial outpost {outpost.ColonialOutpostId} Cycle", errors);
            Reference(empireIds, outpost.EmpireId, $"Colonial outpost {outpost.ColonialOutpostId} empire", errors);
            Reference(systemIds, outpost.SystemId, $"Colonial outpost {outpost.ColonialOutpostId} system", errors);
            EnsureEmpireCycle(state, outpost.EmpireId, outpost.CycleId, $"Colonial outpost {outpost.ColonialOutpostId}", errors);
            EnsureSystemCycle(state, outpost.SystemId, outpost.CycleId, $"Colonial outpost {outpost.ColonialOutpostId}", errors);
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

    private static void EnsureSystemCycle(
        GameState state,
        Guid? systemId,
        Guid cycleId,
        string label,
        List<string> errors)
    {
        if (systemId.HasValue
            && state.Systems.FirstOrDefault(item => item.SystemId == systemId.Value) is { } system
            && system.CycleId != cycleId)
        {
            errors.Add($"{label} and its system belong to different Cycles.");
        }
    }

    private static void EnsureBattleCycle(
        GameState state,
        Guid? battleId,
        Guid cycleId,
        string label,
        List<string> errors)
    {
        if (battleId.HasValue
            && state.BattleRecords.FirstOrDefault(item => item.BattleId == battleId.Value) is { } battle
            && battle.CycleId != cycleId)
        {
            errors.Add($"{label} and its source battle belong to different Cycles.");
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
        foreach (var (value, label) in state.GameLifecycleEvents
                     .Select(item => (item.FactJson, $"Game lifecycle event {item.GameLifecycleEventId} fact JSON"))
                     .Concat(state.Events.Select(item => (item.FactJson, $"Event {item.EventId} fact JSON")))
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

    private static void Required(string? value, string error, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            errors.Add(error);
        }
    }

    private static void ValidateBattleFleetMembership(
        GameState state,
        BattleRecord battle,
        List<string> errors)
    {
        var participants = state.BattleFleetParticipants
            .Where(item => item.BattleId == battle.BattleId)
            .ToArray();
        var attackerFleetIds = participants
            .Where(item => item.Side == BattleFleetSide.Attacker)
            .Select(item => item.FleetId)
            .ToHashSet();
        var defenderFleetIds = participants
            .Where(item => item.Side == BattleFleetSide.Defender)
            .Select(item => item.FleetId)
            .ToHashSet();
        if (attackerFleetIds.Count == 0 || defenderFleetIds.Count == 0)
        {
            errors.Add($"Battle {battle.BattleId} must retain at least one normalized fleet on each side.");
        }

        IReadOnlyList<Guid>? legacyAttackerFleetIds = null;
        IReadOnlyList<Guid>? legacyDefenderFleetIds = null;
        try
        {
            legacyAttackerFleetIds = BattleFleetParticipantCompatibility.ParseLegacyFleetIds(
                battle.AttackerFleetIds,
                $"Battle {battle.BattleId} attacker fleets");
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        try
        {
            legacyDefenderFleetIds = BattleFleetParticipantCompatibility.ParseLegacyFleetIds(
                battle.DefenderFleetIds,
                $"Battle {battle.BattleId} defender fleets");
        }
        catch (InvalidOperationException exception)
        {
            errors.Add(exception.Message);
        }

        if (legacyAttackerFleetIds is not null
            && !attackerFleetIds.SetEquals(legacyAttackerFleetIds))
        {
            errors.Add($"Battle {battle.BattleId} attacker fleet membership does not match its retained legacy list.");
        }
        if (legacyDefenderFleetIds is not null
            && !defenderFleetIds.SetEquals(legacyDefenderFleetIds))
        {
            errors.Add($"Battle {battle.BattleId} defender fleet membership does not match its retained legacy list.");
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
