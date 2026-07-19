using Cycles.Core;

namespace Cycles.Tests;

public sealed class TurnForecastTests
{
    [Fact]
    public void ForecastUsesTheSameIncomeAndMilitaryProgrammeCalculationsAsResolution()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var resources = Assert.Single(state.EmpireResources);
        resources.Industry = 50;
        resources.Research = 170;
        var priorities = Assert.Single(state.EmpirePriorities);
        priorities.MilitaryWeight = 50;
        priorities.ExpansionWeight = 50;

        var forecast = TurnForecastCalculator.Calculate(state, cycle.CycleId, empire.EmpireId);

        Assert.Equal(new ResourceDelta(80, 40, 20), forecast.ExpectedIncome);
        Assert.Equal(50, forecast.AutomaticMilitaryProgramme.MilitaryWeight);
        Assert.Equal(50, forecast.AutomaticMilitaryProgramme.IndustrySpent);
        Assert.Equal(2, forecast.AutomaticMilitaryProgramme.ShipCount);
        Assert.Equal(4, forecast.AutomaticMilitaryProgramme.DeliveryTick);
        Assert.True(forecast.SurveyProjectionExpectedNextWindow);

        new TickEngine().RunTick(state, cycle.CycleId, TestState.Now.AddHours(1));

        var resolvedResources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
        Assert.Equal(forecast.ExpectedIncome.Industry, resolvedResources.LastGeneratedIndustry);
        Assert.Equal(forecast.ExpectedIncome.Research, resolvedResources.LastGeneratedResearch);
        Assert.Equal(forecast.ExpectedIncome.Population, resolvedResources.LastGeneratedPopulation);
        Assert.Equal(forecast.AutomaticMilitaryProgramme.IndustrySpent, resolvedResources.LastSpentIndustry);
        var construction = Assert.Single(state.ShipConstructions);
        Assert.Equal(forecast.AutomaticMilitaryProgramme.ShipCount, construction.ShipCount);
        Assert.Equal(forecast.AutomaticMilitaryProgramme.DeliveryTick, construction.CompleteAfterTick);
        Assert.Contains(state.EmpireDoctrineUnlocks, item =>
            item.CycleId == cycle.CycleId
            && item.EmpireId == empire.EmpireId
            && item.DoctrineKey == EconomyProcessor.SurveyProjectionDoctrineKey);
    }

    [Fact]
    public void ForecastProjectsTheCompleteEligibleColonisationReservationSet()
    {
        var state = TestState.CreateColonisationContentionState(currentTurnPopulationOutput: 100);
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        foreach (var fleet in state.Fleets)
        {
            OrderService.SubmitColoniseOrder(state, fleet.FleetId, TestState.Now);
        }

        var forecast = TurnForecastCalculator.Calculate(state, cycle.CycleId, empire.EmpireId);

        Assert.Equal(2, forecast.ColonisationReservation.OrderCount);
        Assert.Equal(200, forecast.ColonisationReservation.PopulationRequired);
        Assert.Equal(200, forecast.ColonisationReservation.AvailablePopulationAfterIncome);
        Assert.True(forecast.ColonisationReservation.IsFullyFunded);
    }

    [Fact]
    public void ForecastAggregatesQueuedDeliveriesAndExcludesAnotherCycle()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        state.ShipConstructions.AddRange(
        [
            QueuedConstruction(cycle.CycleId, empire.EmpireId, deliveryTick: 3, shipCount: 2),
            QueuedConstruction(cycle.CycleId, empire.EmpireId, deliveryTick: 3, shipCount: 4)
        ]);

        var foreign = TestState.CreateSingleEmpireState();
        var foreignCycle = foreign.GetActiveCycle()!;
        var foreignEmpire = Assert.Single(foreign.Empires);
        foreign.Systems.Single().IndustryOutput = 9_999;
        foreign.ShipConstructions.Add(QueuedConstruction(
            foreignCycle.CycleId,
            foreignEmpire.EmpireId,
            deliveryTick: 2,
            shipCount: 99));
        MergeForecastState(state, foreign);

        var forecast = TurnForecastCalculator.Calculate(state, cycle.CycleId, empire.EmpireId);

        Assert.Equal(80, forecast.ExpectedIncome.Industry);
        var delivery = Assert.Single(forecast.ScheduledDeliveries);
        Assert.Equal(3, delivery.DeliveryTick);
        Assert.Equal(6, delivery.ShipCount);
        Assert.Equal(150, delivery.IndustryCommitted);
        Assert.DoesNotContain(forecast.ScheduledDeliveries, item => item.ShipCount == 99);
    }

    private static ShipConstruction QueuedConstruction(
        Guid cycleId,
        Guid empireId,
        int deliveryTick,
        int shipCount) =>
        new()
        {
            CycleId = cycleId,
            EmpireId = empireId,
            ShipCount = shipCount,
            IndustrySpent = shipCount * EconomyProcessor.ShipIndustryCost,
            StartedTick = 0,
            CompleteAfterTick = deliveryTick,
            Status = ShipConstructionStatus.Queued,
            CreatedAt = TestState.Now,
            UpdatedAt = TestState.Now
        };

    private static void MergeForecastState(GameState target, GameState source)
    {
        target.Cycles.AddRange(source.Cycles);
        target.Players.AddRange(source.Players);
        target.Empires.AddRange(source.Empires);
        target.Factions.AddRange(source.Factions);
        target.MatchParticipants.AddRange(source.MatchParticipants);
        target.EmpireResources.AddRange(source.EmpireResources);
        target.EmpirePriorities.AddRange(source.EmpirePriorities);
        target.Systems.AddRange(source.Systems);
        target.Fleets.AddRange(source.Fleets);
        target.ShipConstructions.AddRange(source.ShipConstructions);
    }
}
