using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerDockerSeedIntegrationTests
{
    [Fact]
    public void Checked_in_seed_executes_twice_against_the_current_schema()
    {
        var serverConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString);
        var script = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "Fixtures",
            "002_seed_cycles_data.sql"));

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Execute(connection, script);
        Execute(connection, script);

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT_BIG(*)
            FROM dbo.Cycles AS cycle
            INNER JOIN dbo.CycleConfigurations AS configuration
                ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
               AND configuration.GameID = cycle.GameID
            WHERE cycle.Status = N'Active'
              AND cycle.SchedulingMode = N'Scheduled'
              AND cycle.NextTickAt IS NOT NULL
              AND configuration.Status = N'Materialized'
              AND configuration.SchedulingMode = cycle.SchedulingMode;
            """;

        Assert.Equal(1L, (long)command.ExecuteScalar()!);
    }

    private static void Execute(SqlConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 60;
        command.ExecuteNonQuery();
    }
}
