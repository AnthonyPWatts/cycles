namespace Cycles.Core;

public static class TurnForecastCalculator
{
    public static EmpireTurnForecast Calculate(GameState state, Guid cycleId, Guid empireId)
    {
        ArgumentNullException.ThrowIfNull(state);

        var cycle = state.Cycles.SingleOrDefault(item => item.CycleId == cycleId)
            ?? throw new InvalidOperationException("Cycle was not found.");
        _ = state.Empires.SingleOrDefault(item => item.CycleId == cycleId && item.EmpireId == empireId)
            ?? throw new InvalidOperationException("Empire was not found in the requested Cycle.");
        var resources = state.EmpireResources.Single(item => item.EmpireId == empireId);
        var priorities = state.EmpirePriorities.Single(item => item.EmpireId == empireId);
        StrategicPriorityPolicy.Validate(priorities);

        var nextTickNumber = cycle.CurrentTickNumber + 1;
        var expectedIncome = InfluenceCalculator.CalculateEmpireResourceGeneration(state, cycleId, empireId);
        var availableIndustryAfterIncome = Math.Max(0, resources.Industry + expectedIncome.Industry);
        var militaryProgramme = EconomyProcessor.ProjectMilitaryProgramme(
            availableIndustryAfterIncome,
            priorities.MilitaryWeight,
            nextTickNumber);

        var fleetsById = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId)
            .ToDictionary(fleet => fleet.FleetId);
        var eligibleColonisationOrders = state.FleetOrders
            .Where(order => order.CycleId == cycleId
                            && order.Status == FleetOrderStatus.Pending
                            && order.ExecuteAfterTick <= nextTickNumber
                            && order.OrderType == FleetOrderType.Colonise
                            && fleetsById.TryGetValue(order.FleetId, out var fleet)
                            && fleet.EmpireId == empireId
                            && OrderService.IsColonisationEligibleAtClosure(state, order, fleetsById))
            .ToArray();
        var availablePopulationAfterIncome = Math.Max(0, resources.Population + expectedIncome.Population);
        var requiredPopulation = eligibleColonisationOrders.Length * OrderService.ColonisationPopulationCost;
        var colonisationReservation = new ColonisationReservationForecast(
            eligibleColonisationOrders.Length,
            requiredPopulation,
            availablePopulationAfterIncome,
            availablePopulationAfterIncome >= requiredPopulation);

        var scheduledDeliveries = state.ShipConstructions
            .Where(item => item.CycleId == cycleId
                           && item.EmpireId == empireId
                           && item.Status == ShipConstructionStatus.Queued)
            .GroupBy(item => item.CompleteAfterTick)
            .OrderBy(group => group.Key)
            .Select(group => new ScheduledConstructionDeliveryForecast(
                group.Key,
                group.Sum(item => item.ShipCount),
                group.Sum(item => item.IndustrySpent)))
            .ToArray();

        var projectedResearchAfterIncome = Math.Max(0, resources.Research + expectedIncome.Research);
        var surveyProjectionExpectedNextWindow = !EconomyProcessor.HasSurveyProjectionDoctrine(state, cycleId, empireId)
                                                 && projectedResearchAfterIncome >= EconomyProcessor.SurveyProjectionResearchThreshold;

        return new EmpireTurnForecast(
            cycleId,
            empireId,
            nextTickNumber,
            expectedIncome,
            colonisationReservation,
            militaryProgramme,
            scheduledDeliveries,
            surveyProjectionExpectedNextWindow);
    }
}

public sealed record EmpireTurnForecast(
    Guid CycleId,
    Guid EmpireId,
    int NextTickNumber,
    ResourceDelta ExpectedIncome,
    ColonisationReservationForecast ColonisationReservation,
    MilitaryProgrammeProjection AutomaticMilitaryProgramme,
    IReadOnlyCollection<ScheduledConstructionDeliveryForecast> ScheduledDeliveries,
    bool SurveyProjectionExpectedNextWindow);

public readonly record struct ColonisationReservationForecast(
    int OrderCount,
    decimal PopulationRequired,
    decimal AvailablePopulationAfterIncome,
    bool IsFullyFunded);

public readonly record struct ScheduledConstructionDeliveryForecast(
    int DeliveryTick,
    int ShipCount,
    decimal IndustryCommitted);
