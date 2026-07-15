using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cycles.Core;

public static class GameSeeder
{
    public const string CuratedColdStartScenarioKey = "development-cold-start-v1";
    public const string CanonicalGalaxyTopologyKey = "sector-crown-v1";
    public const int CanonicalGalaxySectorCount = 16;
    public const int CanonicalGalaxySystemCount = 280;

    private const int CanonicalGalaxySeed = 71421;
    private const double GoldenAngle = 2.399963229728653;

    private static readonly SectorDefinition[] CanonicalSectors =
    [
        new("Aster Reach", 18, 500, 82, "Aster"),
        new("Auric Veil", 15, 650, 103, "Auric"),
        new("Cinder March", 21, 780, 165, "Cinder"),
        new("Glass Expanse", 14, 872, 252, "Glass"),
        new("Hollow Crown", 17, 900, 350, "Hollow"),
        new("Juniper Rift", 20, 862, 458, "Juniper"),
        new("Lacuna Verge", 12, 775, 540, "Lacuna"),
        new("Mournstar Deep", 19, 650, 602, "Mourn"),
        new("Orison Fold", 16, 500, 620, "Orison"),
        new("Red Lattice", 22, 350, 603, "Crimson"),
        new("Sable Drift", 13, 225, 542, "Sable"),
        new("Ternary Reach", 18, 138, 458, "Ternary"),
        new("Umbral Marches", 24, 98, 350, "Umbral"),
        new("Verdant Coil", 15, 132, 245, "Viridian"),
        new("Warden Line", 20, 225, 155, "Warden"),
        new("Zenith Arc", 16, 355, 102, "Zenith")
    ];

    private static readonly string[] GeneratedSystemSuffixes =
    [
        "Lantern", "Anchorage", "Bastion", "Meridian", "Crossing", "Relay",
        "Spur", "Crown", "Haven", "Watch", "Gate", "Shoal", "Beacon",
        "Vault", "Span", "Harbour", "Needle", "Cairn", "Reach", "Wake",
        "Station", "Refuge", "Coil", "Array"
    ];

    private static readonly string[] SystemNames =
    [
        "Pseudopolis",
        "Yanaka's Reach",
        "Treaty Gate",
        "Aster Vale",
        "Brightfall",
        "Cinderhome",
        "Dawnward",
        "Ebon Strait",
        "Glass Meridian",
        "Hollow Crown",
        "Juniper Rift",
        "Keystone",
        "Lacuna",
        "Mournstar",
        "Nadir Crossing",
        "Orison",
        "Pale Harbour",
        "Quietus",
        "Red Lattice",
        "Sable Point",
        "Ternary",
        "Umbral Way",
        "Verdant Coil",
        "Warden's Line",
        "Xanthe",
        "Yarrow",
        "Zenith Yard"
    ];

    private static readonly string[] EmpireNames =
    [
        "Aurelian Compact",
        "Khepri Mandate",
        "Novan League",
        "Vestige Combine",
        "Helio Archive",
        "Marrow Directorate"
    ];

    private static readonly string[] AdmiralNames =
    [
        "Elian Voss",
        "Mara Sutekh",
        "Tavian Orre",
        "Ilya Sen",
        "Nadia Kepler",
        "Soren Vale"
    ];

    public static GameState CreateDefault(
        int systemCount = 24,
        int empireCount = 4,
        int seed = 71421,
        DateTimeOffset? createdAt = null)
    {
        return Create(systemCount, empireCount, seed, createdAt, Guid.NewGuid);
    }

    public static GameState CreateCuratedColdStart(DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var identitySequence = new DeterministicIdentitySequence(CanonicalGalaxySeed);
        var state = Create(CanonicalGalaxySystemCount, 4, CanonicalGalaxySeed, now, identitySequence.Next);

        ApplyCuratedColdStart(state, now, identitySequence.Next);
        return state;
    }

