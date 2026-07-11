using System.Text.Json;

namespace Cycles.Core;

public static class DiplomacyService
{
    public static DiplomaticRelationshipState GetState(GameState state, Guid cycleId, Guid firstEmpireId, Guid secondEmpireId)
    {
        var (first, second) = NormalisePair(firstEmpireId, secondEmpireId);
        return state.DiplomaticRelationships
                   .SingleOrDefault(item => item.CycleId == cycleId
                                            && item.FirstEmpireId == first
                                            && item.SecondEmpireId == second)
                   ?.State
               ?? DiplomaticRelationshipState.Neutral;
    }

    public static DiplomaticRelationship SetState(
        GameState state,
        Guid cycleId,
        Guid firstEmpireId,
        Guid secondEmpireId,
        DiplomaticRelationshipState relationshipState,
        int tickNumber,
        DateTimeOffset now)
    {
        var (first, second) = NormalisePair(firstEmpireId, secondEmpireId);
        if (!state.Empires.Any(item => item.CycleId == cycleId && item.EmpireId == first)
            || !state.Empires.Any(item => item.CycleId == cycleId && item.EmpireId == second))
        {
            throw new InvalidOperationException("Diplomatic relationships require two empires in the same Cycle.");
        }

        var relationship = state.DiplomaticRelationships.SingleOrDefault(item =>
            item.CycleId == cycleId
            && item.FirstEmpireId == first
            && item.SecondEmpireId == second);
        if (relationship is null)
        {
            relationship = new DiplomaticRelationship
            {
                CycleId = cycleId,
                FirstEmpireId = first,
                SecondEmpireId = second
            };
            state.DiplomaticRelationships.Add(relationship);
        }

        relationship.State = relationshipState;
        relationship.UpdatedTick = tickNumber;
        relationship.UpdatedAt = now;
        return relationship;
    }

    public static void RecordAggression(
        GameState state,
        Guid cycleId,
        int tickNumber,
        Guid attackerEmpireId,
        Guid defenderEmpireId,
        GalaxySystem system,
        DateTimeOffset now)
    {
        var priorState = GetState(state, cycleId, attackerEmpireId, defenderEmpireId);
        var attacker = state.Empires.Single(item => item.EmpireId == attackerEmpireId);
        var defender = state.Empires.Single(item => item.EmpireId == defenderEmpireId);

        state.Events.Add(new EventRecord
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            EventType = EventType.DiplomaticAggression,
            SystemId = system.SystemId,
            EmpireId = attackerEmpireId,
            Severity = EventSeverity.High,
            DisplayText = $"{attacker.EmpireName} attacked {defender.EmpireName} at {system.SystemName}.",
            FactJson = JsonSerializer.Serialize(new
            {
                attackerEmpireId,
                defenderEmpireId,
                systemId = system.SystemId,
                priorState
            }, GameStateJson.Options),
            CreatedAt = now
        });

        if (priorState is not (DiplomaticRelationshipState.NonAggressionPact or DiplomaticRelationshipState.Alliance))
        {
            return;
        }

        SetState(
            state,
            cycleId,
            attackerEmpireId,
            defenderEmpireId,
            DiplomaticRelationshipState.Neutral,
            tickNumber,
            now);
        state.Events.Add(new EventRecord
        {
            CycleId = cycleId,
            TickNumber = tickNumber,
            EventType = EventType.TreatyCancelledByAggression,
            SystemId = system.SystemId,
            EmpireId = attackerEmpireId,
            Severity = EventSeverity.High,
            DisplayText = $"{attacker.EmpireName}'s attack on {defender.EmpireName} ended their {FormatState(priorState)}.",
            FactJson = JsonSerializer.Serialize(new
            {
                attackerEmpireId,
                defenderEmpireId,
                systemId = system.SystemId,
                priorState,
                newState = DiplomaticRelationshipState.Neutral
            }, GameStateJson.Options),
            CreatedAt = now
        });
    }

    private static (Guid First, Guid Second) NormalisePair(Guid firstEmpireId, Guid secondEmpireId)
    {
        if (firstEmpireId == secondEmpireId)
        {
            throw new ArgumentException("A diplomatic relationship requires two different empires.");
        }

        return firstEmpireId.CompareTo(secondEmpireId) < 0
            ? (firstEmpireId, secondEmpireId)
            : (secondEmpireId, firstEmpireId);
    }

    private static string FormatState(DiplomaticRelationshipState state) => state switch
    {
        DiplomaticRelationshipState.NonAggressionPact => "non-aggression pact",
        DiplomaticRelationshipState.Alliance => "alliance",
        _ => state.ToString()
    };
}
