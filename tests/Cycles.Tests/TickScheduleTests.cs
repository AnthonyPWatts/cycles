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
        var store = new InMemoryGameStateStore(state);
        var worker = new TickWorker(
            store,
            Options.Create(new TickWorkerOptions()),
            TimeProvider.System,
            NullLogger<TickWorker>.Instance);

        var ran = worker.RunIfDue(TestState.Now);

        Assert.True(ran);
        Assert.Equal(1, store.RunTickCalls);
        Assert.Equal(1, state.GetActiveCycle()!.CurrentTickNumber);
    }

    [Fact]
    public void Worker_does_not_run_when_store_reports_tick_is_not_due()
    {
        var state = TestState.CreateSingleEmpireState();
        var store = new InMemoryGameStateStore(state);
        var worker = new TickWorker(
            store,
            Options.Create(new TickWorkerOptions()),
            TimeProvider.System,
            NullLogger<TickWorker>.Instance);

        var ran = worker.RunIfDue(state.GetActiveCycle()!.StartAt.AddTicks(-1));

        Assert.False(ran);
        Assert.Equal(0, store.RunTickCalls);
        Assert.Equal(0, state.GetActiveCycle()!.CurrentTickNumber);
    }

    private sealed class InMemoryGameStateStore(GameState state) : IGameStateStore
    {
        public string Description => "In-memory worker state";
        public int RunTickCalls { get; private set; }
        public GameState LoadOrCreate() => state;
        public T Update<T>(Func<GameState, T> update) => update(state);

        public TickResult RunTick(DateTimeOffset now)
        {
            RunTickCalls++;
            return new TickEngine().RunTick(state, state.GetActiveCycle()!.CycleId, now);
        }

        public TickResult? RunTickIfDue(DateTimeOffset now) =>
            TickSchedule.IsDue(state, now) ? RunTick(now) : null;

        public void Replace(GameState replacement) => throw new NotSupportedException();
    }
}
