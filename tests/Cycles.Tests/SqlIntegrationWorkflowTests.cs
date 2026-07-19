namespace Cycles.Tests;

public sealed class SqlIntegrationWorkflowTests
{
    [Fact]
    public void Sql_server_job_requires_counted_integration_evidence_before_smoke_checks()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ci.yml"))
            .ReplaceLineEndings("\n");
        var requiredTestIndex = workflow.IndexOf("run: ./eng/test.ps1 -RequireSqlIntegration", StringComparison.Ordinal);
        var cliSmokeIndex = workflow.IndexOf("- name: CLI SQL smoke check", StringComparison.Ordinal);
        var gameplaySmokeIndex = workflow.IndexOf("- name: Alpha gameplay smoke check", StringComparison.Ordinal);

        Assert.Contains("name: SQL Server integration", workflow, StringComparison.Ordinal);
        Assert.Contains("CYCLES_REQUIRE_SQL_INTEGRATION: \"1\"", workflow, StringComparison.Ordinal);
        Assert.True(requiredTestIndex >= 0);
        Assert.True(requiredTestIndex < cliSmokeIndex);
        Assert.True(cliSmokeIndex < gameplaySmokeIndex);
    }
}
