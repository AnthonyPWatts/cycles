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
        var result = store.RunTickIfDue(now);
        if (result is null)
        {
            return false;
        }

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
