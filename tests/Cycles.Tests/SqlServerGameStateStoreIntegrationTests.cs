using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class SqlServerGameStateStoreIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = "CYCLES_SQL_INTEGRATION_CONNECTION_STRING";

    [Fact]
    public void Store_round_trips_and_updates_state_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 90210);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");

        store.Replace(state);

        var loaded = store.LoadOrCreate();
        Assert.Equal(cycle.CycleId, loaded.GetActiveCycle()?.CycleId);
        Assert.Equal(8, loaded.Systems.Count);
        Assert.Equal(2, loaded.Empires.Count);

        var result = store.Update(current =>
        {
            var activeCycle = current.GetActiveCycle()
                ?? throw new InvalidOperationException("Stored state must contain an active Cycle.");
            return new TickEngine().RunTick(current, activeCycle.CycleId, DateTimeOffset.Parse("2026-06-23T12:00:00Z"));
        });

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        Assert.Equal(1, updated.GetActiveCycle()?.CurrentTickNumber);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
        Assert.Contains(updated.Events, item => item.CycleId == cycle.CycleId && item.TickNumber == 1 && item.EventType == EventType.ResourcesGenerated);
    }

    [Fact]
    public void Store_persists_order_submission_and_tick_outcome_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateMovementState(linkSystems: true);
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        var fleet = state.Fleets.Single();
        var destination = state.Systems.Single(system => system.SystemName == "Destination");

        store.Replace(state);

        var order = store.Update(current => OrderService.SubmitMoveOrder(
            current,
            fleet.FleetId,
            destination.SystemId,
            TestState.Now));

        Assert.Equal(FleetOrderStatus.Pending, order.Status);

        var afterOrder = store.LoadOrCreate();
        Assert.Single(afterOrder.FleetOrders, item => item.FleetOrderId == order.FleetOrderId);

        var result = store.Update(current => new TickEngine().RunTick(current, cycle.CycleId, TestState.Now));

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        var movedFleet = updated.Fleets.Single(item => item.FleetId == fleet.FleetId);
        var processedOrder = updated.FleetOrders.Single(item => item.FleetOrderId == order.FleetOrderId);

        Assert.Equal(destination.SystemId, movedFleet.CurrentSystemId);
        Assert.Equal(FleetOrderStatus.Processed, processedOrder.Status);
        Assert.Equal(1, processedOrder.ProcessedTick);
        Assert.Contains(updated.Events, item => item.EventType == EventType.FleetMoved && item.SystemId == destination.SystemId);
    }

    [Fact]
    public void Store_rolls_back_when_duplicate_running_tick_is_detected()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            Status = TickLogStatus.Running
        });

        store.Replace(state);

        var ex = Assert.Throws<InvalidOperationException>(
            () => store.Update(current => new TickEngine().RunTick(current, cycle.CycleId, TestState.Now)));

        Assert.Contains("already running", ex.Message, StringComparison.OrdinalIgnoreCase);

        var updated = store.LoadOrCreate();
        Assert.Equal(0, updated.GetActiveCycle()?.CurrentTickNumber);
        Assert.Single(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.Status == TickLogStatus.Running);
    }

    [Fact]
    public void Store_updates_rows_and_removes_missing_rows_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = GameSeeder.CreateDefault(systemCount: 8, empireCount: 2, seed: 451);
        var retainedPlayer = state.Players[0];
        var removedPlayer = state.Players[1];
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");

        store.Replace(state);

        store.Update(current =>
        {
            var player = current.Players.Single(item => item.PlayerId == retainedPlayer.PlayerId);
            player.Username = "renamed-player";
            return player.PlayerId;
        });

        var updated = store.LoadOrCreate();
        Assert.Contains(updated.Players, item => item.PlayerId == retainedPlayer.PlayerId && item.Username == "renamed-player");
        Assert.Contains(updated.Players, item => item.PlayerId == removedPlayer.PlayerId);
        Assert.Equal(cycle.CycleId, updated.GetActiveCycle()?.CycleId);

        var replacement = GameSeeder.CreateDefault(systemCount: 6, empireCount: 1, seed: 452);
        store.Replace(replacement);

        var replaced = store.LoadOrCreate();
        Assert.DoesNotContain(replaced.Players, item => item.PlayerId == removedPlayer.PlayerId);
        Assert.DoesNotContain(replaced.Cycles, item => item.CycleId == cycle.CycleId);
        Assert.Single(replaced.Cycles);
        Assert.Equal(6, replaced.Systems.Count);
    }

    [Fact]
    public void Store_allows_failed_and_completed_logs_for_retried_tick_when_connection_string_is_configured()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var store = new SqlServerGameStateStore(connectionString);
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle() ?? throw new InvalidOperationException("Seed state must contain an active Cycle.");
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now.AddMinutes(-5),
            CompletedAt = TestState.Now.AddMinutes(-5),
            Status = TickLogStatus.Failed,
            DiagnosticLog = "failed attempt"
        });

        store.Replace(state);

        var result = store.Update(current => new TickEngine().RunTick(current, cycle.CycleId, TestState.Now));

        Assert.Equal(TickLogStatus.Completed, result.Status);

        var updated = store.LoadOrCreate();
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Failed);
        Assert.Contains(updated.TickLogs, log => log.CycleId == cycle.CycleId && log.TickNumber == 1 && log.Status == TickLogStatus.Completed);
    }
}
