using Cycles.Core;

namespace Cycles.Tests;

public sealed class InfluenceTests
{
    [Fact]
    public void PresenceSplitsResourcesProportionally()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 70, defenderShips: 30);
        var cycle = state.GetActiveCycle()!;

        InfluenceCalculator.GenerateResources(state, cycle.CycleId, 1, TestState.Now);

        var first = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[0].EmpireId);
        var second = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[1].EmpireId);

        Assert.Equal(70m, first.Industry);
        Assert.Equal(30m, second.Industry);
        Assert.Equal(70m, first.Research);
        Assert.Equal(30m, second.Research);
        Assert.Equal(70m, first.LastGeneratedIndustry);
        Assert.Equal(30m, second.LastGeneratedIndustry);
        Assert.Equal(70m, first.LastGeneratedResearch);
        Assert.Equal(30m, second.LastGeneratedResearch);
    }

    [Fact]
    public void SingleEmpireReceivesFullSystemOutput()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;

        InfluenceCalculator.GenerateResources(state, cycle.CycleId, 1, TestState.Now);

        var resources = Assert.Single(state.EmpireResources);
        Assert.Equal(80m, resources.Industry);
        Assert.Equal(40m, resources.Research);
        Assert.Equal(20m, resources.Population);
        Assert.Equal(80m, resources.LastGeneratedIndustry);
        Assert.Equal(40m, resources.LastGeneratedResearch);
        Assert.Equal(20m, resources.LastGeneratedPopulation);
    }

    [Fact]
    public void MultipleActiveFleetsFromOneEmpireAggregatePresence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 40, defenderShips: 40);
        SetExpansionWeight(state, 0);
        var cycle = state.GetActiveCycle()!;
        var firstFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        state.Fleets.Add(new Fleet
        {
            CycleId = cycle.CycleId,
            EmpireId = firstFleet.EmpireId,
            FleetName = "Reinforcement",
            CurrentSystemId = firstFleet.CurrentSystemId,
            ShipCount = 40,
            Status = FleetStatus.Active,
            CreatedAt = TestState.Now
        });

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, firstFleet.CurrentSystemId);

        Assert.Equal(80m, presence[firstFleet.EmpireId]);
        Assert.Equal(40m, presence[state.Empires[1].EmpireId]);
    }

    [Fact]
    public void DestroyedAndInTransitFleetsDoNotContributePresence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 40, defenderShips: 40);
        var cycle = state.GetActiveCycle()!;
        var firstFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[0].EmpireId);
        var secondFleet = state.Fleets.Single(fleet => fleet.EmpireId == state.Empires[1].EmpireId);
        foreach (var empire in state.Empires)
        {
            empire.HomeSystemId = Guid.NewGuid();
        }

        firstFleet.Status = FleetStatus.Destroyed;
        secondFleet.Status = FleetStatus.InTransit;

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, firstFleet.CurrentSystemId);

        Assert.Empty(presence);
    }

    [Fact]
    public void HomeSystemMinimumPresenceAppliesOnlyToHomeEmpire()
    {
        var state = TestState.CreateSingleEmpireState(includeFleet: false);
        SetExpansionWeight(state, 0);
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var system = Assert.Single(state.Systems);

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);

        Assert.Equal(InfluenceCalculator.HomeSystemMinimumPresence, presence[empire.EmpireId]);
    }

    [Fact]
    public void ExpansionPriorityProjectsFleetPresence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 50);
        var cycle = state.GetActiveCycle()!;

        SetExpansionWeight(state.EmpirePriorities[0], 100);
        SetExpansionWeight(state.EmpirePriorities[1], 0);

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, state.Systems[0].SystemId);

        Assert.Equal(100m, presence[state.Empires[0].EmpireId]);
        Assert.Equal(50m, presence[state.Empires[1].EmpireId]);
    }

    [Fact]
    public void ExpansionProjectionChangesResourceShare()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 50);
        var cycle = state.GetActiveCycle()!;

        SetExpansionWeight(state.EmpirePriorities[0], 100);
        SetExpansionWeight(state.EmpirePriorities[1], 0);

        InfluenceCalculator.GenerateResources(state, cycle.CycleId, 1, TestState.Now);

        var first = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[0].EmpireId);
        var second = state.EmpireResources.Single(resource => resource.EmpireId == state.Empires[1].EmpireId);

        Assert.Equal(66.67m, first.Industry);
        Assert.Equal(33.33m, second.Industry);
        Assert.Equal(66.67m, first.LastGeneratedIndustry);
        Assert.Equal(33.33m, second.LastGeneratedIndustry);
    }

    [Fact]
    public void SurveyProjectionDoctrineIncreasesEffectivePresence()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 50);
        var cycle = state.GetActiveCycle()!;
        SetExpansionWeight(state, 0);
        var firstEmpire = state.Empires[0];
        var firstResources = state.EmpireResources.Single(resource => resource.EmpireId == firstEmpire.EmpireId);
        firstResources.Research = EconomyProcessor.SurveyProjectionResearchThreshold;
        EconomyProcessor.ApplyResearchUnlocks(state, cycle.CycleId, tickNumber: 1, TestState.Now);

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, state.Systems[0].SystemId);

        Assert.Equal(55m, presence[firstEmpire.EmpireId]);
        Assert.Equal(50m, presence[state.Empires[1].EmpireId]);
    }

    private static void SetExpansionWeight(GameState state, int expansionWeight)
    {
        foreach (var priority in state.EmpirePriorities)
        {
            SetExpansionWeight(priority, expansionWeight);
        }
    }

    private static void SetExpansionWeight(EmpirePriority priority, int expansionWeight)
    {
        priority.IndustryWeight = 100 - expansionWeight;
        priority.ResearchWeight = 0;
        priority.MilitaryWeight = 0;
        priority.ExpansionWeight = expansionWeight;
    }
}
