using Cycles.Application;

namespace Cycles.Worker;

public enum WorkerDatabaseHealth
{
    Unknown,
    Reachable,
    Unreachable
}

public sealed record WorkerHealthSnapshot(
    bool Enabled,
    bool Started,
    bool Stopping,
    WorkerDatabaseHealth Database,
    DateTimeOffset? LastSuccessfulPollAt,
    DateTimeOffset? LastFailedPollAt,
    WorkerScheduleStatus? Schedule);

public sealed class WorkerHealthState
{
    private readonly object sync = new();
    private WorkerHealthSnapshot snapshot = new(
        Enabled: true,
        Started: false,
        Stopping: false,
        WorkerDatabaseHealth.Unknown,
        LastSuccessfulPollAt: null,
        LastFailedPollAt: null,
        Schedule: null);

    public WorkerHealthSnapshot Snapshot
    {
        get
        {
            lock (sync)
            {
                return snapshot;
            }
        }
    }

    public void MarkStarted(bool enabled)
    {
        lock (sync)
        {
            snapshot = snapshot with
            {
                Enabled = enabled,
                Started = true,
                Stopping = false
            };
        }
    }

    public void MarkScheduleChecked(DateTimeOffset checkedAt, WorkerScheduleStatus schedule)
    {
        ArgumentNullException.ThrowIfNull(schedule);
        lock (sync)
        {
            snapshot = snapshot with
            {
                Database = WorkerDatabaseHealth.Reachable,
                LastSuccessfulPollAt = checkedAt,
                Schedule = schedule
            };
        }
    }

    public void MarkPollFailed(DateTimeOffset failedAt)
    {
        lock (sync)
        {
            snapshot = snapshot with
            {
                Database = WorkerDatabaseHealth.Unreachable,
                LastFailedPollAt = failedAt
            };
        }
    }

    public void MarkStopping()
    {
        lock (sync)
        {
            snapshot = snapshot with { Stopping = true };
        }
    }
}

public sealed record WorkerReadinessReport(
    string Status,
    string Database,
    string Scheduling,
    string TickFreshness,
    DateTimeOffset? LastSuccessfulPollAt);

public static class WorkerHealthEvaluator
{
    public static (bool IsReady, WorkerReadinessReport Report) EvaluateReadiness(
        WorkerHealthSnapshot snapshot,
        TickWorkerOptions options,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(options);

        var database = snapshot.Database switch
        {
            WorkerDatabaseHealth.Reachable => "reachable",
            WorkerDatabaseHealth.Unreachable => "unreachable",
            _ => "unknown"
        };
        var scheduling = GetSchedulingStatus(snapshot);
        var tickFreshness = GetTickFreshness(snapshot, options, now);
        var isReady = snapshot.Enabled
            && snapshot.Started
            && !snapshot.Stopping
            && snapshot.Database == WorkerDatabaseHealth.Reachable
            && scheduling == "ready"
            && tickFreshness == "current";

        return (isReady, new WorkerReadinessReport(
            isReady ? "ready" : "notReady",
            database,
            scheduling,
            tickFreshness,
            snapshot.LastSuccessfulPollAt));
    }

    private static string GetSchedulingStatus(WorkerHealthSnapshot snapshot)
    {
        if (!snapshot.Enabled)
        {
            return "disabled";
        }
        if (!snapshot.Started)
        {
            return "starting";
        }
        if (snapshot.Stopping)
        {
            return "stopping";
        }
        if (snapshot.Schedule is null)
        {
            return "unknown";
        }
        if (snapshot.Schedule.RecoveryBlockedCycleCount > 0)
        {
            return "recoveryRequired";
        }
        if (snapshot.Schedule.SuspiciousRunningAttemptCount > 0)
        {
            return "suspiciousRunningAttempt";
        }

        return "ready";
    }

    private static string GetTickFreshness(
        WorkerHealthSnapshot snapshot,
        TickWorkerOptions options,
        DateTimeOffset now)
    {
        if (snapshot.LastSuccessfulPollAt is null || snapshot.Schedule is null)
        {
            return "unknown";
        }

        var pollIntervalSeconds = Math.Clamp(options.PollIntervalSeconds, 1, 300);
        var staleAfter = TimeSpan.FromSeconds(Math.Clamp(
            options.HealthStaleAfterSeconds,
            pollIntervalSeconds * 2,
            3600));
        if (now - snapshot.LastSuccessfulPollAt > staleAfter)
        {
            return "stalePoll";
        }

        var tickTolerance = TimeSpan.FromSeconds(Math.Clamp(
            options.TickFreshnessToleranceSeconds,
            pollIntervalSeconds,
            3600));
        if (snapshot.Schedule.EarliestNextTickAt is { } nextTickAt
            && now - nextTickAt > tickTolerance)
        {
            return "overdueTick";
        }

        return "current";
    }
}
