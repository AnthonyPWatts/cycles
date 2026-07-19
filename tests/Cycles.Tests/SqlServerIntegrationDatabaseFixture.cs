using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[CollectionDefinition(CollectionName, DisableParallelization = true)]
public sealed class SqlServerIntegrationCollection : ICollectionFixture<SqlServerIntegrationDatabaseFixture>
{
    public const string CollectionName = "SQL Server integration";
}

public sealed class SqlServerIntegrationDatabaseFixture : IDisposable
{
    private readonly string? originalConnectionString;
    private readonly SqlServerIntegrationDatabase? database;

    public SqlServerIntegrationDatabaseFixture()
    {
        originalConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(originalConnectionString))
        {
            return;
        }

        database = new SqlServerIntegrationDatabase(originalConnectionString);
        Environment.SetEnvironmentVariable(SqlIntegrationGuard.ConnectionStringEnvironmentVariable, database.ConnectionString);
    }

    public void Dispose()
    {
        try
        {
            database?.Dispose();
        }
        finally
        {
            Environment.SetEnvironmentVariable(SqlIntegrationGuard.ConnectionStringEnvironmentVariable, originalConnectionString);
        }
    }

}

internal sealed class SqlServerIntegrationDatabase : IDisposable
{
    private const string MarkerTable = "CyclesIntegrationRunMarker";
    private readonly string databaseName;
    private readonly Guid markerToken = Guid.NewGuid();

    public SqlServerIntegrationDatabase(string serverConnectionString, string? finalMigrationId = null)
    {
        var builder = new SqlConnectionStringBuilder(serverConnectionString)
        {
            InitialCatalog = $"CyclesIntegration_{Environment.ProcessId}_{Guid.NewGuid():N}"
        };
        databaseName = builder.InitialCatalog;
        ConnectionString = builder.ConnectionString;

        CreateDatabaseAndMarker();
        try
        {
            new SqlServerMigrator(ConnectionString).MigrateThrough(finalMigrationId);
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string ConnectionString { get; }

    public void Dispose()
    {
        if (string.Equals(databaseName, "CyclesDb", StringComparison.OrdinalIgnoreCase)
            || !databaseName.StartsWith("CyclesIntegration_", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Refusing to drop non-integration database '{databaseName}'.");
        }

        using (var target = new SqlConnection(ConnectionString))
        {
            target.Open();
            using var marker = target.CreateCommand();
            marker.CommandText = $"SELECT MarkerToken FROM dbo.{MarkerTable};";
            var storedToken = marker.ExecuteScalar();
            if (storedToken is not Guid value || value != markerToken)
            {
                throw new InvalidOperationException("Refusing to drop an integration database without its run-specific marker.");
            }
        }

        var masterBuilder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
        using var master = new SqlConnection(masterBuilder.ConnectionString);
        master.Open();
        using var drop = master.CreateCommand();
        drop.CommandText = """
            DECLARE @Sql nvarchar(max) =
                N'ALTER DATABASE ' + QUOTENAME(@DatabaseName) + N' SET SINGLE_USER WITH ROLLBACK IMMEDIATE; '
                + N'DROP DATABASE ' + QUOTENAME(@DatabaseName) + N';';
            EXEC sys.sp_executesql @Sql;
            """;
        drop.Parameters.Add(new SqlParameter("@DatabaseName", System.Data.SqlDbType.NVarChar, 128) { Value = databaseName });
        drop.ExecuteNonQuery();
    }

    private void CreateDatabaseAndMarker()
    {
        var masterBuilder = new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = "master" };
        using (var master = new SqlConnection(masterBuilder.ConnectionString))
        {
            master.Open();
            using var create = master.CreateCommand();
            create.CommandText = "DECLARE @Sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName) + N';'; EXEC sys.sp_executesql @Sql;";
            create.Parameters.Add(new SqlParameter("@DatabaseName", System.Data.SqlDbType.NVarChar, 128) { Value = databaseName });
            create.ExecuteNonQuery();
        }

        using var connection = new SqlConnection(ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = $"""
            CREATE TABLE dbo.{MarkerTable}
            (
                MarkerToken UNIQUEIDENTIFIER NOT NULL,
                CreatedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_{MarkerTable}_CreatedAt DEFAULT SYSDATETIMEOFFSET()
            );
            INSERT INTO dbo.{MarkerTable}(MarkerToken) VALUES (@MarkerToken);
            """;
        command.Parameters.Add(new SqlParameter("@MarkerToken", System.Data.SqlDbType.UniqueIdentifier) { Value = markerToken });
        command.ExecuteNonQuery();
    }
}
