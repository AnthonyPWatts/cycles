using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class SqlServerMigratorTests
{
    [Fact]
    public void SplitBatchesTreatsGoOnOwnLineAsBatchSeparator()
    {
        const string script = """
            SELECT 1;
            GO
            SELECT 2;
            GO 2 -- rerun this batch twice
            """;

        var batches = SqlServerMigrator.SplitBatches(script);

        Assert.Equal(3, batches.Count);
        Assert.Contains("SELECT 1;", batches[0], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", batches[1], StringComparison.Ordinal);
        Assert.Contains("SELECT 2;", batches[2], StringComparison.Ordinal);
    }

    [Fact]
    public void EmbeddedMigrationsIncludeInitialSchemaAndHistoryTable()
    {
        var migrations = SqlServerMigrator.LoadEmbeddedMigrations();
        var initialSchema = Assert.Single(migrations, migration => migration.MigrationId == "001_initial_schema");
        var retryHistory = Assert.Single(migrations, migration => migration.MigrationId == "002_allow_tick_retry_history");

        Assert.Contains("SchemaMigrations", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.Players", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.TickLogs", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("UX_TickLogs_Cycle_Tick_Completed", retryHistory.Script, StringComparison.Ordinal);
    }
}
