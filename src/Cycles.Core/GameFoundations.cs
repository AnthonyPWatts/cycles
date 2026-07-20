using System.Text.Json;

namespace Cycles.Core;

public static class GameFoundationConstants
{
    public static readonly Guid LegacyGameId = Guid.Parse("01fcdded-9718-4436-b585-d97d504b1d57");
    public static readonly Guid LegacyLifecycleEventId = Guid.Parse("b283628d-2899-475c-9c6e-5dd8e20c2e91");

    public const string LegacyGameName = "Legacy Standard Game";
    public const string LegacyGamePolicyKey = "legacy-single-lineage-v1";
    public const int LegacyGamePolicyVersion = 1;
    public const string LegacyCyclePolicyKey = "legacy-cycle-policy-v1";
    public const int LegacyCyclePolicyVersion = 1;
    public const string LegacyUnclassifiedProfileKey = "legacy-unclassified";
    public const string LegacyImportFactJson =
        "{\"source\":\"legacy-single-lineage\",\"schemaVersion\":1}";
}

public static class LegacyGameFoundation
{
    public static void Apply(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Cycles.Count == 0)
        {
            return;
        }

        ValidateLegacyScope(state);

        var operationalCycles = state.Cycles
            .Where(cycle => cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired)
            .ToArray();
        if (operationalCycles.Length > 1)
        {
            throw new InvalidOperationException(
                "Legacy Game adaptation requires at most one operational Cycle; the stored state contains more than one.");
        }

        var orderedCycles = state.Cycles
            .OrderBy(cycle => cycle.StartAt)
            .ThenBy(cycle => cycle.CreatedAt)
            .ThenBy(cycle => cycle.CycleId)
            .ToArray();
        var cycleSequence = orderedCycles
            .Select((cycle, index) => (cycle.CycleId, SequenceNumber: index + 1))
            .ToDictionary(item => item.CycleId, item => item.SequenceNumber);

        var gameStatus = operationalCycles.Length == 1
            ? GameLifecycleStatus.Active
            : GameLifecycleStatus.Completed;
        var gameCreatedAt = orderedCycles.Min(cycle => cycle.CreatedAt);
        var derivedGame = new Game
        {
            GameId = GameFoundationConstants.LegacyGameId,
            Name = GameFoundationConstants.LegacyGameName,
            Purpose = GamePurpose.Standard,
            Status = gameStatus,
            Visibility = GameVisibility.Private,
            CreationSource = GameCreationSource.LegacyImport,
            GamePolicyKey = GameFoundationConstants.LegacyGamePolicyKey,
            GamePolicyVersion = GameFoundationConstants.LegacyGamePolicyVersion,
            PolicyProvenanceStatus = ProvenanceStatus.LegacyUnverified,
            CreatedAt = gameCreatedAt,
            FirstStartedAt = orderedCycles.Min(cycle => cycle.StartAt),
            CompletedAt = gameStatus == GameLifecycleStatus.Completed
                ? orderedCycles.Max(cycle => cycle.EndAt)
                : null
        };

        var game = state.Games.SingleOrDefault();
        if (game is null)
        {
            game = derivedGame;
            state.Games.Add(game);
        }
        else
        {
            UpdateDerivedLifecycle(game, derivedGame);
        }

        foreach (var cycle in orderedCycles)
        {
            var configuration = state.CycleConfigurations.SingleOrDefault(item => item.CycleConfigurationId == cycle.CycleId);
            ApplyLegacyLinkage(state, cycle, configuration);
            if (configuration is null)
            {
                state.CycleConfigurations.Add(CreateConfiguration(cycle, cycleSequence[cycle.CycleId]));
            }
            else
            {
                UpdateConfiguration(configuration, cycle, cycleSequence[cycle.CycleId]);
            }
        }

        foreach (var participant in state.MatchParticipants)
        {
            participant.GameId = GameFoundationConstants.LegacyGameId;
        }

        foreach (var derivedEnrolment in CreateEnrolments(
                     state,
                     operationalCycles.SingleOrDefault()?.CycleId,
                     game))
        {
            var enrolment = state.GameEnrolments.SingleOrDefault(item => item.PlayerId == derivedEnrolment.PlayerId);
            if (enrolment is null)
            {
                state.GameEnrolments.Add(derivedEnrolment);
                continue;
            }

            enrolment.Status = derivedEnrolment.Status;
            enrolment.EnrolledAt = derivedEnrolment.EnrolledAt;
            enrolment.StatusChangedAt = derivedEnrolment.StatusChangedAt;
            enrolment.EndedAt = derivedEnrolment.EndedAt;
        }

