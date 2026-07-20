using System.Globalization;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cycles.Core;

public static class GameSeeder
{
    public const string CuratedColdStartScenarioKey = "development-match-v2";
    public const int DevelopmentSetupAlgorithmVersion = 1;
    public const int DefaultDevelopmentScenarioSeed = 20260717;
    public const string CanonicalGalaxyTopologyKey = "territorial-graph-v2";
    public const int CanonicalGalaxySectorCount = 8;
    public const int CanonicalGalaxySystemCount = 64;
    public const int CanonicalGalaxyBridgeCount = 11;
    public const int CanonicalGalaxyRouteCount = 91;

    private const int CanonicalGalaxySeed = 71421;
    private static readonly Guid TonyPlayerId = Guid.Parse("2bbf6b63-b50f-4fe3-bc11-913c2b74aa01");
    private static readonly Guid WillPlayerId = Guid.Parse("8bf77462-f7ce-4c67-8c20-29e4e1e5bb02");
    private static readonly Guid DevelopmentAiPlayerId = Guid.Parse("3ecfbf78-b6b3-42cd-a811-85efb916cc03");

    private static readonly SectorDefinition[] CanonicalSectors =
    [
        new(
            "Aster Reach",
            700,
            340,
            ["Treaty Gate", "Aster Vale", "Nadir Crossing", "Pale Harbour", "Yanaka's Reach", "Pseudopolis", "Brightfall", "Dawnward"],
            [(-64, -30), (-2, -47), (53, -22), (15, -3), (54, 37), (-22, 33), (-65, 36), (-47, 4)]),
        new(
            "Cinder March",
            160,
            340,
            ["Cinderhome", "Ebon Strait", "Glass Meridian", "Keystone", "Ashen Gate", "Cinder Relay", "Pyre Anchorage", "Ember Watch"],
            [(-65, -10), (-42, -48), (-15, -22), (-48, 15), (50, -50), (20, 5), (55, 20), (40, 52)]),
        new(
            "Hollow Crown",
            500,
            120,
            ["Hollow Crown", "Juniper Rift", "Hollow Lantern", "Crown Meridian", "Hollow Bastion", "Vigil Cairn", "Glass Refuge", "Silent Array"],
            [(-58, -35), (-20, -25), (20, -35), (55, -10), (50, 32), (10, 35), (5, 58), (-48, 45)]),
        new(
            "Lacuna Verge",
            830,
            155,
            ["Lacuna", "Mournstar", "Lacuna Shoal", "Penumbral Span", "Mourn Relay", "Deep Vault", "Lacuna Beacon", "Far Meridian"],
            [(-60, 0), (-35, -40), (5, -55), (45, -35), (60, 5), (35, 45), (-5, 60), (-45, 38)]),
        new(
            "Orison Fold",
            800,
            555,
            ["Orison", "Quietus", "Orison Lantern", "Pale Coil", "Orison Anchorage", "Quiet Harbour", "Fold Meridian", "Pilgrim's Wake"],
            [(-50, -45), (-8, -40), (40, -42), (47, 0), (40, 35), (25, 55), (-40, 57), (0, 0)]),
        new(
            "Red Lattice",
            490,
            555,
            ["Red Lattice", "Sable Point", "Ternary", "Crimson Needle", "Crimson Relay", "Sable Vault", "Ternary Watch", "Red Haven"],
            [(-45, -45), (-23, -16), (32, -45), (55, 0), (55, 48), (-14, 35), (-45, 17), (13, 2)]),
        new(
            "Umbral Marches",
            180,
            555,
            ["Umbral Way", "Verdant Coil", "Umbral Lantern", "Shadow Cairn", "Umbral Bastion", "Viridian Refuge", "Night Span", "Marcher Beacon"],
            [(-60, 48), (-30, 35), (-5, -10), (20, 35), (55, -45), (40, 10), (30, -48), (62, -15)]),
        new(
            "Warden Line",
            170,
            125,
            ["Warden's Line", "Xanthe", "Yarrow", "Warden Watch", "Zenith Yard", "Sentinel Spur", "High Anchorage", "Northstar Gate"],
            [(-55, -45), (-14, -35), (34, -20), (-45, -2), (44, 16), (-25, 32), (10, 50), (55, 50)])
    ];

