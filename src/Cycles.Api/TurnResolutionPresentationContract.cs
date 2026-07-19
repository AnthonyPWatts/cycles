using Cycles.Core;

internal static class TurnResolutionPresentationContract
{
    private static readonly IReadOnlyCollection<TurnResolutionPhaseResponse> PhaseResponses =
    [
        new(1, TurnResolutionPhase.ResourceIncome, "Resource income", "Active phase-start presence generates resources; this income can fund programme spending in the same turn."),
        new(2, TurnResolutionPhase.DueConstruction, "Due construction", "Ships due now arrive at home and may defend, but they generated no income and cannot inherit a sealed command."),
        new(3, TurnResolutionPhase.ProgrammeSpending, "Programme spending", "Committed priorities spend the post-income stockpile and start construction; a new build gains no immediate progress."),
        new(4, TurnResolutionPhase.RecallArrivalsAndMovement, "Recall, arrivals, movement, and Holds", "Recall reverses outbound fleets before passive arrival; new movement and explicit or implicit Holds then fix every position before combat."),
        new(5, TurnResolutionPhase.Combat, "Combat", "Attacks use the shared post-movement world. Submission time does not make one fleet fight first."),
        new(6, TurnResolutionPhase.Colonisation, "Colonisation", "An admitted colonising fleet must survive combat and remain eligible before its reserved Population is spent."),
        new(7, TurnResolutionPhase.DerivedState, "Derived state", "Control and metrics describe the world after movement, combat, and colonisation."),
        new(8, TurnResolutionPhase.NextWindowProgression, "Next-window progression", "Research unlocked here begins to affect play in the next command window."),
        new(9, TurnResolutionPhase.Publication, "Publication", "The server commits one complete result and then reopens commands; event display order grants no priority.")
    ];

    public static TurnResolutionPresentationResponse Create(GameState state, Cycle cycle, Empire empire)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(cycle);
        ArgumentNullException.ThrowIfNull(empire);

        if (empire.CycleId != cycle.CycleId)
        {
            throw new InvalidOperationException("Turn resolution presentation requires an Empire from the requested Cycle.");
        }

        var forecast = TurnForecastCalculator.Calculate(state, cycle.CycleId, empire.EmpireId);
        var playerFleetIds = state.Fleets
            .Where(item => item.CycleId == cycle.CycleId && item.EmpireId == empire.EmpireId)
            .Select(item => item.FleetId)
            .ToHashSet();
        var pendingOrders = state.FleetOrders
            .Where(item => item.CycleId == cycle.CycleId && item.Status == FleetOrderStatus.Pending)
            .ToArray();
        var dueOrderFleetIds = pendingOrders
            .Where(item => item.ExecuteAfterTick <= forecast.NextTickNumber)
            .Select(item => item.FleetId);
        var activeFleetIds = state.Fleets
            .Where(item => item.CycleId == cycle.CycleId
                           && item.Status == FleetStatus.Active
                           && item.ShipCount > 0)
            .Select(item => item.FleetId);
        var (stageLabel, stageDescription, commandsAccepted) = DescribeStage(cycle.TurnStage);
        var expectedIncome = new ResourceProjectionResponse(
            forecast.ExpectedIncome.Industry,
            forecast.ExpectedIncome.Research,
            forecast.ExpectedIncome.Population);
        var scheduledDeliveries = forecast.ScheduledDeliveries
            .Select(item => new ScheduledConstructionDeliveryResponse(
                item.DeliveryTick,
                item.ShipCount,
                item.IndustryCommitted))
            .ToArray();
        var automaticMilitaryProgramme = forecast.AutomaticMilitaryProgramme;
        var hasOngoingJourney = state.Fleets.Any(item => item.CycleId == cycle.CycleId
                                                         && item.EmpireId == empire.EmpireId
                                                         && item.Status == FleetStatus.InTransit
                                                         && item.ShipCount > 0);
        var hasScheduledEffects = expectedIncome.Industry != 0
                                  || expectedIncome.Research != 0
                                  || expectedIncome.Population != 0
                                  || automaticMilitaryProgramme.ShipCount > 0
                                  || scheduledDeliveries.Length > 0
                                  || hasOngoingJourney
                                  || forecast.SurveyProjectionExpectedNextWindow;

