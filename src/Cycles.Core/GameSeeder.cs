using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cycles.Core;

public static class GameSeeder
{
    public const string CuratedColdStartScenarioKey = "development-cold-start-v1";

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
        const int seed = 71421;
        var now = createdAt ?? DateTimeOffset.UtcNow;
        var identitySequence = new DeterministicIdentitySequence(seed);
        var state = Create(24, 4, seed, now, identitySequence.Next);

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
        var random = new Random(seed);
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
