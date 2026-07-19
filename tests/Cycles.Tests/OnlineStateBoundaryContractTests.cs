using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class OnlineStateBoundaryContractTests
{
    private static readonly string[] ExpectedWholeStateCalls =
    [
        "Api/ApiAdminEndpoints.cs|LoadOrCreate|1",
        "Api/ApiAdminEndpoints.cs|RunTick|1",
        "Api/ApiAdminRoleEndpoints.cs|Update|1",
        "Api/ApiOrderEndpoints.cs|UpdateActiveCycleExclusively|6",
        "Api/DashboardAccessMiddleware.cs|LoadOrCreate|1",
        "Api/DashboardBootstrapContext.cs|LoadOrCreate|1",
        "Api/ExternalAuthentication.cs|Update|1",
        "Api/PlaygroundAccessMiddleware.cs|Replace|2",
        "Api/Program.cs|LoadOrCreate|13",
        "Api/Program.cs|RunTick|1",
        "Api/Program.cs|Update|1",
        "Worker/TickWorker.cs|RunTickIfDue|1"
    ];

    private static readonly Regex WholeStateCallPattern = new(
        @"\.\s*(?<method>UpdateActiveCycleExclusively|RunTickIfDue|LoadOrCreate|RunTick|Replace|Update)\s*\(",
        RegexOptions.CultureInvariant);

    [Fact]
    public void Online_whole_state_and_unspecified_tick_call_sites_cannot_grow_unnoticed()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OnlineSource");
        var actualCalls = Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .SelectMany(file => WholeStateCallPattern
                .Matches(File.ReadAllText(file))
                .Select(match => new
                {
                    Path = Path.GetRelativePath(sourceRoot, file).Replace('\\', '/'),
                    Method = match.Groups["method"].Value
                }))
            .GroupBy(call => (call.Path, call.Method))
            .Select(group => $"{group.Key.Path}|{group.Key.Method}|{group.Count()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(ExpectedWholeStateCalls, actualCalls);
    }
}
