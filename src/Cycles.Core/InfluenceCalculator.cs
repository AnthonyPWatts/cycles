using System.Text.Json;

namespace Cycles.Core;

public static class InfluenceCalculator
{
    public const int HomeSystemMinimumPresence = 10;
    public const int ColonialOutpostPresence = 5;
    public const decimal MaximumExpansionProjectionBonus = 1m;

    public static IReadOnlyDictionary<Guid, decimal> CalculateEffectivePresence(
        GameState state,
        Guid cycleId,
        Guid systemId)
    {
        var presence = state.Fleets
            .Where(fleet => fleet.CycleId == cycleId
                            && fleet.Status == FleetStatus.Active
                            && fleet.CurrentSystemId == systemId
                            && fleet.ShipCount > 0)
            .GroupBy(fleet => fleet.EmpireId)
            .ToDictionary(group => group.Key, group => (decimal)group.Sum(fleet => fleet.ShipCount));

        foreach (var homeEmpire in state.Empires.Where(empire => empire.CycleId == cycleId && empire.HomeSystemId == systemId))
        {
            presence[homeEmpire.EmpireId] = presence.TryGetValue(homeEmpire.EmpireId, out var currentPresence)
                ? Math.Max(currentPresence, HomeSystemMinimumPresence)
                : HomeSystemMinimumPresence;
        }

        foreach (var outpost in state.ColonialOutposts.Where(item => item.CycleId == cycleId && item.SystemId == systemId))
        {
            if (presence.TryGetValue(outpost.EmpireId, out var currentPresence))
            {
                presence[outpost.EmpireId] = currentPresence + ColonialOutpostPresence;
            }
        }

        foreach (var (empireId, effectivePresence) in presence.ToArray())
        {
            var projectionBonus = CalculateExpansionProjectionBonus(state, empireId);
            var doctrineBonus = CalculateDoctrinePresenceBonus(state, cycleId, empireId);
            presence[empireId] = decimal.Round(effectivePresence * (1m + projectionBonus + doctrineBonus), 2);
        }

        return presence;
    }

    public static void GenerateResources(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var generatedByEmpire = new Dictionary<Guid, ResourceDelta>();
        var cycleEmpireIds = state.Empires
            .Where(empire => empire.CycleId == cycleId)
            .Select(empire => empire.EmpireId)
            .ToHashSet();

        foreach (var resources in state.EmpireResources.Where(resource => cycleEmpireIds.Contains(resource.EmpireId)))
        {
            resources.LastGeneratedIndustry = 0;
            resources.LastGeneratedResearch = 0;
            resources.LastGeneratedPopulation = 0;
            resources.LastSpentIndustry = 0;
            resources.LastSpentResearch = 0;
            resources.LastSpentPopulation = 0;
        }

        foreach (var system in state.Systems.Where(system => system.CycleId == cycleId))
        {
            var presence = CalculateEffectivePresence(state, cycleId, system.SystemId);
            if (presence.Count == 0)
            {
                continue;
            }

            var totalPresence = presence.Values.Sum();
            foreach (var (empireId, effectivePresence) in presence)
            {
                var share = effectivePresence / totalPresence;
                var delta = new ResourceDelta(
                    decimal.Round(system.IndustryOutput * share, 2),
                    decimal.Round(system.ResearchOutput * share, 2),
                    decimal.Round(system.PopulationOutput * share, 2));

                if (!generatedByEmpire.TryAdd(empireId, delta))
                {
                    generatedByEmpire[empireId] = generatedByEmpire[empireId] + delta;
                }
            }
        }

        foreach (var (empireId, delta) in generatedByEmpire)
        {
            var resources = state.EmpireResources.Single(resource => resource.EmpireId == empireId);
            resources.Industry = Math.Max(0, resources.Industry + delta.Industry);
            resources.Research = Math.Max(0, resources.Research + delta.Research);
            resources.Population = Math.Max(0, resources.Population + delta.Population);
            resources.LastGeneratedIndustry += delta.Industry;
            resources.LastGeneratedResearch += delta.Research;
            resources.LastGeneratedPopulation += delta.Population;
            resources.UpdatedAt = now;

            var empire = state.Empires.Single(item => item.EmpireId == empireId);
            state.Events.Add(new EventRecord
            {
                CycleId = cycleId,
                TickNumber = tickNumber,
                EventType = EventType.ResourcesGenerated,
                EmpireId = empireId,
                Severity = EventSeverity.Low,
                DisplayText = $"{empire.EmpireName} gained {delta.Industry:0.##} industry, {delta.Research:0.##} research, and {delta.Population:0.##} population.",
                FactJson = JsonSerializer.Serialize(new
                {
                    empireId,
                    delta.Industry,
                    delta.Research,
                    delta.Population
                }, GameStateJson.Options),
                CreatedAt = now
            });
        }
    }

    private static decimal CalculateExpansionProjectionBonus(GameState state, Guid empireId)
    {
        var priority = state.EmpirePriorities.SingleOrDefault(item => item.EmpireId == empireId);
        if (priority is null)
        {
            return 0m;
        }

        var expansionWeight = Math.Clamp(priority.ExpansionWeight, 0, 100);
        return MaximumExpansionProjectionBonus * expansionWeight / 100m;
    }

    private static decimal CalculateDoctrinePresenceBonus(GameState state, Guid cycleId, Guid empireId) =>
        EconomyProcessor.HasSurveyProjectionDoctrine(state, cycleId, empireId)
            ? EconomyProcessor.SurveyProjectionPresenceBonus
            : 0m;
}

public readonly record struct ResourceDelta(decimal Industry, decimal Research, decimal Population)
{
    public static ResourceDelta operator +(ResourceDelta first, ResourceDelta second) =>
        new(first.Industry + second.Industry, first.Research + second.Research, first.Population + second.Population);
}
