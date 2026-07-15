using Cycles.Core;

namespace Cycles.Tests;

public sealed class CanonicalGalaxyTopologyTests
{
    private static readonly string[] LegacySystemNames =
    [
        "Pseudopolis", "Yanaka's Reach", "Treaty Gate", "Aster Vale", "Brightfall", "Cinderhome",
        "Dawnward", "Ebon Strait", "Glass Meridian", "Hollow Crown", "Juniper Rift", "Keystone",
        "Lacuna", "Mournstar", "Nadir Crossing", "Orison", "Pale Harbour", "Quietus",
        "Red Lattice", "Sable Point", "Ternary", "Umbral Way", "Verdant Coil", "Warden's Line"
    ];

    [Fact]
    public void Curated_galaxy_is_a_connected_irregular_territorial_graph()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var sectors = state.Sectors.Where(item => item.CycleId == cycle.CycleId).OrderBy(item => item.SortOrder).ToArray();
        var systems = state.Systems.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var links = state.SystemLinks.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var sectorBySystem = systems.ToDictionary(item => item.SystemId, item => item.SectorId);

        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, sectors.Length);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, systems.Length);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, links.Length);
        Assert.Equal(Enumerable.Repeat(8, GameSeeder.CanonicalGalaxySectorCount),
            sectors.Select(sector => systems.Count(system => system.SectorId == sector.SectorId)));
        Assert.Equal(systems.Length, systems.Select(item => item.SystemName).Distinct(StringComparer.Ordinal).Count());
        Assert.All(systems, item =>
        {
            Assert.InRange(item.X, 40, 960);
            Assert.InRange(item.Y, 40, 660);
        });

        var crossLinks = links
            .Where(link => sectorBySystem[link.SystemAId] != sectorBySystem[link.SystemBId])
            .ToArray();
        Assert.Equal(GameSeeder.CanonicalGalaxyBridgeCount, crossLinks.Length);
        Assert.All(crossLinks, link => Assert.Equal(2, link.TravelTicks));

        foreach (var sector in sectors)
        {
            var memberIds = systems.Where(item => item.SectorId == sector.SectorId).Select(item => item.SystemId).ToHashSet();
            var localLinks = links.Where(link => memberIds.Contains(link.SystemAId) && memberIds.Contains(link.SystemBId)).ToArray();
            var exits = crossLinks
                .SelectMany(link => new[] { link.SystemAId, link.SystemBId })
                .Where(memberIds.Contains)
                .ToArray();

            Assert.Equal(memberIds.Count + (memberIds.Count / 3), localLinks.Length);
            Assert.All(memberIds, systemId => Assert.InRange(
                localLinks.Count(link => link.SystemAId == systemId || link.SystemBId == systemId),
                2,
                4));
            Assert.Contains(memberIds, systemId => localLinks.Count(link => link.SystemAId == systemId || link.SystemBId == systemId) > 2);
            Assert.InRange(exits.Length, 2, 5);
            Assert.Equal(2, exits.Distinct().Count());
            Assert.Equal(memberIds.Count, ReachableSystemIds(memberIds.First(), localLinks).Count);
        }

        var gatewayBridgeDegrees = crossLinks
            .SelectMany(link => new[] { link.SystemAId, link.SystemBId })
            .GroupBy(item => item)
            .Select(group => group.Count())
            .ToArray();
        Assert.All(gatewayBridgeDegrees, degree => Assert.InRange(degree, 1, 3));
        Assert.Contains(1, gatewayBridgeDegrees);
        Assert.Contains(gatewayBridgeDegrees, degree => degree > 1);
        var multiBridgeGateways = crossLinks
            .SelectMany(link => new[] { link.SystemAId, link.SystemBId })
            .GroupBy(item => item)
            .Where(group => group.Count() > 1)
            .Select(group => systems.Single(system => system.SystemId == group.Key))
            .ToArray();
        Assert.NotEmpty(multiBridgeGateways);
        Assert.All(multiBridgeGateways, system =>
        {
            Assert.True(system.StrategicValue >= 35);
            Assert.True(system.HistoricalSignificance >= 2);
        });
        var sectorDegrees = sectors
            .Select(sector => AdjacentSectors(sector.SectorId, crossLinks, sectorBySystem).Count)
            .ToArray();
        Assert.All(sectorDegrees, degree => Assert.InRange(degree, 2, 5));
        Assert.Equal([2, 3, 4, 5], sectorDegrees.Distinct().Order().ToArray());
        Assert.Equal(sectors.Length, ReachableSectorIds(sectors[0].SectorId, crossLinks, sectorBySystem).Count);
        Assert.Equal(systems.Length, ReachableSystemIds(systems[0].SystemId, links).Count);

        var sectorRadii = sectors
            .Select(sector => (int)Math.Round(Math.Sqrt(Math.Pow(sector.CentreX - 500, 2) + Math.Pow(sector.CentreY - 350, 2))))
            .Distinct()
            .Count();
        Assert.True(sectorRadii >= 6, "The sector layout should occupy an irregular map rather than a circle.");

        var graphDegrees = systems.ToDictionary(
            system => system.SystemId,
            system => links.Count(link => link.SystemAId == system.SystemId || link.SystemBId == system.SystemId));
        var superconnected = systems.Where(system => graphDegrees[system.SystemId] >= 5).ToArray();
        Assert.NotEmpty(superconnected);
        Assert.All(superconnected, system =>
        {
            Assert.True(system.StrategicValue >= 35);
            Assert.True(system.HistoricalSignificance >= 2);
        });

        var treatyGate = systems.Single(item => item.SystemName == "Treaty Gate");
        Assert.Contains(crossLinks, link => link.SystemAId == treatyGate.SystemId || link.SystemBId == treatyGate.SystemId);
        var asterVale = systems.Single(item => item.SystemName == "Aster Vale");
        var nadirCrossing = systems.Single(item => item.SystemName == "Nadir Crossing");
        Assert.Contains(links, link => link.Connects(asterVale.SystemId, nadirCrossing.SystemId));
    }

    [Fact]
    public void Canonical_topology_upgrade_preserves_existing_system_identities_and_is_idempotent()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var legacyNames = LegacySystemNames.ToHashSet(StringComparer.Ordinal);
        var retainedIds = state.Systems
            .Where(item => legacyNames.Contains(item.SystemName))
            .ToDictionary(item => item.SystemName, item => item.SystemId, StringComparer.Ordinal);
        var aster = state.Systems.Single(item => item.SystemName == "Aster Vale");
        var nadir = state.Systems.Single(item => item.SystemName == "Nadir Crossing");

        state.Systems.RemoveAll(item => !legacyNames.Contains(item.SystemName));
        state.Sectors.Clear();
        state.Systems.ForEach(item => item.SectorId = Guid.Empty);
        state.SystemLinks.Clear();
        state.SystemLinks.Add(new SystemLink
        {
            CycleId = state.GetActiveCycle()!.CycleId,
            SystemAId = aster.SystemId,
            SystemBId = nadir.SystemId,
            Distance = 100,
            TravelTicks = 1
        });

        var result = GameSeeder.UpgradeGalaxyTopology(state);

        Assert.True(result.Changed);
        Assert.Equal(8, result.SectorsAdded);
        Assert.Equal(40, result.SystemsAdded);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, result.LinksAdded);
        Assert.Equal(1, result.LinksRemoved);
        Assert.Equal(retainedIds, state.Systems.Where(item => legacyNames.Contains(item.SystemName))
            .ToDictionary(item => item.SystemName, item => item.SystemId, StringComparer.Ordinal));
        Assert.True(GameStateTransfer.Validate(state).IsValid);

        var second = GameSeeder.UpgradeGalaxyTopology(state);
        Assert.False(second.Changed);
        Assert.Equal(64, state.Systems.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, state.SystemLinks.Count);
    }

    [Fact]
    public void Topology_upgrade_repairs_canonical_route_drift()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        state.SystemLinks[0].TravelTicks = 99;

        var result = GameSeeder.UpgradeGalaxyTopology(state);

        Assert.True(result.Changed);
        Assert.Equal(0, result.SectorsAdded);
        Assert.Equal(0, result.SystemsAdded);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, result.LinksAdded);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, result.LinksRemoved);
        Assert.All(state.SystemLinks, link => Assert.Contains(link.TravelTicks, new[] { 1, 2 }));
        Assert.False(GameSeeder.UpgradeGalaxyTopology(state).Changed);
    }

    [Fact]
    public void Topology_upgrade_refuses_route_changes_while_a_fleet_is_in_transit_without_partial_mutation()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var driftedLink = state.SystemLinks[0];
        driftedLink.TravelTicks = 99;
        var fleet = state.Fleets[0];
        fleet.Status = FleetStatus.InTransit;
        fleet.DestinationSystemId = state.Systems.First(system => system.SystemId != fleet.CurrentSystemId).SystemId;

        var exception = Assert.Throws<InvalidOperationException>(() => GameSeeder.UpgradeGalaxyTopology(state));

        Assert.Contains("no fleets in transit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(8, state.Sectors.Count);
        Assert.Equal(64, state.Systems.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, state.SystemLinks.Count);
        Assert.Equal(99, driftedLink.TravelTicks);
    }

    [Fact]
    public void Topology_upgrade_refuses_a_custom_galaxy()
    {
        var state = GameSeeder.CreateDefault(systemCount: 12, empireCount: 2, seed: 99, createdAt: TestState.Now);

        var exception = Assert.Throws<InvalidOperationException>(() => GameSeeder.UpgradeGalaxyTopology(state));

        Assert.Contains("only supports", exception.Message, StringComparison.Ordinal);
    }

    private static HashSet<Guid> AdjacentSectors(
        Guid sectorId,
        IEnumerable<SystemLink> crossLinks,
        IReadOnlyDictionary<Guid, Guid> sectorBySystem) =>
        crossLinks
            .Where(link => sectorBySystem[link.SystemAId] == sectorId || sectorBySystem[link.SystemBId] == sectorId)
            .Select(link => sectorBySystem[link.SystemAId] == sectorId
                ? sectorBySystem[link.SystemBId]
                : sectorBySystem[link.SystemAId])
            .ToHashSet();

    private static HashSet<Guid> ReachableSectorIds(
        Guid start,
        IReadOnlyCollection<SystemLink> crossLinks,
        IReadOnlyDictionary<Guid, Guid> sectorBySystem)
    {
        var visited = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var adjacent in AdjacentSectors(current, crossLinks, sectorBySystem).Where(visited.Add))
            {
                queue.Enqueue(adjacent);
            }
        }

        return visited;
    }

    private static HashSet<Guid> ReachableSystemIds(Guid start, IReadOnlyCollection<SystemLink> links)
    {
        var visited = new HashSet<Guid> { start };
        var queue = new Queue<Guid>();
        queue.Enqueue(start);
        while (queue.TryDequeue(out var current))
        {
            foreach (var adjacent in links
                         .Where(link => link.SystemAId == current || link.SystemBId == current)
                         .Select(link => link.SystemAId == current ? link.SystemBId : link.SystemAId)
                         .Where(visited.Add))
            {
                queue.Enqueue(adjacent);
            }
        }

        return visited;
    }
}