        return new TurnResolutionPresentationResponse(
            cycle.CycleId,
            empire.EmpireId,
            cycle.CurrentTickNumber,
            cycle.TurnStage,
            stageLabel,
            stageDescription,
            commandsAccepted,
            forecast.NextTickNumber,
            SubmissionTimeGrantsInitiative: false,
            pendingOrders.Count(item => playerFleetIds.Contains(item.FleetId)),
            pendingOrders.Count(item => item.CommandSource == FleetOrderCommandSource.Human),
            activeFleetIds.Concat(dueOrderFleetIds).Distinct().Count(),
            new TurnForecastResponse(
                expectedIncome,
                new ColonisationReservationProjectionResponse(
                    forecast.ColonisationReservation.OrderCount,
                    forecast.ColonisationReservation.PopulationRequired,
                    forecast.ColonisationReservation.AvailablePopulationAfterIncome,
                    forecast.ColonisationReservation.IsFullyFunded),
                new AutomaticMilitaryProgrammeProjectionResponse(
                    automaticMilitaryProgramme.MilitaryWeight,
                    automaticMilitaryProgramme.IndustrySpent,
                    automaticMilitaryProgramme.ShipCount,
                    automaticMilitaryProgramme.ShipCount > 0
                        ? automaticMilitaryProgramme.DeliveryTick
                        : null),
                scheduledDeliveries,
                forecast.SurveyProjectionExpectedNextWindow,
                hasScheduledEffects),
            PhaseResponses);
    }

    public static (TurnResolutionPhase? Phase, int? Order) GetEventPhase(EventType eventType) =>
        eventType switch
        {
            EventType.ResourcesGenerated => (TurnResolutionPhase.ResourceIncome, 1),
            EventType.ShipConstructionCompleted => (TurnResolutionPhase.DueConstruction, 2),
            EventType.ShipConstructionQueued => (TurnResolutionPhase.ProgrammeSpending, 3),
            EventType.FleetMoved or EventType.FleetRecalled or EventType.FleetReturned
                or EventType.FleetArrived or EventType.FleetHeld =>
                (TurnResolutionPhase.RecallArrivalsAndMovement, 4),
            EventType.CombatResolved or EventType.DiplomaticAggression
                or EventType.TreatyCancelledByAggression or EventType.AdmiralBattleReported =>
                (TurnResolutionPhase.Combat, 5),
            EventType.ColonialOutpostEstablished => (TurnResolutionPhase.Colonisation, 6),
            EventType.DoctrineUnlocked => (TurnResolutionPhase.NextWindowProgression, 8),
            EventType.ChronicleCreated => (TurnResolutionPhase.Combat, 5),
            _ => (null, null)
        };

    private static (string Label, string Description, bool CommandsAccepted) DescribeStage(
        TurnResolutionStage stage) =>
        stage switch
        {
            TurnResolutionStage.CommandOpen => (
                "Command window open",
                "Human intentions, cancellations, replacements, and priority changes may be accepted.",
                true),
            TurnResolutionStage.Closing => (
                "Command window closing",
                "Human commands are closed while internal planners and Colonise reservation checks complete.",
                false),
            TurnResolutionStage.Sealed => (
                "Turn ledger sealed",
                "The complete ledger is immutable and no command source may append or replace an intention.",
                false),
            TurnResolutionStage.Resolving => (
                "Turn resolving",
                "The server is processing the sealed ledger through the authoritative gameplay phases.",
                false),
            TurnResolutionStage.Publishing => (
                "Publishing result",
                "Outcomes are complete and the server is committing facts before the next command window.",
                false),
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, "Unsupported turn resolution stage.")
        };
}

public enum TurnResolutionPhase
{
    ResourceIncome,
    DueConstruction,
    ProgrammeSpending,
    RecallArrivalsAndMovement,
    Combat,
    Colonisation,
    DerivedState,
    NextWindowProgression,
    Publication
}

public sealed record TurnResolutionPresentationResponse(
    Guid CycleId,
    Guid EmpireId,
    int CurrentTickNumber,
    TurnResolutionStage Stage,
    string StageLabel,
    string StageDescription,
    bool CommandsAccepted,
    int NextTickNumber,
    bool SubmissionTimeGrantsInitiative,
    int PlayerPendingOrderCount,
    int GamePendingHumanOrderCount,
    int GameFleetIntentionCount,
    TurnForecastResponse Forecast,
    IReadOnlyCollection<TurnResolutionPhaseResponse> Phases);

public sealed record TurnForecastResponse(
    ResourceProjectionResponse ExpectedIncome,
    ColonisationReservationProjectionResponse ColonisationReservation,
    AutomaticMilitaryProgrammeProjectionResponse AutomaticMilitaryProgramme,
    IReadOnlyCollection<ScheduledConstructionDeliveryResponse> ScheduledDeliveries,
    bool SurveyProjectionExpectedNextWindow,
    bool HasScheduledEffects);

public sealed record ResourceProjectionResponse(
    decimal Industry,
    decimal Research,
    decimal Population);

public sealed record ColonisationReservationProjectionResponse(
    int OrderCount,
    decimal PopulationRequired,
    decimal AvailablePopulationAfterIncome,
    bool IsFullyFunded);

public sealed record AutomaticMilitaryProgrammeProjectionResponse(
    int MilitaryWeight,
    decimal ProjectedIndustrySpend,
    int ProjectedShipCount,
    int? ProjectedDeliveryTick);

public sealed record ScheduledConstructionDeliveryResponse(
    int DeliveryTick,
    int ShipCount,
    decimal IndustryCommitted);

public sealed record TurnResolutionPhaseResponse(
    int Order,
    TurnResolutionPhase Phase,
    string Title,
    string Consequence);
