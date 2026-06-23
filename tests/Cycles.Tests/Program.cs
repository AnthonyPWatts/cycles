using Cycles.Core;

var tests = new (string Name, Action Test)[]
{
    ("70/30 influence splits resources proportionally", InfluenceSplitsResourcesProportionally),
    ("single empire receives full system output", SingleEmpireReceivesFullOutput),
    ("movement orders move only along links", MovementOrdersUseLinkedSystems),
    ("combat creates battle records, events, and Chronicle entries", CombatCreatesHistory),
    ("running tick prevents duplicate processing", RunningTickPreventsDuplicateProcessing),
    ("Chronicle scoring favours major battles", ChronicleScoringFavoursMajorBattles)
};

var failures = 0;
foreach (var (name, test) in tests)
{
    try
    {
        test();
        Console.WriteLine($"PASS {name}");
    }
    catch (Exception ex)
    {
        failures++;
        Console.WriteLine($"FAIL {name}");
        Console.WriteLine(ex.Message);
    }
}

return failures == 0 ? 0 : 1;

static void InfluenceSplitsResourcesProportionally()
{
    var state = TestState.CreateTwoEmpireState(attackerShips: 70, defenderShips: 30);
    var cycle = state.GetActiveCycle()!;

    InfluenceCalculator.GenerateResources(state, cycle.CycleId, 1, DateTimeOffset.UtcNow);

    var first = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[0].EmpireId);
    var second = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[1].EmpireId);

    AssertEqual(70m, first.Industry, "first empire industry");
    AssertEqual(30m, second.Industry, "second empire industry");
    AssertEqual(70m, first.Research, "first empire research");
    AssertEqual(30m, second.Research, "second empire research");
}

static void SingleEmpireReceivesFullOutput()
{
    var state = TestState.CreateSingleEmpireState();
    var cycle = state.GetActiveCycle()!;

    InfluenceCalculator.GenerateResources(state, cycle.CycleId, 1, DateTimeOffset.UtcNow);

    var resources = state.EmpireResources.Single();
    AssertEqual(80m, resources.Industry, "industry");
    AssertEqual(40m, resources.Research, "research");
    AssertEqual(20m, resources.Population, "population");
}

static void MovementOrdersUseLinkedSystems()
{
    var state = TestState.CreateMovementState(linkSystems: true);
    var cycle = state.GetActiveCycle()!;
    var fleet = state.Fleets.Single();
    var destination = state.Systems.Single(system => system.SystemName == "Destination");

    OrderService.SubmitMoveOrder(state, fleet.FleetId, destination.SystemId, DateTimeOffset.UtcNow);
    var result = new TickEngine().RunTick(state, cycle.CycleId, DateTimeOffset.UtcNow);

    var movedFleet = state.Fleets.Single(item => item.FleetId == fleet.FleetId);
    AssertEqual(TickLogStatus.Completed, result.Status, "tick status");
    AssertEqual(destination.SystemId, movedFleet.CurrentSystemId, "fleet destination");

    var invalidState = TestState.CreateMovementState(linkSystems: false);
    var invalidFleet = invalidState.Fleets.Single();
    var invalidDestination = invalidState.Systems.Single(system => system.SystemName == "Destination");
    AssertThrows<InvalidOperationException>(
        () => OrderService.SubmitMoveOrder(invalidState, invalidFleet.FleetId, invalidDestination.SystemId, DateTimeOffset.UtcNow),
        "unlinked move submission");
}

static void CombatCreatesHistory()
{
    var state = TestState.CreateTwoEmpireState(attackerShips: 160, defenderShips: 140, strategicValue: 65, historicalSignificance: 2);
    var cycle = state.GetActiveCycle()!;
    var attackerFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);

    OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, state.Empires[1].EmpireId, DateTimeOffset.UtcNow);
    var result = new TickEngine().RunTick(state, cycle.CycleId, DateTimeOffset.UtcNow);

    AssertEqual(TickLogStatus.Completed, result.Status, "tick status");
    AssertEqual(1, state.BattleRecords.Count, "battle count");
    AssertTrue(state.Events.Any(item => item.EventType == EventType.CombatResolved), "combat event exists");
    AssertTrue(state.ChronicleEntries.Count >= 1, "Chronicle entry exists");
}

static void RunningTickPreventsDuplicateProcessing()
{
    var state = TestState.CreateSingleEmpireState();
    var cycle = state.GetActiveCycle()!;
    state.TickLogs.Add(new TickLog
    {
        CycleId = cycle.CycleId,
        TickNumber = 1,
        StartedAt = DateTimeOffset.UtcNow,
        Status = TickLogStatus.Running
    });

    AssertThrows<InvalidOperationException>(
        () => new TickEngine().RunTick(state, cycle.CycleId, DateTimeOffset.UtcNow),
        "running tick lock");
}

static void ChronicleScoringFavoursMajorBattles()
{
    var system = new GalaxySystem { StrategicValue = 30, HistoricalSignificance = 1 };
    var minor = new BattleRecord
    {
        AttackerShipsBefore = 10,
        DefenderShipsBefore = 10,
        AttackerLosses = 2,
        DefenderLosses = 3,
        Outcome = BattleOutcome.AttackerVictory
    };
    var major = new BattleRecord
    {
        AttackerShipsBefore = 80,
        DefenderShipsBefore = 180,
        AttackerLosses = 20,
        DefenderLosses = 160,
        Outcome = BattleOutcome.AttackerVictory
    };

    AssertTrue(ChronicleScoring.ScoreBattle(major, system) > ChronicleScoring.ScoreBattle(minor, system), "major battle score");
    AssertTrue(ChronicleScoring.ScoreBattle(major, system) >= ChronicleScoring.ChronicleThreshold, "major battle threshold");
}

