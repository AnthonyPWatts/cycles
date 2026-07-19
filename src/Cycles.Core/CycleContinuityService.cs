using System.Text.Json;

namespace Cycles.Core;

public static class CycleContinuityService
{
    private const decimal PreservedSystemShare = 0.10m;

    public static CycleContinuityResult GenerateNextCycle(
        GameState state,
        Guid completedCycleId,
        DateTimeOffset startsAt,
        int? seed = null)
    {
        ArgumentNullException.ThrowIfNull(state);

        var workingState = state.DeepClone();
        var result = GenerateNextCycleCore(workingState, completedCycleId, startsAt, seed);
        state.ReplaceWith(workingState);
        return result;
    }

    private static CycleContinuityResult GenerateNextCycleCore(
        GameState state,
        Guid completedCycleId,
        DateTimeOffset startsAt,
        int? seed)
    {
        var sourceCycle = state.Cycles.SingleOrDefault(cycle => cycle.CycleId == completedCycleId)
            ?? throw new InvalidOperationException("Completed Cycle was not found.");

        if (sourceCycle.Status != CycleStatus.Completed)
        {
            throw new InvalidOperationException("Only completed Cycles can generate a successor Cycle.");
        }

        if (state.Cycles.Any(cycle => cycle.Status is CycleStatus.Active or CycleStatus.RecoveryRequired))
        {
            throw new InvalidOperationException(
                "A successor Cycle cannot be generated while another Cycle is active or requires recovery.");
        }

        if (state.Cycles.Any(cycle => cycle.PreviousCycleId == completedCycleId))
        {
            throw new InvalidOperationException("The completed Cycle already has a successor.");
        }

        var sourceSystems = state.Systems
            .Where(system => system.CycleId == completedCycleId)
            .OrderBy(system => system.SystemName)
            .ToArray();
        var sourceEmpires = GetRankedSourceEmpires(state, completedCycleId);
        if (sourceSystems.Length == 0 || sourceEmpires.Length == 0)
        {
            throw new InvalidOperationException("Completed Cycle must have systems and ranked empires before continuity can be generated.");
        }

        var seedValue = seed ?? CreateContinuitySeed(sourceCycle);
        var successorSystemCount = Math.Max(sourceSystems.Length, sourceEmpires.Length);
        var generated = GameSeeder.CreateDefault(successorSystemCount, sourceEmpires.Length, seedValue, startsAt);
        var newCycle = generated.GetActiveCycle()
            ?? throw new InvalidOperationException("Generated state did not contain an active Cycle.");
        newCycle.Name = CreateNextCycleName(sourceCycle, state.Cycles.Count(cycle => cycle.Status == CycleStatus.Completed));
        newCycle.StartAt = startsAt;
        newCycle.EndAt = startsAt.Add(sourceCycle.EndAt - sourceCycle.StartAt);
        newCycle.TickLengthMinutes = sourceCycle.TickLengthMinutes;
        newCycle.CreatedByPlayerId = sourceCycle.CreatedByPlayerId;
        newCycle.CreatedAt = startsAt;
        newCycle.PreviousCycleId = sourceCycle.CycleId;

        var successorEmpires = ApplySuccessorEmpires(generated, newCycle.CycleId, sourceEmpires);
        var preservedSystems = ApplyHistoricalSystems(generated, newCycle.CycleId, SelectHistoricalSystems(state, completedCycleId, sourceSystems.Length));
        UpdateSeedEvent(generated, newCycle, sourceCycle, seedValue, preservedSystems, successorEmpires);
        PrepareSuccessorConfiguration(state, generated, newCycle);
        AppendGeneratedState(state, generated);
        LegacyGameFoundation.ApplyLifecycleTransition(state);

        return new CycleContinuityResult(newCycle.CycleId, sourceCycle.CycleId, seedValue, preservedSystems, successorEmpires);
    }

    private static Empire[] GetRankedSourceEmpires(GameState state, Guid cycleId)
    {
        var rankedEmpireIds = state.CycleRankings
            .Where(ranking => ranking.CycleId == cycleId)
            .OrderBy(ranking => ranking.Rank)
            .Select(ranking => ranking.EmpireId)
            .ToArray();

        if (rankedEmpireIds.Length > 0)
        {
            return rankedEmpireIds
                .Select(empireId => state.Empires.Single(empire => empire.EmpireId == empireId))
                .ToArray();
        }

        return state.Empires
            .Where(empire => empire.CycleId == cycleId && empire.Status == EmpireStatus.Active)
            .OrderBy(empire => empire.EmpireName)
            .ToArray();
    }

