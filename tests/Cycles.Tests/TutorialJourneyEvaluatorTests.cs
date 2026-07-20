using Cycles.Application;
using Cycles.Core;

namespace Cycles.Tests;

public sealed class TutorialJourneyEvaluatorTests
{
    private static readonly DateTimeOffset StartedAt = new(2026, 7, 20, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Acknowledgement_and_cross_cycle_facts_cannot_manufacture_move_progress()
    {
        var fixture = CreateFixture();
        var foreignCycleId = Guid.NewGuid();
        fixture.State.FleetOrders.Add(new FleetOrder
        {
            CycleId = foreignCycleId,
            FleetId = fixture.HomeGuardId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = fixture.FirstlightId,
            Status = FleetOrderStatus.Processed,
            ProcessedTick = 1,
            CreatedAt = StartedAt.AddMinutes(1)
        });
        fixture.State.Events.Add(new EventRecord
        {
            CycleId = foreignCycleId,
            TickNumber = 1,
            EventType = EventType.FleetMoved,
            SystemId = fixture.FirstlightId,
            EmpireId = fixture.Context.EmpireId,
            CreatedAt = StartedAt.AddMinutes(2)
        });

        var journey = TutorialJourneyEvaluator.Evaluate(
            fixture.State,
            fixture.Context,
            fixture.Run,
            new HashSet<string>(StringComparer.Ordinal)
            {
                TutorialJourneyEvaluator.MoveOutcomeAcknowledgement
            });

        Assert.Equal("T0", journey.CurrentLesson?.Key);
        Assert.False(journey.CurrentLesson?.MechanicalEvidence.Satisfied);
        Assert.Equal("WaitingForEvidence", journey.CurrentLesson?.CompletionState);
    }

    [Fact]
    public void Owned_processed_move_and_matching_event_unlock_the_next_lesson_after_acknowledgement()
    {
        var fixture = CreateFixture();
        var order = new FleetOrder
        {
            CycleId = fixture.Context.CycleId,
            FleetId = fixture.HomeGuardId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = fixture.FirstlightId,
            Status = FleetOrderStatus.Processed,
            ProcessedTick = 1,
            CreatedAt = StartedAt.AddMinutes(1)
        };
        var movement = new EventRecord
        {
            CycleId = fixture.Context.CycleId,
            TickNumber = 1,
            EventType = EventType.FleetMoved,
            SystemId = fixture.FirstlightId,
            EmpireId = fixture.Context.EmpireId,
            CreatedAt = StartedAt.AddMinutes(2)
        };
        fixture.State.FleetOrders.Add(order);
        fixture.State.Events.Add(movement);

        var journey = TutorialJourneyEvaluator.Evaluate(
            fixture.State,
            fixture.Context,
            fixture.Run,
            new HashSet<string>(StringComparer.Ordinal)
            {
                TutorialJourneyEvaluator.MoveOutcomeAcknowledgement
            });

        Assert.Equal("Completed", journey.Lessons[0].CompletionState);
        Assert.Equal([order.FleetOrderId, movement.EventId], journey.Lessons[0].MechanicalEvidence.FactIds);
        Assert.Equal("T1", journey.CurrentLesson?.Key);
    }

    [Fact]
    public void Recovery_state_is_reported_without_inventing_or_rewinding_progress()
    {
        var fixture = CreateFixture();
        fixture.State.Cycles.Single().Status = CycleStatus.RecoveryRequired;

        var journey = TutorialJourneyEvaluator.Evaluate(
            fixture.State,
            fixture.Context,
            fixture.Run,
            new HashSet<string>(StringComparer.Ordinal));

        Assert.Equal("RecoveryRequired", journey.JourneyStatus);
        Assert.False(journey.CanResolve);
        Assert.True(journey.CanStartFresh);
        Assert.Equal("T0", journey.CurrentLesson?.Key);
    }

    [Fact]
    public void Current_lesson_resolves_only_after_its_required_intention_is_queued()
    {
        var fixture = CreateFixture();
        fixture.State.FleetOrders.Add(new FleetOrder
        {
            CycleId = fixture.Context.CycleId,
            FleetId = fixture.HomeGuardId,
            OrderType = FleetOrderType.Hold,
            Status = FleetOrderStatus.Pending,
            ExecuteAfterTick = 2,
            CreatedAt = StartedAt.AddMinutes(1)
        });

        var wrongIntention = TutorialJourneyEvaluator.Evaluate(
            fixture.State,
            fixture.Context,
            fixture.Run,
            new HashSet<string>(StringComparer.Ordinal));
        Assert.False(wrongIntention.CanResolve);
        Assert.Equal("Ready", wrongIntention.CurrentLesson?.EntryState);

        fixture.State.FleetOrders.Clear();
        fixture.State.FleetOrders.Add(new FleetOrder
        {
            CycleId = fixture.Context.CycleId,
            FleetId = fixture.HomeGuardId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = fixture.FirstlightId,
            Status = FleetOrderStatus.Pending,
            ExecuteAfterTick = 2,
            CreatedAt = StartedAt.AddMinutes(2)
        });

        var ready = TutorialJourneyEvaluator.Evaluate(
            fixture.State,
            fixture.Context,
            fixture.Run,
            new HashSet<string>(StringComparer.Ordinal));
        Assert.True(ready.CanResolve);
        Assert.Equal("WaitingForResolution", ready.CurrentLesson?.EntryState);
    }

    private static Fixture CreateFixture()
    {
        var playerId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        var empireId = Guid.NewGuid();
        var firstlightId = Guid.NewGuid();
        var greenwaterId = Guid.NewGuid();
        var context = new GameCommandContext(
            new GameAccessContext(playerId, gameId, Guid.NewGuid(), GamePermission.Read),
            cycleId,
            Guid.NewGuid(),
            empireId);
        var homeGuardId = Guid.NewGuid();
        var state = new GameState
        {
            Cycles =
            [
                new Cycle
                {
                    CycleId = cycleId,
                    GameId = gameId,
                    Status = CycleStatus.Active,
                    CurrentTickNumber = 1
                }
            ],
            Systems =
            [
                new GalaxySystem { SystemId = firstlightId, CycleId = cycleId, SystemName = "Firstlight" },
                new GalaxySystem { SystemId = greenwaterId, CycleId = cycleId, SystemName = "Greenwater" }
            ],
            Fleets =
            [
                new Fleet { FleetId = homeGuardId, CycleId = cycleId, EmpireId = empireId, FleetName = "Home Guard", ShipCount = 20 },
                new Fleet { CycleId = cycleId, EmpireId = empireId, FleetName = "Survey Wing", ShipCount = 12 },
                new Fleet { CycleId = cycleId, EmpireId = empireId, FleetName = "Vanguard", ShipCount = 24 }
            ]
        };
        var run = new TutorialRunSnapshot(
            Guid.NewGuid(),
            gameId,
            cycleId,
            playerId,
            GameProfileCatalogue.TwinReachesProfileKey,
            TutorialJourneyEvaluator.FoundationsDefinitionVersion,
            TutorialRunStatus.Active,
            StartedAt,
            SupersededByTutorialRunId: null,
            EndedAt: null);
        return new Fixture(state, context, run, homeGuardId, firstlightId);
    }

    private sealed record Fixture(
        GameState State,
        GameCommandContext Context,
        TutorialRunSnapshot Run,
        Guid HomeGuardId,
        Guid FirstlightId);
}
