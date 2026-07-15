using System.Diagnostics;
using Cycles.Core;

namespace Cycles.Infrastructure.SqlServer;

public sealed record SqlServerStateProfileOptions(
    int SystemCount = GameSeeder.CanonicalGalaxySystemCount,
    int EmpireCount = 4,
    int HistoryTicks = 0,
    int Iterations = 3,
    int Seed = 71421)
{
    public void Validate()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(SystemCount, 2);
        ArgumentOutOfRangeException.ThrowIfLessThan(EmpireCount, 1);
        if (SystemCount < EmpireCount)
        {
            throw new ArgumentOutOfRangeException(nameof(SystemCount), "There must be at least one system per empire.");
        }

        ArgumentOutOfRangeException.ThrowIfNegative(HistoryTicks);
        ArgumentOutOfRangeException.ThrowIfLessThan(Iterations, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(Iterations, 20);
    }
}

public sealed record SqlServerStateProfileSample(
    int Iteration,
    int RetainedRecords,
    double ReplaceMilliseconds,
    double LoadMilliseconds,
    double GenericUpdateMilliseconds,
    double FocusedTickMilliseconds);

public sealed record SqlServerStateProfileResult(
    SqlServerStateProfileOptions Options,
    IReadOnlyList<SqlServerStateProfileSample> Samples)
{
    public double AverageReplaceMilliseconds => Samples.Average(sample => sample.ReplaceMilliseconds);
    public double AverageLoadMilliseconds => Samples.Average(sample => sample.LoadMilliseconds);
    public double AverageGenericUpdateMilliseconds => Samples.Average(sample => sample.GenericUpdateMilliseconds);
    public double AverageFocusedTickMilliseconds => Samples.Average(sample => sample.FocusedTickMilliseconds);
}

public static class SqlServerStateProfiler
{
    private static readonly DateTimeOffset ProfileStart = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static SqlServerStateProfileResult Run(string connectionString, SqlServerStateProfileOptions options)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        options.Validate();

        var store = new SqlServerGameStateStore(connectionString);
        var samples = new List<SqlServerStateProfileSample>(options.Iterations);
        for (var iteration = 1; iteration <= options.Iterations; iteration++)
        {
            var state = GameSeeder.CreateDefault(
                options.SystemCount,
                options.EmpireCount,
                options.Seed + iteration - 1,
                ProfileStart);
            var cycleId = state.GetActiveCycle()!.CycleId;
            var replaceMilliseconds = Measure(() => store.Replace(state));

            for (var tick = 1; tick <= options.HistoryTicks; tick++)
            {
                var result = store.RunTick(cycleId, ProfileStart.AddHours(tick));
                if (result.Status != TickLogStatus.Completed)
                {
                    throw new InvalidOperationException($"Profile history tick {tick} failed.");
                }
            }

            GameState? loaded = null;
            var loadMilliseconds = Measure(() => loaded = store.LoadOrCreate());
            var retainedRecords = GameStateRecordCounter.CountCycleRecords(loaded!, cycleId);
            var updateMilliseconds = Measure(() => store.Update(current =>
            {
                var cycle = current.Cycles.Single(item => item.CycleId == cycleId);
                cycle.Name = string.Concat(cycle.Name);
                return cycle.CurrentTickNumber;
            }));
            var tickMilliseconds = Measure(() =>
            {
                var result = store.RunTick(cycleId, ProfileStart.AddHours(options.HistoryTicks + 1));
                if (result.Status != TickLogStatus.Completed)
                {
                    throw new InvalidOperationException("Profile measurement tick failed.");
                }
            });

            samples.Add(new SqlServerStateProfileSample(
                iteration,
                retainedRecords,
                replaceMilliseconds,
                loadMilliseconds,
                updateMilliseconds,
                tickMilliseconds));
        }

        return new SqlServerStateProfileResult(options, samples);
    }

    private static double Measure(Action action)
    {
        var stopwatch = Stopwatch.StartNew();
        action();
        stopwatch.Stop();
        return stopwatch.Elapsed.TotalMilliseconds;
    }
}