    private static SuccessorEmpireContinuity[] ApplySuccessorEmpires(GameState generated, Guid newCycleId, IReadOnlyList<Empire> sourceEmpires)
    {
        var newEmpires = generated.Empires
            .Where(empire => empire.CycleId == newCycleId)
            .ToArray();
        var continuities = new List<SuccessorEmpireContinuity>();

        for (var index = 0; index < newEmpires.Length; index++)
        {
            var sourceEmpire = sourceEmpires[index];
            var newEmpire = newEmpires[index];
            var generatedPlayerId = newEmpire.PlayerId;
            newEmpire.PlayerId = sourceEmpire.PlayerId;
            newEmpire.EmpireName = CreateSuccessorEmpireName(sourceEmpire, index);
            var faction = generated.GetEmpireFaction(newEmpire.EmpireId);
            faction.FactionName = newEmpire.EmpireName;
            var participant = generated.MatchParticipants.Single(item =>
                item.CycleId == newCycleId && item.PlayerId == generatedPlayerId);
            participant.PlayerId = sourceEmpire.PlayerId;
            continuities.Add(new SuccessorEmpireContinuity(
                sourceEmpire.EmpireId,
                newEmpire.EmpireId,
                sourceEmpire.PlayerId,
                sourceEmpire.EmpireName,
                newEmpire.EmpireName));
        }

        return continuities.ToArray();
    }

    private static IReadOnlyList<HistoricalSystemCandidate> SelectHistoricalSystems(
        GameState state,
        Guid cycleId,
        int systemCount)
    {
        var signalsBySystem = state.SystemHistoricalSignals
            .Where(signal => signal.CycleId == cycleId)
            .GroupBy(signal => signal.SystemId)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderByDescending(signal => signal.HostedCycleLargestBattle)
                    .ThenByDescending(signal => signal.HistoricalSignificanceAfter)
                    .ThenByDescending(signal => signal.TotalLosses)
                    .First());
        var majorEventRanksBySystem = state.CycleMajorEvents
            .Where(item => item.CycleId == cycleId && item.SystemId.HasValue)
            .GroupBy(item => item.SystemId!.Value)
            .ToDictionary(group => group.Key, group => group.Min(item => item.SelectionRank));
        var maxPreservedSystems = Math.Max(1, (int)Math.Ceiling(systemCount * PreservedSystemShare));

