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
    public void Curated_galaxy_is_a_connected_sixteen_sector_crown()
    {
        var state = GameSeeder.CreateCuratedColdStart(TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var sectors = state.Sectors.Where(item => item.CycleId == cycle.CycleId).OrderBy(item => item.SortOrder).ToArray();
        var systems = state.Systems.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var links = state.SystemLinks.Where(item => item.CycleId == cycle.CycleId).ToArray();
        var sectorBySystem = systems.ToDictionary(item => item.SystemId, item => item.SectorId);

        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, sectors.Length);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, systems.Length);
        Assert.Equal(296, links.Length);
        Assert.Equal([18, 15, 21, 14, 17, 20, 12, 19, 16, 22, 13, 18, 24, 15, 20, 16],
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
        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, crossLinks.Length);
        Assert.All(crossLinks, link => Assert.Equal(2, link.TravelTicks));

        foreach (var sector in sectors)
        {
            var memberIds = systems.Where(item => item.SectorId == sector.SectorId).Select(item => item.SystemId).ToHashSet();
            var localLinks = links.Where(link => memberIds.Contains(link.SystemAId) && memberIds.Contains(link.SystemBId)).ToArray();
            var exits = crossLinks
                .SelectMany(link => new[] { link.SystemAId, link.SystemBId })
                .Where(memberIds.Contains)
                .ToArray();

            Assert.Equal(memberIds.Count, localLinks.Length);
            Assert.All(memberIds, systemId => Assert.Equal(2, localLinks.Count(link => link.SystemAId == systemId || link.SystemBId == systemId)));
            Assert.Equal(2, exits.Length);
            Assert.Equal(2, exits.Distinct().Count());
        }

        Assert.All(crossLinks.SelectMany(link => new[] { link.SystemAId, link.SystemBId }).GroupBy(item => item),
            gateway => Assert.Single(gateway));
        Assert.Equal(2, sectors.Select(sector => AdjacentSectors(sector.SectorId, crossLinks, sectorBySystem).Count).Distinct().Single());
        Assert.Equal(sectors.Length, ReachableSectorIds(sectors[0].SectorId, crossLinks, sectorBySystem).Count);
        Assert.Equal(systems.Length, ReachableSystemIds(systems[0].SystemId, links).Count);

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
        Assert.Equal(16, result.SectorsAdded);
        Assert.Equal(256, result.SystemsAdded);
        Assert.Equal(296, result.LinksAdded);
        Assert.Equal(1, result.LinksRemoved);
        Assert.Equal(retainedIds, state.Systems.Where(item => legacyNames.Contains(item.SystemName))
            .ToDictionary(item => item.SystemName, item => item.SystemId, StringComparer.Ordinal));
        Assert.True(GameStateTransfer.Validate(state).IsValid);

        var second = GameSeeder.UpgradeGalaxyTopology(state);
        Assert.False(second.Changed);
        Assert.Equal(280, state.Systems.Count);
        Assert.Equal(296, state.SystemLinks.Count);
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
        Assert.Equal(296, result.LinksAdded);
        Assert.Equal(296, result.LinksRemoved);
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
        Assert.Equal(16, state.Sectors.Count);
        Assert.Equal(280, state.Systems.Count);
        Assert.Equal(296, state.SystemLinks.Count);
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