    internal static GameState CreateDeterministicScenario(
        int systemCount,
        int empireCount,
        int seed,
        DateTimeOffset createdAt)
    {
        var identitySequence = new DeterministicIdentitySequence(seed);
        return Create(systemCount, empireCount, seed, createdAt, identitySequence.Next);
    }

    public static GalaxyTopologyUpgradeResult UpgradeGalaxyTopology(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var activeCycles = state.Cycles.Where(item => item.Status == CycleStatus.Active).ToArray();
        if (activeCycles.Length != 1)
        {
            throw new InvalidOperationException($"Galaxy topology upgrade requires exactly one active Cycle; found {activeCycles.Length}.");
        }

        var cycle = activeCycles[0];
        if (IsCanonicalGalaxy(state, cycle.CycleId))
        {
            return new GalaxyTopologyUpgradeResult(false, 0, 0, 0, 0);
        }

        var cycleSystems = state.Systems.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var legacyNames = SystemNames.Take(24).ToHashSet(StringComparer.Ordinal);
        var hasCuratedBriefing = state.Events.Any(item =>
            item.CycleId == cycle.CycleId
            && item.EventType == EventType.OpeningBriefingIssued
            && item.FactJson.Contains(CuratedColdStartScenarioKey, StringComparison.Ordinal));
        if (cycleSystems.Length != legacyNames.Count
            || !cycleSystems.Select(item => item.SystemName).ToHashSet(StringComparer.Ordinal).SetEquals(legacyNames)
            || !hasCuratedBriefing)
        {
            throw new InvalidOperationException(
                "Galaxy topology upgrade only supports the original 24-system curated Development opening or an already-current canonical galaxy.");
        }

        if (state.Sectors.Any(item => item.CycleId == cycle.CycleId))
        {
            throw new InvalidOperationException("Galaxy topology upgrade found an unrecognised partial sector model and will not guess how to repair it.");
        }

        if (state.Fleets.Any(item => item.CycleId == cycle.CycleId
                                    && (item.Status == FleetStatus.InTransit || item.DestinationSystemId.HasValue))
            || state.FleetOrders.Any(item => item.CycleId == cycle.CycleId && item.Status == FleetOrderStatus.Pending))
        {
            throw new InvalidOperationException(
                "Galaxy topology upgrade requires no fleets in transit and no pending orders. Resolve or cancel them before changing the route network.");
        }

        var assignments = BuildCanonicalAssignments(cycle.CycleId);
        AddCanonicalSectors(state, cycle.CycleId);
        var existingByName = cycleSystems.ToDictionary(item => item.SystemName, StringComparer.Ordinal);
        var systemsAdded = 0;
        var now = DateTimeOffset.UtcNow;
        foreach (var assignment in assignments)
        {
            if (existingByName.TryGetValue(assignment.SystemName, out var existing))
            {
                existing.SectorId = assignment.SectorId;
                existing.X = assignment.X;
                existing.Y = assignment.Y;
                continue;
            }

            state.Systems.Add(CreateCanonicalSystem(cycle.CycleId, assignment, now));
            systemsAdded++;
        }

        var linksRemoved = state.SystemLinks.RemoveAll(item => item.CycleId == cycle.CycleId);
        var linksAdded = AddCanonicalLinks(state, cycle.CycleId, assignments);
        return new GalaxyTopologyUpgradeResult(true, CanonicalGalaxySectorCount, systemsAdded, linksAdded, linksRemoved);
    }

