using Cycles.Core;

namespace Cycles.Tests;

public sealed class DiplomacyTests
{
    [Fact]
    public void Missing_relationship_defaults_to_neutral()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 40);
        var cycle = state.GetActiveCycle()!;

        var relationship = DiplomacyService.GetState(
            state,
            cycle.CycleId,
            state.Empires[0].EmpireId,
            state.Empires[1].EmpireId);

        Assert.Equal(DiplomaticRelationshipState.Neutral, relationship);
        Assert.Empty(state.DiplomaticRelationships);
    }

    [Fact]
    public void Relationship_pair_is_canonical_and_cloned()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 40);
        var cycle = state.GetActiveCycle()!;
        var first = state.Empires[0].EmpireId;
        var second = state.Empires[1].EmpireId;

        var relationship = DiplomacyService.SetState(
            state,
            cycle.CycleId,
            second,
            first,
            DiplomaticRelationshipState.Alliance,
            0,
            TestState.Now);
        var clone = state.DeepClone();

        Assert.True(relationship.FirstEmpireId.CompareTo(relationship.SecondEmpireId) < 0);
        Assert.Equal(DiplomaticRelationshipState.Alliance, DiplomacyService.GetState(clone, cycle.CycleId, first, second));
        Assert.NotSame(relationship, clone.DiplomaticRelationships.Single());
    }

    [Fact]
    public void Attack_through_treaty_records_aggression_and_returns_relationship_to_neutral()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 40);
        var cycle = state.GetActiveCycle()!;
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        var attackerFleet = state.Fleets.Single(item => item.EmpireId == attacker.EmpireId);
        DiplomacyService.SetState(
            state,
            cycle.CycleId,
            attacker.EmpireId,
            defender.EmpireId,
            DiplomaticRelationshipState.NonAggressionPact,
            0,
            TestState.Now);
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, defender.EmpireId, TestState.Now);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.True(result.Status == TickLogStatus.Completed, state.TickLogs.Last().DiagnosticLog);

        Assert.Equal(
            DiplomaticRelationshipState.Neutral,
            DiplomacyService.GetState(state, cycle.CycleId, attacker.EmpireId, defender.EmpireId));
        Assert.Contains(state.Events, item => item.EventType == EventType.DiplomaticAggression);
        Assert.Contains(state.Events, item => item.EventType == EventType.TreatyCancelledByAggression);
        Assert.DoesNotContain(state.DiplomaticRelationships, item => item.State == DiplomaticRelationshipState.War);
    }

    [Fact]
    public void Neutral_attack_records_aggression_without_inventing_war()
    {
        var state = TestState.CreateTwoEmpireContest(attackerShips: 50, defenderShips: 40);
        var cycle = state.GetActiveCycle()!;
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        var attackerFleet = state.Fleets.Single(item => item.EmpireId == attacker.EmpireId);
        OrderService.SubmitAttackOrder(state, attackerFleet.FleetId, defender.EmpireId, TestState.Now);

        var result = new TickEngine().RunTick(state, cycle.CycleId, TestState.Now);

        Assert.True(result.Status == TickLogStatus.Completed, state.TickLogs.Last().DiagnosticLog);

        Assert.Equal(
            DiplomaticRelationshipState.Neutral,
            DiplomacyService.GetState(state, cycle.CycleId, attacker.EmpireId, defender.EmpireId));
        Assert.Contains(state.Events, item => item.EventType == EventType.DiplomaticAggression);
        Assert.DoesNotContain(state.Events, item => item.EventType == EventType.TreatyCancelledByAggression);
        Assert.Empty(state.DiplomaticRelationships);
    }
}
