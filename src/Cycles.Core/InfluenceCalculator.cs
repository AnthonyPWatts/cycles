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
            .GroupBy(state.GetFactionId)
            .ToDictionary(group => group.Key, group => (decimal)group.Sum(fleet => fleet.ShipCount));

        foreach (var homeEmpire in state.Empires.Where(empire => empire.CycleId == cycleId && empire.HomeSystemId == systemId))
        {
            var factionId = state.GetEmpireFaction(homeEmpire.EmpireId).FactionId;
            presence[factionId] = presence.TryGetValue(factionId, out var currentPresence)
                ? Math.Max(currentPresence, HomeSystemMinimumPresence)
                : HomeSystemMinimumPresence;
        }

        foreach (var outpost in state.ColonialOutposts.Where(item => item.CycleId == cycleId && item.SystemId == systemId))
        {
            var factionId = state.GetEmpireFaction(outpost.EmpireId).FactionId;
            if (presence.TryGetValue(factionId, out var currentPresence))
            {
                presence[factionId] = currentPresence + ColonialOutpostPresence;
            }
        }

        foreach (var (factionId, effectivePresence) in presence.ToArray())
        {
            var empireId = state.GetEmpireIdForFaction(factionId);
            if (!empireId.HasValue)
            {
                continue;
            }

            var projectionBonus = CalculateExpansionProjectionBonus(state, empireId.Value);
            var doctrineBonus = CalculateDoctrinePresenceBonus(state, cycleId, empireId.Value);
            presence[factionId] = decimal.Round(effectivePresence * (1m + projectionBonus + doctrineBonus), 2);
        }

        return presence;
    }

    public static void GenerateResources(GameState state, Guid cycleId, int tickNumber, DateTimeOffset now)
    {
        var generatedByEmpire = CalculateResourceGeneration(state, cycleId);
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

        foreach (var (empireId, delta) in generatedByEmpire.OrderBy(item => item.Key))
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
                FactionId = state.GetEmpireFaction(empireId).FactionId,
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

    internal static IReadOnlyDictionary<Guid, ResourceDelta> CalculateResourceGeneration(GameState state, Guid cycleId)
    {
        var generatedByEmpire = new Dictionary<Guid, ResourceDelta>();

        foreach (var system in state.Systems
                     .Where(system => system.CycleId == cycleId)
                     .OrderBy(system => system.SystemId))
        {
            var presence = CalculateEffectivePresence(state, cycleId, system.SystemId);
            if (presence.Count == 0)
            {
                continue;
            }

            var totalPresence = presence.Values.Sum();
            foreach (var (factionId, effectivePresence) in presence.OrderBy(item => item.Key))
            {
                var share = effectivePresence / totalPresence;
                var empireId = state.GetEmpireIdForFaction(factionId);
                if (!empireId.HasValue)
                {
                    continue;
                }

                var delta = new ResourceDelta(
                    decimal.Round(system.IndustryOutput * share, 2),
                    decimal.Round(system.ResearchOutput * share, 2),
                    decimal.Round(system.PopulationOutput * share, 2));

                if (!generatedByEmpire.TryAdd(empireId.Value, delta))
                {
                    generatedByEmpire[empireId.Value] = generatedByEmpire[empireId.Value] + delta;
                }
            }
        }

        return generatedByEmpire;
    }

    public static ResourceDelta CalculateEmpireResourceGeneration(
        GameState state,
        Guid cycleId,
        Guid empireId)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (!state.Empires.Any(empire => empire.CycleId == cycleId && empire.EmpireId == empireId))
        {
            throw new InvalidOperationException("Empire was not found in the requested Cycle.");
        }

        var generatedByEmpire = CalculateResourceGeneration(state, cycleId);
        return generatedByEmpire.TryGetValue(empireId, out var delta)
            ? delta
            : new ResourceDelta(0, 0, 0);
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
