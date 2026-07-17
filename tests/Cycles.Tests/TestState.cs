using Cycles.Core;

namespace Cycles.Tests;

internal static class TestState
{
    public static readonly DateTimeOffset Now = new(2026, 6, 23, 20, 0, 0, TimeSpan.Zero);

    public static GameState CreateSingleEmpireState(bool includeFleet = true)
    {
        var state = new GameState();
        var cycle = AddCycle(state);
        var player = AddPlayer(state, "one");
        var system = AddSystem(state, cycle.CycleId, "Home", 80, 40, 20, 20, 0);
        var empire = AddEmpire(state, cycle.CycleId, player.PlayerId, "One", system.SystemId);
        AddResources(state, empire.EmpireId);

        if (includeFleet)
        {
            AddFleet(state, cycle.CycleId, empire.EmpireId, system.SystemId, 50);
        }

        return state;
    }

    public static GameState CreateTwoEmpireContest(
        int attackerShips,
        int defenderShips,
        int strategicValue = 25,
        int historicalSignificance = 0)
    {
        var state = new GameState();
        var cycle = AddCycle(state);
        var firstPlayer = AddPlayer(state, "first");
        var secondPlayer = AddPlayer(state, "second");
        var system = AddSystem(state, cycle.CycleId, "Contest", 100, 100, 100, strategicValue, historicalSignificance);
        var firstEmpire = AddEmpire(state, cycle.CycleId, firstPlayer.PlayerId, "First", system.SystemId);
        var secondEmpire = AddEmpire(state, cycle.CycleId, secondPlayer.PlayerId, "Second", Guid.NewGuid());

        AddResources(state, firstEmpire.EmpireId);
        AddResources(state, secondEmpire.EmpireId);
        AddFleet(state, cycle.CycleId, firstEmpire.EmpireId, system.SystemId, attackerShips);
        AddFleet(state, cycle.CycleId, secondEmpire.EmpireId, system.SystemId, defenderShips);
        return state;
    }

    public static GameState CreateMovementState(bool linkSystems, int travelTicks = 1)
    {
        var state = new GameState();
        var cycle = AddCycle(state);
        var player = AddPlayer(state, "mover");
        var origin = AddSystem(state, cycle.CycleId, "Origin", 10, 10, 10, 10, 0);
        var destination = AddSystem(state, cycle.CycleId, "Destination", 10, 10, 10, 10, 0);
        var empire = AddEmpire(state, cycle.CycleId, player.PlayerId, "Mover", origin.SystemId);

        AddResources(state, empire.EmpireId);
        AddFleet(state, cycle.CycleId, empire.EmpireId, origin.SystemId, 25);

        if (linkSystems)
        {
            state.SystemLinks.Add(new SystemLink
            {
                CycleId = cycle.CycleId,
                SystemAId = origin.SystemId,
                SystemBId = destination.SystemId,
                Distance = travelTicks,
                TravelTicks = travelTicks
            });
        }

        return state;
    }

    private static Cycle AddCycle(GameState state)
    {
        var cycle = new Cycle
        {
            Name = "Test Cycle",
            StartAt = Now,
            EndAt = Now.AddDays(90),
            TickLengthMinutes = 60,
            Status = CycleStatus.Active,
            CreatedAt = Now
        };
        state.Cycles.Add(cycle);
        return cycle;
    }

    private static Player AddPlayer(GameState state, string username)
    {
        var player = new Player
        {
            Username = username,
            CreatedAt = Now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
        state.GetActiveCycle()!.CreatedByPlayerId ??= player.PlayerId;
        return player;
    }

    private static GalaxySystem AddSystem(
        GameState state,
        Guid cycleId,
        string name,
        decimal industry,
        decimal research,
        decimal population,
        int strategicValue,
        int historicalSignificance)
    {
        var system = new GalaxySystem
        {
            CycleId = cycleId,
            SystemName = name,
            X = 100,
            Y = 100,
            IndustryOutput = industry,
            ResearchOutput = research,
            PopulationOutput = population,
            StrategicValue = strategicValue,
            HistoricalSignificance = historicalSignificance,
            CreatedAt = Now
        };
        state.Systems.Add(system);
        return system;
    }

    private static Empire AddEmpire(GameState state, Guid cycleId, Guid playerId, string name, Guid homeSystemId)
    {
        var empire = new Empire
        {
            CycleId = cycleId,
            PlayerId = playerId,
            EmpireName = name,
            HomeSystemId = homeSystemId,
            CreatedAt = Now,
            Status = EmpireStatus.Active
        };
        state.Empires.Add(empire);
        var faction = new Faction
        {
            FactionId = empire.EmpireId,
            CycleId = cycleId,
            EmpireId = empire.EmpireId,
            FactionName = name,
            Kind = FactionKind.Empire,
            Status = FactionStatus.Active,
            CreatedAt = Now
        };
        state.Factions.Add(faction);
        state.MatchParticipants.Add(new MatchParticipant
        {
            CycleId = cycleId,
            PlayerId = playerId,
            EmpireId = empire.EmpireId,
            Status = MatchParticipantStatus.Active,
            JoinedAt = Now
        });
        return empire;
    }

    private static void AddResources(GameState state, Guid empireId)
    {
        state.EmpireResources.Add(new EmpireResource
        {
            EmpireId = empireId,
            UpdatedAt = Now
        });
        state.EmpirePriorities.Add(new EmpirePriority
        {
            EmpireId = empireId,
            IndustryWeight = 0,
            ResearchWeight = 0,
            MilitaryWeight = 0,
            ExpansionWeight = 100,
            UpdatedAt = Now
        });
    }

    private static void AddFleet(GameState state, Guid cycleId, Guid empireId, Guid systemId, int shipCount)
    {
        var faction = state.GetEmpireFaction(empireId);
        state.Fleets.Add(new Fleet
        {
            CycleId = cycleId,
            EmpireId = empireId,
            FactionId = faction.FactionId,
            FleetName = $"{shipCount} ships",
            CurrentSystemId = systemId,
            ShipCount = shipCount,
            Status = FleetStatus.Active,
            CreatedAt = Now
        });
    }
}
