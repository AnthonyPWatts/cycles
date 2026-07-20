using Cycles.Core;

namespace Cycles.Tests;

public sealed class ProfileCatalogueTests
{
    [Fact]
    public void Code_owned_catalogue_is_valid_and_versioned_uniquely()
    {
        var validation = GameProfileCatalogue.Validate();

        Assert.True(validation.IsValid, string.Join(Environment.NewLine, validation.Errors));
        Assert.Equal(
            GameProfileCatalogue.All.Count,
            GameProfileCatalogue.All.Select(item => (item.Key, item.Version)).Distinct().Count());
        Assert.All(GameProfileCatalogue.All, profile =>
        {
            Assert.Equal(64, profile.GamePolicy.ContentHash.Length);
            Assert.Equal(64, profile.Map.ContentHash.Length);
            Assert.Equal(64, profile.Scenario.ContentHash.Length);
            Assert.Equal(64, profile.CyclePolicy.ContentHash.Length);
        });
    }

    [Fact]
    public void Changed_content_under_an_existing_version_is_rejected()
    {
        var twinReaches = GameProfileCatalogue.TwinReaches;
        var changedSystems = twinReaches.Map.Systems
            .Select(system => system.Key == "hearth" ? system with { X = system.X + 1 } : system)
            .ToArray();
        var changed = twinReaches with
        {
            Map = twinReaches.Map with { Systems = changedSystems }
        };

        var validation = GameProfileCatalogue.Validate([changed]);

        Assert.False(validation.IsValid);
        Assert.Contains(validation.Errors, error =>
            error.Contains("content changed without a version bump", StringComparison.Ordinal));
    }

    [Fact]
    public void Standard_profile_describes_the_current_canonical_galaxy()
    {
        var map = GameProfileCatalogue.Standard.Map;

        Assert.Equal(GameSeeder.CanonicalGalaxyTopologyKey, map.Key);
        Assert.Equal(GameSeeder.CanonicalGalaxySectorCount, map.Sectors.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxySystemCount, map.Systems.Count);
        Assert.Equal(GameSeeder.CanonicalGalaxyRouteCount, map.Routes.Count);
        Assert.Equal(1, map.MinimumHumanSeats);
        Assert.Equal(6, map.MaximumHumanSeats);
        Assert.Equal(GamePurpose.Standard, GameProfileCatalogue.Standard.Purpose);
        Assert.Equal(CycleSchedulingMode.Scheduled, GameProfileCatalogue.Standard.CyclePolicy.SchedulingMode);
    }

    [Fact]
    public void Twin_reaches_has_the_approved_small_connected_topology()
    {
        var map = GameProfileCatalogue.TwinReaches.Map;
        var degree = map.Systems.ToDictionary(
            item => item.Key,
            item => map.Routes.Count(route =>
                route.FirstSystemKey == item.Key || route.SecondSystemKey == item.Key),
            StringComparer.Ordinal);

        Assert.Equal(2, map.Sectors.Count);
        Assert.Equal(10, map.Systems.Count);
        Assert.Equal(13, map.Routes.Count);
        Assert.All(degree.Values, value => Assert.InRange(value, 2, 3));
        var bridge = Assert.Single(map.Routes, item => item.TravelTicks == 2);
        Assert.Equal("gatehouse|threshold", bridge.Key);
        Assert.All(map.Routes.Where(item => item != bridge), item => Assert.Equal(1, item.TravelTicks));
        Assert.Equal(GamePurpose.Training, GameProfileCatalogue.TwinReaches.Purpose);
        Assert.Equal(CycleSchedulingMode.SelfPaced, GameProfileCatalogue.TwinReaches.CyclePolicy.SchedulingMode);
        Assert.Null(map.AtlasKey);
    }
}
