using Cycles.Core;

namespace Cycles.Tests;

public sealed class EmpireMetricTests
{
    [Fact]
    public void MapControlPercentUsesProportionalEffectivePresence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 70, defenderShips: 30);
        var cycle = state.GetActiveCycle()!;
        SetExpansionWeight(state, 0);

        var metrics = EmpireMetricCalculator.CreateTickMetrics(state, cycle.CycleId, 4, TestState.Now);

        Assert.Equal(2, metrics.Count);
        Assert.Equal(state.Empires[0].EmpireId, metrics[0].EmpireId);
        Assert.True(metrics[0].IsWinner);
        Assert.Equal(1, metrics[0].Rank);
        Assert.Equal(70m, metrics[0].MapControlPercent);
        Assert.Equal(70m, metrics[0].TotalEffectivePresence);
        Assert.Equal(70, metrics[0].ActiveShipCount);

        Assert.Equal(state.Empires[1].EmpireId, metrics[1].EmpireId);
        Assert.False(metrics[1].IsWinner);
        Assert.Equal(2, metrics[1].Rank);
        Assert.Equal(30m, metrics[1].MapControlPercent);
        Assert.Equal(30m, metrics[1].TotalEffectivePresence);
        Assert.Equal(30, metrics[1].ActiveShipCount);
    }

    [Fact]
    public void EmptySystemsRemainInMapControlDenominator()
    {
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle()!;
        SetExpansionWeight(state, 0);

        var metric = Assert.Single(EmpireMetricCalculator.CreateTickMetrics(state, cycle.CycleId, 2, TestState.Now));

        Assert.Equal(50m, metric.MapControlPercent);
        Assert.Equal(25m, metric.TotalEffectivePresence);
        Assert.Equal(25, metric.ActiveShipCount);
    }

    [Fact]
    public void RankingUsesActiveShipsAfterMapControlAndPresenceTies()
    {
        var state = CreateTwoHomeSystemTie();
        var cycle = state.GetActiveCycle()!;

        var metrics = EmpireMetricCalculator.CreateTickMetrics(state, cycle.CycleId, 1, TestState.Now);

        Assert.Equal(state.Empires[0].EmpireId, metrics[0].EmpireId);
        Assert.Equal(state.Empires[1].EmpireId, metrics[1].EmpireId);
        Assert.Equal(50m, metrics[0].MapControlPercent);
        Assert.Equal(50m, metrics[1].MapControlPercent);
        Assert.Equal(10m, metrics[0].TotalEffectivePresence);
        Assert.Equal(10m, metrics[1].TotalEffectivePresence);
        Assert.Equal(10, metrics[0].ActiveShipCount);
        Assert.Equal(1, metrics[1].ActiveShipCount);
    }

    [Fact]
    public void CompletedTickRecordsEmpireMetricSnapshots()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 70, defenderShips: 30);
        var cycle = state.GetActiveCycle()!;
        SetExpansionWeight(state, 0);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var metrics = state.EmpireMetrics.OrderBy(metric => metric.Rank).ToList();
        Assert.Equal(2, metrics.Count);
        Assert.All(metrics, metric => Assert.Equal(1, metric.TickNumber));
        Assert.Equal(state.Empires[0].EmpireId, metrics[0].EmpireId);
        Assert.Equal(70m, metrics[0].MapControlPercent);
        Assert.True(metrics[0].IsWinner);
    }

    private static GameState CreateTwoHomeSystemTie()
    {
        var state = new GameState();
        var cycle = new Cycle
        {
            Name = "Metric Tie",
            StartAt = TestState.Now,
            EndAt = TestState.Now.AddDays(90),
            TickLengthMinutes = 60,
            Status = CycleStatus.Active,
            CreatedAt = TestState.Now
        };
        state.Cycles.Add(cycle);

        var firstPlayer = AddPlayer(state, "first");
        var secondPlayer = AddPlayer(state, "second");
        var firstSystem = AddSystem(state, cycle.CycleId, "First Home");
        var secondSystem = AddSystem(state, cycle.CycleId, "Second Home");
        var firstEmpire = AddEmpire(state, cycle.CycleId, firstPlayer.PlayerId, "First", firstSystem.SystemId);
        var secondEmpire = AddEmpire(state, cycle.CycleId, secondPlayer.PlayerId, "Second", secondSystem.SystemId);

        AddPriority(state, firstEmpire.EmpireId);
        AddPriority(state, secondEmpire.EmpireId);
        AddFleet(state, cycle.CycleId, firstEmpire.EmpireId, firstSystem.SystemId, 10);
        AddFleet(state, cycle.CycleId, secondEmpire.EmpireId, secondSystem.SystemId, 1);

        return state;
    }

    private static Player AddPlayer(GameState state, string username)
    {
        var player = new Player
        {
            Username = username,
            CreatedAt = TestState.Now,
            Status = PlayerStatus.Active
        };
        state.Players.Add(player);
        return player;
    }

    private static GalaxySystem AddSystem(GameState state, Guid cycleId, string name)
    {
        var system = new GalaxySystem
        {
            CycleId = cycleId,
            SystemName = name,
            CreatedAt = TestState.Now
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
            CreatedAt = TestState.Now,
            Status = EmpireStatus.Active
        };
        state.Empires.Add(empire);
        return empire;
    }

    private static void AddPriority(GameState state, Guid empireId) =>
        state.EmpirePriorities.Add(new EmpirePriority
        {
            EmpireId = empireId,
            IndustryWeight = 100,
            ResearchWeight = 0,
            MilitaryWeight = 0,
            ExpansionWeight = 0,
            UpdatedAt = TestState.Now
        });

    private static void AddFleet(GameState state, Guid cycleId, Guid empireId, Guid systemId, int shipCount) =>
        state.Fleets.Add(new Fleet
        {
            CycleId = cycleId,
            EmpireId = empireId,
            FleetName = $"{shipCount} ships",
            CurrentSystemId = systemId,
            ShipCount = shipCount,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        });

    private static void SetExpansionWeight(GameState state, int expansionWeight)
    {
        foreach (var priority in state.EmpirePriorities)
        {
            priority.IndustryWeight = 100 - expansionWeight;
            priority.ResearchWeight = 0;
            priority.MilitaryWeight = 0;
            priority.ExpansionWeight = expansionWeight;
        }
    }
}
