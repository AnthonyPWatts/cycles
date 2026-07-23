namespace Cycles.Tests;

public sealed class SqlIntegrationWorkflowTests
{
    [Fact]
    public void Build_job_tests_the_Cloudflare_public_asset_boundary()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ci.yml"))
            .ReplaceLineEndings("\n");
        var edgeTestIndex = workflow.IndexOf(
            "- name: Test Cloudflare public asset boundary",
            StringComparison.Ordinal);
        var dotnetTestIndex = workflow.IndexOf("- name: Test\n", StringComparison.Ordinal);

        Assert.Contains("uses: actions/setup-node@v6", workflow, StringComparison.Ordinal);
        Assert.Contains("node-version: 24", workflow, StringComparison.Ordinal);
        Assert.Contains("run: npm test --prefix deploy/cloudflare", workflow, StringComparison.Ordinal);
        Assert.True(edgeTestIndex >= 0);
        Assert.True(edgeTestIndex < dotnetTestIndex);
    }

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
