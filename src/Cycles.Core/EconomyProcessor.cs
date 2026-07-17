using System.Text.Json;

namespace Cycles.Core;

public static class EconomyProcessor
{
    public const decimal ShipIndustryCost = 25m;
    public const int ShipBuildDelayTicks = 3;
    public const string SurveyProjectionDoctrineKey = "survey-projection";
    public const decimal SurveyProjectionResearchThreshold = 200m;
    public const decimal SurveyProjectionPresenceBonus = 0.10m;

    public static void CompleteShipConstruction(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var completing = state.ShipConstructions
            .Where(item => item.CycleId == cycleId
                           && item.Status == ShipConstructionStatus.Queued
                           && item.CompleteAfterTick <= tickNumber)
            .OrderBy(item => item.StartedTick)
            .ThenBy(item => item.CreatedAt)
            .ToList();

        foreach (var construction in completing)
        {
            var empire = state.Empires.Single(item => item.CycleId == cycleId && item.EmpireId == construction.EmpireId);
            var homeFleet = GetOrCreateHomeFleet(state, cycleId, empire, now);
            homeFleet.ShipCount += construction.ShipCount;
            homeFleet.Status = FleetStatus.Active;

            construction.Status = ShipConstructionStatus.Completed;
            construction.CompletedTick = tickNumber;
            construction.UpdatedAt = now;

            var homeSystem = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.ShipConstructionCompleted,
                SystemId = homeSystem.SystemId,
                EmpireId = empire.EmpireId,
                Severity = EventSeverity.Normal,
                DisplayText = $"{empire.EmpireName} completed {construction.ShipCount} ship(s) at {homeSystem.SystemName}.",
                FactJson = JsonSerializer.Serialize(new
                {
                    construction.ShipConstructionId,
                    empire.EmpireId,
                    homeFleet.FleetId,
                    homeSystem.SystemId,
                    construction.ShipCount,
                    construction.IndustrySpent
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    public static void ApplyPrioritySpending(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        foreach (var empire in state.Empires.Where(item => item.CycleId == cycleId && item.Status == EmpireStatus.Active))
        {
            var resources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
            var priorities = state.EmpirePriorities.SingleOrDefault(item => item.EmpireId == empire.EmpireId)
                ?? throw new InvalidOperationException($"Empire {empire.EmpireId} does not have strategic priorities.");

            StrategicPriorityPolicy.Validate(priorities);
            ClampResources(resources);

            resources.LastSpentIndustry = 0;
            resources.LastSpentResearch = 0;
            resources.LastSpentPopulation = 0;

            var militaryBudget = decimal.Round(resources.Industry * priorities.MilitaryWeight / 100m, 2);
            var shipCount = (int)Math.Floor(militaryBudget / ShipIndustryCost);
            if (shipCount <= 0)
            {
                resources.UpdatedAt = now;
                continue;
            }

            var spentIndustry = shipCount * ShipIndustryCost;
            resources.Industry = Math.Max(0, resources.Industry - spentIndustry);
            resources.LastSpentIndustry = spentIndustry;
            resources.UpdatedAt = now;

            var construction = new ShipConstruction
            {
                CycleId = cycleId,
                EmpireId = empire.EmpireId,
                ShipCount = shipCount,
                IndustrySpent = spentIndustry,
                StartedTick = tickNumber,
                CompleteAfterTick = tickNumber + ShipBuildDelayTicks,
                Status = ShipConstructionStatus.Queued,
                CreatedAt = now,
                UpdatedAt = now
            };
            state.ShipConstructions.Add(construction);

            var homeSystem = state.Systems.Single(system => system.SystemId == empire.HomeSystemId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.ShipConstructionQueued,
                SystemId = homeSystem.SystemId,
                EmpireId = empire.EmpireId,
                Severity = EventSeverity.Low,
                DisplayText = $"{empire.EmpireName} committed {spentIndustry:0.##} industry to {shipCount} ship(s).",
                FactJson = JsonSerializer.Serialize(new
                {
                    construction.ShipConstructionId,
                    empire.EmpireId,
                    homeSystem.SystemId,
                    construction.ShipCount,
                    construction.IndustrySpent,
                    construction.StartedTick,
                    construction.CompleteAfterTick,
                    ShipIndustryCost,
                    ShipBuildDelayTicks
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    public static void ApplyResearchUnlocks(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        foreach (var empire in state.Empires.Where(item => item.CycleId == cycleId && item.Status == EmpireStatus.Active))
        {
            if (HasSurveyProjectionDoctrine(state, cycleId, empire.EmpireId))
            {
                continue;
            }

            var resources = state.EmpireResources.Single(item => item.EmpireId == empire.EmpireId);
            if (resources.Research < SurveyProjectionResearchThreshold)
            {
                continue;
            }

            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.DoctrineUnlocked,
                EmpireId = empire.EmpireId,
                Severity = EventSeverity.Normal,
                DisplayText = $"{empire.EmpireName} unlocked Survey Projection doctrine.",
                FactJson = JsonSerializer.Serialize(new
                {
                    empire.EmpireId,
                    doctrine = SurveyProjectionDoctrineKey,
                    researchThreshold = SurveyProjectionResearchThreshold,
                    currentResearch = resources.Research,
                    presenceBonus = SurveyProjectionPresenceBonus
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    public static bool HasSurveyProjectionDoctrine(GameState state, Guid cycleId, Guid empireId) =>
        state.Events.Any(item => item.CycleId == cycleId
                                 && item.EmpireId == empireId
                                 && item.EventType == EventType.DoctrineUnlocked
                                 && item.FactJson.Contains(SurveyProjectionDoctrineKey, StringComparison.Ordinal));

    private static Fleet GetOrCreateHomeFleet(GameState state, Guid cycleId, Empire empire, DateTimeOffset now)
    {
        var homeFleet = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.EmpireId == empire.EmpireId
                            && fleet.CurrentSystemId == empire.HomeSystemId
                            && fleet.Status == FleetStatus.Active)
            .OrderBy(fleet => string.Equals(fleet.FleetName, $"{empire.EmpireName} Home Fleet", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
            .ThenBy(fleet => fleet.CreatedAt)
            .FirstOrDefault();

        if (homeFleet is not null)
        {
            return homeFleet;
        }

        homeFleet = new Fleet
        {
            CycleId = cycleId,
            EmpireId = empire.EmpireId,
            FactionId = state.GetEmpireFaction(empire.EmpireId).FactionId,
            FleetName = $"{empire.EmpireName} Home Fleet",
            CurrentSystemId = empire.HomeSystemId,
            ShipCount = 0,
            Status = FleetStatus.Active,
            CreatedAt = now
        };
        state.Fleets.Add(homeFleet);
        return homeFleet;
    }

    private static void ClampResources(EmpireResource resources)
    {
        resources.Industry = Math.Max(0, resources.Industry);
        resources.Research = Math.Max(0, resources.Research);
        resources.Population = Math.Max(0, resources.Population);
    }
}
