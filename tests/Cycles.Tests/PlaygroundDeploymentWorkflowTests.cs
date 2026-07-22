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
    public void Deployment_can_deliberately_choose_sql_maintenance_before_publishing_the_new_api()
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
        Assert.Contains("database_action:", workflow, StringComparison.Ordinal);
        Assert.Contains("- none", workflow, StringComparison.Ordinal);
        Assert.Contains("- migrate", workflow, StringComparison.Ordinal);
        Assert.Contains("- migrate-and-upgrade", workflow, StringComparison.Ordinal);
        Assert.Contains("- reseed", workflow, StringComparison.Ordinal);
        Assert.Contains("case \"${{ inputs.database_action }}\" in", workflow, StringComparison.Ordinal);
        Assert.Contains("show \"sqlserver:$connection_string\"", workflow, StringComparison.Ordinal);
        Assert.Contains("echo \"::add-mask::$connection_string\"", workflow, StringComparison.Ordinal);
        Assert.Contains("for attempt in 1 2 3", workflow, StringComparison.Ordinal);
        Assert.Contains("sleep $((attempt * 15))", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "ASPNETCORE_ENVIRONMENT=${{ vars.PLAYGROUND_HOST_ENVIRONMENT || 'Development' }}",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "Cycles__Authentication__Mode=${{ vars.PLAYGROUND_AUTHENTICATION_MODE || 'DevelopmentSelector' }}",
            workflow,
            StringComparison.Ordinal);
        Assert.DoesNotContain("Cycles__TrustedPlayerSelection__Enabled", workflow, StringComparison.Ordinal);
        Assert.Contains("- name: Start API\n        if: always()", workflow.ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Contains("--output none", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Deployment_is_manual_and_no_database_action_does_not_touch_sql()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"))
            .ReplaceLineEndings("\n");

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("workflow_run:", workflow, StringComparison.Ordinal);
        Assert.Contains("database_action:", workflow, StringComparison.Ordinal);
        Assert.Contains("default: none", workflow, StringComparison.Ordinal);
        Assert.Contains(
            "- name: Stop API for database maintenance\n        if: inputs.database_action != 'none'",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains(
            "- name: Run explicit database maintenance\n        if: inputs.database_action != 'none'",
            workflow,
            StringComparison.Ordinal);
        Assert.Contains("Restore operator CLI\n        if: inputs.database_action != 'none'", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: false", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("Resolve revision", workflow, StringComparison.Ordinal);
    }

    [Fact]
    public void Sql_maintenance_fails_closed_against_the_monthly_allowance_budget()
    {
        var workflow = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "deploy-playground.yml"))
            .ReplaceLineEndings("\n");

        var budgetIndex = workflow.IndexOf("Check SQL allowance budget", StringComparison.Ordinal);
        var connectionStringIndex = workflow.IndexOf("az webapp config connection-string list", StringComparison.Ordinal);

        Assert.True(budgetIndex >= 0);
        Assert.True(budgetIndex < connectionStringIndex);
        Assert.Contains("SQL_ENGINEERING_BUDGET_VCORE_SECONDS: \"75000\"", workflow, StringComparison.Ordinal);
        Assert.Contains("SQL_PLAY_RESERVE_VCORE_SECONDS: \"25000\"", workflow, StringComparison.Ordinal);
        Assert.Contains("free_amount_remaining", workflow, StringComparison.Ordinal);
        Assert.Contains("inputs.override_sql_budget", workflow, StringComparison.Ordinal);
        Assert.Contains("Azure did not return the current SQL free-allowance balance.", workflow, StringComparison.Ordinal);
        Assert.Contains("SQL maintenance is blocked.", workflow, StringComparison.Ordinal);
        Assert.Contains("can incur paid usage", workflow, StringComparison.Ordinal);
    }
}
