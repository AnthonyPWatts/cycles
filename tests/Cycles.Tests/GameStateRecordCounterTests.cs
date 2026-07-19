using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameStateRecordCounterTests
{
    [Fact]
    public void CountIncludesEveryCycleScopedCollectionAndRelatedPlayer()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        var empire = state.Empires.Single();
        var player = state.Players.Single();
        state.AdminRoleAuditRecords.Add(new AdminRoleAuditRecord
        {
            TargetPlayerId = player.PlayerId,
            Action = AdminRoleAuditAction.Bootstrap,
            Reason = "Test bootstrap.",
            Source = "test",
            CreatedAt = TestState.Now
        });
        state.DiplomaticRelationships.Add(new DiplomaticRelationship
        {
            CycleId = cycle.CycleId,
            FirstEmpireId = empire.EmpireId,
            SecondEmpireId = Guid.NewGuid(),
            State = DiplomaticRelationshipState.Neutral,
            UpdatedAt = TestState.Now
        });
        state.EmpireDoctrineUnlocks.Add(new EmpireDoctrineUnlock
        {
            CycleId = cycle.CycleId,
            EmpireId = empire.EmpireId,
            DoctrineKey = EconomyProcessor.SurveyProjectionDoctrineKey,
            UnlockedTickNumber = 1,
            UnlockedAt = TestState.Now
        });

        var expected = state.Players.Count
                       + state.AdminRoleAuditRecords.Count
                       + state.Cycles.Count
                       + state.Systems.Count
                       + state.SystemLinks.Count
                       + state.Empires.Count
                       + state.Factions.Count
                       + state.MatchParticipants.Count
                       + state.EmpireResources.Count
                       + state.EmpireDoctrineUnlocks.Count
                       + state.EmpirePriorities.Count
                       + state.EmpireMetrics.Count
                       + state.CycleRankings.Count
                       + state.CycleMajorEvents.Count
                       + state.SystemHistoricalSignals.Count
                       + state.Admirals.Count
                       + state.AdmiralBattleHistories.Count
                       + state.Fleets.Count
                       + state.FleetOrders.Count
                       + state.ShipConstructions.Count
                       + state.ColonialOutposts.Count
                       + state.DiplomaticRelationships.Count
                       + state.TickLogs.Count
                       + state.Events.Count
                       + state.BattleRecords.Count
                       + state.ChronicleEntries.Count;

        Assert.Equal(expected, GameStateRecordCounter.CountCycleRecords(state, cycle.CycleId));
    }
}
