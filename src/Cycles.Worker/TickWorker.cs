using Cycles.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cycles.Worker;

public sealed class TickWorker(
    IDueCycleQuery dueCycles,
    ICycleResolutionStore resolutions,
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

        RunSafely(timeProvider.GetUtcNow());
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            RunSafely(timeProvider.GetUtcNow());
        }
    }

    public bool RunIfDue(DateTimeOffset now)
    {
        var workItem = dueCycles.GetNextDue(now);
        if (workItem is null)
        {
            return false;
        }

        var result = resolutions.ResolveIfDue(workItem, now);
        switch (result)
        {
            case CycleResolutionResult.Completed completed:
                logger.LogInformation(
                    "Completed scheduled tick {TickNumber} for Game {GameId}, Cycle {CycleId} with {OrderCount} orders; it was due at {NextTickAt} ({DueAgeMilliseconds} ms ago).",
                    completed.Value.TickNumber,
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId,
                    completed.Value.OrdersProcessed,
                    workItem.NextTickAt,
                    Math.Max(0, (now - workItem.NextTickAt).TotalMilliseconds));
                return true;

            case CycleResolutionResult.RecoveryRequired recovery:
                logger.LogError(
                    "Scheduled tick {TickNumber} for Game {GameId}, Cycle {CycleId} failed and requires recovery.",
                    recovery.Value.TickNumber,
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId);
                return true;

            case CycleResolutionResult.Busy:
                logger.LogDebug(
                    "Scheduled resolution for Game {GameId}, Cycle {CycleId} is busy.",
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId);
                break;

            case CycleResolutionResult.Stale:
            case CycleResolutionResult.NotDue:
                logger.LogDebug(
                    "Discarded stale scheduled work for Game {GameId}, Cycle {CycleId}, due at {NextTickAt}.",
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId,
                    workItem.NextTickAt);
                break;

            case CycleResolutionResult.Unavailable:
                logger.LogWarning(
                    "Scheduled work for Game {GameId}, Cycle {CycleId} became unavailable before resolution.",
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId);
                break;
        }

        return false;
    }

    private void RunSafely(DateTimeOffset now)
    {
        try
        {
            RunIfDue(now);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Scheduled Cycle discovery or resolution failed; the Worker will retry on the next poll.");
        }
    }
}

public sealed class TickWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 30;
}