    private static GameState Create(
        int systemCount,
        int empireCount,
        int seed,
        DateTimeOffset? createdAt,
        Func<Guid> nextId)
    {
        if (systemCount < empireCount)
        {
            throw new ArgumentOutOfRangeException(nameof(systemCount), "There must be at least one system per empire.");
        }

        var now = createdAt ?? DateTimeOffset.UtcNow;
        var state = new GameState();
        var cycle = new Cycle
        {
            CycleId = nextId(),
            Name = $"Cycle {now:yyyy.MM}",
            StartAt = now,
            EndAt = now.AddDays(90),
            TickLengthMinutes = 60,
            CurrentTickNumber = 0,
            Status = CycleStatus.Active,
            CreatedAt = now
        };

        state.Cycles.Add(cycle);
        AddGalaxy(state, cycle, systemCount, seed, now, nextId);
        AddPlayersAndEmpires(state, cycle, empireCount, now, nextId);

        state.Events.Add(new EventRecord
        {
            EventId = nextId(),
            CycleId = cycle.CycleId,
            TickNumber = 0,
            EventType = EventType.CycleSeeded,
            Severity = EventSeverity.Normal,
            DisplayText = $"The {cycle.Name} began with {state.Empires.Count} empires and {state.Systems.Count} systems.",
            FactJson = JsonSerializer.Serialize(new
            {
                cycleId = cycle.CycleId,
                empireCount = state.Empires.Count,
                systemCount = state.Systems.Count,
                sectorCount = state.Sectors.Count,
                topologyKey = state.Sectors.Count == CanonicalGalaxySectorCount ? CanonicalGalaxyTopologyKey : "single-sector-v1",
                seed
            }, GameStateJson.Options),
            CreatedAt = now
        });

        return state;
    }

    private static void AddGalaxy(
        GameState state,
        Cycle cycle,
        int systemCount,
        int seed,
        DateTimeOffset now,
        Func<Guid> nextId)
    {
        if (systemCount == CanonicalGalaxySystemCount && seed == CanonicalGalaxySeed)
        {
            AddCanonicalGalaxy(state, cycle, now);
            return;
        }

        var random = new Random(seed);
        var sector = new GalaxySector
        {
            SectorId = CreateTopologyId(cycle.CycleId, "sector", "Known Space"),
            CycleId = cycle.CycleId,
            SectorName = "Known Space",
            CentreX = 500,
            CentreY = 350,
            SortOrder = 0
        };
        state.Sectors.Add(sector);
        var names = SystemNames
            .Concat(Enumerable.Range(1, Math.Max(0, systemCount - SystemNames.Length)).Select(i => $"Frontier {i.ToString(CultureInfo.InvariantCulture)}"))
            .Take(systemCount)
            .ToArray();

        for (var index = 0; index < systemCount; index++)
        {
            var industry = random.Next(20, 86);
            var research = random.Next(15, 76);
            var population = random.Next(10, 66);
            var strategicValue = (industry + research + population) / 5;

            state.Systems.Add(new GalaxySystem
            {
                SystemId = nextId(),
                CycleId = cycle.CycleId,
                SectorId = sector.SectorId,
                SystemName = names[index],
                X = random.Next(60, 941),
                Y = random.Next(60, 641),
                IndustryOutput = industry,
                ResearchOutput = research,
                PopulationOutput = population,
                StrategicValue = strategicValue,
                HistoricalSignificance = index < 3 ? 2 : 0,
                CreatedAt = now
            });
        }

        EnsureConnectivity(state, cycle.CycleId, nextId);
        AddNearestNeighbourLinks(state, cycle.CycleId, neighboursPerSystem: 2, nextId);
    }

    private static void AddCanonicalGalaxy(GameState state, Cycle cycle, DateTimeOffset now)
    {
        AddCanonicalSectors(state, cycle.CycleId);
        var assignments = BuildCanonicalAssignments(cycle.CycleId);
        state.Systems.AddRange(assignments.Select(assignment => CreateCanonicalSystem(cycle.CycleId, assignment, now)));
        AddCanonicalLinks(state, cycle.CycleId, assignments);
    }

    private static void AddCanonicalSectors(GameState state, Guid cycleId)
    {
        for (var index = 0; index < CanonicalSectors.Length; index++)
        {
            var definition = CanonicalSectors[index];
            state.Sectors.Add(new GalaxySector
            {
                SectorId = CreateTopologyId(cycleId, "sector", definition.Name),
                CycleId = cycleId,
                SectorName = definition.Name,
                CentreX = definition.CentreX,
                CentreY = definition.CentreY,
                SortOrder = index
            });
        }
    }