        if (state.GameLifecycleEvents.All(item => item.GameLifecycleEventId != GameFoundationConstants.LegacyLifecycleEventId))
        {
            state.GameLifecycleEvents.Add(new GameLifecycleEvent
            {
                GameLifecycleEventId = GameFoundationConstants.LegacyLifecycleEventId,
                GameId = GameFoundationConstants.LegacyGameId,
                Type = GameLifecycleEventType.LegacyImported,
                ToStatus = gameStatus.ToString(),
                FactJson = GameFoundationConstants.LegacyImportFactJson,
                CreatedAt = gameCreatedAt
            });
        }

        CycleScheduling.NormalizePersistedSchedule(state);
    }

    public static void ApplyLifecycleTransition(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Cycles.Count == 0)
        {
            return;
        }

        ValidateLifecycleTransitionScope(state);
        GameLifecycleTransitions.ApplyCycleState(state, GameFoundationConstants.LegacyGameId);
    }

    private static void ValidateLifecycleTransitionScope(GameState state)
    {
        if (state.Games.Count != 1
            || state.Games[0].GameId != GameFoundationConstants.LegacyGameId)
        {
            throw new InvalidOperationException(
                "Legacy Game lifecycle transition requires exactly one fixed-ID Game.");
        }

        if (state.Cycles.Any(cycle => cycle.GameId != GameFoundationConstants.LegacyGameId)
            || state.CycleConfigurations.Any(configuration =>
                configuration.GameId != GameFoundationConstants.LegacyGameId)
            || state.GameEnrolments.Any(enrolment =>
                enrolment.GameId != GameFoundationConstants.LegacyGameId)
            || state.MatchParticipants.Any(participant =>
                participant.GameId != GameFoundationConstants.LegacyGameId)
            || state.GameLifecycleEvents.Any(gameEvent =>
                gameEvent.GameId != GameFoundationConstants.LegacyGameId))
        {
            throw new InvalidOperationException(
                "Legacy Game lifecycle transition found rows outside the fixed-ID Game.");
        }

        var cyclesById = state.Cycles.ToDictionary(cycle => cycle.CycleId);
        foreach (var cycle in state.Cycles)
        {
            if (!cycle.CycleConfigurationId.HasValue
                || state.CycleConfigurations.Count(configuration =>
                    configuration.CycleConfigurationId == cycle.CycleConfigurationId.Value) != 1)
            {
                throw new InvalidOperationException(
                    $"Legacy Game lifecycle transition found no unique configuration for Cycle {cycle.CycleId}.");
            }
        }

        foreach (var participant in state.MatchParticipants)
        {
            if (!cyclesById.ContainsKey(participant.CycleId)
                || state.GameEnrolments.Count(enrolment =>
                    enrolment.PlayerId == participant.PlayerId) != 1)
            {
                throw new InvalidOperationException(
                    $"Legacy Game lifecycle transition found no unique Game enrolment for participant {participant.MatchParticipantId}.");
            }
        }
    }

    private static void ValidateLegacyScope(GameState state)
    {
        if (state.Games.Count > 1 || state.Games.Any(game => game.GameId != GameFoundationConstants.LegacyGameId))
        {
            throw new InvalidOperationException(
                "Legacy Game adaptation cannot run after a different or additional Game has been introduced.");
        }

        if (state.CycleConfigurations.Any(configuration =>
                configuration.GameId != GameFoundationConstants.LegacyGameId
                || configuration.CycleConfigurationId == Guid.Empty)
            || state.GameEnrolments.Any(enrolment =>
                enrolment.GameId != GameFoundationConstants.LegacyGameId
                || (enrolment.Origin == GameEnrolmentOrigin.LegacyImport
                    && enrolment.GameEnrolmentId != enrolment.PlayerId))
            || state.MatchParticipants.Any(participant =>
                participant.GameId != Guid.Empty
                && participant.GameId != GameFoundationConstants.LegacyGameId)
            || state.GameLifecycleEvents.Any(gameEvent => gameEvent.GameId != GameFoundationConstants.LegacyGameId))
        {
            throw new InvalidOperationException(
                "Legacy Game adaptation found foundation rows belonging to a different or non-deterministic legacy identity.");
        }

        var cycleIds = state.Cycles.Select(cycle => cycle.CycleId).ToHashSet();
        var conflictingCycle = state.Cycles.FirstOrDefault(cycle =>
            cycle.GameId is not null && cycle.GameId != GameFoundationConstants.LegacyGameId
            || cycle.CycleConfigurationId is not null && cycle.CycleConfigurationId != cycle.CycleId
            || cycle.PreviousCycleId is not null && !cycleIds.Contains(cycle.PreviousCycleId.Value));
        if (conflictingCycle is not null)
        {
            throw new InvalidOperationException(
                $"Legacy Game adaptation found conflicting scope on Cycle {conflictingCycle.CycleId}.");
        }

        var foreignConfiguration = state.CycleConfigurations.FirstOrDefault(configuration =>
            !cycleIds.Contains(configuration.CycleConfigurationId));
        if (foreignConfiguration is not null)
        {
            throw new InvalidOperationException(
                $"Legacy Game adaptation found configuration {foreignConfiguration.CycleConfigurationId} without its Cycle.");
        }

        var foreignParticipant = state.MatchParticipants.FirstOrDefault(participant => !cycleIds.Contains(participant.CycleId));
        if (foreignParticipant is not null)
        {
            throw new InvalidOperationException(
                $"Legacy Game adaptation cannot place participant {foreignParticipant.MatchParticipantId} because its Cycle is absent.");
        }

        var importEvents = state.GameLifecycleEvents
            .Where(item => item.GameLifecycleEventId == GameFoundationConstants.LegacyLifecycleEventId)
            .ToArray();
        if (importEvents.Length > 1
            || importEvents.Length == 1
            && (importEvents[0].Type != GameLifecycleEventType.LegacyImported
                || importEvents[0].FactJson != GameFoundationConstants.LegacyImportFactJson))
        {
            throw new InvalidOperationException("The fixed legacy lifecycle event has conflicting audit facts.");
        }
    }

    private static void UpdateDerivedLifecycle(Game game, Game derived)
    {
        ValidateImmutableIdentityAndPolicy(game);

        game.Status = derived.Status;
        game.CreatedAt = derived.CreatedAt;
        game.FirstStartedAt = derived.FirstStartedAt;
        game.CompletedAt = derived.CompletedAt;
    }

    private static void ValidateImmutableIdentityAndPolicy(Game game)
    {
        if (game.Name != GameFoundationConstants.LegacyGameName
            || game.Purpose != GamePurpose.Standard
            || game.Visibility != GameVisibility.Private
            || game.CreationSource != GameCreationSource.LegacyImport
            || game.GamePolicyKey != GameFoundationConstants.LegacyGamePolicyKey
            || game.GamePolicyVersion != GameFoundationConstants.LegacyGamePolicyVersion
            || game.PolicyProvenanceStatus != ProvenanceStatus.LegacyUnverified)
        {
            throw new InvalidOperationException("The fixed legacy Game has conflicting immutable identity or policy fields.");
        }
    }

    private static void ApplyLegacyLinkage(
        GameState state,
        Cycle cycle,
        CycleConfiguration? configuration)
    {
        cycle.GameId ??= GameFoundationConstants.LegacyGameId;
        cycle.CycleConfigurationId ??= cycle.CycleId;

        if (configuration is not null)
        {
            cycle.MapProfileKey ??= configuration.MapProfileKey;
            cycle.MapProfileVersion ??= configuration.MapProfileVersion;
            cycle.MapProfileContentHash ??= configuration.MapProfileContentHash;
            cycle.MapSeed ??= configuration.MapSeed;
            cycle.ScenarioProfileKey ??= configuration.ScenarioProfileKey;
            cycle.ScenarioProfileVersion ??= configuration.ScenarioProfileVersion;
            cycle.ScenarioProfileContentHash ??= configuration.ScenarioProfileContentHash;
            cycle.ScenarioSeed ??= configuration.ScenarioSeed;
            cycle.CyclePolicyKey ??= configuration.CyclePolicyKey;
            cycle.CyclePolicyVersion ??= configuration.CyclePolicyVersion;
            cycle.CyclePolicyContentHash ??= configuration.CyclePolicyContentHash;
            cycle.ProfileProvenanceStatus ??= configuration.ProvenanceStatus;
        }

        var profile = ClassifyLegacyProfiles(state, cycle.CycleId);
        cycle.MapProfileKey ??= profile.MapKey;
        cycle.MapProfileVersion ??= profile.MapVersion;
        cycle.MapSeed ??= profile.MapSeed;
        cycle.ScenarioProfileKey ??= profile.ScenarioKey;
        cycle.ScenarioProfileVersion ??= profile.ScenarioVersion;
        cycle.ScenarioSeed ??= profile.ScenarioSeed;
        cycle.CyclePolicyKey ??= GameFoundationConstants.LegacyCyclePolicyKey;
        cycle.CyclePolicyVersion ??= GameFoundationConstants.LegacyCyclePolicyVersion;
        cycle.ProfileProvenanceStatus ??= ProvenanceStatus.LegacyUnverified;
        cycle.SchedulingMode = CycleSchedulingMode.Scheduled;
    }

    private static CycleConfiguration CreateConfiguration(Cycle cycle, int sequenceNumber) => new()
    {
        CycleConfigurationId = cycle.CycleId,
        GameId = GameFoundationConstants.LegacyGameId,
        SequenceNumber = sequenceNumber,
        Status = CycleConfigurationStatus.Materialized,
        ProvenanceStatus = ProvenanceStatus.LegacyUnverified,
        MapProfileKey = cycle.MapProfileKey,
        MapProfileVersion = cycle.MapProfileVersion,
        MapProfileContentHash = cycle.MapProfileContentHash,
        MapSeed = cycle.MapSeed,
        ScenarioProfileKey = cycle.ScenarioProfileKey,
        ScenarioProfileVersion = cycle.ScenarioProfileVersion,
        ScenarioProfileContentHash = cycle.ScenarioProfileContentHash,
        ScenarioSeed = cycle.ScenarioSeed,
        CyclePolicyKey = GameFoundationConstants.LegacyCyclePolicyKey,
        CyclePolicyVersion = GameFoundationConstants.LegacyCyclePolicyVersion,
        SchedulingMode = CycleSchedulingMode.Scheduled,
        MinimumHumanSeats = null,
        MaximumHumanSeats = null,
        ScheduledStartAt = cycle.StartAt,
        ScheduledEndAt = cycle.EndAt,
        TickLengthMinutes = cycle.TickLengthMinutes,
        CreatedAt = cycle.CreatedAt,
        LockedAt = cycle.CreatedAt,
        MaterializedAt = cycle.CreatedAt
    };

    private static void UpdateConfiguration(
        CycleConfiguration configuration,
        Cycle cycle,
        int sequenceNumber)
    {
        if (configuration.SequenceNumber != sequenceNumber)
        {
            throw new InvalidOperationException(
                $"Legacy Cycle configuration {configuration.CycleConfigurationId} has sequence {configuration.SequenceNumber}, expected {sequenceNumber}.");
        }

        configuration.MapProfileKey ??= cycle.MapProfileKey;
        configuration.MapProfileVersion ??= cycle.MapProfileVersion;
        configuration.MapProfileContentHash ??= cycle.MapProfileContentHash;
        configuration.MapSeed ??= cycle.MapSeed;
        configuration.ScenarioProfileKey ??= cycle.ScenarioProfileKey;
        configuration.ScenarioProfileVersion ??= cycle.ScenarioProfileVersion;
        configuration.ScenarioProfileContentHash ??= cycle.ScenarioProfileContentHash;
        configuration.ScenarioSeed ??= cycle.ScenarioSeed;
        configuration.CyclePolicyKey = string.IsNullOrWhiteSpace(configuration.CyclePolicyKey)
            ? GameFoundationConstants.LegacyCyclePolicyKey
            : configuration.CyclePolicyKey;
        configuration.CyclePolicyVersion = configuration.CyclePolicyVersion == 0
            ? GameFoundationConstants.LegacyCyclePolicyVersion
            : configuration.CyclePolicyVersion;
        configuration.SchedulingMode = CycleSchedulingMode.Scheduled;
        configuration.ScheduledStartAt ??= cycle.StartAt;
        configuration.ScheduledEndAt ??= cycle.EndAt;
        configuration.TickLengthMinutes ??= cycle.TickLengthMinutes;
        configuration.LockedAt ??= cycle.CreatedAt;
        configuration.MaterializedAt ??= cycle.CreatedAt;
    }

    private static IEnumerable<GameEnrolment> CreateEnrolments(
        GameState state,
        Guid? operationalCycleId,
        Game game)
    {
        foreach (var participantGroup in state.MatchParticipants
                     .GroupBy(participant => participant.PlayerId)
                     .OrderBy(group => group.Key))
        {
            var participants = participantGroup
                .OrderByDescending(participant => participant.JoinedAt)
                .ThenBy(participant => participant.MatchParticipantId)
                .ToArray();
            var operationalParticipant = operationalCycleId is null
                ? null
                : participants.SingleOrDefault(participant => participant.CycleId == operationalCycleId.Value);
            var status = ResolveEnrolmentStatus(operationalParticipant, game.Status);
            var latestParticipationAt = participants.Max(participant => participant.EndedAt ?? participant.JoinedAt);
            var statusChangedAt = ResolveStatusChangedAt(
                latestParticipationAt,
                operationalParticipant,
                game,
                status);

            yield return new GameEnrolment
            {
                GameEnrolmentId = participantGroup.Key,
                GameId = GameFoundationConstants.LegacyGameId,
                PlayerId = participantGroup.Key,
                Status = status,
                Origin = GameEnrolmentOrigin.LegacyImport,
                EnrolledAt = participants.Min(participant => participant.JoinedAt),
                StatusChangedAt = statusChangedAt,
                EndedAt = status is GameEnrolmentStatus.Completed or GameEnrolmentStatus.Withdrawn
                    ? statusChangedAt
                    : null
            };
        }
    }

    private static GameEnrolmentStatus ResolveEnrolmentStatus(
        MatchParticipant? operationalParticipant,
        GameLifecycleStatus gameStatus)
    {
        if (gameStatus == GameLifecycleStatus.Completed)
        {
            return GameEnrolmentStatus.Completed;
        }

        if (operationalParticipant is null)
        {
            return GameEnrolmentStatus.Historical;
        }

        if (operationalParticipant.Status == MatchParticipantStatus.Withdrawn)
        {
            return GameEnrolmentStatus.Withdrawn;
        }

        return GameEnrolmentStatus.Enrolled;
    }

    private static DateTimeOffset ResolveStatusChangedAt(
        DateTimeOffset latestParticipationAt,
        MatchParticipant? operationalParticipant,
        Game game,
        GameEnrolmentStatus status) => status switch
        {
            GameEnrolmentStatus.Completed => game.CompletedAt
                ?? throw new InvalidOperationException("A completed legacy Game must have a completion timestamp."),
            GameEnrolmentStatus.Enrolled or GameEnrolmentStatus.Withdrawn =>
                (operationalParticipant ?? throw new InvalidOperationException("Operational enrolment has no participant."))
                .EndedAt ?? operationalParticipant.JoinedAt,
            _ => latestParticipationAt
        };

    private static LegacyProfileClassification ClassifyLegacyProfiles(GameState state, Guid cycleId)
    {
        var mapKey = GameFoundationConstants.LegacyUnclassifiedProfileKey;
        int? mapVersion = null;
        int? mapSeed = null;
        var candidateMapSeeds = state.Events
            .Where(item => item.CycleId == cycleId && item.EventType == EventType.CycleSeeded)
            .Select(item => TryReadCanonicalMapFact(item.FactJson, out var seed) ? (int?)seed : null)
            .Where(seed => seed is not null)
            .Select(seed => seed!.Value)
            .Distinct()
            .ToArray();
        if (candidateMapSeeds.Length > 1)
        {
            throw new InvalidOperationException(
                $"Legacy map classification found conflicting canonical seeds for Cycle {cycleId}.");
        }

        if (candidateMapSeeds.Length == 1
            && state.Sectors.Count(item => item.CycleId == cycleId) == GameSeeder.CanonicalGalaxySectorCount
            && state.Systems.Count(item => item.CycleId == cycleId) == GameSeeder.CanonicalGalaxySystemCount
            && state.SystemLinks.Count(item => item.CycleId == cycleId) == GameSeeder.CanonicalGalaxyRouteCount)
        {
            mapKey = GameSeeder.CanonicalGalaxyTopologyKey;
            mapSeed = candidateMapSeeds[0];
        }

        var scenarioKey = GameFoundationConstants.LegacyUnclassifiedProfileKey;
        int? scenarioVersion = null;
        int? scenarioSeed = null;
        var participantEmpireIds = state.MatchParticipants
            .Where(item => item.CycleId == cycleId)
            .Select(item => item.EmpireId)
            .ToHashSet();
        if (mapKey == GameSeeder.CanonicalGalaxyTopologyKey
            && TryReadDevelopmentScenarioFacts(state, cycleId, participantEmpireIds, out var candidateScenarioSeed))
        {
            scenarioKey = GameSeeder.CuratedColdStartScenarioKey;
            scenarioSeed = candidateScenarioSeed;
        }

        return new LegacyProfileClassification(
            mapKey,
            mapVersion,
            mapSeed,
            scenarioKey,
            scenarioVersion,
            scenarioSeed);
    }

    private static bool TryReadCanonicalMapFact(string factJson, out int seed)
    {
        seed = default;
        try
        {
            using var document = JsonDocument.Parse(factJson);
            var root = document.RootElement;
            return root.TryGetProperty("topologyKey", out var topologyKey)
                   && topologyKey.GetString() == GameSeeder.CanonicalGalaxyTopologyKey
                   && root.TryGetProperty("systemCount", out var systemCount)
                   && systemCount.TryGetInt32(out var systems)
                   && systems == GameSeeder.CanonicalGalaxySystemCount
                   && root.TryGetProperty("sectorCount", out var sectorCount)
                   && sectorCount.TryGetInt32(out var sectors)
                   && sectors == GameSeeder.CanonicalGalaxySectorCount
                   && root.TryGetProperty("seed", out var seedProperty)
                   && seedProperty.TryGetInt32(out seed);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private static bool TryReadDevelopmentScenarioFacts(
        GameState state,
        Guid cycleId,
        IReadOnlySet<Guid> participantEmpireIds,
        out int scenarioSeed)
    {
        scenarioSeed = default;
        if (participantEmpireIds.Count == 0)
        {
            return false;
        }

        int? sharedSeed = null;
        foreach (var empireId in participantEmpireIds)
        {
            var empireSeeds = state.Events
                .Where(item => item.CycleId == cycleId
                               && item.EventType == EventType.OpeningBriefingIssued
                               && item.EmpireId == empireId)
                .Select(item => TryReadDevelopmentScenarioFact(item.FactJson, out var seed) ? (int?)seed : null)
                .Where(seed => seed is not null)
                .Select(seed => seed!.Value)
                .Distinct()
                .ToArray();
            if (empireSeeds.Length != 1 || sharedSeed is not null && sharedSeed.Value != empireSeeds[0])
            {
                return false;
            }

            sharedSeed = empireSeeds[0];
        }

        if (sharedSeed is null)
        {
            return false;
        }

        scenarioSeed = sharedSeed.Value;
        return true;
    }

    private static bool TryReadDevelopmentScenarioFact(string factJson, out int scenarioSeed)
    {
        scenarioSeed = default;
        try
        {
            using var document = JsonDocument.Parse(factJson);
            var root = document.RootElement;
            return root.TryGetProperty("scenarioKey", out var scenarioKey)
                   && scenarioKey.GetString() == GameSeeder.CuratedColdStartScenarioKey
                   && root.TryGetProperty("mapVersion", out var mapVersion)
                   && mapVersion.GetString() == GameSeeder.CanonicalGalaxyTopologyKey
                   && root.TryGetProperty("scenarioSeed", out var seedProperty)
                   && seedProperty.TryGetInt32(out scenarioSeed);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException)
        {
            return false;
        }
    }

    private sealed record LegacyProfileClassification(
        string MapKey,
        int? MapVersion,
        int? MapSeed,
        string ScenarioKey,
        int? ScenarioVersion,
        int? ScenarioSeed);
}

public sealed class Game
{
    public Guid GameId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public GamePurpose Purpose { get; set; } = GamePurpose.Standard;
    public GameLifecycleStatus Status { get; set; } = GameLifecycleStatus.Forming;
    public GameVisibility Visibility { get; set; } = GameVisibility.Private;
    public GameCreationSource CreationSource { get; set; } = GameCreationSource.Operator;
    public string GamePolicyKey { get; set; } = "";
    public int GamePolicyVersion { get; set; }
    public string? GamePolicyContentHash { get; set; }
    public ProvenanceStatus PolicyProvenanceStatus { get; set; } = ProvenanceStatus.Verified;
    public Guid? CreatedByPlayerId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? FirstStartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public DateTimeOffset? TerminatedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class CycleConfiguration
{
    public Guid CycleConfigurationId { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public int SequenceNumber { get; set; }
    public CycleConfigurationStatus Status { get; set; } = CycleConfigurationStatus.Draft;
    public ProvenanceStatus ProvenanceStatus { get; set; } = ProvenanceStatus.Verified;
    public string? MapProfileKey { get; set; }
    public int? MapProfileVersion { get; set; }
    public string? MapProfileContentHash { get; set; }
    public int? MapSeed { get; set; }
    public string? ScenarioProfileKey { get; set; }
    public int? ScenarioProfileVersion { get; set; }
    public string? ScenarioProfileContentHash { get; set; }
    public int? ScenarioSeed { get; set; }
    public string CyclePolicyKey { get; set; } = "";
    public int CyclePolicyVersion { get; set; }
    public string? CyclePolicyContentHash { get; set; }
    public CycleSchedulingMode SchedulingMode { get; set; } = CycleSchedulingMode.Scheduled;
    public int? MinimumHumanSeats { get; set; }
    public int? MaximumHumanSeats { get; set; }
    public DateTimeOffset? ScheduledStartAt { get; set; }
    public DateTimeOffset? ScheduledEndAt { get; set; }
    public int? TickLengthMinutes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LockedAt { get; set; }
    public DateTimeOffset? MaterializedAt { get; set; }
    public DateTimeOffset? CancelledAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class GameEnrolment
{
    public Guid GameEnrolmentId { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public Guid PlayerId { get; set; }
    public GameEnrolmentStatus Status { get; set; } = GameEnrolmentStatus.Enrolled;
    public GameEnrolmentOrigin Origin { get; set; } = GameEnrolmentOrigin.Direct;
    public string? OriginatingRequestId { get; set; }
    public DateTimeOffset EnrolledAt { get; set; }
    public DateTimeOffset StatusChangedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public byte[] RowVersion { get; set; } = [];
}

public sealed class GameLifecycleEvent
{
    public Guid GameLifecycleEventId { get; set; } = Guid.NewGuid();
    public Guid GameId { get; set; }
    public GameLifecycleEventType Type { get; set; }
    public Guid? SubjectPlayerId { get; set; }
    public Guid? ActorPlayerId { get; set; }
    public string? FromStatus { get; set; }
    public string? ToStatus { get; set; }
    public string? Reason { get; set; }
    public string? CorrelationId { get; set; }
    public string FactJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}

public enum GamePurpose
{
    Standard,
    Training
}

public enum CycleSchedulingMode
{
    Scheduled,
    SelfPaced
}

public enum GameLifecycleStatus
{
    Forming,
    Starting,
    Active,
    Intermission,
    Completed,
    Cancelled,
    Terminated
}

public enum GameVisibility
{
    Private,
    Unlisted,
    Public
}

public enum GameCreationSource
{
    Operator,
    Player,
    TrainingProvisioning,
    Matchmaking,
    LegacyImport
}

public enum ProvenanceStatus
{
    Verified,
    LegacyUnverified
}

public enum CycleConfigurationStatus
{
    Draft,
    Locked,
    Materialized,
    Cancelled
}

public enum GameEnrolmentStatus
{
    Enrolled,
    Historical,
    Completed,
    Withdrawn
}

public enum GameEnrolmentOrigin
{
    Direct,
    Invitation,
    ManualOrganiser,
    Matchmaking,
    LegacyImport
}

public enum GameLifecycleEventType
{
    LegacyImported,
    Created,
    StatusChanged,
    ConfigurationChanged,
    EnrolmentChanged
}
