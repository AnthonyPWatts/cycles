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
}