    private static readonly (int FirstLocalIndex, int SecondLocalIndex)[][] CanonicalLocalRoutesBySector =
    [
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0), (1, 3), (3, 5)],
        [(0, 1), (1, 2), (2, 3), (3, 0), (4, 5), (5, 6), (6, 7), (7, 4), (1, 6), (3, 4)],
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0), (1, 6), (3, 5)],
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0), (1, 3), (5, 7)],
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0), (3, 7), (5, 7)],
        [(0, 1), (1, 2), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 0), (1, 5), (2, 6)],
        [(0, 1), (1, 2), (2, 0), (2, 3), (3, 4), (4, 5), (5, 6), (6, 7), (7, 3), (1, 7)],
        [(0, 1), (1, 2), (2, 4), (4, 7), (7, 6), (6, 5), (5, 3), (3, 0), (1, 3), (3, 4)]
    ];

    private static readonly BridgeDefinition[] CanonicalBridges =
    [
        new(7, 0, 2, 0),
        new(7, 7, 1, 0),
        new(2, 4, 3, 0),
        new(2, 4, 0, 0),
        new(3, 4, 0, 0),
        new(1, 4, 6, 0),
        new(1, 0, 5, 0),
        new(6, 4, 5, 0),
        new(5, 4, 0, 4),
        new(5, 4, 4, 6),
        new(0, 4, 4, 2)
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
        var state = Create(systemCount, empireCount, seed, createdAt, Guid.NewGuid);
        LegacyGameFoundation.Apply(state);
        return state;
    }

    public static GameState CreateCuratedColdStart(DateTimeOffset? createdAt = null)
        => CreateDevelopmentMatch(DefaultDevelopmentScenarioSeed, createdAt);

    public static GameState CreateDevelopmentMatch(
        int scenarioSeed = DefaultDevelopmentScenarioSeed,
        DateTimeOffset? createdAt = null)
    {
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var identitySequence = new DeterministicIdentitySequence(CanonicalGalaxySeed);
        var state = Create(CanonicalGalaxySystemCount, 3, CanonicalGalaxySeed, now, identitySequence.Next);

        ApplyDevelopmentMatch(state, scenarioSeed, now, identitySequence.Next);
        LegacyGameFoundation.Apply(state);
        return state;
    }

    internal static GameState CreateDeterministicScenario(
        int systemCount,
        int empireCount,
        int seed,
        DateTimeOffset createdAt)
    {
        var identitySequence = new DeterministicIdentitySequence(seed);
        var state = Create(systemCount, empireCount, seed, createdAt, identitySequence.Next);
        LegacyGameFoundation.Apply(state);
        return state;
    }

    internal static MapProfileBlueprint CreateCanonicalProfileBlueprint()
    {
        var cycle = new Cycle { CycleId = Guid.Empty };
        var state = new GameState();
        AddCanonicalGalaxy(state, cycle, DateTimeOffset.UnixEpoch);

        var sectorNames = state.Sectors.ToDictionary(item => item.SectorId, item => item.SectorName);
        var systemNames = state.Systems.ToDictionary(item => item.SystemId, item => item.SystemName);
        return new MapProfileBlueprint(
            state.Sectors
                .OrderBy(item => item.SortOrder)
                .ThenBy(item => item.SectorName, StringComparer.Ordinal)
                .Select(item => new MapSectorProfile(
                    item.SectorName,
                    item.SectorName,
                    item.CentreX,
                    item.CentreY,
                    item.SortOrder))
                .ToArray(),
            state.Systems
                .OrderBy(item => item.SystemName, StringComparer.Ordinal)
                .Select(item => new MapSystemProfile(
                    item.SystemName,
                    item.SystemName,
                    sectorNames[item.SectorId],
                    item.X,
                    item.Y,
                    item.IndustryOutput,
                    item.ResearchOutput,
                    item.PopulationOutput,
                    item.StrategicValue,
                    item.HistoricalSignificance))
                .ToArray(),
            state.SystemLinks
                .Select(item =>
                {
                    var first = systemNames[item.SystemAId];
                    var second = systemNames[item.SystemBId];
                    return string.CompareOrdinal(first, second) <= 0
                        ? new MapRouteProfile($"{first}|{second}", first, second, item.TravelTicks)
                        : new MapRouteProfile($"{second}|{first}", second, first, item.TravelTicks);
                })
                .OrderBy(item => item.Key, StringComparer.Ordinal)
                .ToArray());
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
        var assignments = BuildCanonicalAssignments(cycle.CycleId);
        var hasCanonicalLayout = HasCanonicalGalaxyLayout(state, cycle.CycleId, assignments);
        var hasCanonicalIdentity = HasCanonicalGalaxyIdentity(state, cycle.CycleId, assignments);
        if (hasCanonicalLayout && HasCanonicalGalaxyLinks(state, cycle.CycleId, assignments))
        {
            return new GalaxyTopologyUpgradeResult(false, 0, 0, 0, 0);
        }

        var cycleSystems = state.Systems.Where(item => item.CycleId == cycle.CycleId).ToArray();
        if (!hasCanonicalLayout && !hasCanonicalIdentity)
        {
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
                    "Galaxy topology upgrade only supports the original 24-system curated Development opening or the canonical galaxy layout.");
            }

            if (state.Sectors.Any(item => item.CycleId == cycle.CycleId))
            {
                throw new InvalidOperationException("Galaxy topology upgrade found an unrecognised partial sector model and will not guess how to repair it.");
            }
        }

        if (state.Fleets.Any(item => item.CycleId == cycle.CycleId
                                    && (item.Status == FleetStatus.InTransit || item.DestinationSystemId.HasValue))
            || state.FleetOrders.Any(item => item.CycleId == cycle.CycleId && item.Status == FleetOrderStatus.Pending))
        {
            throw new InvalidOperationException(
                "Galaxy topology upgrade requires no fleets in transit and no pending orders. Resolve or cancel them before changing the route network.");
        }

        var sectorsAdded = 0;
        var systemsAdded = 0;
        if (!hasCanonicalLayout)
        {
            if (hasCanonicalIdentity)
            {
                ApplyCanonicalLayout(state, cycle.CycleId, assignments);
            }
            else
            {
                AddCanonicalSectors(state, cycle.CycleId);
                sectorsAdded = CanonicalGalaxySectorCount;
                var existingByName = cycleSystems.ToDictionary(item => item.SystemName, StringComparer.Ordinal);
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
            }
        }

        var linksRemoved = state.SystemLinks.RemoveAll(item => item.CycleId == cycle.CycleId);
        var linksAdded = AddCanonicalLinks(state, cycle.CycleId, assignments);
        return new GalaxyTopologyUpgradeResult(true, sectorsAdded, systemsAdded, linksAdded, linksRemoved);
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
        if (systemCount == CanonicalGalaxySystemCount)
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
        ValidateCanonicalTopologyDefinitions();
        var assignments = new List<CanonicalSystemAssignment>(CanonicalGalaxySystemCount);
        for (var sectorIndex = 0; sectorIndex < CanonicalSectors.Length; sectorIndex++)
        {
            var definition = CanonicalSectors[sectorIndex];
            var localRoutes = CanonicalLocalRoutesBySector[sectorIndex];
            for (var localIndex = 0; localIndex < definition.SystemNames.Length; localIndex++)
            {
                var coordinates = definition.LocalCoordinates[localIndex];
                var localDegree = localRoutes.Count(route =>
                    route.FirstLocalIndex == localIndex || route.SecondLocalIndex == localIndex);
                var bridgeDegree = CanonicalBridges.Count(bridge =>
                    (bridge.FirstSectorIndex == sectorIndex && bridge.FirstLocalIndex == localIndex)
                    || (bridge.SecondSectorIndex == sectorIndex && bridge.SecondLocalIndex == localIndex));
                assignments.Add(new CanonicalSystemAssignment(
                    definition.SystemNames[localIndex],
                    sectorIndex,
                    localIndex,
                    CreateTopologyId(cycleId, "sector", definition.Name),
                    definition.CentreX + coordinates.X,
                    definition.CentreY + coordinates.Y,
                    localDegree,
                    bridgeDegree));
            }
        }

        return assignments;
    }

    private static void ValidateCanonicalTopologyDefinitions()
    {
        if (CanonicalSectors.Length != CanonicalGalaxySectorCount
            || CanonicalSectors.Any(item => item.SystemNames.Length != 8 || item.LocalCoordinates.Length != 8)
            || CanonicalSectors.Sum(item => item.SystemNames.Length) != CanonicalGalaxySystemCount
            || CanonicalSectors.SelectMany(item => item.SystemNames).Distinct(StringComparer.Ordinal).Count() != CanonicalGalaxySystemCount)
        {
            throw new InvalidOperationException("Canonical territorial sectors must define eight uniquely named systems each.");
        }

        if (CanonicalLocalRoutesBySector.Length != CanonicalGalaxySectorCount
            || CanonicalLocalRoutesBySector.Any(routes => routes.Length != 10)
            || CanonicalBridges.Length != CanonicalGalaxyBridgeCount
            || CanonicalLocalRoutesBySector.Sum(routes => routes.Length) + CanonicalBridges.Length != CanonicalGalaxyRouteCount)
        {
            throw new InvalidOperationException("Canonical territorial route definitions do not match the public topology counts.");
        }

        for (var sectorIndex = 0; sectorIndex < CanonicalSectors.Length; sectorIndex++)
        {
            var localRoutes = CanonicalLocalRoutesBySector[sectorIndex];
            var normalisedRoutes = localRoutes
                .Select(route => route.FirstLocalIndex < route.SecondLocalIndex
                    ? (route.FirstLocalIndex, route.SecondLocalIndex)
                    : (route.SecondLocalIndex, route.FirstLocalIndex))
                .ToArray();
            var localDegrees = Enumerable.Range(0, 8)
                .Select(localIndex => localRoutes.Count(route =>
                    route.FirstLocalIndex == localIndex || route.SecondLocalIndex == localIndex))
                .ToArray();
            if (localRoutes.Any(route => route.FirstLocalIndex is < 0 or >= 8
                                         || route.SecondLocalIndex is < 0 or >= 8
                                         || route.FirstLocalIndex == route.SecondLocalIndex)
                || normalisedRoutes.Distinct().Count() != localRoutes.Length
                || localDegrees.Any(degree => degree is < 2 or > 4)
                || ReachableLocalSystemCount(localRoutes) != 8)
            {
                throw new InvalidOperationException($"Sector {CanonicalSectors[sectorIndex].Name} must define one connected, bounded ten-route composition.");
            }

            var gatewayDegrees = Enumerable.Range(0, 8)
                .Select(localIndex => CanonicalBridges.Count(bridge =>
                    (bridge.FirstSectorIndex == sectorIndex && bridge.FirstLocalIndex == localIndex)
                    || (bridge.SecondSectorIndex == sectorIndex && bridge.SecondLocalIndex == localIndex)))
                .Where(degree => degree > 0)
                .ToArray();
            if (gatewayDegrees.Length != 2 || gatewayDegrees.Any(degree => degree is < 1 or > 3))
            {
                throw new InvalidOperationException($"Sector {CanonicalSectors[sectorIndex].Name} must expose exactly two bounded gateways.");
            }
        }
    }

    private static int ReachableLocalSystemCount(
        IReadOnlyCollection<(int FirstLocalIndex, int SecondLocalIndex)> routes)
    {
        var visited = new HashSet<int> { 0 };
        var queue = new Queue<int>();
        queue.Enqueue(0);
        while (queue.TryDequeue(out var current))
        {
            foreach (var adjacent in routes
                         .Where(route => route.FirstLocalIndex == current || route.SecondLocalIndex == current)
                         .Select(route => route.FirstLocalIndex == current ? route.SecondLocalIndex : route.FirstLocalIndex)
                         .Where(visited.Add))
            {
                queue.Enqueue(adjacent);
            }
        }

        return visited.Count;
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
        var isGateway = assignment.BridgeDegree > 0;
        var totalDegree = assignment.LocalDegree + assignment.BridgeDegree;
        var strategicValue = ((industry + research + population) / 5)
                             + (isGateway ? 12 : 0)
                             + (Math.Max(0, assignment.BridgeDegree - 1) * 5);
        if (totalDegree >= 5 || assignment.BridgeDegree >= 2)
        {
            strategicValue = Math.Max(35, strategicValue);
        }

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
            HistoricalSignificance = assignment.BridgeDegree >= 2 ? 2 : isGateway ? 1 : 0,
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

        for (var sectorIndex = 0; sectorIndex < sectorMembers.Length; sectorIndex++)
        {
            var members = sectorMembers[sectorIndex];
            foreach (var route in CanonicalLocalRoutesBySector[sectorIndex])
            {
                added += AddCanonicalLink(
                    state,
                    cycleId,
                    systemsByName[members[route.FirstLocalIndex].SystemName],
                    systemsByName[members[route.SecondLocalIndex].SystemName],
                    travelTicks: 1);
            }
        }

        foreach (var bridge in CanonicalBridges)
        {
            added += AddCanonicalLink(
                state,
                cycleId,
                systemsByName[sectorMembers[bridge.FirstSectorIndex][bridge.FirstLocalIndex].SystemName],
                systemsByName[sectorMembers[bridge.SecondSectorIndex][bridge.SecondLocalIndex].SystemName],
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

    private static bool HasCanonicalGalaxyLayout(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
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

        return true;
    }

    private static bool HasCanonicalGalaxyIdentity(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var systems = state.Systems.Where(item => item.CycleId == cycleId).ToArray();
        var sectors = state.Sectors.Where(item => item.CycleId == cycleId).ToArray();
        if (systems.Length != CanonicalGalaxySystemCount || sectors.Length != CanonicalGalaxySectorCount)
        {
            return false;
        }

        var expectedSystems = assignments.ToDictionary(item => item.SystemName, StringComparer.Ordinal);
        return systems.All(system =>
                   expectedSystems.TryGetValue(system.SystemName, out var expected)
                   && system.SystemId == CreateTopologyId(cycleId, "system", expected.SystemName))
               && sectors.All(sector => CanonicalSectors.Any(definition =>
                   sector.SectorId == CreateTopologyId(cycleId, "sector", definition.Name)));
    }

    private static void ApplyCanonicalLayout(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var sectorsById = state.Sectors
            .Where(item => item.CycleId == cycleId)
            .ToDictionary(item => item.SectorId);
        for (var sectorIndex = 0; sectorIndex < CanonicalSectors.Length; sectorIndex++)
        {
            var definition = CanonicalSectors[sectorIndex];
            var sector = sectorsById[CreateTopologyId(cycleId, "sector", definition.Name)];
            sector.SectorName = definition.Name;
            sector.CentreX = definition.CentreX;
            sector.CentreY = definition.CentreY;
            sector.SortOrder = sectorIndex;
        }

        var systemsByName = state.Systems
            .Where(item => item.CycleId == cycleId)
            .ToDictionary(item => item.SystemName, StringComparer.Ordinal);
        foreach (var assignment in assignments)
        {
            var system = systemsByName[assignment.SystemName];
            system.SectorId = assignment.SectorId;
            system.X = assignment.X;
            system.Y = assignment.Y;
        }
    }

    private static bool HasCanonicalGalaxyLinks(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var systems = state.Systems.Where(item => item.CycleId == cycleId).ToArray();
        var systemNamesById = systems.ToDictionary(item => item.SystemId, item => item.SystemName);
        var links = state.SystemLinks.Where(item => item.CycleId == cycleId).ToArray();
        var expectedLinks = ExpectedCanonicalLinks(assignments);
        if (links.Length != expectedLinks.Count)
        {
            return false;
        }

        foreach (var link in links)
        {
            if (!systemNamesById.TryGetValue(link.SystemAId, out var firstName)
                || !systemNamesById.TryGetValue(link.SystemBId, out var secondName)
                || !expectedLinks.TryGetValue(NormalizedPair(firstName, secondName), out var expectedTravelTicks)
                || link.TravelTicks != expectedTravelTicks)
            {
                return false;
            }
        }

        return true;
    }

    private static Dictionary<string, int> ExpectedCanonicalLinks(IReadOnlyCollection<CanonicalSystemAssignment> assignments)
    {
        var members = assignments
            .GroupBy(item => item.SectorIndex)
            .OrderBy(group => group.Key)
            .Select(group => group.OrderBy(item => item.LocalIndex).ToArray())
            .ToArray();
        var links = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var sectorIndex = 0; sectorIndex < members.Length; sectorIndex++)
        {
            var sector = members[sectorIndex];
            foreach (var route in CanonicalLocalRoutesBySector[sectorIndex])
            {
                links.Add(
                    NormalizedPair(
                        sector[route.FirstLocalIndex].SystemName,
                        sector[route.SecondLocalIndex].SystemName),
                    1);
            }
        }

        foreach (var bridge in CanonicalBridges)
        {
            links.Add(
                NormalizedPair(
                    members[bridge.FirstSectorIndex][bridge.FirstLocalIndex].SystemName,
                    members[bridge.SecondSectorIndex][bridge.SecondLocalIndex].SystemName),
                2);
        }

        return links;
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
                Kind = PlayerKind.Human,
                CreatedAt = now,
                LastLoginAt = now,
                Status = PlayerStatus.Active
            };
            state.Players.Add(player);
            cycle.CreatedByPlayerId ??= player.PlayerId;

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

            var faction = new Faction
            {
                FactionId = empire.EmpireId,
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                FactionName = empire.EmpireName,
                Kind = FactionKind.Empire,
                Status = FactionStatus.Active,
                CreatedAt = now
            };
            state.Factions.Add(faction);
            state.MatchParticipants.Add(new MatchParticipant
            {
                MatchParticipantId = nextId(),
                GameId = GameFoundationConstants.LegacyGameId,
                CycleId = cycle.CycleId,
                PlayerId = player.PlayerId,
                EmpireId = empire.EmpireId,
                Status = MatchParticipantStatus.Active,
                JoinedAt = now
            });

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
                FactionId = faction.FactionId,
                AdmiralId = admiral.AdmiralId,
                FleetName = $"{empire.EmpireName} Home Fleet",
                CurrentSystemId = homeSystem.SystemId,
                ShipCount = 60,
                Status = FleetStatus.Active,
                CreatedAt = now
            });
        }
    }

    private static void ApplyDevelopmentMatch(
        GameState state,
        int scenarioSeed,
        DateTimeOffset now,
        Func<Guid> nextId)
    {
        var cycle = state.GetActiveCycle()
            ?? throw new InvalidOperationException("The Development match requires an active Cycle.");
        var empires = state.Empires
            .Where(item => item.CycleId == cycle.CycleId)
            .OrderBy(item => item.EmpireName, StringComparer.Ordinal)
            .ToArray();
        if (empires.Length != 3)
        {
            throw new InvalidOperationException("The canonical Development match requires exactly three Empires.");
        }

        AssignDevelopmentPlayers(state, cycle, empires, scenarioSeed, now);
        var bundles = SelectDevelopmentBundles(state, cycle.CycleId, empires.Length, scenarioSeed);
        var assignmentRandom = StableRandom.ForStream(scenarioSeed, "starting-bundle-assignment");
        assignmentRandom.Shuffle(bundles);

        state.Fleets.RemoveAll(item => item.CycleId == cycle.CycleId);
        state.Events.RemoveAll(item => item.CycleId == cycle.CycleId && item.EventType == EventType.OpeningBriefingIssued);

        var neutralFaction = new Faction
        {
            FactionId = nextId(),
            CycleId = cycle.CycleId,
            FactionName = "Free Captains",
            Kind = FactionKind.Neutral,
            Status = FactionStatus.Active,
            CreatedAt = now
        };
        state.Factions.Add(neutralFaction);

        var reservedSystems = new HashSet<Guid>();
        for (var index = 0; index < empires.Length; index++)
        {
            var empire = empires[index];
            var faction = state.GetEmpireFaction(empire.EmpireId);
            var bundle = bundles[index];
            empire.HomeSystemId = bundle.Home.SystemId;
            faction.FactionName = empire.EmpireName;

            reservedSystems.Add(bundle.Home.SystemId);
            reservedSystems.Add(bundle.MoveTarget.SystemId);
            reservedSystems.Add(bundle.SurveyTarget.SystemId);
            reservedSystems.Add(bundle.Flashpoint.SystemId);

            var admiral = state.Admirals.Single(item => item.EmpireId == empire.EmpireId);
            var homeGuard = new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                FactionId = faction.FactionId,
                AdmiralId = admiral.AdmiralId,
                FleetName = $"{empire.EmpireName} Home Guard",
                CurrentSystemId = bundle.Home.SystemId,
                ShipCount = 30,
                Status = FleetStatus.Active,
                CreatedAt = now
            };
            var vanguard = new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                FactionId = faction.FactionId,
                FleetName = $"{bundle.Flashpoint.SystemName} Vanguard",
                CurrentSystemId = bundle.Flashpoint.SystemId,
                ShipCount = 18,
                Status = FleetStatus.Active,
                CreatedAt = now
            };
            var survey = new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = empire.EmpireId,
                FactionId = faction.FactionId,
                FleetName = $"{bundle.SurveyTarget.SystemName} Survey",
                CurrentSystemId = bundle.SurveyTarget.SystemId,
                ShipCount = 12,
                Status = FleetStatus.Active,
                CreatedAt = now
            };
            state.Fleets.AddRange([homeGuard, vanguard, survey]);

            state.Fleets.Add(new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = Guid.Empty,
                FactionId = neutralFaction.FactionId,
                FleetName = $"{bundle.Flashpoint.SystemName} Free Captains",
                CurrentSystemId = bundle.Flashpoint.SystemId,
                ShipCount = 8,
                Status = FleetStatus.Active,
                CreatedAt = now
            });

            state.Events.Add(new EventRecord
            {
                EventId = nextId(),
                CycleId = cycle.CycleId,
                TickNumber = 0,
                EventType = EventType.OpeningBriefingIssued,
                SystemId = bundle.Flashpoint.SystemId,
                EmpireId = empire.EmpireId,
                FactionId = faction.FactionId,
                Severity = EventSeverity.High,
                DisplayText = $"Day 1 briefing: Free Captains contest {bundle.Flashpoint.SystemName}. {bundle.SurveyTarget.SystemName} is ready for an outpost, while {bundle.MoveTarget.SystemName} offers immediate expansion.",
                FactJson = JsonSerializer.Serialize(new
                {
                    scenarioKey = CuratedColdStartScenarioKey,
                    scenarioSeed,
                    mapVersion = CanonicalGalaxyTopologyKey,
                    setupAlgorithmVersion = DevelopmentSetupAlgorithmVersion,
                    focusSystemId = bundle.Flashpoint.SystemId,
                    objectives = new
                    {
                        move = new
                        {
                            fleetId = homeGuard.FleetId,
                            targetSystemId = bundle.MoveTarget.SystemId
                        },
                        colonise = new
                        {
                            fleetId = survey.FleetId,
                            systemId = bundle.SurveyTarget.SystemId
                        },
                        attack = new
                        {
                            fleetId = vanguard.FleetId,
                            systemId = bundle.Flashpoint.SystemId,
                            targetFactionId = neutralFaction.FactionId
                        }
                    }
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }

        var remoteCandidates = state.Systems
            .Where(item => item.CycleId == cycle.CycleId && !reservedSystems.Contains(item.SystemId))
            .OrderBy(item => item.SystemId)
            .ToList();
        var remoteRandom = StableRandom.ForStream(scenarioSeed, "remote-neutral-fleets");
        remoteRandom.Shuffle(remoteCandidates);
        for (var index = 0; index < empires.Length; index++)
        {
            var system = remoteCandidates[index];
            state.Fleets.Add(new Fleet
            {
                FleetId = nextId(),
                CycleId = cycle.CycleId,
                EmpireId = Guid.Empty,
                FactionId = neutralFaction.FactionId,
                FleetName = $"Free Captain Patrol {index + 1}",
                CurrentSystemId = system.SystemId,
                ShipCount = remoteRandom.NextInt(8, 15),
                Status = FleetStatus.Active,
                CreatedAt = now
            });
        }
    }

    private static void AssignDevelopmentPlayers(
        GameState state,
        Cycle cycle,
        IReadOnlyList<Empire> empires,
        int scenarioSeed,
        DateTimeOffset now)
    {
        var identities = new List<DevelopmentPlayerIdentity>
        {
            new(TonyPlayerId, "Tony", PlayerKind.Human),
            new(WillPlayerId, "Will", PlayerKind.Human),
            new(DevelopmentAiPlayerId, "Ariadne", PlayerKind.AI)
        };
        StableRandom.ForStream(scenarioSeed, "player-empire-assignment").Shuffle(identities);

        for (var index = 0; index < empires.Count; index++)
        {
            var empire = empires[index];
            var player = state.Players.Single(item => item.PlayerId == empire.PlayerId);
            var participant = state.MatchParticipants.Single(item => item.EmpireId == empire.EmpireId);
            var identity = identities[index];
            player.PlayerId = identity.PlayerId;
            player.Username = identity.Name;
            player.Email = "";
            player.PasswordHash = "";
            player.Kind = identity.Kind;
            player.CreatedAt = now;
            player.LastLoginAt = null;
            empire.PlayerId = identity.PlayerId;
            participant.PlayerId = identity.PlayerId;
        }

        cycle.CreatedByPlayerId = TonyPlayerId;
    }

    private static List<DevelopmentBundle> SelectDevelopmentBundles(
        GameState state,
        Guid cycleId,
        int count,
        int scenarioSeed)
    {
        var cycleSystems = state.Systems.Where(item => item.CycleId == cycleId).ToArray();
        var candidatesBySector = cycleSystems
            .Where(item => item.HistoricalSignificance == 0)
            .Select(home => CreateDevelopmentBundle(state, cycleId, home))
            .Where(item => item is not null)
            .Cast<DevelopmentBundle>()
            .GroupBy(item => item.Home.SectorId)
            .ToDictionary(group => group.Key, group => group.OrderBy(item => item.Home.SystemId).ToArray());
        var sectors = state.Sectors
            .Where(item => item.CycleId == cycleId && candidatesBySector.ContainsKey(item.SectorId))
            .OrderBy(item => item.SortOrder)
            .ToArray();
        var sectorDistances = BuildSectorDistances(state, cycleId, sectors);
        DevelopmentSelection? best = null;

        foreach (var sectorSelection in Choose(sectors, count))
        {
            foreach (var bundleSelection in CartesianBundles(sectorSelection, candidatesBySector))
            {
                var totals = bundleSelection.Select(item => TotalOutput(item.Home)).ToArray();
                var strategicValues = bundleSelection.Select(item => item.Home.StrategicValue).ToArray();
                var outputSpread = totals.Max() - totals.Min();
                var strategicSpread = strategicValues.Max() - strategicValues.Min();
                if (outputSpread > decimal.Round(totals.Min() * 0.15m, 2) || strategicSpread > 5)
                {
                    continue;
                }

                var minimumDistance = bundleSelection
                    .SelectMany((first, firstIndex) => bundleSelection.Skip(firstIndex + 1)
                        .Select(second => sectorDistances[(first.Home.SectorId, second.Home.SectorId)]))
                    .Min();
                var tieBreaker = StableTieBreaker(
                    scenarioSeed,
                    "home-bundle-selection",
                    bundleSelection.Select(item => item.Home.SystemId));
                var selection = new DevelopmentSelection(
                    bundleSelection.ToList(),
                    minimumDistance,
                    outputSpread,
                    strategicSpread,
                    tieBreaker);
                if (best is null || selection.IsBetterThan(best))
                {
                    best = selection;
                }
            }
        }

        return best?.Bundles
            ?? throw new InvalidOperationException("The canonical map has no fair, distinct-sector Development start for the requested Empire count.");
    }

    private static DevelopmentBundle? CreateDevelopmentBundle(GameState state, Guid cycleId, GalaxySystem home)
    {
        var systemsById = state.Systems.Where(item => item.CycleId == cycleId).ToDictionary(item => item.SystemId);
        var localNeighbours = state.SystemLinks
            .Where(item => item.CycleId == cycleId && (item.SystemAId == home.SystemId || item.SystemBId == home.SystemId))
            .Select(item => systemsById[item.SystemAId == home.SystemId ? item.SystemBId : item.SystemAId])
            .Where(item => item.SectorId == home.SectorId)
            .OrderBy(item => item.SystemId)
            .ToArray();
        if (localNeighbours.Length < 2)
        {
            return null;
        }

        var moveTarget = localNeighbours[0];
        var flashpoint = localNeighbours[1];
        var surveyTarget = systemsById.Values
            .Where(item => item.SectorId == home.SectorId
                           && item.SystemId != home.SystemId
                           && item.SystemId != moveTarget.SystemId
                           && item.SystemId != flashpoint.SystemId)
            .OrderBy(item => item.SystemId)
            .FirstOrDefault();
        return surveyTarget is null ? null : new DevelopmentBundle(home, moveTarget, surveyTarget, flashpoint);
    }

    private static Dictionary<(Guid First, Guid Second), int> BuildSectorDistances(
        GameState state,
        Guid cycleId,
        IReadOnlyCollection<GalaxySector> sectors)
    {
        var sectorBySystem = state.Systems
            .Where(item => item.CycleId == cycleId)
            .ToDictionary(item => item.SystemId, item => item.SectorId);
        var adjacent = sectors.ToDictionary(item => item.SectorId, _ => new HashSet<Guid>());
        foreach (var link in state.SystemLinks.Where(item => item.CycleId == cycleId))
        {
            var first = sectorBySystem[link.SystemAId];
            var second = sectorBySystem[link.SystemBId];
            if (first != second)
            {
                adjacent[first].Add(second);
                adjacent[second].Add(first);
            }
        }

        var result = new Dictionary<(Guid First, Guid Second), int>();
        foreach (var origin in adjacent.Keys)
        {
            var distances = new Dictionary<Guid, int> { [origin] = 0 };
            var queue = new Queue<Guid>();
            queue.Enqueue(origin);
            while (queue.TryDequeue(out var current))
            {
                foreach (var next in adjacent[current].Where(item => !distances.ContainsKey(item)))
                {
                    distances[next] = distances[current] + 1;
                    queue.Enqueue(next);
                }
            }

            foreach (var pair in distances)
            {
                result[(origin, pair.Key)] = pair.Value;
            }
        }

        return result;
    }

    private static IEnumerable<IReadOnlyList<GalaxySector>> Choose(IReadOnlyList<GalaxySector> values, int count)
    {
        var selection = new GalaxySector[count];
        IEnumerable<IReadOnlyList<GalaxySector>> Select(int start, int depth)
        {
            if (depth == count)
            {
                yield return selection.ToArray();
                yield break;
            }

            for (var index = start; index <= values.Count - (count - depth); index++)
            {
                selection[depth] = values[index];
                foreach (var result in Select(index + 1, depth + 1))
                {
                    yield return result;
                }
            }
        }

        return Select(0, 0);
    }

    private static IEnumerable<IReadOnlyList<DevelopmentBundle>> CartesianBundles(
        IReadOnlyList<GalaxySector> sectors,
        IReadOnlyDictionary<Guid, DevelopmentBundle[]> candidatesBySector)
    {
        var selection = new DevelopmentBundle[sectors.Count];
        IEnumerable<IReadOnlyList<DevelopmentBundle>> Select(int depth)
        {
            if (depth == sectors.Count)
            {
                yield return selection.ToArray();
                yield break;
            }

            foreach (var candidate in candidatesBySector[sectors[depth].SectorId])
            {
                selection[depth] = candidate;
                foreach (var result in Select(depth + 1))
                {
                    yield return result;
                }
            }
        }

        return Select(0);
    }

    private static decimal TotalOutput(GalaxySystem system) =>
        system.IndustryOutput + system.ResearchOutput + system.PopulationOutput;

    private static ulong StableTieBreaker(int scenarioSeed, string streamName, IEnumerable<Guid> values)
    {
        var value = $"{scenarioSeed}:{DevelopmentSetupAlgorithmVersion}:{streamName}:{string.Join(':', values.Select(item => item.ToString("N")))}";
        return BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
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
        var aurelianFaction = state.GetEmpireFaction(aurelian.EmpireId);
        var khepriFaction = state.GetEmpireFaction(khepri.EmpireId);
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
            FactionId = aurelianFaction.FactionId,
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
            FactionId = aurelianFaction.FactionId,
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
            FactionId = khepriFaction.FactionId,
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
            FactionId = aurelianFaction.FactionId,
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
                        targetEmpireId = khepri.EmpireId,
                        targetFactionId = khepriFaction.FactionId
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
        int CentreX,
        int CentreY,
        string[] SystemNames,
        (int X, int Y)[] LocalCoordinates);

    private sealed record BridgeDefinition(
        int FirstSectorIndex,
        int FirstLocalIndex,
        int SecondSectorIndex,
        int SecondLocalIndex);

    private sealed record DevelopmentPlayerIdentity(Guid PlayerId, string Name, PlayerKind Kind);

    private sealed record DevelopmentBundle(
        GalaxySystem Home,
        GalaxySystem MoveTarget,
        GalaxySystem SurveyTarget,
        GalaxySystem Flashpoint);

    private sealed record DevelopmentSelection(
        List<DevelopmentBundle> Bundles,
        int MinimumSectorDistance,
        decimal OutputSpread,
        int StrategicSpread,
        ulong TieBreaker)
    {
        public bool IsBetterThan(DevelopmentSelection other) =>
            MinimumSectorDistance > other.MinimumSectorDistance
            || (MinimumSectorDistance == other.MinimumSectorDistance && OutputSpread < other.OutputSpread)
            || (MinimumSectorDistance == other.MinimumSectorDistance && OutputSpread == other.OutputSpread && StrategicSpread < other.StrategicSpread)
            || (MinimumSectorDistance == other.MinimumSectorDistance && OutputSpread == other.OutputSpread && StrategicSpread == other.StrategicSpread && TieBreaker < other.TieBreaker);
    }

    private sealed record CanonicalSystemAssignment(
        string SystemName,
        int SectorIndex,
        int LocalIndex,
        Guid SectorId,
        int X,
        int Y,
        int LocalDegree,
        int BridgeDegree);

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

    private sealed class StableRandom
    {
        private ulong state;

        private StableRandom(ulong seed)
        {
            state = seed;
        }

        public static StableRandom ForStream(int scenarioSeed, string streamName)
        {
            var material = $"cycles-development:{DevelopmentSetupAlgorithmVersion}:{scenarioSeed}:{streamName}";
            var seed = BinaryPrimitives.ReadUInt64LittleEndian(SHA256.HashData(Encoding.UTF8.GetBytes(material)));
            return new StableRandom(seed);
        }

        public int NextInt(int minimumInclusive, int maximumExclusive)
        {
            if (minimumInclusive >= maximumExclusive)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumExclusive));
            }

            return minimumInclusive + (int)(NextUInt64() % (uint)(maximumExclusive - minimumInclusive));
        }

        public void Shuffle<T>(IList<T> values)
        {
            for (var index = values.Count - 1; index > 0; index--)
            {
                var swapIndex = NextInt(0, index + 1);
                (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
            }
        }

        private ulong NextUInt64()
        {
            state += 0x9E3779B97F4A7C15UL;
            var value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            return value ^ (value >> 31);
        }
    }
}

public sealed record GalaxyTopologyUpgradeResult(
    bool Changed,
    int SectorsAdded,
    int SystemsAdded,
    int LinksAdded,
    int LinksRemoved);
