using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class CycleSchedulingMigrationTests
{
    [Fact]
    public void Embedded_migration_persists_and_indexes_the_scheduler_contract()
    {
        var migration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == "025_add_cycle_scheduling");

        Assert.Contains("CycleConfigurations ADD SchedulingMode", migration.Script, StringComparison.Ordinal);
        Assert.Contains("Cycles ADD SchedulingMode", migration.Script, StringComparison.Ordinal);
        Assert.Contains("Cycles ADD NextTickAt", migration.Script, StringComparison.Ordinal);
        Assert.Contains("MAX(log.CompletedAt)", migration.Script, StringComparison.Ordinal);
        Assert.Contains("CK_Cycles_NextTickAt_Coherence", migration.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE INDEX IX_Cycles_Due", migration.Script, StringComparison.Ordinal);
        Assert.Contains("ON dbo.Cycles(NextTickAt, CycleID)", migration.Script, StringComparison.Ordinal);
        Assert.Contains("INCLUDE(GameID)", migration.Script, StringComparison.Ordinal);
        Assert.Contains("TR_Cycles_EnforceSchedulingCapability", migration.Script, StringComparison.Ordinal);
        Assert.Contains("TR_CycleConfigurations_ProtectMaterializedProvenance", migration.Script, StringComparison.Ordinal);
        Assert.Contains("FROM inserted AS configuration", migration.Script, StringComparison.Ordinal);
        Assert.Contains("configuration.SchedulingMode <> cycle.SchedulingMode", migration.Script, StringComparison.Ordinal);
        Assert.Contains("configuration.Status <> N'Materialized'", migration.Script, StringComparison.Ordinal);
        Assert.Contains("Every Cycle must reference a materialized Cycle configuration", migration.Script, StringComparison.Ordinal);
    }
}
