using System.Text.RegularExpressions;

namespace Cycles.Tests;

public sealed class OnlineStateBoundaryContractTests
{
    private static readonly string[] ExpectedLegacyStoreTypeReferences =
    [
        "Api/ApiAdminEndpoints.cs|4",
        "Api/ApiOrderEndpoints.cs|12",
        "Api/DashboardBootstrapContext.cs|1",
        "Api/Program.cs|20",
        "Worker/Program.cs|1",
        "Worker/TickWorker.cs|1"
    ];

    private static readonly string[] ExpectedLegacyStoreCalls =
    [
        "Api/ApiAdminEndpoints.cs|LoadOrCreate|1",
        "Api/ApiAdminEndpoints.cs|RunTick|1",
        "Api/ApiOrderEndpoints.cs|UpdateActiveCycleExclusively|6",
        "Api/DashboardBootstrapContext.cs|LoadOrCreate|1",
        "Api/Program.cs|LoadOrCreate|11",
        "Worker/TickWorker.cs|RunTickIfDue|1"
    ];

    private static readonly Regex LegacyStoreTypePattern = new(
        @"\bIGameStateStore\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex TypedReceiverPattern = new(
        @"\bIGameStateStore\s+(?<receiver>[A-Za-z_]\w*)\b",
        RegexOptions.CultureInvariant);

    private static readonly Regex ServiceResolvedReceiverPattern = new(
        @"\bvar\s+(?<receiver>[A-Za-z_]\w*)\s*=\s*[^;]*\b(?:GetRequiredService|GetService)\s*<\s*IGameStateStore\s*>\s*\(",
        RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private const string LegacyStoreMethodNames =
        "UpdateActiveCycleExclusively|RunTickIfDue|LoadOrCreate|RunTick|Replace|Update";

    [Fact]
    public void Online_legacy_game_state_store_dependency_cannot_grow_unnoticed()
    {
        var sourceFiles = ReadOnlineSourceFiles();
        var actualTypeReferences = FindLegacyStoreTypeReferences(sourceFiles);
        var actualCalls = FindLegacyStoreCalls(sourceFiles);

        AssertExactAllowance(ExpectedLegacyStoreTypeReferences, actualTypeReferences);
        AssertExactAllowance(ExpectedLegacyStoreCalls, actualCalls);

        Assert.True(
            (ExpectedLegacyStoreTypeReferences.Length == 0) == (actualTypeReferences.Length == 0),
            "The final online architecture has no IGameStateStore tokens; reaching it requires an empty explicit allowance.");
    }

    [Fact]
    public void Focused_account_authentication_and_admin_boundaries_cannot_reacquire_the_legacy_store()
    {
        var focusedFiles = new[]
        {
            "Api/ApiAdminRoleEndpoints.cs",
            "Api/DashboardAccessMiddleware.cs",
            "Api/DevelopmentAuth.cs",
            "Api/ExternalAuthentication.cs",
            "Api/TrustedPlayerSelection.cs"
        };
        var sourceFiles = ReadOnlineSourceFiles()
            .Where(file => focusedFiles.Contains(file.Path, StringComparer.Ordinal))
            .ToArray();

        Assert.Equal(focusedFiles.Order(StringComparer.Ordinal), sourceFiles.Select(file => file.Path).Order(StringComparer.Ordinal));
        Assert.All(sourceFiles, file =>
        {
            Assert.DoesNotContain("IGameStateStore", file.Content, StringComparison.Ordinal);
            Assert.DoesNotContain("SqlServerGameStateStore", file.Content, StringComparison.Ordinal);
        });
    }

    private static OnlineSourceFile[] ReadOnlineSourceFiles()
    {
        var sourceRoot = Path.Combine(AppContext.BaseDirectory, "Fixtures", "OnlineSource");
        return Directory
            .EnumerateFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Select(file => new OnlineSourceFile(
                Path.GetRelativePath(sourceRoot, file).Replace('\\', '/'),
                File.ReadAllText(file)))
            .ToArray();
    }

    private static string[] FindLegacyStoreTypeReferences(IEnumerable<OnlineSourceFile> sourceFiles) =>
        sourceFiles
            .Select(file => new
            {
                file.Path,
                Count = LegacyStoreTypePattern.Matches(file.Content).Count
            })
            .Where(item => item.Count > 0)
            .Select(item => $"{item.Path}|{item.Count}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static string[] FindLegacyStoreCalls(IEnumerable<OnlineSourceFile> sourceFiles) =>
        sourceFiles
            .SelectMany(file => FindLegacyStoreCalls(file)
                .Select(method => new { file.Path, Method = method }))
            .GroupBy(call => (call.Path, call.Method))
            .Select(group => $"{group.Key.Path}|{group.Key.Method}|{group.Count()}")
            .Order(StringComparer.Ordinal)
            .ToArray();

    private static IEnumerable<string> FindLegacyStoreCalls(OnlineSourceFile sourceFile)
    {
        var receivers = TypedReceiverPattern
            .Matches(sourceFile.Content)
            .Concat(ServiceResolvedReceiverPattern.Matches(sourceFile.Content))
            .Select(match => match.Groups["receiver"].Value)
            .Distinct(StringComparer.Ordinal);

        foreach (var receiver in receivers)
        {
            var callPattern = new Regex(
                $@"\b{Regex.Escape(receiver)}\s*\.\s*(?<method>{LegacyStoreMethodNames})\s*\(",
                RegexOptions.CultureInvariant);
            foreach (Match match in callPattern.Matches(sourceFile.Content))
            {
                yield return match.Groups["method"].Value;
            }
        }
    }

    private static void AssertExactAllowance(string[] expected, string[] actual)
    {
        Assert.Equal(expected, actual);
        if (expected.Length == 0)
        {
            Assert.Empty(actual);
        }
    }

    private sealed record OnlineSourceFile(string Path, string Content);
}
