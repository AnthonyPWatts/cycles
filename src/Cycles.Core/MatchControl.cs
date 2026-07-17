namespace Cycles.Core;

public static class MatchControl
{
    public const int MaximumEmpireCount = 6;

    public static Faction GetEmpireFaction(this GameState state, Guid empireId) =>
        state.Factions.Single(item => item.EmpireId == empireId && item.Kind == FactionKind.Empire);

    public static Guid? GetEmpireIdForFaction(this GameState state, Guid factionId) =>
        state.Factions.Single(item => item.FactionId == factionId).EmpireId;

    public static Guid GetFactionId(this GameState state, Fleet fleet) =>
        fleet.FactionId != Guid.Empty
            ? fleet.FactionId
            : state.GetEmpireFaction(fleet.EmpireId).FactionId;

    public static Faction GetFleetFaction(this GameState state, Fleet fleet) =>
        state.Factions.Single(item => item.FactionId == state.GetFactionId(fleet));

    public static MatchParticipant? GetParticipant(this GameState state, Guid cycleId, Guid playerId) =>
        state.MatchParticipants.SingleOrDefault(item => item.CycleId == cycleId && item.PlayerId == playerId);

    public static MatchParticipant? GetCurrentParticipantForEmpire(this GameState state, Guid empireId) =>
        state.MatchParticipants.SingleOrDefault(item => item.EmpireId == empireId && item.EndedAt is null);

    public static Empire RequireCommandableEmpire(this GameState state, Guid cycleId, Guid playerId)
    {
        var participant = state.GetParticipant(cycleId, playerId)
            ?? throw new InvalidOperationException("The player is not participating in this Cycle.");
        if (participant.Status != MatchParticipantStatus.Active || participant.EndedAt is not null)
        {
            throw new InvalidOperationException("The player is not an active participant in this Cycle.");
        }

        var empire = state.Empires.Single(item => item.EmpireId == participant.EmpireId);
        if (empire.Status != EmpireStatus.Active)
        {
            throw new InvalidOperationException("The participant's Empire is not active.");
        }

        return empire;
    }

    public static void DefeatEmpire(GameState state, Guid empireId, DateTimeOffset endedAt)
    {
        var empire = state.Empires.Single(item => item.EmpireId == empireId);
        empire.Status = EmpireStatus.Defeated;

        var faction = state.GetEmpireFaction(empireId);
        faction.Status = FactionStatus.Defeated;

        var participant = state.GetCurrentParticipantForEmpire(empireId);
        if (participant is not null)
        {
            participant.Status = MatchParticipantStatus.Defeated;
            participant.EndedAt = endedAt;
        }
    }

    public static void CompleteCycle(GameState state, Guid cycleId, DateTimeOffset endedAt)
    {
        var cycle = state.Cycles.Single(item => item.CycleId == cycleId);
        cycle.Status = CycleStatus.Completed;
        cycle.EndAt = endedAt;

        foreach (var participant in state.MatchParticipants.Where(item =>
                     item.CycleId == cycleId
                     && item.Status == MatchParticipantStatus.Active
                     && item.EndedAt is null))
        {
            participant.Status = MatchParticipantStatus.Completed;
            participant.EndedAt = endedAt;
        }
    }
}
