using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class SqlServerStateProfilerTests
{
    [Fact]
    public void ProfileOptionsAcceptBoundedValidDimensions()
    {
        var options = new SqlServerStateProfileOptions(
            SystemCount: 24,
            EmpireCount: 4,
            HistoryTicks: 100,
            Iterations: 3);

        options.Validate();
    }

    [Theory]
    [InlineData(1, 2, 0, 1)]
    [InlineData(24, 4, -1, 1)]
    [InlineData(24, 4, 0, 0)]
    [InlineData(24, 4, 0, 21)]
    public void ProfileOptionsRejectInvalidOrUnboundedWork(
        int systemCount,
        int empireCount,
        int historyTicks,
        int iterations)
    {
        var options = new SqlServerStateProfileOptions(
            systemCount,
            empireCount,
            historyTicks,
            iterations);

        Assert.Throws<ArgumentOutOfRangeException>(options.Validate);
    }
}