static void AssertEqual<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected {expected}, actual {actual}");
    }
}

static void AssertTrue(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException($"{label}: condition was false");
    }
}

static void AssertThrows<TException>(Action action, string label)
    where TException : Exception
{
    try
    {
        action();
    }
    catch (TException)
    {
        return;
    }

    throw new InvalidOperationException($"{label}: expected {typeof(TException).Name}");
}

internal static class TestState
{
    public static GameState CreateSingleEmpireState()
    {
        var now = DateTimeOffset.UtcNow;
        var state = new GameState();
        var cycle = AddCycle(state, now);
        var player = AddPlayer(state, "one", now);
        var system = AddSystem(state, cycle.CycleId, "Home", 80, 40, 20, 20, 0, now);
        var empire = AddEmpire(state, cycle.CycleId, player.PlayerId, "One", system.SystemId, now);
        AddResources(state, empire.EmpireId, now);
        AddFleet(state, cycle.CycleId, empire.EmpireId, system.SystemId, 50, now);
        return state;
    }

    public static GameState CreateTwoEmpireState(
        int attackerShips,
        int defenderShips,
        int strategicValue = 25,
        int historicalSignificance = 0)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new GameState();
        var cycle = AddCycle(state, now);
        var firstPlayer = AddPlayer(state, "first", now);
        var secondPlayer = AddPlayer(state, "second", now);
        var system = AddSystem(state, cycle.CycleId, "Contest", 100, 100, 100, strategicValue, historicalSignificance, now);
        var firstEmpire = AddEmpire(state, cycle.CycleId, firstPlayer.PlayerId, "First", system.SystemId, now);
        var secondEmpire = AddEmpire(state, cycle.CycleId, secondPlayer.PlayerId, "Second", Guid.NewGuid(), now);

        AddResources(state, firstEmpire.EmpireId, now);
        AddResources(state, secondEmpire.EmpireId, now);
        AddFleet(state, cycle.CycleId, firstEmpire.EmpireId, system.SystemId, attackerShips, now);
        AddFleet(state, cycle.CycleId, secondEmpire.EmpireId, system.SystemId, defenderShips, now);
        return state;
    }

    public static GameState CreateMovementState(bool linkSystems)
    {
        var now = DateTimeOffset.UtcNow;
        var state = new GameState();
        var cycle = AddCycle(state, now);
        var player = AddPlayer(state, "mover", now);
        var origin = AddSystem(state, cycle.CycleId, "Origin", 10, 10, 10, 10, 0, now);
        var destination = AddSystem(state, cycle.CycleId, "Destination", 10, 10, 10, 10, 0, now);
        var empire = AddEmpire(state, cycle.CycleId, player.PlayerId, "Mover", origin.SystemId, now);

        AddResources(state, empire.EmpireId, now);
        AddFleet(state, cycle.CycleId, empire.EmpireId, origin.SystemId, 25, now);

        if (linkSystems)
        {
            state.SystemLinks.Add(new SystemLink
            {
                CycleId = cycle.CycleId,
                SystemAId = origin.SystemId,
                SystemBId = destination.SystemId,
                Distance = 1,
                TravelTicks = 1
            });
        }

        return state;
    }

    private static Cycle AddCycle(GameState state, DateTimeOffset now)
    {
        var cycle = new Cycle
        {
            Name = "Test Cycle",
            StartAt = now,
            EndAt = now.AddDays(90),
            TickLengthMinutes = 60,
            Status = CycleStatus.Active,
            CreatedAt = now
        };
        state.Cycles.Add(cycle);
        return cycle;
    }

    private static Player AddPlayer(GameState state, string username, DateTimeOffset now)
    {
        var player = new Player
        {
            Username = username,
            CreatedAt = now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
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
        int historicalSignificance,
        DateTimeOffset now)
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
            CreatedAt = now
        };
        state.Systems.Add(system);
        return system;
    }

    private static Empire AddEmpire(GameState state, Guid cycleId, Guid playerId, string name, Guid homeSystemId, DateTimeOffset now)
    {
        var empire = new Empire
        {
            CycleId = cycleId,
            PlayerId = playerId,
            EmpireName = name,
            HomeSystemId = homeSystemId,
            CreatedAt = now,
            Status = EmpireStatus.Active
        };
        state.Empires.Add(empire);
        return empire;
    }

    private static void AddResources(GameState state, Guid empireId, DateTimeOffset now)
    {
        state.EmpireResources.Add(new EmpireResource
        {
            EmpireId = empireId,
            UpdatedAt = now
        });
        state.EmpirePriorities.Add(new EmpirePriority
        {
            EmpireId = empireId,
            UpdatedAt = now
        });
    }

    private static void AddFleet(GameState state, Guid cycleId, Guid empireId, Guid systemId, int shipCount, DateTimeOffset now)
    {
        state.Fleets.Add(new Fleet
        {
            CycleId = cycleId,
            EmpireId = empireId,
            FleetName = $"{shipCount} ships",
            CurrentSystemId = systemId,
            ShipCount = shipCount,
            Status = FleetStatus.Active,
            CreatedAt = now
        });
    }
}