        return state.Systems
            .Where(system => system.CycleId == cycleId)
            .Select(system =>
            {
                signalsBySystem.TryGetValue(system.SystemId, out var signal);
                majorEventRanksBySystem.TryGetValue(system.SystemId, out var majorEventRank);
                return new HistoricalSystemCandidate(system, signal, majorEventRank);
            })
            .Where(candidate => candidate.Signal is not null
                                || candidate.MajorEventRank.HasValue
                                || candidate.System.HistoricalSignificance > 0)
            .OrderBy(candidate => candidate.MajorEventRank ?? int.MaxValue)
            .ThenByDescending(candidate => candidate.Signal?.HostedCycleLargestBattle ?? false)
            .ThenByDescending(candidate => candidate.Signal?.HistoricalSignificanceAfter ?? candidate.System.HistoricalSignificance)
            .ThenByDescending(candidate => candidate.Signal?.TotalLosses ?? 0)
            .ThenBy(candidate => candidate.System.SystemName)
            .Take(maxPreservedSystems)
            .ToArray();
    }

    private static PreservedSystemContinuity[] ApplyHistoricalSystems(
        GameState generated,
        Guid newCycleId,
        IReadOnlyList<HistoricalSystemCandidate> candidates)
    {
        if (candidates.Count == 0)
        {
            return [];
        }

        var newSystems = generated.Systems.Where(system => system.CycleId == newCycleId).ToArray();
        var usedSystemIds = new HashSet<Guid>();
        var continuities = new List<PreservedSystemContinuity>();

        foreach (var candidate in candidates)
        {
            var target = newSystems.FirstOrDefault(system => !usedSystemIds.Contains(system.SystemId)
                                                             && string.Equals(system.SystemName, candidate.System.SystemName, StringComparison.Ordinal))
                ?? newSystems
                    .Where(system => !usedSystemIds.Contains(system.SystemId))
                    .OrderByDescending(system => system.StrategicValue)
                    .ThenBy(system => system.SystemName)
                    .First();

            usedSystemIds.Add(target.SystemId);

            var inheritedSignificance = Math.Max(
                candidate.System.HistoricalSignificance,
                candidate.Signal?.HistoricalSignificanceAfter ?? 0);
            var strategicValueBonus = Math.Min(25, inheritedSignificance + ((candidate.Signal?.TotalLosses ?? 0) / 10));

            target.SystemName = candidate.System.SystemName;
            target.HistoricalSignificance = Math.Max(target.HistoricalSignificance, inheritedSignificance);
            target.StrategicValue += strategicValueBonus;

            continuities.Add(new PreservedSystemContinuity(
                candidate.System.SystemId,
                target.SystemId,
                target.SystemName,
                target.HistoricalSignificance,
                target.StrategicValue,
                candidate.Signal?.SystemHistoricalSignalId,
                candidate.MajorEventRank));
        }

        return continuities.ToArray();
    }

    private static void UpdateSeedEvent(
        GameState generated,
        Cycle newCycle,
        Cycle sourceCycle,
        int seed,
        IReadOnlyList<PreservedSystemContinuity> preservedSystems,
        IReadOnlyList<SuccessorEmpireContinuity> successorEmpires)
    {
        var seedEvent = generated.Events.Single(item => item.CycleId == newCycle.CycleId && item.EventType == EventType.CycleSeeded);
        seedEvent.Severity = EventSeverity.Historic;
        seedEvent.DisplayText = preservedSystems.Count == 0
            ? $"{newCycle.Name} began after {sourceCycle.Name} with {successorEmpires.Count} successor empires."
            : $"{newCycle.Name} began after {sourceCycle.Name}, preserving {preservedSystems.Count} historically significant system(s).";
        seedEvent.FactJson = JsonSerializer.Serialize(new
        {
            sourceCycleId = sourceCycle.CycleId,
            newCycleId = newCycle.CycleId,
            seed,
            systemCount = generated.Systems.Count(system => system.CycleId == newCycle.CycleId),
            empireCount = successorEmpires.Count,
            preservedSystems = preservedSystems.Select(system => new
            {
                system.SourceSystemId,
                system.NewSystemId,
                system.SystemName,
                system.HistoricalSignificance,
                system.StrategicValue,
                system.SourceSignalId,
                system.SourceMajorEventRank
            }),
            successorEmpires = successorEmpires.Select(empire => new
            {
                empire.SourceEmpireId,
                empire.NewEmpireId,
                empire.PlayerId,
                empire.SourceEmpireName,
                empire.NewEmpireName
            })
        }, GameStateJson.Options);
    }

    private static void AppendGeneratedState(GameState state, GameState generated)
    {
        state.Cycles.AddRange(generated.Cycles);
        state.CycleConfigurations.AddRange(generated.CycleConfigurations);
        state.Empires.AddRange(generated.Empires);
        state.Factions.AddRange(generated.Factions);
        state.MatchParticipants.AddRange(generated.MatchParticipants);
        state.EmpireResources.AddRange(generated.EmpireResources);
        state.EmpirePriorities.AddRange(generated.EmpirePriorities);
        state.Admirals.AddRange(generated.Admirals);
        state.AdmiralBattleHistories.AddRange(generated.AdmiralBattleHistories);
        state.Sectors.AddRange(generated.Sectors);
        state.Systems.AddRange(generated.Systems);
        state.SystemLinks.AddRange(generated.SystemLinks);
        state.Fleets.AddRange(generated.Fleets);
        state.FleetOrders.AddRange(generated.FleetOrders);
        state.ShipConstructions.AddRange(generated.ShipConstructions);
        state.TickLogs.AddRange(generated.TickLogs);
        state.Events.AddRange(generated.Events);
        state.BattleRecords.AddRange(generated.BattleRecords);
        state.BattleFleetParticipants.AddRange(generated.BattleFleetParticipants);
        state.ChronicleEntries.AddRange(generated.ChronicleEntries);
    }

    private static void PrepareSuccessorConfiguration(
        GameState state,
        GameState generated,
        Cycle newCycle)
    {
        var configuration = generated.CycleConfigurations.Single(item =>
            item.CycleConfigurationId == newCycle.CycleConfigurationId);
        var previousSequence = state.CycleConfigurations
            .Where(item => item.GameId == GameFoundationConstants.LegacyGameId)
            .Select(item => item.SequenceNumber)
            .DefaultIfEmpty(0)
            .Max();

        configuration.SequenceNumber = checked(previousSequence + 1);
        configuration.ScheduledStartAt = newCycle.StartAt;
        configuration.ScheduledEndAt = newCycle.EndAt;
        configuration.TickLengthMinutes = newCycle.TickLengthMinutes;
    }

    private static int CreateContinuitySeed(Cycle sourceCycle)
    {
        var hash = HashCode.Combine(sourceCycle.CycleId, sourceCycle.CurrentTickNumber, sourceCycle.EndAt);
        return hash == int.MinValue ? int.MaxValue : Math.Abs(hash);
    }

    private static string CreateNextCycleName(Cycle sourceCycle, int completedCycleCount) =>
        $"Cycle {completedCycleCount + 1}: After {sourceCycle.Name}";

    private static string CreateSuccessorEmpireName(Empire sourceEmpire, int rankIndex)
    {
        var suffix = rankIndex == 0 ? "Legacy" : "Remnant";
        var name = $"{sourceEmpire.EmpireName} {suffix}";
        return name.Length <= 120 ? name : name[..120];
    }

    private sealed record HistoricalSystemCandidate(
        GalaxySystem System,
        SystemHistoricalSignal? Signal,
        int? MajorEventRank);
}

public sealed record CycleContinuityResult(
    Guid CycleId,
    Guid SourceCycleId,
    int Seed,
    IReadOnlyList<PreservedSystemContinuity> PreservedSystems,
    IReadOnlyList<SuccessorEmpireContinuity> SuccessorEmpires);

public sealed record PreservedSystemContinuity(
    Guid SourceSystemId,
    Guid NewSystemId,
    string SystemName,
    int HistoricalSignificance,
    int StrategicValue,
    Guid? SourceSignalId,
    int? SourceMajorEventRank);

public sealed record SuccessorEmpireContinuity(
    Guid SourceEmpireId,
    Guid NewEmpireId,
    Guid PlayerId,
    string SourceEmpireName,
    string NewEmpireName);
