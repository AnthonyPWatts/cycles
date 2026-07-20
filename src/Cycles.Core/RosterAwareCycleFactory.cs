using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cycles.Core;

public sealed record CycleMaterializationResult(
    Guid GameId,
    Guid CycleConfigurationId,
    Guid CycleId,
    string ProfileKey,
    bool Created);

public static class RosterAwareCycleFactory
{
    public static CycleMaterializationResult Materialize(
        GameState state,
        Guid cycleConfigurationId,
        DateTimeOffset materializedAt)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (cycleConfigurationId == Guid.Empty)
        {
            throw new ArgumentException("A Cycle configuration identifier is required.", nameof(cycleConfigurationId));
        }

        var candidate = state.DeepClone();
        var result = MaterializeCore(candidate, cycleConfigurationId, materializedAt);
        var validation = GameStateTransfer.Validate(candidate);
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "The roster-aware Cycle factory produced invalid state: " + string.Join(" ", validation.Errors));
        }

        state.ReplaceWith(candidate);
        return result;
    }

    private static CycleMaterializationResult MaterializeCore(
        GameState state,
        Guid cycleConfigurationId,
        DateTimeOffset materializedAt)
    {
        var configuration = state.CycleConfigurations.SingleOrDefault(item =>
                item.CycleConfigurationId == cycleConfigurationId)
            ?? throw new InvalidOperationException(
                $"Cycle configuration {cycleConfigurationId} is unavailable.");
        var game = state.Games.SingleOrDefault(item => item.GameId == configuration.GameId)
            ?? throw new InvalidOperationException(
                $"Cycle configuration {cycleConfigurationId} has no containing Game.");
        var profile = GameProfileCatalogue.Resolve(configuration);

        ValidateGameAndConfiguration(game, configuration, profile, materializedAt);

        if (configuration.Status == CycleConfigurationStatus.Materialized)
        {
            var existing = state.Cycles.SingleOrDefault(item =>
                    item.CycleConfigurationId == configuration.CycleConfigurationId)
                ?? throw new InvalidOperationException(
                    $"Materialized Cycle configuration {configuration.CycleConfigurationId} has no Cycle.");
            if (existing.CycleId != configuration.CycleConfigurationId
                || existing.GameId != game.GameId
                || game.Status != GameLifecycleStatus.Active)
            {
                throw new InvalidOperationException(
                    $"Materialized Cycle configuration {configuration.CycleConfigurationId} does not match its deterministic Cycle.");
            }
            return new CycleMaterializationResult(
                game.GameId,
                configuration.CycleConfigurationId,
                existing.CycleId,
                profile.Key,
                false);
        }

        if (configuration.Status != CycleConfigurationStatus.Locked)
        {
            throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} must be locked before materialization.");
        }
        if (game.Status != GameLifecycleStatus.Starting)
        {
            throw new InvalidOperationException(
                $"Game {game.GameId} must be Starting before its roster is materialized.");
        }
        if (state.Cycles.Any(item => item.GameId == game.GameId
                                     && item.Status is CycleStatus.Active or CycleStatus.RecoveryRequired))
        {
            throw new InvalidOperationException($"Game {game.GameId} already has an operational Cycle.");
        }

        var roster = ResolveRoster(state, game, configuration, profile);
        var previousCycleId = ResolvePreviousCycleId(state, game, configuration);
        var startAt = configuration.ScheduledStartAt ?? materializedAt;
        var endAt = configuration.ScheduledEndAt
            ?? startAt.AddDays(profile.CyclePolicy.DefaultDurationDays);
        if (endAt <= startAt)
        {
            throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} must end after it starts.");
        }

        var cycle = new Cycle
        {
            CycleId = configuration.CycleConfigurationId,
            GameId = game.GameId,
            CycleConfigurationId = configuration.CycleConfigurationId,
            PreviousCycleId = previousCycleId,
            Name = configuration.SequenceNumber == 1
                ? profile.DisplayName
                : $"{profile.DisplayName} · Cycle {configuration.SequenceNumber}",
            StartAt = startAt,
            EndAt = endAt,
            TickLengthMinutes = profile.CyclePolicy.TickLengthMinutes,
            CurrentTickNumber = 0,
            Status = CycleStatus.Active,
            TurnStage = TurnResolutionStage.CommandOpen,
            MapProfileKey = profile.Map.Key,
            MapProfileVersion = profile.Map.Version,
            MapProfileContentHash = profile.Map.ContentHash,
            MapSeed = configuration.MapSeed,
            ScenarioProfileKey = profile.Scenario.Key,
            ScenarioProfileVersion = profile.Scenario.Version,
            ScenarioProfileContentHash = profile.Scenario.ContentHash,
            ScenarioSeed = configuration.ScenarioSeed,
            CyclePolicyKey = profile.CyclePolicy.Key,
            CyclePolicyVersion = profile.CyclePolicy.Version,
            CyclePolicyContentHash = profile.CyclePolicy.ContentHash,
            SchedulingMode = profile.CyclePolicy.SchedulingMode,
            NextTickAt = profile.CyclePolicy.SchedulingMode == CycleSchedulingMode.Scheduled
                ? startAt
                : null,
            ProfileProvenanceStatus = ProvenanceStatus.Verified,
            CreatedByPlayerId = game.CreatedByPlayerId,
            CreatedAt = materializedAt
        };
        if (state.Cycles.Any(item => item.CycleId == cycle.CycleId))
        {
            throw new InvalidOperationException(
                $"Cycle identifier {cycle.CycleId} is already in use by another configuration.");
        }
        state.Cycles.Add(cycle);

        var map = MaterializeMap(state, cycle, profile.Map);
        MaterializeScenario(state, game, cycle, profile.Scenario, roster, map, materializedAt);
        AddProvenanceEvents(state, game, configuration, cycle, profile, roster, materializedAt);

        configuration.Status = CycleConfigurationStatus.Materialized;
        configuration.MaterializedAt = materializedAt;
        game.Status = GameLifecycleStatus.Active;
        game.FirstStartedAt ??= materializedAt;

        return new CycleMaterializationResult(
            game.GameId,
            configuration.CycleConfigurationId,
            cycle.CycleId,
            profile.Key,
            true);
    }

    private static void ValidateGameAndConfiguration(
        Game game,
        CycleConfiguration configuration,
        GameProfileDefinition profile,
        DateTimeOffset materializedAt)
    {
        if (game.Purpose != profile.Purpose
            || game.Visibility != profile.GamePolicy.Visibility
            || !string.Equals(game.GamePolicyKey, profile.GamePolicy.Key, StringComparison.Ordinal)
            || game.GamePolicyVersion != profile.GamePolicy.Version
            || !string.Equals(game.GamePolicyContentHash, profile.GamePolicy.ContentHash, StringComparison.OrdinalIgnoreCase)
            || game.PolicyProvenanceStatus != ProvenanceStatus.Verified)
        {
            throw new InvalidOperationException(
                $"Game {game.GameId} does not match immutable profile {profile.Key} v{profile.Version}.");
        }
        if (game.CreationSource == GameCreationSource.LegacyImport)
        {
            throw new InvalidOperationException("The roster-aware factory cannot materialize a legacy-import Game.");
        }
        if (configuration.ProvenanceStatus != ProvenanceStatus.Verified
            || configuration.MinimumHumanSeats != profile.MinimumHumanSeats
            || configuration.MaximumHumanSeats != profile.MaximumHumanSeats
            || configuration.SchedulingMode != profile.CyclePolicy.SchedulingMode
            || configuration.TickLengthMinutes != profile.CyclePolicy.TickLengthMinutes
            || !configuration.MapSeed.HasValue
            || !configuration.ScenarioSeed.HasValue)
        {
            throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} does not retain the complete locked profile contract.");
        }
        if (!configuration.LockedAt.HasValue || materializedAt < configuration.LockedAt.Value)
        {
            throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} cannot be materialized before it is locked.");
        }
    }

    private static IReadOnlyList<Player> ResolveRoster(
        GameState state,
        Game game,
        CycleConfiguration configuration,
        GameProfileDefinition profile)
    {
        var enrolments = state.GameEnrolments
            .Where(item => item.GameId == game.GameId && item.Status == GameEnrolmentStatus.Enrolled)
            .ToArray();
        var playersById = state.Players.ToDictionary(item => item.PlayerId);
        var roster = enrolments.Select(enrolment =>
            {
                if (!playersById.TryGetValue(enrolment.PlayerId, out var player))
                {
                    throw new InvalidOperationException(
                        $"Game enrolment {enrolment.GameEnrolmentId} has no existing Player account.");
                }
                if (player.Kind != PlayerKind.Human || player.Status != PlayerStatus.Active)
                {
                    throw new InvalidOperationException(
                        $"Game enrolment {enrolment.GameEnrolmentId} does not belong to an active Human Player.");
                }
                return player;
            })
            .OrderBy(item => StableRosterOrder(configuration.ScenarioSeed!.Value, item.PlayerId))
            .ThenBy(item => item.PlayerId)
            .ToArray();
        if (roster.Length < profile.MinimumHumanSeats || roster.Length > profile.MaximumHumanSeats)
        {
            throw new InvalidOperationException(
                $"Game {game.GameId} has {roster.Length} enrolled Human Players; profile {profile.Key} requires {profile.MinimumHumanSeats}–{profile.MaximumHumanSeats}.");
        }
        return roster;
    }

    private static Guid? ResolvePreviousCycleId(
        GameState state,
        Game game,
        CycleConfiguration configuration)
    {
        var previousConfigurations = state.CycleConfigurations
            .Where(item => item.GameId == game.GameId
                           && item.SequenceNumber < configuration.SequenceNumber)
            .OrderByDescending(item => item.SequenceNumber)
            .ToArray();
        if (configuration.SequenceNumber == 1)
        {
            if (previousConfigurations.Length != 0)
            {
                throw new InvalidOperationException(
                    $"First Cycle configuration {configuration.CycleConfigurationId} cannot have a predecessor.");
            }
            return null;
        }

        var previousConfiguration = previousConfigurations.FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} is missing its predecessor configuration.");
        if (previousConfiguration.SequenceNumber != configuration.SequenceNumber - 1
            || previousConfiguration.Status != CycleConfigurationStatus.Materialized)
        {
            throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} has a non-contiguous predecessor.");
        }
        var previousCycle = state.Cycles.SingleOrDefault(item =>
                item.CycleConfigurationId == previousConfiguration.CycleConfigurationId)
            ?? throw new InvalidOperationException(
                $"Predecessor configuration {previousConfiguration.CycleConfigurationId} has no Cycle.");
        if (previousCycle.Status != CycleStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Predecessor Cycle {previousCycle.CycleId} must be completed before a successor is materialized.");
        }
        return previousCycle.CycleId;
    }

    private static MaterializedMap MaterializeMap(
        GameState state,
        Cycle cycle,
        MapProfileDefinition profile)
    {
        var sectorIds = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var definition in profile.Sectors.OrderBy(item => item.SortOrder))
        {
            var sectorId = CreateId(cycle.CycleId, "sector", definition.Key);
            sectorIds.Add(definition.Key, sectorId);
            state.Sectors.Add(new GalaxySector
            {
                SectorId = sectorId,
                CycleId = cycle.CycleId,
                SectorName = definition.Name,
                CentreX = definition.CentreX,
                CentreY = definition.CentreY,
                SortOrder = definition.SortOrder
            });
        }

        var systems = new Dictionary<string, GalaxySystem>(StringComparer.Ordinal);
        foreach (var definition in profile.Systems.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var system = new GalaxySystem
            {
                SystemId = CreateId(cycle.CycleId, "system", definition.Key),
                CycleId = cycle.CycleId,
                SectorId = sectorIds[definition.SectorKey],
                SystemName = definition.Name,
                X = definition.X,
                Y = definition.Y,
                IndustryOutput = definition.IndustryOutput,
                ResearchOutput = definition.ResearchOutput,
                PopulationOutput = definition.PopulationOutput,
                StrategicValue = definition.StrategicValue,
                HistoricalSignificance = definition.HistoricalSignificance,
                CreatedAt = cycle.CreatedAt
            };
            systems.Add(definition.Key, system);
            state.Systems.Add(system);
        }

        foreach (var definition in profile.Routes.OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            var first = systems[definition.FirstSystemKey];
            var second = systems[definition.SecondSystemKey];
            state.SystemLinks.Add(new SystemLink
            {
                SystemLinkId = CreateId(cycle.CycleId, "route", definition.Key),
                CycleId = cycle.CycleId,
                SystemAId = first.SystemId,
                SystemBId = second.SystemId,
                Distance = decimal.Round((decimal)Distance(first, second), 2),
                TravelTicks = definition.TravelTicks
            });
        }

        return new MaterializedMap(systems);
    }

    private static void MaterializeScenario(
        GameState state,
        Game game,
        Cycle cycle,
        ScenarioProfileDefinition scenario,
        IReadOnlyList<Player> roster,
        MaterializedMap map,
        DateTimeOffset createdAt)
    {
        var homes = SelectHomeSystems(scenario, map.Systems, roster.Count);
        for (var index = 0; index < roster.Count; index++)
        {
            var player = roster[index];
            var empireId = CreateId(cycle.CycleId, "empire", player.PlayerId.ToString("N"));
            var home = homes[index];
            var empireName = scenario.HumanEmpireNames[index];
            var faction = new Faction
            {
                FactionId = empireId,
                CycleId = cycle.CycleId,
                EmpireId = empireId,
                FactionName = empireName,
                Kind = FactionKind.Empire,
                Status = FactionStatus.Active,
                CreatedAt = createdAt
            };
            state.Empires.Add(new Empire
            {
                EmpireId = empireId,
                CycleId = cycle.CycleId,
                PlayerId = player.PlayerId,
                EmpireName = empireName,
                HomeSystemId = home.SystemId,
                CreatedAt = createdAt,
                Status = EmpireStatus.Active
            });
            state.Factions.Add(faction);
            state.MatchParticipants.Add(new MatchParticipant
            {
                MatchParticipantId = CreateId(cycle.CycleId, "participant", player.PlayerId.ToString("N")),
                GameId = game.GameId,
                CycleId = cycle.CycleId,
                PlayerId = player.PlayerId,
                EmpireId = empireId,
                Status = MatchParticipantStatus.Active,
                JoinedAt = createdAt
            });
            state.EmpireResources.Add(new EmpireResource
            {
                EmpireResourceId = CreateId(cycle.CycleId, "resources", player.PlayerId.ToString("N")),
                EmpireId = empireId,
                Industry = scenario.StartingIndustry,
                Research = scenario.StartingResearch,
                Population = scenario.StartingPopulation,
                UpdatedAt = createdAt
            });
            state.EmpirePriorities.Add(new EmpirePriority
            {
                EmpirePriorityId = CreateId(cycle.CycleId, "priorities", player.PlayerId.ToString("N")),
                EmpireId = empireId,
                IndustryWeight = 0,
                ResearchWeight = 0,
                MilitaryWeight = scenario.InitialMilitaryWeight,
                ExpansionWeight = scenario.InitialExpansionWeight,
                UpdatedAt = createdAt
            });
            var admiral = new Admiral
            {
                AdmiralId = CreateId(cycle.CycleId, "admiral", player.PlayerId.ToString("N")),
                CycleId = cycle.CycleId,
                EmpireId = empireId,
                AdmiralName = scenario.HumanAdmiralNames[index],
                ReputationScore = 0,
                Status = AdmiralStatus.Active,
                CreatedAt = createdAt,
                UpdatedAt = createdAt
            };
            state.Admirals.Add(admiral);

            for (var fleetIndex = 0; fleetIndex < scenario.HumanFleets.Count; fleetIndex++)
            {
                var template = scenario.HumanFleets[fleetIndex];
                var system = string.Equals(template.SystemKey, "@home", StringComparison.Ordinal)
                    ? home
                    : map.Systems[template.SystemKey];
                state.Fleets.Add(new Fleet
                {
                    FleetId = CreateId(cycle.CycleId, "human-fleet", $"{player.PlayerId:N}:{fleetIndex}"),
                    CycleId = cycle.CycleId,
                    EmpireId = empireId,
                    FactionId = faction.FactionId,
                    AdmiralId = template.UsesStartingAdmiral ? admiral.AdmiralId : null,
                    FleetName = template.NameTemplate.Replace("{empire}", empireName, StringComparison.Ordinal),
                    CurrentSystemId = system.SystemId,
                    ShipCount = template.ShipCount,
                    Status = FleetStatus.Active,
                    CreatedAt = createdAt
                });
            }
        }

        if (scenario.NeutralFaction is not { } neutral)
        {
            return;
        }
        var neutralFaction = new Faction
        {
            FactionId = CreateId(cycle.CycleId, "neutral-faction", neutral.Name),
            CycleId = cycle.CycleId,
            EmpireId = null,
            FactionName = neutral.Name,
            Kind = FactionKind.Neutral,
            Status = FactionStatus.Active,
            CreatedAt = createdAt
        };
        state.Factions.Add(neutralFaction);
        for (var index = 0; index < neutral.Fleets.Count; index++)
        {
            var template = neutral.Fleets[index];
            state.Fleets.Add(new Fleet
            {
                FleetId = CreateId(cycle.CycleId, "neutral-fleet", index.ToString()),
                CycleId = cycle.CycleId,
                EmpireId = Guid.Empty,
                FactionId = neutralFaction.FactionId,
                FleetName = template.NameTemplate,
                CurrentSystemId = map.Systems[template.SystemKey].SystemId,
                ShipCount = template.ShipCount,
                Status = FleetStatus.Active,
                CreatedAt = createdAt
            });
        }
    }

    private static IReadOnlyList<GalaxySystem> SelectHomeSystems(
        ScenarioProfileDefinition scenario,
        IReadOnlyDictionary<string, GalaxySystem> systems,
        int count)
    {
        var concreteHome = scenario.HumanFleets.FirstOrDefault(item =>
            item.UsesStartingAdmiral
            && !string.Equals(item.SystemKey, "@home", StringComparison.Ordinal));
        if (concreteHome is not null)
        {
            if (count != 1)
            {
                throw new InvalidOperationException(
                    $"Scenario {scenario.Key} uses a fixed home and therefore supports exactly one Human Player.");
            }
            return [systems[concreteHome.SystemKey]];
        }

        var candidates = systems.Values
            .OrderByDescending(item => item.StrategicValue)
            .ThenBy(item => item.SystemName, StringComparer.Ordinal)
            .ToList();
        var selected = new List<GalaxySystem> { candidates[0] };
        while (selected.Count < count)
        {
            var next = candidates
                .Except(selected)
                .OrderByDescending(item => selected.Min(existing => Distance(item, existing)))
                .ThenBy(item => item.SystemName, StringComparer.Ordinal)
                .First();
            selected.Add(next);
        }
        return selected;
    }

    private static void AddProvenanceEvents(
        GameState state,
        Game game,
        CycleConfiguration configuration,
        Cycle cycle,
        GameProfileDefinition profile,
        IReadOnlyCollection<Player> roster,
        DateTimeOffset createdAt)
    {
        var factJson = JsonSerializer.Serialize(new
        {
            gameId = game.GameId,
            cycleId = cycle.CycleId,
            cycleConfigurationId = configuration.CycleConfigurationId,
            profile = new { profile.Key, profile.Version },
            map = new { profile.Map.Key, profile.Map.Version, profile.Map.ContentHash, configuration.MapSeed },
            scenario = new { profile.Scenario.Key, profile.Scenario.Version, profile.Scenario.ContentHash, configuration.ScenarioSeed },
            cyclePolicy = new { profile.CyclePolicy.Key, profile.CyclePolicy.Version, profile.CyclePolicy.ContentHash },
            rosterPlayerIds = roster.Select(item => item.PlayerId).OrderBy(item => item).ToArray()
        }, GameStateJson.Options);
        state.Events.Add(new EventRecord
        {
            EventId = CreateId(cycle.CycleId, "event", "cycle-seeded"),
            CycleId = cycle.CycleId,
            TickNumber = 0,
            EventType = EventType.CycleSeeded,
            Severity = EventSeverity.Normal,
            FactJson = factJson,
            DisplayText = $"{cycle.Name} materialized from {profile.Key} v{profile.Version} for {roster.Count} Human Player{(roster.Count == 1 ? "" : "s")}.",
            CreatedAt = createdAt
        });
        state.GameLifecycleEvents.Add(new GameLifecycleEvent
        {
            GameLifecycleEventId = CreateId(cycle.CycleId, "game-event", "cycle-materialized"),
            GameId = game.GameId,
            Type = GameLifecycleEventType.StatusChanged,
            ActorPlayerId = game.CreatedByPlayerId,
            FromStatus = GameLifecycleStatus.Starting.ToString(),
            ToStatus = GameLifecycleStatus.Active.ToString(),
            Reason = "The locked roster and profile were materialized into an active Cycle.",
            CorrelationId = configuration.CycleConfigurationId.ToString("N"),
            FactJson = factJson,
            CreatedAt = createdAt
        });
    }

    private static ulong StableRosterOrder(int scenarioSeed, Guid playerId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{scenarioSeed}:{playerId:N}"));
        return BinaryPrimitives.ReadUInt64LittleEndian(hash);
    }

    private static Guid CreateId(Guid cycleId, string kind, string key)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"cycles:{cycleId:N}:{kind}:{key}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static double Distance(GalaxySystem first, GalaxySystem second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt((x * x) + (y * y));
    }

    private sealed record MaterializedMap(IReadOnlyDictionary<string, GalaxySystem> Systems);
}
