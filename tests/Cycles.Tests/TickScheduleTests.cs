using Cycles.Application;
using Cycles.Core;
using Cycles.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cycles.Tests;

public sealed class TickScheduleTests
{
    [Fact]
    public void First_tick_is_due_when_cycle_start_has_arrived()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;

        Assert.True(TickSchedule.IsDue(state, cycle.StartAt));
        Assert.False(TickSchedule.IsDue(state, cycle.StartAt.AddTicks(-1)));
    }

    [Fact]
    public void Next_tick_uses_last_completion_time_and_cycle_cadence()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.TickLengthMinutes = 60;
        state.TickLogs.Add(new TickLog
        {
            CycleId = cycle.CycleId,
            TickNumber = 1,
            StartedAt = TestState.Now,
            CompletedAt = TestState.Now.AddMinutes(2),
            Status = TickLogStatus.Completed
        });

        Assert.False(TickSchedule.IsDue(state, TestState.Now.AddMinutes(61)));
        Assert.True(TickSchedule.IsDue(state, TestState.Now.AddMinutes(62)));
    }

    [Fact]
    public void Recovery_required_cycle_is_not_due()
    {
        var state = TestState.CreateSingleEmpireState();
        var cycle = state.GetActiveCycle()!;
        cycle.Status = CycleStatus.RecoveryRequired;

        Assert.False(TickSchedule.IsDue(state, TestState.Now.AddDays(1)));
    }

    [Fact]
    public void Worker_runs_one_due_tick_through_store_boundary()
    {
        var state = TestState.CreateSingleEmpireState();
        var store = new InMemoryCycleScheduler(state);
        var worker = new TickWorker(
            store,
            store,
            Options.Create(new TickWorkerOptions()),
            TimeProvider.System,
            NullLogger<TickWorker>.Instance);

        var ran = worker.RunIfDue(TestState.Now);

        Assert.True(ran);
        Assert.Equal(1, store.ResolveCalls);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public void Worker_does_not_run_when_store_reports_tick_is_not_due()
    {
        var state = TestState.CreateSingleEmpireState();
        var store = new InMemoryCycleScheduler(state);
        var worker = new TickWorker(
            store,
            store,
            Options.Create(new TickWorkerOptions()),
            TimeProvider.System,
            NullLogger<TickWorker>.Instance);

        var ran = worker.RunIfDue(state.GetActiveCycle()!.StartAt.AddTicks(-1));

        Assert.False(ran);
        Assert.Equal(0, store.ResolveCalls);
        Assert.Equal(0, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public void Self_paced_cycle_is_never_discovered_by_the_scheduled_worker()
    {
        var state = TestState.CreateSingleEmpireState();
        state.GetActiveCycle()!.SchedulingMode = CycleSchedulingMode.SelfPaced;
        var store = new InMemoryCycleScheduler(state);
        var worker = new TickWorker(
            store,
            store,
            Options.Create(new TickWorkerOptions()),
            TimeProvider.System,
            NullLogger<TickWorker>.Instance);

        var ran = worker.RunIfDue(TestState.Now.AddDays(1));

        Assert.False(ran);
        Assert.Equal(0, store.ResolveCalls);
    }

    private sealed class InMemoryCycleScheduler(GameState state) : IDueCycleQuery, ICycleResolutionStore
    {
        private readonly Guid gameId = Guid.NewGuid();

        public int ResolveCalls { get; private set; }

        public DueCycleWorkItem? GetNextDue(DateTimeOffset now)
        {
            var cycle = state.GetActiveCycle();
            if (cycle is null || !TickSchedule.IsDue(state, now))
            {
                return null;
            }

            var nextTickAt = cycle.NextTickAt ?? cycle.StartAt;
            return new DueCycleWorkItem(
                new GameCycleScope(gameId, cycle.CycleId),
                nextTickAt);
        }

        public CycleResolutionResult ResolveIfDue(
            DueCycleWorkItem workItem,
            DateTimeOffset now)
        {
            ResolveCalls++;
            var cycle = state.GetActiveCycle();
            if (cycle is null || workItem.Scope != new GameCycleScope(gameId, cycle.CycleId))
            {
                return new CycleResolutionResult.Unavailable();
            }

            if (!TickSchedule.IsDue(state, now))
            {
                return new CycleResolutionResult.NotDue();
            }

            return ResolveScope(workItem.Scope, now);
        }

        public CycleResolutionResult ResolveExplicit(
            ExplicitCycleResolutionRequest request,
            DateTimeOffset now) =>
            ResolveScope(request.Scope, now);

        private CycleResolutionResult ResolveScope(GameCycleScope scope, DateTimeOffset now)
        {
            var cycle = state.GetActiveCycle();
            if (cycle is null || scope != new GameCycleScope(gameId, cycle.CycleId))
            {
                return new CycleResolutionResult.Unavailable();
            }

            var result = new TickEngine().RunTick(state, cycle.CycleId, now);
            return result.Status == TickLogStatus.Completed
                ? new CycleResolutionResult.Completed(result)
                : new CycleResolutionResult.RecoveryRequired(result);
        }
    }
}
