using System.Security.Cryptography;
using System.Text.Json;

namespace Cycles.Core;

public sealed record ProfileCatalogueValidationResult(IReadOnlyList<string> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record GamePolicyProfile(
    string Key,
    int Version,
    GamePurpose Purpose,
    GameVisibility Visibility,
    string ContentHash);

public sealed record CyclePolicyProfile(
    string Key,
    int Version,
    CycleSchedulingMode SchedulingMode,
    int TickLengthMinutes,
    int DefaultDurationDays,
    string ContentHash);

public sealed record MapSectorProfile(
    string Key,
    string Name,
    int CentreX,
    int CentreY,
    int SortOrder);

public sealed record MapSystemProfile(
    string Key,
    string Name,
    string SectorKey,
    int X,
    int Y,
    decimal IndustryOutput,
    decimal ResearchOutput,
    decimal PopulationOutput,
    int StrategicValue,
    int HistoricalSignificance);

public sealed record MapRouteProfile(
    string Key,
    string FirstSystemKey,
    string SecondSystemKey,
    int TravelTicks);

internal sealed record MapProfileBlueprint(
    IReadOnlyList<MapSectorProfile> Sectors,
    IReadOnlyList<MapSystemProfile> Systems,
    IReadOnlyList<MapRouteProfile> Routes);

public sealed record MapProfileDefinition(
    string Key,
    int Version,
    int MinimumHumanSeats,
    int MaximumHumanSeats,
    string? AtlasKey,
    IReadOnlyList<MapSectorProfile> Sectors,
    IReadOnlyList<MapSystemProfile> Systems,
    IReadOnlyList<MapRouteProfile> Routes,
    string ContentHash);

public sealed record ScenarioFleetProfile(
    string NameTemplate,
    string SystemKey,
    int ShipCount,
    bool UsesStartingAdmiral);

public sealed record NeutralFactionProfile(
    string Name,
    IReadOnlyList<ScenarioFleetProfile> Fleets);

public sealed record ScenarioProfileDefinition(
    string Key,
    int Version,
    GamePurpose Purpose,
    string MapProfileKey,
    int MapProfileVersion,
    int MinimumHumanSeats,
    int MaximumHumanSeats,
    IReadOnlyList<string> HumanEmpireNames,
    IReadOnlyList<string> HumanAdmiralNames,
    decimal StartingIndustry,
    decimal StartingResearch,
    decimal StartingPopulation,
    int InitialMilitaryWeight,
    int InitialExpansionWeight,
    IReadOnlyList<ScenarioFleetProfile> HumanFleets,
    NeutralFactionProfile? NeutralFaction,
    string ContentHash);

public sealed record GameProfileDefinition(
    string Key,
    int Version,
    string DisplayName,
    GamePurpose Purpose,
    GamePolicyProfile GamePolicy,
    MapProfileDefinition Map,
    ScenarioProfileDefinition Scenario,
    CyclePolicyProfile CyclePolicy,
    int MinimumHumanSeats,
    int MaximumHumanSeats);

public static class GameProfileCatalogue
{
    public const string StandardProfileKey = "standard-galaxy-v1";
    public const string TwinReachesProfileKey = "tutorial-foundations-v1";

    private const string StandardGamePolicyHash = "e6c2801c790406df5def2c66ec5514f86fd7610b8f98380b5b1ad8cccda837f3";
    private const string TrainingGamePolicyHash = "a839601ccb697d03bcee2a8e80a9c1676a0eca5fc96f274650b2136767faeb5f";
    private const string StandardMapHash = "b18d578b06c93325fd418c269f88fc9c2449156ff4ad91e3453be56fe95cf333";
    private const string TwinReachesMapHash = "4455d1666a72f6bf477b2ad3fe708638517c4c89f1854f3fc66c25c356f82b3b";
    private const string StandardScenarioHash = "358933ff2ac2ad0fcb14e8f85237cacea949f14bb1133c14b2cae4e9f9cb3ed3";
    private const string TwinReachesScenarioHash = "6a01dee3caf0f76682da3d869e2ad8efe92559c465594dfe4d317f99eecf7f43";
    private const string StandardCyclePolicyHash = "2d6ab2fd22654f211795599d6f7d84f7c16cb24f16f4190025f366b61db51ff9";
    private const string TrainingCyclePolicyHash = "bc2e18915dd423e44647710236dc19a9d16192a2ff88c5d5cb13c63399162a56";

    public static GameProfileDefinition Standard { get; } = CreateStandard();
    public static GameProfileDefinition TwinReaches { get; } = CreateTwinReaches();
    public static IReadOnlyList<GameProfileDefinition> All { get; } =
        Array.AsReadOnly<GameProfileDefinition>([Standard, TwinReaches]);

    public static ProfileCatalogueValidationResult Validate() => Validate(All);

    public static void EnsureValid()
    {
        var validation = Validate();
        if (!validation.IsValid)
        {
            throw new InvalidOperationException(
                "The code-owned Game profile catalogue is invalid: " + string.Join(" ", validation.Errors));
        }
    }

    public static GameProfileDefinition Resolve(CycleConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        EnsureValid();

        var matches = All.Where(profile =>
                string.Equals(profile.Map.Key, configuration.MapProfileKey, StringComparison.Ordinal)
                && profile.Map.Version == configuration.MapProfileVersion
                && string.Equals(profile.Map.ContentHash, configuration.MapProfileContentHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(profile.Scenario.Key, configuration.ScenarioProfileKey, StringComparison.Ordinal)
                && profile.Scenario.Version == configuration.ScenarioProfileVersion
                && string.Equals(profile.Scenario.ContentHash, configuration.ScenarioProfileContentHash, StringComparison.OrdinalIgnoreCase)
                && string.Equals(profile.CyclePolicy.Key, configuration.CyclePolicyKey, StringComparison.Ordinal)
                && profile.CyclePolicy.Version == configuration.CyclePolicyVersion
                && string.Equals(profile.CyclePolicy.ContentHash, configuration.CyclePolicyContentHash, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        return matches.Length switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} does not match an immutable code-owned profile."),
            _ => throw new InvalidOperationException(
                $"Cycle configuration {configuration.CycleConfigurationId} matches more than one code-owned profile.")
        };
    }

    internal static ProfileCatalogueValidationResult Validate(
        IReadOnlyCollection<GameProfileDefinition> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);
        var errors = new List<string>();

        foreach (var duplicate in profiles
                     .GroupBy(item => (item.Key, item.Version))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Game profile {duplicate.Key.Key} v{duplicate.Key.Version} is declared more than once.");
        }

        ValidateSharedContent(profiles.Select(item => item.GamePolicy), "Game policy", errors);
        ValidateSharedContent(profiles.Select(item => item.Map), "Map profile", errors);
        ValidateSharedContent(profiles.Select(item => item.Scenario), "Scenario profile", errors);
        ValidateSharedContent(profiles.Select(item => item.CyclePolicy), "Cycle policy", errors);

        foreach (var profile in profiles)
        {
            ValidateProfile(profile, errors);
        }

        return new ProfileCatalogueValidationResult(errors);
    }

    private static void ValidateProfile(GameProfileDefinition profile, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(profile.Key) || profile.Version <= 0)
        {
            errors.Add("Every Game profile requires a stable key and positive version.");
        }
        if (profile.MinimumHumanSeats <= 0 || profile.MaximumHumanSeats < profile.MinimumHumanSeats)
        {
            errors.Add($"Game profile {profile.Key} v{profile.Version} has invalid human-seat bounds.");
        }
        if (profile.Purpose != profile.GamePolicy.Purpose
            || profile.Purpose != profile.Scenario.Purpose)
        {
            errors.Add($"Game profile {profile.Key} v{profile.Version} has inconsistent purpose metadata.");
        }
        if (profile.MinimumHumanSeats != profile.Scenario.MinimumHumanSeats
            || profile.MaximumHumanSeats != profile.Scenario.MaximumHumanSeats
            || profile.MinimumHumanSeats < profile.Map.MinimumHumanSeats
            || profile.MaximumHumanSeats > profile.Map.MaximumHumanSeats)
        {
            errors.Add($"Game profile {profile.Key} v{profile.Version} has incompatible roster bounds.");
        }
        if (profile.CyclePolicy.SchedulingMode == CycleSchedulingMode.SelfPaced
            && profile.Purpose != GamePurpose.Training)
        {
            errors.Add($"Game profile {profile.Key} v{profile.Version} makes a non-Training Game self-paced.");
        }

        ValidateHash(
            $"Game policy {profile.GamePolicy.Key} v{profile.GamePolicy.Version}",
            profile.GamePolicy.ContentHash,
            CalculateGamePolicyHash(profile.GamePolicy),
            errors);
        ValidateHash(
            $"Map profile {profile.Map.Key} v{profile.Map.Version}",
            profile.Map.ContentHash,
            CalculateMapHash(profile.Map),
            errors);
        ValidateHash(
            $"Scenario profile {profile.Scenario.Key} v{profile.Scenario.Version}",
            profile.Scenario.ContentHash,
            CalculateScenarioHash(profile.Scenario),
            errors);
        ValidateHash(
            $"Cycle policy {profile.CyclePolicy.Key} v{profile.CyclePolicy.Version}",
            profile.CyclePolicy.ContentHash,
            CalculateCyclePolicyHash(profile.CyclePolicy),
            errors);

        ValidateMap(profile.Map, errors);
        ValidateScenario(profile.Map, profile.Scenario, errors);
    }

    private static void ValidateMap(MapProfileDefinition map, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(map.Key) || map.Version <= 0)
        {
            errors.Add("Every Map profile requires a stable key and positive version.");
        }
        if (map.MinimumHumanSeats <= 0 || map.MaximumHumanSeats < map.MinimumHumanSeats)
        {
            errors.Add($"Map profile {map.Key} v{map.Version} has invalid human-seat bounds.");
        }
        if (map.MaximumHumanSeats > map.Systems.Count)
        {
            errors.Add($"Map profile {map.Key} v{map.Version} has more human seats than systems.");
        }

        AddDuplicateErrors(map.Sectors.Select(item => item.Key), $"Map profile {map.Key} sector key", errors);
        AddDuplicateErrors(map.Sectors.Select(item => item.Name), $"Map profile {map.Key} sector name", errors);
        AddDuplicateErrors(map.Sectors.Select(item => item.SortOrder.ToString()), $"Map profile {map.Key} sector sort order", errors);
        AddDuplicateErrors(map.Systems.Select(item => item.Key), $"Map profile {map.Key} system key", errors);
        AddDuplicateErrors(map.Systems.Select(item => item.Name), $"Map profile {map.Key} system name", errors);
        AddDuplicateErrors(map.Routes.Select(item => item.Key), $"Map profile {map.Key} route key", errors);
        foreach (var duplicate in map.Systems
                     .GroupBy(item => (item.X, item.Y))
                     .Where(group => group.Count() > 1))
        {
            errors.Add($"Map profile {map.Key} duplicates system coordinates {duplicate.Key.X},{duplicate.Key.Y}.");
        }

        var sectorKeys = map.Sectors.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        var systemKeys = map.Systems.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var sector in map.Sectors)
        {
            if (sector.CentreX is < 0 or > 1000 || sector.CentreY is < 0 or > 700)
            {
                errors.Add($"Map profile {map.Key} sector {sector.Key} lies outside the atlas bounds.");
            }
        }
        foreach (var system in map.Systems)
        {
            if (!sectorKeys.Contains(system.SectorKey))
            {
                errors.Add($"Map profile {map.Key} system {system.Key} references an unknown sector.");
            }
            if (system.X is < 0 or > 1000 || system.Y is < 0 or > 700)
            {
                errors.Add($"Map profile {map.Key} system {system.Key} lies outside the atlas bounds.");
            }
            if (system.IndustryOutput < 0 || system.ResearchOutput < 0 || system.PopulationOutput < 0
                || system.StrategicValue < 0 || system.HistoricalSignificance < 0)
            {
                errors.Add($"Map profile {map.Key} system {system.Key} has invalid output or history values.");
            }
        }

        var routePairs = new HashSet<string>(StringComparer.Ordinal);
        foreach (var route in map.Routes)
        {
            if (!systemKeys.Contains(route.FirstSystemKey) || !systemKeys.Contains(route.SecondSystemKey))
            {
                errors.Add($"Map profile {map.Key} route {route.Key} references an unknown system.");
                continue;
            }
            if (string.Equals(route.FirstSystemKey, route.SecondSystemKey, StringComparison.Ordinal)
                || route.TravelTicks is < 1 or > 12)
            {
                errors.Add($"Map profile {map.Key} route {route.Key} has invalid endpoints or travel time.");
            }
            var pair = string.CompareOrdinal(route.FirstSystemKey, route.SecondSystemKey) <= 0
                ? $"{route.FirstSystemKey}|{route.SecondSystemKey}"
                : $"{route.SecondSystemKey}|{route.FirstSystemKey}";
            if (!routePairs.Add(pair))
            {
                errors.Add($"Map profile {map.Key} contains duplicate route {pair}.");
            }
        }

        if (map.Systems.Count > 0 && ReachableSystemCount(map) != map.Systems.Count)
        {
            errors.Add($"Map profile {map.Key} topology is not connected.");
        }
    }

    private static void ValidateScenario(
        MapProfileDefinition map,
        ScenarioProfileDefinition scenario,
        List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(scenario.Key) || scenario.Version <= 0)
        {
            errors.Add("Every Scenario profile requires a stable key and positive version.");
        }
        if (!string.Equals(scenario.MapProfileKey, map.Key, StringComparison.Ordinal)
            || scenario.MapProfileVersion != map.Version)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} targets a different Map profile.");
        }
        if (scenario.MinimumHumanSeats <= 0
            || scenario.MaximumHumanSeats < scenario.MinimumHumanSeats
            || scenario.MinimumHumanSeats < map.MinimumHumanSeats
            || scenario.MaximumHumanSeats > map.MaximumHumanSeats)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} has invalid human-seat bounds.");
        }
        if (scenario.HumanEmpireNames.Count < scenario.MaximumHumanSeats
            || scenario.HumanAdmiralNames.Count < scenario.MaximumHumanSeats
            || scenario.HumanEmpireNames.Distinct(StringComparer.Ordinal).Count() != scenario.HumanEmpireNames.Count
            || scenario.HumanAdmiralNames.Distinct(StringComparer.Ordinal).Count() != scenario.HumanAdmiralNames.Count)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} lacks unique roster names for its capacity.");
        }
        if (scenario.InitialMilitaryWeight < 0
            || scenario.InitialExpansionWeight < 0
            || scenario.InitialMilitaryWeight + scenario.InitialExpansionWeight != StrategicPriorityPolicy.TotalWeight)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} has invalid initial priorities.");
        }

        var systemKeys = map.Systems.Select(item => item.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var fleet in scenario.HumanFleets)
        {
            if (fleet.ShipCount <= 0
                || (!string.Equals(fleet.SystemKey, "@home", StringComparison.Ordinal)
                    && !systemKeys.Contains(fleet.SystemKey)))
            {
                errors.Add($"Scenario profile {scenario.Key} fleet {fleet.NameTemplate} has an invalid location or ship count.");
            }
        }
        if (scenario.HumanFleets.Count == 0)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} has no human starting fleet.");
        }
        if (scenario.NeutralFaction is { } neutral
            && (string.IsNullOrWhiteSpace(neutral.Name) || neutral.Fleets.Count == 0))
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} has an incomplete neutral faction.");
        }
        if (scenario.HumanFleets.Count(item => item.UsesStartingAdmiral) != 1)
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} must identify exactly one starting-admiral fleet.");
        }
        if (scenario.MaximumHumanSeats > 1
            && scenario.HumanFleets.Any(item => !string.Equals(item.SystemKey, "@home", StringComparison.Ordinal)))
        {
            errors.Add($"Scenario profile {scenario.Key} v{scenario.Version} cannot share a fixed Human home across several seats.");
        }
        foreach (var fleet in scenario.NeutralFaction?.Fleets ?? [])
        {
            if (fleet.ShipCount <= 0
                || fleet.UsesStartingAdmiral
                || !systemKeys.Contains(fleet.SystemKey))
            {
                errors.Add($"Scenario profile {scenario.Key} neutral fleet {fleet.NameTemplate} has an invalid location or ship count.");
            }
        }
    }

    private static void ValidateSharedContent<T>(
        IEnumerable<T> values,
        string label,
        List<string> errors)
        where T : notnull
    {
        var metadata = values.Select(value => value switch
        {
            GamePolicyProfile item => (item.Key, item.Version, item.ContentHash),
            MapProfileDefinition item => (item.Key, item.Version, item.ContentHash),
            ScenarioProfileDefinition item => (item.Key, item.Version, item.ContentHash),
            CyclePolicyProfile item => (item.Key, item.Version, item.ContentHash),
            _ => throw new InvalidOperationException($"Unsupported profile type {typeof(T).Name}.")
        });
        foreach (var group in metadata.GroupBy(item => (item.Key, item.Version)))
        {
            if (group.Select(item => item.ContentHash).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1)
            {
                errors.Add($"{label} {group.Key.Key} v{group.Key.Version} is associated with changed content.");
            }
        }
    }

    private static void ValidateHash(
        string label,
        string declared,
        string calculated,
        List<string> errors)
    {
        if (declared.Length != 64 || declared.Any(character => !char.IsAsciiHexDigit(character)))
        {
            errors.Add($"{label} declares an invalid content hash.");
        }
        else if (!string.Equals(declared, calculated, StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"{label} content changed without a version bump; declared {declared}, calculated {calculated}.");
        }
    }

    private static void AddDuplicateErrors(
        IEnumerable<string> values,
        string label,
        List<string> errors)
    {
        foreach (var duplicate in values
                     .GroupBy(item => item, StringComparer.Ordinal)
                     .Where(group => string.IsNullOrWhiteSpace(group.Key) || group.Count() > 1))
        {
            errors.Add($"{label} '{duplicate.Key}' is missing or duplicated.");
        }
    }

    private static int ReachableSystemCount(MapProfileDefinition map)
    {
        var adjacent = map.Systems.ToDictionary(
            item => item.Key,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        foreach (var route in map.Routes)
        {
            if (!adjacent.ContainsKey(route.FirstSystemKey) || !adjacent.ContainsKey(route.SecondSystemKey))
            {
                continue;
            }
            adjacent[route.FirstSystemKey].Add(route.SecondSystemKey);
            adjacent[route.SecondSystemKey].Add(route.FirstSystemKey);
        }

        var first = map.Systems.FirstOrDefault()?.Key;
        if (first is null)
        {
            return 0;
        }
        var visited = new HashSet<string>(StringComparer.Ordinal) { first };
        var queue = new Queue<string>();
        queue.Enqueue(first);
        while (queue.TryDequeue(out var current))
        {
            foreach (var next in adjacent[current].Where(visited.Add))
            {
                queue.Enqueue(next);
            }
        }
        return visited.Count;
    }

    private static string CalculateGamePolicyHash(GamePolicyProfile policy) => Hash(new
    {
        policy.Key,
        policy.Version,
        purpose = policy.Purpose.ToString(),
        visibility = policy.Visibility.ToString()
    });

    private static string CalculateCyclePolicyHash(CyclePolicyProfile policy) => Hash(new
    {
        policy.Key,
        policy.Version,
        schedulingMode = policy.SchedulingMode.ToString(),
        policy.TickLengthMinutes,
        policy.DefaultDurationDays
    });

    private static string CalculateMapHash(MapProfileDefinition map) => Hash(new
    {
        map.Key,
        map.Version,
        map.MinimumHumanSeats,
        map.MaximumHumanSeats,
        map.AtlasKey,
        sectors = map.Sectors.OrderBy(item => item.Key, StringComparer.Ordinal),
        systems = map.Systems.OrderBy(item => item.Key, StringComparer.Ordinal),
        routes = map.Routes.OrderBy(item => item.Key, StringComparer.Ordinal)
    });

    private static string CalculateScenarioHash(ScenarioProfileDefinition scenario) => Hash(new
    {
        scenario.Key,
        scenario.Version,
        purpose = scenario.Purpose.ToString(),
        scenario.MapProfileKey,
        scenario.MapProfileVersion,
        scenario.MinimumHumanSeats,
        scenario.MaximumHumanSeats,
        scenario.HumanEmpireNames,
        scenario.HumanAdmiralNames,
        scenario.StartingIndustry,
        scenario.StartingResearch,
        scenario.StartingPopulation,
        scenario.InitialMilitaryWeight,
        scenario.InitialExpansionWeight,
        scenario.HumanFleets,
        scenario.NeutralFaction
    });

    private static string Hash(object value) => Convert.ToHexString(
            SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(value, GameStateJson.Options)))
        .ToLowerInvariant();

    private static GameProfileDefinition CreateStandard()
    {
        var blueprint = GameSeeder.CreateCanonicalProfileBlueprint();
        var map = new MapProfileDefinition(
            GameSeeder.CanonicalGalaxyTopologyKey,
            1,
            1,
            6,
            GameSeeder.CanonicalGalaxyTopologyKey,
            blueprint.Sectors,
            blueprint.Systems,
            blueprint.Routes,
            StandardMapHash);
        var scenario = new ScenarioProfileDefinition(
            StandardProfileKey,
            1,
            GamePurpose.Standard,
            map.Key,
            map.Version,
            1,
            6,
            ["Aurelian Compact", "Khepri Mandate", "Novan League", "Vestige Combine", "Helio Archive", "Marrow Directorate"],
            ["Elian Voss", "Mara Sutekh", "Tavian Orre", "Ilya Sen", "Nadia Kepler", "Soren Vale"],
            100,
            100,
            100,
            StrategicPriorityPolicy.DefaultMilitaryWeight,
            StrategicPriorityPolicy.DefaultExpansionWeight,
            [new ScenarioFleetProfile("{empire} Home Fleet", "@home", 60, true)],
            null,
            StandardScenarioHash);
        return new GameProfileDefinition(
            StandardProfileKey,
            1,
            "Standard galaxy",
            GamePurpose.Standard,
            new GamePolicyProfile(
                "standard-private-v1",
                1,
                GamePurpose.Standard,
                GameVisibility.Private,
                StandardGamePolicyHash),
            map,
            scenario,
            new CyclePolicyProfile(
                "standard-hourly-v1",
                1,
                CycleSchedulingMode.Scheduled,
                60,
                90,
                StandardCyclePolicyHash),
            1,
            6);
    }

    private static GameProfileDefinition CreateTwinReaches()
    {
        var sectors = new MapSectorProfile[]
        {
            new("inner-reach", "Inner Reach", 330, 350, 0),
            new("outer-reach", "Outer Reach", 780, 350, 1)
        };
        var systems = new MapSystemProfile[]
        {
            new("hearth", "Hearth", "inner-reach", 160, 350, 30, 20, 15, 16, 0),
            new("firstlight", "Firstlight", "inner-reach", 280, 240, 18, 28, 22, 18, 0),
            new("greenwater", "Greenwater", "inner-reach", 310, 460, 12, 22, 45, 20, 0),
            new("watchpoint", "Watchpoint", "inner-reach", 430, 350, 25, 12, 18, 24, 0),
            new("gatehouse", "Gatehouse", "inner-reach", 520, 250, 20, 30, 15, 32, 1),
            new("threshold", "Threshold", "outer-reach", 650, 250, 15, 35, 18, 32, 1),
            new("ironwell", "Ironwell", "outer-reach", 770, 180, 45, 12, 12, 24, 0),
            new("quiet-archive", "Quiet Archive", "outer-reach", 800, 340, 10, 50, 10, 24, 0),
            new("new-haven", "New Haven", "outer-reach", 740, 500, 15, 18, 50, 24, 0),
            new("redoubt", "Redoubt", "outer-reach", 900, 430, 30, 20, 20, 28, 0)
        };
        var routes = new MapRouteProfile[]
        {
            Route("hearth", "firstlight"),
            Route("hearth", "greenwater"),
            Route("firstlight", "watchpoint"),
            Route("greenwater", "watchpoint"),
            Route("firstlight", "gatehouse"),
            Route("watchpoint", "gatehouse"),
            Route("threshold", "ironwell"),
            Route("threshold", "quiet-archive"),
            Route("ironwell", "quiet-archive"),
            Route("quiet-archive", "new-haven"),
            Route("ironwell", "redoubt"),
            Route("new-haven", "redoubt"),
            Route("gatehouse", "threshold", 2)
        };
        var map = new MapProfileDefinition(
            TwinReachesProfileKey,
            1,
            1,
            1,
            null,
            sectors,
            systems,
            routes,
            TwinReachesMapHash);
        var scenario = new ScenarioProfileDefinition(
            TwinReachesProfileKey,
            1,
            GamePurpose.Training,
            map.Key,
            map.Version,
            1,
            1,
            ["Wayfarer Compact"],
            ["Elian Voss"],
            0,
            80,
            100,
            67,
            33,
            [
                new ScenarioFleetProfile("Home Guard", "hearth", 20, true),
                new ScenarioFleetProfile("Survey Wing", "greenwater", 12, false),
                new ScenarioFleetProfile("Vanguard", "watchpoint", 24, false)
            ],
            new NeutralFactionProfile(
                "Drift Corsairs",
                [
                    new ScenarioFleetProfile("Watchpoint Corsairs", "watchpoint", 6, false),
                    new ScenarioFleetProfile("Redoubt Corsairs", "redoubt", 4, false)
                ]),
            TwinReachesScenarioHash);
        return new GameProfileDefinition(
            TwinReachesProfileKey,
            1,
            "Twin Reaches",
            GamePurpose.Training,
            new GamePolicyProfile(
                "training-private-v1",
                1,
                GamePurpose.Training,
                GameVisibility.Private,
                TrainingGamePolicyHash),
            map,
            scenario,
            new CyclePolicyProfile(
                "training-self-paced-v1",
                1,
                CycleSchedulingMode.SelfPaced,
                60,
                90,
                TrainingCyclePolicyHash),
            1,
            1);
    }

    private static MapRouteProfile Route(string first, string second, int travelTicks = 1)
    {
        var key = string.CompareOrdinal(first, second) <= 0 ? $"{first}|{second}" : $"{second}|{first}";
        return new MapRouteProfile(key, first, second, travelTicks);
    }
}