    private static List<CanonicalSystemAssignment> BuildCanonicalAssignments(Guid cycleId)
    {
        var anchoredNames = new Dictionary<int, string[]>
        {
            [0] =
            [
                "Treaty Gate", "Yanaka's Reach", "Pseudopolis", "Brightfall", "Cinderhome", "Dawnward",
                "Ebon Strait", "Glass Meridian", "Hollow Crown", "Juniper Rift", "Keystone", "Lacuna",
                "Mournstar", "Orison", "Quietus", "Pale Harbour", "Nadir Crossing", "Aster Vale"
            ],
            [6] = ["Yarrow"],
            [7] = ["Xanthe"],
            [9] = ["Red Lattice"],
            [10] = ["Sable Point"],
            [11] = ["Ternary"],
            [12] = ["Umbral Way"],
            [13] = ["Verdant Coil"],
            [14] = ["Warden's Line"],
            [15] = ["Zenith Yard"]
        };

        var usedNames = anchoredNames.Values.SelectMany(item => item).ToHashSet(StringComparer.Ordinal);
        var assignments = new List<CanonicalSystemAssignment>(CanonicalGalaxySystemCount);
        for (var sectorIndex = 0; sectorIndex < CanonicalSectors.Length; sectorIndex++)
        {
            var definition = CanonicalSectors[sectorIndex];
            var members = anchoredNames.TryGetValue(sectorIndex, out var anchors)
                ? anchors.ToList()
                : [];

            foreach (var suffix in GeneratedSystemSuffixes)
            {
                if (members.Count == definition.SystemCount)
                {
                    break;
                }

                var candidate = $"{definition.SystemRoot} {suffix}";
                if (!string.Equals(candidate, definition.Name, StringComparison.Ordinal) && usedNames.Add(candidate))
                {
                    members.Add(candidate);
                }
            }

            if (members.Count != definition.SystemCount)
            {
                throw new InvalidOperationException($"Sector {definition.Name} could not be assigned {definition.SystemCount} unique system names.");
            }

            for (var localIndex = 0; localIndex < members.Count; localIndex++)
            {
                var angle = (-Math.PI / 2) + (sectorIndex * 0.31) + ((Math.PI * 2 * localIndex) / members.Count);
                var radiusX = 44 + (9 * Math.Sin((localIndex * GoldenAngle) + sectorIndex));
                var radiusY = 34 + (8 * Math.Cos((localIndex * 1.73) + sectorIndex));
                var x = (int)Math.Round(definition.CentreX + (Math.Cos(angle) * radiusX));
                var y = (int)Math.Round(definition.CentreY + (Math.Sin(angle) * radiusY));
                assignments.Add(new CanonicalSystemAssignment(
                    members[localIndex],
                    sectorIndex,
                    localIndex,
                    CreateTopologyId(cycleId, "sector", definition.Name),
                    x,
                    y,
                    localIndex == 0 || localIndex == members.Count / 2));
            }
        }

        return assignments;
    }

