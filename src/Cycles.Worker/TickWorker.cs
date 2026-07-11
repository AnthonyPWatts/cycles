using Cycles.Core;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cycles.Worker;

public sealed class TickWorker(
    IGameStateStore store,
    IOptions<TickWorkerOptions> options,
    TimeProvider timeProvider,
    ILogger<TickWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Scheduled tick processing is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 1, 300));
        using var timer = new PeriodicTimer(pollInterval, timeProvider);

        RunIfDue(timeProvider.GetUtcNow());
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            RunIfDue(timeProvider.GetUtcNow());
        }
    }

    public bool RunIfDue(DateTimeOffset now)
    {
        var state = store.LoadOrCreate();
        if (!TickSchedule.IsDue(state, now))
        {
            return false;
        }

        var result = store.RunTick(now);
        if (result.Status == TickLogStatus.Completed)
        {
            logger.LogInformation("Completed scheduled tick {TickNumber} with {OrderCount} orders.", result.TickNumber, result.OrdersProcessed);
        }
        else
        {
            logger.LogError("Scheduled tick {TickNumber} failed and requires recovery.", result.TickNumber);
        }

        return true;
    }
}

public sealed class TickWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 30;
}

public static class TickSchedule
{
    public static bool IsDue(GameState state, DateTimeOffset now)
    {
        var cycle = state.GetActiveCycle();
        if (cycle is null)
        {
            return false;
        }

        var lastCompletedAt = state.TickLogs
            .Where(item => item.CycleId == cycle.CycleId
                           && item.Status == TickLogStatus.Completed
                           && item.CompletedAt.HasValue)
            .Max(item => item.CompletedAt);
        var cadence = TimeSpan.FromMinutes(Math.Max(1, cycle.TickLengthMinutes));
        var nextDueAt = lastCompletedAt.HasValue
            ? lastCompletedAt.Value + cadence
            : cycle.StartAt;

        return now >= nextDueAt;
    }
}
