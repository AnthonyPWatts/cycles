namespace Cycles.Tests;

public sealed class PlaygroundDeploymentWorkflowTests
{
    [Fact]
    public void Deployment_quiesces_and_upgrades_the_database_before_publishing_the_new_api()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"));
        var stopIndex = workflow.IndexOf("Stop API for database maintenance", StringComparison.Ordinal);
        var migrationIndex = workflow.IndexOf("db migrate", StringComparison.Ordinal);
        var upgradeIndex = workflow.IndexOf("galaxy upgrade", StringComparison.Ordinal);
        var deployIndex = workflow.IndexOf("azure/webapps-deploy", StringComparison.Ordinal);

        Assert.True(stopIndex >= 0);
        Assert.True(stopIndex < migrationIndex);
        Assert.True(migrationIndex < upgradeIndex);
        Assert.True(upgradeIndex < deployIndex);
        Assert.Contains("echo \"::add-mask::$connection_string\"", workflow, StringComparison.Ordinal);
        Assert.Contains("for attempt in 1 2 3", workflow, StringComparison.Ordinal);
        Assert.Contains("sleep $((attempt * 15))", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Start API\n        if: always()", workflow.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