    private static GalaxySystem CreateCanonicalSystem(
        Guid cycleId,
        CanonicalSystemAssignment assignment,
        DateTimeOffset now)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"{CanonicalGalaxyTopologyKey}:{cycleId:N}:{assignment.SystemName}"));
        var industry = 20 + (hash[0] % 66);
        var research = 15 + (hash[1] % 61);
        var population = 10 + (hash[2] % 56);
        var strategicValue = ((industry + research + population) / 5) + (assignment.IsGateway ? 12 : 0);
        return new GalaxySystem
        {
            SystemId = CreateTopologyId(cycleId, "system", assignment.SystemName),
            CycleId = cycleId,
            SectorId = assignment.SectorId,
            SystemName = assignment.SystemName,
            X = assignment.X,
            Y = assignment.Y,
            IndustryOutput = industry,
            ResearchOutput = research,
            PopulationOutput = population,
            StrategicValue = strategicValue,
            HistoricalSignificance = assignment.IsGateway ? 1 : 0,
            CreatedAt = now
        };
    }

    private static int AddCanonicalLinks(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var systemsByName = state.Systems
            .Where(item => item.CycleId == cycleId)
            .ToDictionary(item => item.SystemName, StringComparer.Ordinal);
        var sectorMembers = assignments
            .GroupBy(item => item.SectorIndex)
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(item => item.LocalIndex).ToArray())
            .ToArray();
        var added = 0;

        foreach (var members in sectorMembers)
        {
            for (var index = 0; index < members.Length; index++)
            {
                added += AddCanonicalLink(
                    state,
                    cycleId,
                    systemsByName[members[index].SystemName],
                    systemsByName[members[(index + 1) % members.Length].SystemName],
                    travelTicks: 1);
            }
        }

        for (var sectorIndex = 0; sectorIndex < sectorMembers.Length; sectorIndex++)
        {
            var current = sectorMembers[sectorIndex];
            var next = sectorMembers[(sectorIndex + 1) % sectorMembers.Length];
            added += AddCanonicalLink(
                state,
                cycleId,
                systemsByName[current[0].SystemName],
                systemsByName[next[next.Length / 2].SystemName],
                travelTicks: 2);
        }

        return added;
    }

    private static int AddCanonicalLink(
        GameState state,
        Guid cycleId,
        GalaxySystem first,
        GalaxySystem second,
        int travelTicks)
    {
        if (state.SystemLinks.Any(item => item.CycleId == cycleId && item.Connects(first.SystemId, second.SystemId)))
        {
            return 0;
        }

        var endpointKey = string.CompareOrdinal(first.SystemId.ToString("N"), second.SystemId.ToString("N")) < 0
            ? $"{first.SystemId:N}:{second.SystemId:N}"
            : $"{second.SystemId:N}:{first.SystemId:N}";
        state.SystemLinks.Add(new SystemLink
        {
            SystemLinkId = CreateTopologyId(cycleId, "link", endpointKey),
            CycleId = cycleId,
            SystemAId = first.SystemId,
            SystemBId = second.SystemId,
            Distance = decimal.Round((decimal)Distance(first, second), 2),
            TravelTicks = travelTicks
        });
        return 1;
    }

    private static bool IsCanonicalGalaxy(GameState state, Guid cycleId)
    {
        var assignments = BuildCanonicalAssignments(cycleId);
        var expectedByName = assignments.ToDictionary(item => item.SystemName, StringComparer.Ordinal);
        var systems = state.Systems.Where(item => item.CycleId == cycleId).ToArray();
        var sectors = state.Sectors.Where(item => item.CycleId == cycleId).ToArray();
        if (systems.Length != CanonicalGalaxySystemCount
            || sectors.Length != CanonicalGalaxySectorCount
            || !systems.Select(item => item.SystemName).ToHashSet(StringComparer.Ordinal).SetEquals(expectedByName.Keys))
        {
            return false;
        }

        var sectorsById = sectors.ToDictionary(item => item.SectorId);
        if (systems.Any(system =>
                !expectedByName.TryGetValue(system.SystemName, out var expected)
                || system.SectorId != expected.SectorId
                || system.X != expected.X
                || system.Y != expected.Y)
            || sectors.Any(sector => sector.SortOrder < 0
                                     || sector.SortOrder >= CanonicalSectors.Length
                                     || sector.SectorName != CanonicalSectors[sector.SortOrder].Name)
            || systems.Any(system => !sectorsById.ContainsKey(system.SectorId)))
        {
            return false;
        }

        var systemNamesById = systems.ToDictionary(item => item.SystemId, item => item.SystemName);
        var actualPairs = state.SystemLinks
            .Where(item => item.CycleId == cycleId)
            .Select(item => NormalizedPair(systemNamesById[item.SystemAId], systemNamesById[item.SystemBId]))
            .ToHashSet(StringComparer.Ordinal);
        return actualPairs.SetEquals(ExpectedCanonicalLinkPairs(assignments));
    }

    private static HashSet<string> ExpectedCanonicalLinkPairs(IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var members = assignments
            .GroupBy(item => item.SectorIndex)
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(item => item.LocalIndex).ToArray())
            .ToArray();
        var pairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sector in members)
        {
            for (var index = 0; index < sector.Length; index++)
            {
                pairs.Add(NormalizedPair(sector[index].SystemName, sector[(index + 1) % sector.Length].SystemName));
            }
        }

        for (var index = 0; index < members.Length; index++)
        {
            var next = members[(index + 1) % members.Length];
            pairs.Add(NormalizedPair(members[index][0].SystemName, next[next.Length / 2].SystemName));
        }

        return pairs;
    }

    private static string NormalizedPair(string first, string second) =>
        string.CompareOrdinal(first, second) < 0 ? $"{first}\n{second}" : $"{second}\n{first}";

    private static Guid CreateTopologyId(Guid cycleId, string kind, string value)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"cycles:{CanonicalGalaxyTopologyKey}:{cycleId:N}:{kind}:{value}"));
        return new Guid(hash.AsSpan(0, 16));
    }

    private static void EnsureConnectivity(GameState state, Guid cycleId, Func<Guid> nextId)
    {
        var systems = state.Systems.Where(system => system.CycleId == cycleId).ToArray();
        for (var index = 1; index < systems.Length; index++)
        {
            var current = systems[index];
            var nearestPrevious = systems
                .Take(index)
                .OrderBy(other => Distance(current, other))
                .First();

            AddLinkIfMissing(state, cycleId, current, nearestPrevious, nextId);
        }
    }

    private static void AddNearestNeighbourLinks(
        GameState state,
        Guid cycleId,
        int neighboursPerSystem,
        Func<Guid> nextId)
    {
        var systems = state.Systems.Where(system => system.CycleId == cycleId).ToArray();
        foreach (var system in systems)
        {
            foreach (var neighbour in systems
                         .Where(candidate => candidate.SystemId != system.SystemId)
                         .OrderBy(candidate => Distance(system, candidate))
                         .Take(neighboursPerSystem))
            {
                AddLinkIfMissing(state, cycleId, system, neighbour, nextId);
            }
        }
    }

    private static void AddLinkIfMissing(
        GameState state,
        Guid cycleId,
        GalaxySystem first,
        GalaxySystem second,
        Func<Guid> nextId)
    {
        if (state.SystemLinks.Any(link => link.CycleId == cycleId && link.Connects(first.SystemId, second.SystemId)))
        {
            return;
        }

        var distance = Distance(first, second);
        state.SystemLinks.Add(new SystemLink
        {
            SystemLinkId = nextId(),
            CycleId = cycleId,
            SystemAId = first.SystemId,
            SystemBId = second.SystemId,
            Distance = decimal.Round((decimal)distance, 2),
            TravelTicks = distance > 420 ? 2 : 1
        });
    }

    private static void AddPlayersAndEmpires(
        GameState state,
        Cycle cycle,
        int empireCount,
        DateTimeOffset now,
        Func<Guid> nextId)
    {
        var homeSystems = SelectHomeSystems(state.Systems.Where(system => system.CycleId == cycle.CycleId), empireCount);

        for (var index = 0; index < empireCount; index++)
        {
            var player = new Player
            {
                PlayerId = nextId(),
                Username = $"player-{index + 1}",
                Email = $"player-{index + 1}@cycles.local",
                PasswordHash = "prototype",
                CreatedAt = now,
                LastLoginAt = now,
                Status = PlayerStatus.Active
            };
            state.Players.Add(player);

            var homeSystem = homeSystems[index];
            var empire = new Empire
            {
                EmpireId = nextId(),
                CycleId = cycle.CycleId,
                PlayerId = player.PlayerId,
                EmpireName = EmpireNames[index % EmpireNames.Length],
                HomeSystemId = homeSystem.SystemId,
                CreatedAt = now,
                Status = EmpireStatus.Active
            };
            state.Empires.Add(empire);

            state.EmpireResources.Add(new EmpireResource
            {
                EmpireResourceId = nextId(),
                EmpireId = empire.EmpireId,
                Industry = 100,
                Research = 100,
                Population = 100,
                UpdatedAt = now
            });

            state.EmpirePriorities.Add(new EmpirePriority
            {
                EmpirePriorityId = nextId(),
                EmpireId = empire.EmpireId,
                IndustryWeight = 0,
                ResearchWeight = 0,
                MilitaryWeight = StrategicPriorityPolicy.DefaultMilitaryWeight,
                ExpansionWeight = StrategicPriorityPolicy.DefaultExpansionWeight,
                UpdatedAt = now
            });

            var admiral = new Admiral
            {
                AdmiralId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                AdmiralName = AdmiralNames[index % AdmiralNames.Length],
                ReputationScore = 0,
                Status = AdmiralStatus.Active,
                CreatedAt = now,
                UpdatedAt = now
            };
            state.Admirals.Add(admiral);

            state.Fleets.Add(new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                AdmiralId = admiral.AdmiralId,
                FleetName = $"{empire.EmpireName} Home Fleet",
                CurrentSystemId = homeSystem.SystemId,
                ShipCount = 60,
                Status = FleetStatus.Active,
                CreatedAt = now
            });
        }
    }

    private static void ApplyCuratedColdStart(
        GameState state,
        DateTimeOffset now,
        Func<Guid> nextId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("The curated cold start requires an active Cycle.");
        var aurelian = state.Empires.Single(empire => empire.CycleId == cycle.CycleId && empire.EmpireName == "Aurelian Compact");
        var khepri = state.Empires.Single(empire => empire.CycleId == cycle.CycleId && empire.EmpireName == "Khepri Mandate");
        var asterVale = state.Systems.Single(system => system.CycleId == cycle.CycleId && system.SystemName == "Aster Vale");
        var nadirCrossing = state.Systems.Single(system => system.CycleId == cycle.CycleId && system.SystemName == "Nadir Crossing");
        var paleHarbour = state.Systems.Single(system => system.CycleId == cycle.CycleId && system.SystemName == "Pale Harbour");
        var treatyGate = state.Systems.Single(system => system.CycleId == cycle.CycleId && system.SystemName == "Treaty Gate");

        SetCuratedHome(state, cycle.CycleId, "Aurelian Compact", "Aster Vale");
        SetCuratedHome(state, cycle.CycleId, "Khepri Mandate", "Hollow Bastion");
        SetCuratedHome(state, cycle.CycleId, "Novan League", "Orison Anchorage");
        SetCuratedHome(state, cycle.CycleId, "Vestige Combine", "Umbral Bastion");

        if (aurelian.HomeSystemId != asterVale.SystemId)
        {
            throw new InvalidOperationException("The curated cold start no longer maps the Aurelian Compact to Aster Vale.");
        }

        treatyGate.HistoricalSignificance = 4;

        var aurelianVanguard = state.Fleets.Single(fleet => fleet.EmpireId == aurelian.EmpireId);
        aurelianVanguard.FleetName = "Treaty Gate Vanguard";
        aurelianVanguard.CurrentSystemId = treatyGate.SystemId;
        aurelianVanguard.ShipCount = 18;

        var homeGuard = new Fleet
        {
            FleetId = nextId(),
            CycleId = cycle.CycleId,
            EmpireId = aurelian.EmpireId,
            FleetName = "Aurelian Home Guard",
            CurrentSystemId = asterVale.SystemId,
            ShipCount = 30,
            Status = FleetStatus.Active,
            CreatedAt = now
        };
        state.Fleets.Add(homeGuard);

        var surveyFleet = new Fleet
        {
            FleetId = nextId(),
            CycleId = cycle.CycleId,
            EmpireId = aurelian.EmpireId,
            FleetName = "Pale Harbour Survey",
            CurrentSystemId = paleHarbour.SystemId,
            ShipCount = 12,
            Status = FleetStatus.Active,
            CreatedAt = now
        };
        state.Fleets.Add(surveyFleet);

        var khepriRaiders = state.Fleets.Single(fleet => fleet.EmpireId == khepri.EmpireId);
        khepriRaiders.FleetName = "Khepri Gate Raiders";
        khepriRaiders.CurrentSystemId = treatyGate.SystemId;
        khepriRaiders.ShipCount = 20;

        state.Fleets.Add(new Fleet
        {
            FleetId = nextId(),
            CycleId = cycle.CycleId,
            EmpireId = khepri.EmpireId,
            FleetName = "Khepri Home Fleet",
            CurrentSystemId = khepri.HomeSystemId,
            ShipCount = 40,
            Status = FleetStatus.Active,
            CreatedAt = now
        });

        state.Events.Add(new EventRecord
        {
            EventId = nextId(),
            CycleId = cycle.CycleId,
            TickNumber = 0,
            EventType = EventType.OpeningBriefingIssued,
            SystemId = treatyGate.SystemId,
            EmpireId = aurelian.EmpireId,
            Severity = EventSeverity.High,
            DisplayText = "Day 1 briefing: Khepri raiders contest Treaty Gate. Pale Harbour is ready for an outpost, while Nadir Crossing offers immediate expansion.",
            FactJson = JsonSerializer.Serialize(new
            {
                scenarioKey = CuratedColdStartScenarioKey,
                focusSystemId = treatyGate.SystemId,
                objectives = new
                {
                    move = new
                    {
                        fleetId = homeGuard.FleetId,
                        targetSystemId = nadirCrossing.SystemId
                    },
                    colonise = new
                    {
                        fleetId = surveyFleet.FleetId,
                        systemId = paleHarbour.SystemId
                    },
                    attack = new
                    {
                        fleetId = aurelianVanguard.FleetId,
                        systemId = treatyGate.SystemId,
                        targetEmpireId = khepri.EmpireId
                    }
                }
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static void SetCuratedHome(GameState state, Guid cycleId, string empireName, string systemName)
    {
        var empire = state.Empires.Single(item => item.CycleId == cycleId && item.EmpireName == empireName);
        var home = state.Systems.Single(item => item.CycleId == cycleId && item.SystemName == systemName);
        empire.HomeSystemId = home.SystemId;
        var originalFleet = state.Fleets.Single(item => item.EmpireId == empire.EmpireId);
        originalFleet.CurrentSystemId = home.SystemId;
    }

    private static List<GalaxySystem> SelectHomeSystems(IEnumerable<GalaxySystem> systems, int count)
    {
        var candidates = systems.OrderByDescending(system => system.StrategicValue).ToList();
        var selected = new List<GalaxySystem> { candidates[0] };

        while (selected.Count < count)
        {
            var next = candidates
                .Except(selected)
                .OrderByDescending(system => selected.Min(selectedSystem => Distance(system, selectedSystem)))
                .First();

            selected.Add(next);
        }

        return selected;
    }

    private static double Distance(GalaxySystem first, GalaxySystem second)
    {
        var x = first.X - second.X;
        var y = first.Y - second.Y;
        return Math.Sqrt((x * x) + (y * y));
    }

    private sealed record SectorDefinition(
        string Name,
        int SystemCount,
        int CentreX,
        int CentreY,
        string SystemRoot);

    private sealed record CanonicalSystemAssignment(
        string SystemName,
        int SectorIndex,
        int LocalIndex,
        Guid SectorId,
        int X,
        int Y,
        bool IsGateway);

    private sealed class DeterministicIdentitySequence(int seed)
    {
        private int sequence;

        public Guid Next()
        {
            var value = $"cycles-balance:{seed}:{sequence++}";
            var hash = SHA256.HashData(Encoding.UTF8.GetBytes(value));
            return new Guid(hash.AsSpan(0, 16));
        }
    }
}

public sealed record GalaxyTopologyUpgradeResult(
    bool Changed,
    int SectorsAdded,
    int SystemsAdded,
    int LinksAdded,
    int LinksRemoved);
