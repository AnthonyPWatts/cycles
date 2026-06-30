using Cycles.Core;

namespace Cycles.Tests;

public sealed class EconomyTests
{
    [Fact]
    public void PriorityWeightsMustTotalOneHundred()
    {
        var state = TestState.CreateSingleEmpireState();
        var empire = Assert.Single(state.Empires);

        var ex = Assert.Throws<InvalidOperationException>(() => OrderService.UpdatePriorities(
            state,
            empire.EmpireId,
            industryWeight: 25,
            researchWeight: 25,
            militaryWeight: 25,
            expansionWeight: 10,
            TestState.Now));

        Assert.Contains("total 100", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MilitaryPrioritySpendsIndustryIntoQueuedShipConstruction()
    {
        var state = CreateShipBuildingState(industry: 100);
        var cycle = state.GetActiveCycle()!;
        var initialShips = Assert.Single(state.Fleets).ShipCount;

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(TickLogStatus.Completed, result.Status);
        var resources = Assert.Single(state.EmpireResources);
        Assert.Equal(0m, resources.Industry);
        Assert.Equal(100m, resources.LastSpentIndustry);
        Assert.Equal(0m, resources.LastSpentResearch);
        Assert.Equal(0m, resources.LastSpentPopulation);

        var construction = Assert.Single(state.ShipConstructions);
        Assert.Equal(ShipConstructionStatus.Queued, construction.Status);
        Assert.Equal(4, construction.ShipCount);
        Assert.Equal(100m, construction.IndustrySpent);
        Assert.Equal(1, construction.StartedTick);
        Assert.Equal(1 + EconomyProcessor.ShipBuildDelayTicks, construction.CompleteAfterTick);
        Assert.Equal(initialShips, Assert.Single(state.Fleets).ShipCount);
        Assert.Contains(state.Events, item => item.EventType == EventType.ShipConstructionQueued);
    }

    [Fact]
    public void QueuedShipsCompleteIntoTheHomeFleetAfterTheBuildDelay()
    {
        var state = CreateShipBuildingState(industry: 100);
        var cycle = state.GetActiveCycle()!;
        var initialShips = Assert.Single(state.Fleets).ShipCount;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.Equal(initialShips, Assert.Single(state.Fleets).ShipCount);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var construction = Assert.Single(state.ShipConstructions);
        Assert.Equal(ShipConstructionStatus.Completed, construction.Status);
        Assert.Equal(4, construction.CompletedTick);
        Assert.Equal(initialShips + 4, Assert.Single(state.Fleets).ShipCount);
        Assert.Contains(state.Events, item => item.EventType == EventType.ShipConstructionCompleted);
    }

    [Fact]
    public void InsufficientIndustryIsReservedAndNeverSpentNegative()
    {
        var state = CreateShipBuildingState(industry: EconomyProcessor.ShipIndustryCost - 1);
        var cycle = state.GetActiveCycle()!;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        var resources = Assert.Single(state.EmpireResources);
        Assert.Equal(EconomyProcessor.ShipIndustryCost - 1, resources.Industry);
        Assert.Equal(0m, resources.LastSpentIndustry);
        Assert.Empty(state.ShipConstructions);
    }

    [Fact]
    public void ResearchThresholdUnlocksSurveyProjectionDoctrineOnce()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var resources = Assert.Single(state.EmpireResources);
        resources.Research = EconomyProcessor.SurveyProjectionResearchThreshold;
        var system = Assert.Single(state.Systems);
        system.IndustryOutput = 0;
        system.ResearchOutput = 0;
        system.PopulationOutput = 0;

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);
        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var unlock = Assert.Single(state.Events, item => item.EventType == EventType.DoctrineUnlocked);
        Assert.Equal(resources.EmpireId, unlock.EmpireId);
        Assert.Contains(EconomyProcessor.SurveyProjectionDoctrineKey, unlock.FactJson, StringComparison.Ordinal);
        Assert.Contains(EconomyProcessor.SurveyProjectionResearchThreshold.ToString("0"), unlock.FactJson, StringComparison.Ordinal);
    }

    private static GameState CreateShipBuildingState(decimal industry)
    {
        var state = TestState.CreateSingleEmpireState();
        var system = Assert.Single(state.Systems);
        system.IndustryOutput = 0;
        system.ResearchOutput = 0;
        system.PopulationOutput = 0;

        var resources = Assert.Single(state.EmpireResources);
        resources.Industry = industry;
        resources.Research = 0;
        resources.Population = 0;

        var priorities = Assert.Single(state.EmpirePriorities);
        priorities.IndustryWeight = 0;
        priorities.ResearchWeight = 0;
        priorities.MilitaryWeight = 100;
        priorities.ExpansionWeight = 0;

        return state;
    }
}
