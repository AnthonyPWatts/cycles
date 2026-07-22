using Cycles.Application;
using Cycles.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cycles.Tests;

public sealed class WorkerHealthTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 22, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Readiness_is_healthy_after_a_current_successful_schedule_check()
    {
        var health = CreateReadyHealth();

        var result = WorkerHealthEvaluator.EvaluateReadiness(
            health.Snapshot,
            new TickWorkerOptions(),
            Now);

        Assert.True(result.IsReady);
        Assert.Equal("ready", result.Report.Status);
        Assert.Equal("reachable", result.Report.Database);
        Assert.Equal("ready", result.Report.Scheduling);
        Assert.Equal("current", result.Report.TickFreshness);
    }

    [Theory]
    [InlineData(1, 0, "recoveryRequired")]
    [InlineData(0, 1, "suspiciousRunningAttempt")]
    public void Readiness_distinguishes_recovery_and_suspicious_running_attempts(
        int recoveryBlockedCycles,
        int suspiciousRunningAttempts,
        string expectedSchedulingStatus)
    {
        var health = new WorkerHealthState();
        health.MarkStarted(enabled: true);
        health.MarkScheduleChecked(
            Now,
            new WorkerScheduleStatus(
                activeScheduledCycleCount: 1,
                recoveryBlockedCycles,
                suspiciousRunningAttempts,
                Now.AddMinutes(30)));

        var result = WorkerHealthEvaluator.EvaluateReadiness(
            health.Snapshot,
            new TickWorkerOptions(),
            Now);

        Assert.False(result.IsReady);
        Assert.Equal(expectedSchedulingStatus, result.Report.Scheduling);
    }

    [Fact]
    public void Readiness_reports_database_failure_without_discarding_last_success_time()
    {
        var health = CreateReadyHealth();
        health.MarkPollFailed(Now.AddSeconds(1));

        var result = WorkerHealthEvaluator.EvaluateReadiness(
            health.Snapshot,
            new TickWorkerOptions(),
            Now.AddSeconds(1));

        Assert.False(result.IsReady);
        Assert.Equal("unreachable", result.Report.Database);
        Assert.Equal(Now, result.Report.LastSuccessfulPollAt);
    }

    [Fact]
    public void Readiness_distinguishes_a_stale_poll_from_an_overdue_tick()
    {
        var stalePollHealth = CreateReadyHealth();
        var stalePoll = WorkerHealthEvaluator.EvaluateReadiness(
            stalePollHealth.Snapshot,
            new TickWorkerOptions { HealthStaleAfterSeconds = 60 },
            Now.AddSeconds(61));

        var overdueTickHealth = new WorkerHealthState();
        overdueTickHealth.MarkStarted(enabled: true);
        overdueTickHealth.MarkScheduleChecked(
            Now,
            new WorkerScheduleStatus(1, 0, 0, Now.AddSeconds(-121)));
        var overdueTick = WorkerHealthEvaluator.EvaluateReadiness(
            overdueTickHealth.Snapshot,
            new TickWorkerOptions { TickFreshnessToleranceSeconds = 120 },
            Now);

        Assert.False(stalePoll.IsReady);
        Assert.Equal("stalePoll", stalePoll.Report.TickFreshness);
        Assert.False(overdueTick.IsReady);
        Assert.Equal("overdueTick", overdueTick.Report.TickFreshness);
    }

    [Fact]
    public async Task Shutdown_waits_for_an_active_poll_and_starts_no_replacement_poll()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var schedule = new BlockingSchedule(entered, release);
        var health = new WorkerHealthState();
        using var worker = new TickWorker(
            schedule,
            schedule,
            schedule,
            Options.Create(new TickWorkerOptions { PollIntervalSeconds = 1 }),
            TimeProvider.System,
            health,
            NullLogger<TickWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));

        var stopTask = worker.StopAsync(CancellationToken.None);
        Assert.False(stopTask.IsCompleted);
        release.Set();
        await stopTask.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(health.Snapshot.Stopping);
        Assert.Equal(1, schedule.StatusChecks);
        Assert.Equal(0, schedule.ResolutionCalls);
    }

    private static WorkerHealthState CreateReadyHealth()
    {
        var health = new WorkerHealthState();
        health.MarkStarted(enabled: true);
        health.MarkScheduleChecked(
            Now,
            new WorkerScheduleStatus(1, 0, 0, Now.AddMinutes(30)));
        return health;
    }

    private sealed class BlockingSchedule(
        ManualResetEventSlim entered,
        ManualResetEventSlim release) :
        IDueCycleQuery,
        IWorkerScheduleStatusQuery,
        ICycleResolutionStore
    {
        public int StatusChecks { get; private set; }

        public int ResolutionCalls { get; private set; }

        public WorkerScheduleStatus GetWorkerScheduleStatus(
            DateTimeOffset now,
            TimeSpan runningAttemptSuspicionThreshold)
        {
            StatusChecks++;
            entered.Set();
            release.Wait();
            return new WorkerScheduleStatus(0, 0, 0, null);
        }

        public DueCycleWorkItem? GetNextDue(DateTimeOffset now) => null;

        public CycleResolutionResult ResolveIfDue(DueCycleWorkItem workItem, DateTimeOffset now)
        {
            ResolutionCalls++;
            return new CycleResolutionResult.NotDue();
        }

        public CycleResolutionResult ResolveExplicit(
            ExplicitCycleResolutionRequest request,
            DateTimeOffset now) => new CycleResolutionResult.Unavailable();
    }
}
