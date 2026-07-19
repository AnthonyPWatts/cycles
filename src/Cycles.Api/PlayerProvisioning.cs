using Cycles.Core;

public static class PlayerProvisioning
{
    private static readonly string[] AdmiralGivenNames =
    [
        "Amina", "Anik", "Cassia", "Cela", "Darian", "Elian", "Esra", "Ilya",
        "Imani", "Jalen", "Kael", "Keira", "Lio", "Mara", "Nadia", "Niko",
        "Orin", "Rhea", "Sable", "Soren", "Tavian", "Tessa", "Vela", "Yara"
    ];

    private static readonly string[] AdmiralFamilyNames =
    [
        "Ardent", "Ashar", "Calder", "Damar", "Hale", "Harrow", "Ilyan", "Kepler",
        "Kestrel", "Morrow", "Neris", "Orre", "Rook", "Sen", "Solari", "Sutekh",
        "Teral", "Vale", "Vey", "Voss", "Wren", "Xanthe", "Yarrow", "Zoric"
    ];

    public static Empire AddEmpireForPlayer(
        GameState state,
        Cycle cycle,
        Player player,
        string? requestedEmpireName,
        DateTimeOffset now)
    {
        if (state.Empires.Count(item => item.CycleId == cycle.CycleId) >= MatchControl.MaximumEmpireCount)
        {
            throw new InvalidOperationException($"A Cycle supports at most {MatchControl.MaximumEmpireCount} Empires.");
        }

        var claimedHomeSystems = state.Empires
            .Where(empire => empire.CycleId == cycle.CycleId)
            .Select(empire => empire.HomeSystemId)
            .ToHashSet();
        var homeSystem = state.Systems
            .Where(system => system.CycleId == cycle.CycleId && !claimedHomeSystems.Contains(system.SystemId))
            .OrderByDescending(system => system.StrategicValue)
            .FirstOrDefault()
            ?? state.Systems
                .Where(system => system.CycleId == cycle.CycleId)
                .OrderByDescending(system => system.StrategicValue)
                .First();

        var empire = new Empire
        {
            CycleId = cycle.CycleId,
            PlayerId = player.PlayerId,
            EmpireName = string.IsNullOrWhiteSpace(requestedEmpireName) ? $"{player.Username} Continuance" : requestedEmpireName.Trim(),
            HomeSystemId = homeSystem.SystemId,
            CreatedAt = now,
            Status = EmpireStatus.Active
        };
        state.Empires.Add(empire);
        var faction = new Faction
        {
            FactionId = empire.EmpireId,
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            FactionName = empire.EmpireName,
            Kind = FactionKind.Empire,
            Status = FactionStatus.Active,
            CreatedAt = now
        };
        state.Factions.Add(faction);
        state.MatchParticipants.Add(new MatchParticipant
        {
            GameId = cycle.GameId
                ?? throw new InvalidOperationException($"Cycle {cycle.CycleId} has no Game scope."),
            CycleId = cycle.CycleId,
            PlayerId = player.PlayerId,
            EmpireId = empire.EmpireId,
            Status = MatchParticipantStatus.Active,
            JoinedAt = now
        });

        state.EmpireResources.Add(new EmpireResource
        {
            EmpireId = empire.EmpireId,
            Industry = 100,
            Research = 100,
            Population = 100,
            UpdatedAt = now
        });

        state.EmpirePriorities.Add(new EmpirePriority
        {
            EmpireId = empire.EmpireId,
            IndustryWeight = 0,
            ResearchWeight = 0,
            MilitaryWeight = StrategicPriorityPolicy.DefaultMilitaryWeight,
            ExpansionWeight = StrategicPriorityPolicy.DefaultExpansionWeight,
            UpdatedAt = now
        });

        var admiral = new Admiral
        {
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            AdmiralName = CreateUniqueAdmiralName(state),
            ReputationScore = 0,
            Status = AdmiralStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        state.Admirals.Add(admiral);

        state.Fleets.Add(new Fleet
        {
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            FactionId = faction.FactionId,
            AdmiralId = admiral.AdmiralId,
            FleetName = $"{empire.EmpireName} Home Fleet",
            CurrentSystemId = homeSystem.SystemId,
            ShipCount = 45,
            Status = FleetStatus.Active,
            CreatedAt = now
        });

        return empire;
    }

    public static void RepairLegacyStartingAdmiralName(
        GameState state,
        Empire empire,
        Player player,
        DateTimeOffset now)
    {
        var legacyName = $"{player.Username} Vanguard";
        var admiral = state.Admirals.SingleOrDefault(item =>
            item.EmpireId == empire.EmpireId
            && string.Equals(item.AdmiralName, legacyName, StringComparison.OrdinalIgnoreCase));
        if (admiral is null)
        {
            return;
        }

        admiral.AdmiralName = CreateUniqueAdmiralName(state);
        admiral.UpdatedAt = now;
    }

    private static string CreateUniqueAdmiralName(GameState state)
    {
        var usedNames = state.Admirals
            .Select(item => item.AdmiralName)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var candidateCount = AdmiralGivenNames.Length * AdmiralFamilyNames.Length;
        var start = Random.Shared.Next(candidateCount);

        for (var offset = 0; offset < candidateCount; offset++)
        {
            var index = (start + offset) % candidateCount;
            var candidate = $"{AdmiralGivenNames[index / AdmiralFamilyNames.Length]} {AdmiralFamilyNames[index % AdmiralFamilyNames.Length]}";
            if (!usedNames.Contains(candidate))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("The admiral name pool is exhausted.");
    }
}
