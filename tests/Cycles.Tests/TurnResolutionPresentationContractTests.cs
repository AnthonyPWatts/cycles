using Cycles.Core;

namespace Cycles.Tests;

public sealed class TurnResolutionPresentationContractTests
{
    [Fact]
    public void PresentationExposesTheCompleteOrderedContractAndCycleScopedAggregates()
    {
        var state = GameSeeder.CreateDevelopmentMatch(createdAt: TestState.Now);
        var cycle = state.GetActiveCycle()!;
        var tony = state.Players.Single(item => item.Username == "Tony");
        var tonyEmpire = state.RequireCommandableEmpire(cycle.CycleId, tony.PlayerId);
        var will = state.Players.Single(item => item.Username == "Will");
        var willEmpire = state.RequireCommandableEmpire(cycle.CycleId, will.PlayerId);
        OrderService.SubmitHoldOrder(
            state,
            state.Fleets.First(item => item.EmpireId == tonyEmpire.EmpireId).FleetId,
            TestState.Now);
        OrderService.SubmitHoldOrder(
            state,
            state.Fleets.First(item => item.EmpireId == willEmpire.EmpireId).FleetId,
            TestState.Now);

        var foreign = TestState.CreateSingleEmpireState();
        var foreignCycle = foreign.GetActiveCycle()!;
        var foreignFleet = Assert.Single(foreign.Fleets);
        OrderService.SubmitHoldOrder(foreign, foreignFleet.FleetId, TestState.Now);
        MergePresentationState(state, foreign);

        var response = TurnResolutionPresentationContract.Create(state, cycle, tonyEmpire);

        Assert.Equal(cycle.CycleId, response.CycleId);
        Assert.Equal(tonyEmpire.EmpireId, response.EmpireId);
        Assert.Equal(TurnResolutionStage.CommandOpen, response.Stage);
        Assert.True(response.CommandsAccepted);
        Assert.False(response.SubmissionTimeGrantsInitiative);
        Assert.Equal(1, response.PlayerPendingOrderCount);
        Assert.Equal(2, response.GamePendingHumanOrderCount);
        Assert.Equal(15, response.GameFleetIntentionCount);
        Assert.Equal(Enumerable.Range(1, 9), response.Phases.Select(item => item.Order));
        Assert.Equal(9, response.Phases.Select(item => item.Phase).Distinct().Count());
        Assert.Equal(
            "Recall, arrivals, movement, and Holds",
            response.Phases.Single(item => item.Order == 4).Title);
        Assert.Contains(
            "implicit Holds",
            response.Phases.Single(item => item.Order == 4).Consequence,
            StringComparison.Ordinal);
        Assert.DoesNotContain(response.Phases, item => string.IsNullOrWhiteSpace(item.Consequence));
        Assert.Equal(cycle.CurrentTickNumber + 1, response.NextTickNumber);
        Assert.True(response.Forecast.HasScheduledEffects);
        Assert.DoesNotContain(state.FleetOrders.Where(item => item.CycleId == foreignCycle.CycleId), item =>
            item.CycleId == response.CycleId);
    }

    [Theory]
    [InlineData(TurnResolutionStage.CommandOpen, true)]
    [InlineData(TurnResolutionStage.Closing, false)]
    [InlineData(TurnResolutionStage.Sealed, false)]
    [InlineData(TurnResolutionStage.Resolving, false)]
    [InlineData(TurnResolutionStage.Publishing, false)]
    public void StageMetadataDoesNotImplyCommandsRemainOpen(
        TurnResolutionStage stage,
        bool commandsAccepted)
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        cycle.TurnStage = stage;

        var response = TurnResolutionPresentationContract.Create(state, cycle, empire);

        Assert.Equal(stage, response.Stage);
        Assert.Equal(commandsAccepted, response.CommandsAccepted);
        Assert.False(string.IsNullOrWhiteSpace(response.StageLabel));
        Assert.False(string.IsNullOrWhiteSpace(response.StageDescription));
    }

    [Theory]
    [InlineData(EventType.ResourcesGenerated, TurnResolutionPhase.ResourceIncome, 1)]
    [InlineData(EventType.ShipConstructionCompleted, TurnResolutionPhase.DueConstruction, 2)]
    [InlineData(EventType.ShipConstructionQueued, TurnResolutionPhase.ProgrammeSpending, 3)]
    [InlineData(EventType.FleetRecalled, TurnResolutionPhase.RecallArrivalsAndMovement, 4)]
    [InlineData(EventType.CombatResolved, TurnResolutionPhase.Combat, 5)]
    [InlineData(EventType.ColonialOutpostEstablished, TurnResolutionPhase.Colonisation, 6)]
    [InlineData(EventType.DoctrineUnlocked, TurnResolutionPhase.NextWindowProgression, 8)]
    [InlineData(EventType.ChronicleCreated, TurnResolutionPhase.Combat, 5)]
    public void EventMetadataUsesServerOwnedPhaseOrdering(
        EventType eventType,
        TurnResolutionPhase expectedPhase,
        int expectedOrder)
    {
        var phase = TurnResolutionPresentationContract.GetEventPhase(eventType);

        Assert.Equal(expectedPhase, phase.Phase);
        Assert.Equal(expectedOrder, phase.Order);
    }

    [Theory]
    [InlineData(EventType.PrioritiesChanged)]
    [InlineData(EventType.OrderRejected)]
    public void EventsWithoutReliablePhaseProvenanceRemainUnphased(EventType eventType)
    {
        var phase = TurnResolutionPresentationContract.GetEventPhase(eventType);

        Assert.Null(phase.Phase);
        Assert.Null(phase.Order);
    }

    [Fact]
    public void OngoingJourneyRemainsAScheduledEffectAfterItsMoveOrderIsProcessed()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = Assert.Single(state.Empires);
        var system = Assert.Single(state.Systems);
        system.IndustryOutput = 0;
        system.ResearchOutput = 0;
        system.PopulationOutput = 0;
        var priorities = Assert.Single(state.EmpirePriorities);
        priorities.MilitaryWeight = 0;
        priorities.ExpansionWeight = 100;
        var fleet = Assert.Single(state.Fleets);

        var beforeJourney = TurnResolutionPresentationContract.Create(state, cycle, empire);
        Assert.False(beforeJourney.Forecast.HasScheduledEffects);

        fleet.Status = FleetStatus.InTransit;
        fleet.DestinationSystemId = Guid.NewGuid();
        fleet.DepartureTickNumber = cycle.CurrentTickNumber;
        fleet.ArrivalTickNumber = cycle.CurrentTickNumber + 2;

        var duringJourney = TurnResolutionPresentationContract.Create(state, cycle, empire);

        Assert.True(duringJourney.Forecast.HasScheduledEffects);
        Assert.Equal(0, duringJourney.PlayerPendingOrderCount);
    }

    private static void MergePresentationState(GameState target, GameState source)
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
        target.FleetOrders.AddRange(source.FleetOrders);
        target.ShipConstructions.AddRange(source.ShipConstructions);
    }
}
