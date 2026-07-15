namespace Cycles.Tests;

public sealed class PlaygroundDeploymentWorkflowTests
{
    [Fact]
    public void Deployment_can_deliberately_reseed_disposable_state_before_publishing_the_new_api()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"));
        var stopIndex = workflow.IndexOf("Stop API for database maintenance", StringComparison.Ordinal);
        var migrationIndex = workflow.IndexOf("db migrate", StringComparison.Ordinal);
        var seedIndex = workflow.IndexOf("seed \"sqlserver:$connection_string\" --confirm-replace", StringComparison.Ordinal);
        var upgradeIndex = workflow.IndexOf("galaxy upgrade", StringComparison.Ordinal);
        var deployIndex = workflow.IndexOf("azure/webapps-deploy", StringComparison.Ordinal);

        Assert.True(stopIndex >= 0);
        Assert.True(stopIndex < migrationIndex);
        Assert.True(migrationIndex < seedIndex);
        Assert.True(migrationIndex < upgradeIndex);
        Assert.True(seedIndex < deployIndex);
        Assert.True(upgradeIndex < deployIndex);
        Assert.Contains("reseed:", workflow, StringComparison.Ordinal);
        Assert.Contains("github.event_name", workflow, StringComparison.Ordinal);
        Assert.Contains("inputs.reseed", workflow, StringComparison.Ordinal);
        Assert.Contains("show \"sqlserver:$connection_string\"", workflow, StringComparison.Ordinal);
        Assert.Contains("echo \"::add-mask::$connection_string\"", workflow, StringComparison.Ordinal);
        Assert.Contains("for attempt in 1 2 3", workflow, StringComparison.Ordinal);
        Assert.Contains("sleep $((attempt * 15))", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Start API\n        if: always()", workflow.ReplaceLineEndings("\n"), StringComparison.Ordinal);
    }
}
