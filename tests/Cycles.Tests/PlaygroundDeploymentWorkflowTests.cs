namespace Cycles.Tests;

public sealed class PlaygroundDeploymentWorkflowTests
{
    [Fact]
    public void Deployment_refuses_to_publish_dashboard_code_before_required_edge_assets_are_live()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"));
        var edgePreflightIndex = workflow.IndexOf("Verify required edge assets", StringComparison.Ordinal);
        var stopIndex = workflow.IndexOf("Stop API for database maintenance", StringComparison.Ordinal);

        Assert.True(edgePreflightIndex >= 0);
        Assert.True(edgePreflightIndex < stopIndex);
        Assert.Contains("src/Cycles.Api/wwwroot/assets/icons/*.svg", workflow, StringComparison.Ordinal);
        Assert.Contains("src/Cycles.Api/wwwroot/assets/galaxy/twin-reaches-*.webp", workflow, StringComparison.Ordinal);
        Assert.Contains("relative_asset_path=\"${edge_asset_path#src/Cycles.Api/wwwroot/}\"", workflow, StringComparison.Ordinal);
        Assert.Contains("https://cycles.anthonypwatts.co.uk/${relative_asset_path}", workflow, StringComparison.Ordinal);
        Assert.Contains("Required edge asset ${edge_asset_url} returned HTTP ${status_code}", workflow, StringComparison.Ordinal);
        Assert.Contains("Deploy deploy/cloudflare before this Azure revision.", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployment_can_deliberately_reseed_disposable_state_before_publishing_the_new_api()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"));
        var stopIndex = workflow.IndexOf("Stop API for database maintenance", StringComparison.Ordinal);
        var edgeOriginIndex = workflow.IndexOf("Cycles__EdgeAssetOrigin=https://cycles.anthonypwatts.co.uk", StringComparison.Ordinal);
        var migrationIndex = workflow.IndexOf("db migrate", StringComparison.Ordinal);
        var seedIndex = workflow.IndexOf("seed \"sqlserver:$connection_string\" --confirm-replace", StringComparison.Ordinal);
        var upgradeIndex = workflow.IndexOf("galaxy upgrade", StringComparison.Ordinal);
        var deployIndex = workflow.IndexOf("azure/webapps-deploy", StringComparison.Ordinal);

        Assert.True(stopIndex >= 0);
        Assert.True(stopIndex < edgeOriginIndex);
        Assert.True(edgeOriginIndex < migrationIndex);
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
        Assert.Contains("Cycles__TrustedPlayerSelection__Enabled=true", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Start API\n        if: always()", workflow.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("--output none", workflow, StringComparison.Ordinal);
    }
}
