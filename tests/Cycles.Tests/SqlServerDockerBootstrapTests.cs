namespace Cycles.Tests;

public sealed class SqlServerDockerBootstrapTests
{
    private static readonly string[] RequiredSetOptions =
    [
        "SET ANSI_NULLS ON;",
        "SET QUOTED_IDENTIFIER ON;",
        "SET ANSI_PADDING ON;",
        "SET ANSI_WARNINGS ON;",
        "SET CONCAT_NULL_YIELDS_NULL ON;",
        "SET ARITHABORT ON;",
        "SET NUMERIC_ROUNDABORT OFF;"
    ];

    [Fact]
    public void Seed_script_sets_options_required_by_filtered_indexes()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "002_seed_cycles_data.sql");
        var script = File.ReadAllText(scriptPath);

        foreach (var option in RequiredSetOptions)
        {
            Assert.Contains(option, script, StringComparison.Ordinal);
        }
    }

}
