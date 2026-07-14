using Cycles.Core;

public static class PlayerProvisioning
{
    public static Empire AddEmpireForPlayer(
        GameState state,
        Cycle cycle,
        Player player,
        string? requestedEmpireName,
        DateTimeOffset now)
    {
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
            IndustryWeight = 30,
            ResearchWeight = 25,
            MilitaryWeight = 30,
            ExpansionWeight = 15,
            UpdatedAt = now
        });

        var admiral = new Admiral
        {
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            AdmiralName = $"{player.Username} Vanguard",
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
            AdmiralId = admiral.AdmiralId,
            FleetName = $"{empire.EmpireName} Home Fleet",
            CurrentSystemId = homeSystem.SystemId,
            ShipCount = 45,
            Status = FleetStatus.Active,
            CreatedAt = now
        });

        return empire;
    }
}
