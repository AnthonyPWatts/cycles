using Cycles.Core;
using Cycles.Infrastructure.SqlServer;

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

        Assert.Contains("SET XACT_ABORT ON;", script, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION;", script, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION;", script, StringComparison.Ordinal);
        Assert.Contains("DECLARE @SeededAt DATETIMEOFFSET = SYSDATETIMEOFFSET();", script, StringComparison.Ordinal);
        Assert.Contains("DATEADD(DAY, 90, @SeededAt)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("2026-07-15", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Seed_script_uses_only_active_priority_programmes()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "002_seed_cycles_data.sql");
        var script = File.ReadAllText(scriptPath);

        Assert.Equal(4, script.Split(", 0, 0, 67, 33, @SeededAt", StringSplitOptions.None).Length - 1);
    }

    [Fact]
    public void Seed_script_matches_the_canonical_curated_cold_start()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "002_seed_cycles_data.sql");
        var checkedInScript = File.ReadAllText(scriptPath).ReplaceLineEndings("\n");
        var generatedScript = SqlServerDevelopmentSeedScript.Generate().ReplaceLineEndings("\n");

        Assert.Equal(generatedScript, checkedInScript);
    }

    [Fact]
    public void Seed_script_runs_only_for_an_empty_cycles_database()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "002_seed_cycles_data.sql");
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("IF NOT EXISTS (SELECT 1 FROM dbo.Cycles)", script, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE Status = N'Active'", script, StringComparison.Ordinal);
        Assert.DoesNotContain("WHERE CycleID = @CycleID", script, StringComparison.Ordinal);
    }

    [Fact]
    public void Effective_presence_includes_each_empire_sharing_a_home_system()
    {
        var cycle = new Cycle();
        var system = new GalaxySystem { CycleId = cycle.CycleId, SystemName = "Shared home" };
        var firstEmpire = new Empire { CycleId = cycle.CycleId, HomeSystemId = system.SystemId };
        var secondEmpire = new Empire { CycleId = cycle.CycleId, HomeSystemId = system.SystemId };
        var state = new GameState
        {
            Systems = [system],
            Empires = [firstEmpire, secondEmpire]
        };

        var presence = InfluenceCalculator.CalculateEffectivePresence(state, cycle.CycleId, system.SystemId);

        Assert.Equal(InfluenceCalculator.HomeSystemMinimumPresence, presence[firstEmpire.EmpireId]);
        Assert.Equal(InfluenceCalculator.HomeSystemMinimumPresence, presence[secondEmpire.EmpireId]);
    }

    [Fact]
    public void Entrypoint_waits_for_cycles_database_before_running_migrations()
    {
        var scriptPath = Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "sqldockerdeploykit",
            "entrypoint.sh");
        var script = File.ReadAllText(scriptPath);

        var createDatabaseIndex = script.IndexOf(
            "CREATE DATABASE CyclesDb;",
            StringComparison.Ordinal);
        var databaseReadinessIndex = script.IndexOf(
            "wait_for_database CyclesDb",
            StringComparison.Ordinal);
        var migrationIndex = script.IndexOf(
            "for sql_file in /tmp/app/Migrations/*.sql; do",
            StringComparison.Ordinal);

        Assert.Contains("wait_for_database()", script, StringComparison.Ordinal);
        Assert.Contains("-d \"$database_name\" -Q \"SELECT 1\"", script, StringComparison.Ordinal);
        Assert.True(createDatabaseIndex >= 0, "The entrypoint must create CyclesDb.");
        Assert.True(databaseReadinessIndex > createDatabaseIndex, "The entrypoint must wait after creating CyclesDb.");
        Assert.True(migrationIndex > databaseReadinessIndex, "The entrypoint must wait before opening CyclesDb for migrations.");
    }

}
