using Cycles.Application;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace Cycles.Worker;

public sealed class TickWorker(
    IDueCycleQuery dueCycles,
    IWorkerScheduleStatusQuery scheduleStatus,
    ICycleResolutionStore resolutions,
    IOptions<TickWorkerOptions> options,
    TimeProvider timeProvider,
    WorkerHealthState health,
    ILogger<TickWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        health.MarkStarted(options.Value.Enabled);
        if (!options.Value.Enabled)
        {
            logger.LogInformation("Scheduled tick processing is disabled.");
            return;
        }

        var pollInterval = TimeSpan.FromSeconds(Math.Clamp(options.Value.PollIntervalSeconds, 1, 300));
        using var timer = new PeriodicTimer(pollInterval, timeProvider);

        logger.LogInformation(
            "Scheduled tick Worker started with a {PollIntervalSeconds}-second batch-one polling interval.",
            pollInterval.TotalSeconds);
        await Task.Yield();
        try
        {
            RunSafely(timeProvider.GetUtcNow());
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                RunSafely(timeProvider.GetUtcNow());
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            logger.LogInformation("Scheduled tick Worker stopped after cancellation; no new poll will start.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        health.MarkStopping();
        logger.LogInformation("Scheduled tick Worker shutdown requested; no new poll will start and the host will wait for active resolution up to its shutdown timeout.");
        await base.StopAsync(cancellationToken);
    }

    public bool RunIfDue(DateTimeOffset now)
    {
        var workItem = dueCycles.GetNextDue(now);
        if (workItem is null)
        {
            return false;
        }

        var resolutionStarted = Stopwatch.GetTimestamp();
        var result = resolutions.ResolveIfDue(workItem, now);
        var durationMilliseconds = Stopwatch.GetElapsedTime(resolutionStarted).TotalMilliseconds;
        switch (result)
        {
            case CycleResolutionResult.Completed completed:
                logger.LogInformation(
                    "Completed scheduled tick {TickNumber} for Game {GameId}, Cycle {CycleId} with {OrderCount} orders in {DurationMilliseconds} ms; it was due at {NextTickAt} ({DueAgeMilliseconds} ms ago).",
                    completed.Value.TickNumber,
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId,
                    completed.Value.OrdersProcessed,
                    durationMilliseconds,
                    workItem.NextTickAt,
                    Math.Max(0, (now - workItem.NextTickAt).TotalMilliseconds));
                return true;

            case CycleResolutionResult.RecoveryRequired recovery:
                logger.LogError(
                    "Scheduled tick {TickNumber} for Game {GameId}, Cycle {CycleId} failed after {DurationMilliseconds} ms and requires operator diagnostics and recovery.",
                    recovery.Value.TickNumber,
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId,
                    durationMilliseconds);
                return true;

            case CycleResolutionResult.Busy:
                logger.LogDebug(
                    "Scheduled resolution for Game {GameId}, Cycle {CycleId} was busy after {DurationMilliseconds} ms.",
                    workItem.Scope.GameId,
                    workItem.Scope.CycleId,
                    durationMilliseconds);
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
        var pollStarted = Stopwatch.GetTimestamp();
        try
        {
            var status = ReadScheduleStatus(now);
            health.MarkScheduleChecked(now, status);
            LogScheduleStatus(status);

            if (RunIfDue(now))
            {
                health.MarkScheduleChecked(now, ReadScheduleStatus(now));
            }
            logger.LogDebug(
                "Scheduled tick poll completed in {DurationMilliseconds} ms.",
                Stopwatch.GetElapsedTime(pollStarted).TotalMilliseconds);
        }
        catch (Exception exception)
        {
            health.MarkPollFailed(now);
            logger.LogError(
                exception,
                "Scheduled Cycle discovery or resolution failed after {DurationMilliseconds} ms; the Worker will retry on the next poll.",
                Stopwatch.GetElapsedTime(pollStarted).TotalMilliseconds);
        }
    }

    private WorkerScheduleStatus ReadScheduleStatus(DateTimeOffset now) =>
        scheduleStatus.GetWorkerScheduleStatus(
            now,
            TimeSpan.FromMinutes(Math.Clamp(options.Value.RunningAttemptSuspicionMinutes, 1, 1440)));

    private void LogScheduleStatus(WorkerScheduleStatus status)
    {
        logger.LogDebug(
            "Scheduled tick due check found {ActiveScheduledCycleCount} active scheduled Cycles, {RecoveryBlockedCycleCount} recovery blocks and {SuspiciousRunningAttemptCount} suspicious running attempts; earliest deadline is {EarliestNextTickAt}.",
            status.ActiveScheduledCycleCount,
            status.RecoveryBlockedCycleCount,
            status.SuspiciousRunningAttemptCount,
            status.EarliestNextTickAt);
        if (status.RecoveryBlockedCycleCount > 0)
        {
            logger.LogWarning(
                "Scheduled processing is blocked by {RecoveryBlockedCycleCount} recovery-required Cycles; use operator diagnostics before an explicit repair or retry.",
                status.RecoveryBlockedCycleCount);
        }
        if (status.SuspiciousRunningAttemptCount > 0)
        {
            logger.LogWarning(
                "Found {SuspiciousRunningAttemptCount} persisted running tick attempts older than {RunningAttemptSuspicionMinutes} minutes; inspect them before using explicit abandonment.",
                status.SuspiciousRunningAttemptCount,
                options.Value.RunningAttemptSuspicionMinutes);
        }
    }
}

public sealed class TickWorkerOptions
{
    public bool Enabled { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 30;
    public int RunningAttemptSuspicionMinutes { get; set; } = 5;
    public int HealthStaleAfterSeconds { get; set; } = 120;
    public int TickFreshnessToleranceSeconds { get; set; } = 120;
}
