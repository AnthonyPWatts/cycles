using Cycles.Core;

namespace Cycles.Tests;

public sealed class GameStateTests
{
    [Fact]
    public void DeepClone_preserves_values_without_sharing_mutable_entities()
    {
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 612);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var fleet = state.Fleets.First();
        var targetSystem = state.Systems.First(system => system.SystemId != fleet.CurrentSystemId);
        var attacker = state.Empires[0];
        var defender = state.Empires[1];
        var battle = new BattleRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            SystemId = fleet.CurrentSystemId,
            AttackerEmpireId = attacker.EmpireId,
            DefenderEmpireId = defender.EmpireId,
            AttackerFleetIds = fleet.FleetId.ToString(),
            DefenderFleetIds = state.Fleets.Last().FleetId.ToString(),
            AttackerShipsBefore = 50,
            DefenderShipsBefore = 40,
            AttackerLosses = 10,
            DefenderLosses = 20,
            Outcome = BattleOutcome.AttackerVictory,
            FactJson = """{"kind":"test"}""",
            CreatedAt = TestState.Now
        };
        var eventRecord = new EventRecord
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            EventType = EventType.CombatResolved,
            SystemId = fleet.CurrentSystemId,
            EmpireId = attacker.EmpireId,
            Severity = EventSeverity.High,
            FactJson = battle.FactJson,
            DisplayText = "A test battle was resolved.",
            CreatedAt = TestState.Now
        };

        state.FleetOrders.Add(new FleetOrder
        {
            CycleId = cycle.CycleId,
            FleetId = fleet.FleetId,
            OrderType = FleetOrderType.MoveFleet,
            TargetSystemId = targetSystem.SystemId,
            SubmitTick = 0,
            ExecuteAfterTick = 1,
            CreatedAt = TestState.Now
        });
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            CompletedAt = TestState.Now,
            Status = TickLogStatus.Completed,
            DiagnosticLog = "Processed 1 order."
        });
        state.Events.Add(eventRecord);
        state.BattleRecords.Add(battle);
        state.ChronicleEntries.Add(new ChronicleEntry
        {
            SourceEventId = eventRecord.EventId,
            SourceBattleId = battle.BattleId,
            CycleId = cycle.CycleId,
            SystemId = fleet.CurrentSystemId,
            Title = "Test Chronicle",
            EntryType = ChronicleEntryType.Battle,
            ImportanceScore = 80,
            FactualSummary = "A test battle was preserved.",
            NarrativeText = "A test battle was preserved.",
            CreatedAt = TestState.Now
        });

        var clone = state.DeepClone();

        Assert.Equal(state.Players.Count, clone.Players.Count);
        Assert.Equal(state.Cycles.Count, clone.Cycles.Count);
        Assert.Equal(state.Empires.Count, clone.Empires.Count);
        Assert.Equal(state.EmpireResources.Count, clone.EmpireResources.Count);
        Assert.Equal(state.EmpirePriorities.Count, clone.EmpirePriorities.Count);
        Assert.Equal(state.Systems.Count, clone.Systems.Count);
        Assert.Equal(state.SystemLinks.Count, clone.SystemLinks.Count);
        Assert.Equal(state.Fleets.Count, clone.Fleets.Count);
        Assert.Equal(state.FleetOrders.Count, clone.FleetOrders.Count);
        Assert.Equal(state.TickLogs.Count, clone.TickLogs.Count);
        Assert.Equal(state.Events.Count, clone.Events.Count);
        Assert.Equal(state.BattleRecords.Count, clone.BattleRecords.Count);
        Assert.Equal(state.ChronicleEntries.Count, clone.ChronicleEntries.Count);

        Assert.Equal(state.Fleets[0].FleetId, clone.Fleets[0].FleetId);
        Assert.NotSame(state.Fleets[0], clone.Fleets[0]);
        Assert.Equal(state.FleetOrders[0].FleetOrderId, clone.FleetOrders[0].FleetOrderId);
        Assert.NotSame(state.FleetOrders[0], clone.FleetOrders[0]);
        Assert.Equal(state.Events.Last().EventId, clone.Events.Last().EventId);
        Assert.NotSame(state.Events.Last(), clone.Events.Last());

        clone.Fleets[0].ShipCount += 10;
        clone.FleetOrders[0].Status = FleetOrderStatus.Processed;
        clone.Events.Last().DisplayText = "Changed in clone.";

        Assert.NotEqual(state.Fleets[0].ShipCount, clone.Fleets[0].ShipCount);
        Assert.Equal(FleetOrderStatus.Pending, state.FleetOrders[0].Status);
        Assert.Equal("A test battle was resolved.", state.Events.Last().DisplayText);
    }
}
