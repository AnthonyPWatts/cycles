using Microsoft.Data.SqlClient;
using System.Data;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace Cycles.Infrastructure.SqlServer;

public sealed class SqlServerMigrator
{
    private const string MigrationLockName = "Cycles.SchemaMigrations";
    private const string MigrationResourcePrefix = "Cycles.Infrastructure.SqlServer.Migrations.";

    private static readonly Regex BatchSeparator = new(
        @"^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private readonly string _connectionString;
    private readonly string _databaseName;

    public SqlServerMigrator(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("SQL Server connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _databaseName = GetRequiredDatabaseName(connectionString);
    }

    public SqlServerMigrationStatus GetStatus()
    {
        var migrations = LoadEmbeddedMigrations();
        if (!DatabaseExists())
        {
            return new SqlServerMigrationStatus(
                DatabaseExists: false,
                AppliedMigrationIds: Array.Empty<string>(),
                AvailableMigrations: migrations,
                PendingMigrations: migrations);
        }

        using var connection = OpenTargetConnection();
        var appliedMigrationIds = ReadAppliedMigrationIds(connection, transaction: null);
        var pendingMigrations = migrations
            .Where(migration => !appliedMigrationIds.Contains(migration.MigrationId, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        return new SqlServerMigrationStatus(
            DatabaseExists: true,
            AppliedMigrationIds: appliedMigrationIds,
            AvailableMigrations: migrations,
            PendingMigrations: pendingMigrations);
    }

    public IReadOnlyList<SqlServerMigrationResult> Migrate() => MigrateThrough(finalMigrationId: null);

    internal IReadOnlyList<SqlServerMigrationResult> MigrateThrough(string? finalMigrationId)
    {
        EnsureDatabaseExists();

        var migrations = LoadEmbeddedMigrations();
        if (finalMigrationId is not null
            && !migrations.Any(item => string.Equals(item.MigrationId, finalMigrationId, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Unknown final migration '{finalMigrationId}'.", nameof(finalMigrationId));
        }

        using var connection = OpenTargetConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);

        AcquireMigrationLock(connection, transaction);
        EnsureHistoryTable(connection, transaction);

        var appliedMigrationIds = ReadAppliedMigrationIds(connection, transaction);
        var pendingMigrations = migrations
            .Where(migration => !appliedMigrationIds.Contains(migration.MigrationId, StringComparer.OrdinalIgnoreCase))
            .TakeWhile(migration => finalMigrationId is null || string.Compare(
                migration.MigrationId,
                finalMigrationId,
                StringComparison.OrdinalIgnoreCase) <= 0)
            .ToArray();

        foreach (var migration in pendingMigrations)
        {
            foreach (var batch in SplitBatches(migration.Script))
            {
                ExecuteNonQuery(connection, transaction, batch);
            }

            RecordMigrationIfMissing(connection, transaction, migration);
        }

        transaction.Commit();

        return pendingMigrations
            .Select(migration => new SqlServerMigrationResult(migration.MigrationId, migration.Description))
            .ToArray();
    }

    internal static IReadOnlyList<SqlServerMigration> LoadEmbeddedMigrations()
    {
        var assembly = typeof(SqlServerMigrator).Assembly;
        var migrations = assembly
            .GetManifestResourceNames()
            .Where(name => name.StartsWith(MigrationResourcePrefix, StringComparison.Ordinal)
                && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select(resourceName => ReadEmbeddedMigration(assembly, resourceName))
            .ToArray();

        return migrations.Length > 0
            ? migrations
            : throw new InvalidOperationException("No SQL Server migrations were embedded in Cycles.Infrastructure.SqlServer.");
    }

    internal static IReadOnlyList<string> SplitBatches(string script)
    {
        var batches = new List<string>();
        var currentBatch = new StringBuilder();

        using var reader = new StringReader(script);
        while (reader.ReadLine() is { } line)
        {
            var match = BatchSeparator.Match(line);
            if (!match.Success)
            {
                currentBatch.AppendLine(line);
                continue;
            }

            AddBatch(batches, currentBatch.ToString(), ParseBatchRepeatCount(match));
            currentBatch.Clear();
        }

        AddBatch(batches, currentBatch.ToString(), repeatCount: 1);
        return batches;
    }

    private bool DatabaseExists()
    {
        using var connection = OpenMasterConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONVERT(bit, CASE WHEN EXISTS (SELECT 1 FROM sys.databases WHERE name = @DatabaseName) THEN 1 ELSE 0 END);";
        AddString(command, "@DatabaseName", _databaseName, 128);
        return (bool)command.ExecuteScalar()!;
    }

    private void EnsureDatabaseExists()
    {
        if (DatabaseExists())
        {
            return;
        }

        using var connection = OpenMasterConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            IF DB_ID(@DatabaseName) IS NULL
            BEGIN
                DECLARE @Sql nvarchar(max) = N'CREATE DATABASE ' + QUOTENAME(@DatabaseName);
                EXEC sys.sp_executesql @Sql;
            END;
            """;
        AddString(command, "@DatabaseName", _databaseName, 128);
        command.ExecuteNonQuery();
    }

    private SqlConnection OpenMasterConnection()
    {
        var builder = new SqlConnectionStringBuilder(_connectionString)
        {
            InitialCatalog = "master"
        };

        var connection = new SqlConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private SqlConnection OpenTargetConnection()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static SqlServerMigration ReadEmbeddedMigration(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded migration resource '{resourceName}' could not be opened.");
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);

        var fileName = resourceName[MigrationResourcePrefix.Length..];
        var migrationId = fileName[..^".sql".Length];
        return new SqlServerMigration(
            migrationId,
            BuildDescription(migrationId),
            reader.ReadToEnd());
    }

    private static string BuildDescription(string migrationId)
    {
        var separatorIndex = migrationId.IndexOf('_', StringComparison.Ordinal);
        var description = separatorIndex >= 0
            ? migrationId[(separatorIndex + 1)..]
            : migrationId;
        return string.Join(' ', description.Split('_', StringSplitOptions.RemoveEmptyEntries));
    }

    private static void AcquireMigrationLock(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = @LockTimeout;
            SELECT @Result;
            """);
        AddString(command, "@Resource", MigrationLockName, 255);
        AddInt(command, "@LockTimeout", 15000);

        var result = Convert.ToInt32(command.ExecuteScalar(), null);
        if (result < 0)
        {
            throw new TimeoutException($"Could not acquire SQL Server migration lock '{MigrationLockName}'. Result code: {result}.");
        }
    }

    private static void EnsureHistoryTable(SqlConnection connection, SqlTransaction transaction) =>
        ExecuteNonQuery(connection, transaction, """
            IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SchemaMigrations
                (
                    MigrationID NVARCHAR(128) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
                    Description NVARCHAR(256) NOT NULL,
                    AppliedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAt DEFAULT SYSDATETIMEOFFSET()
                );
            END;
            """);

    private static IReadOnlyList<string> ReadAppliedMigrationIds(SqlConnection connection, SqlTransaction? transaction)
    {
        if (!HistoryTableExists(connection, transaction))
        {
            return Array.Empty<string>();
        }

        using var command = CreateCommand(connection, transaction, """
            SELECT MigrationID
            FROM dbo.SchemaMigrations
            ORDER BY MigrationID;
            """);
        using var reader = command.ExecuteReader();

        var migrationIds = new List<string>();
        while (reader.Read())
        {
            migrationIds.Add(reader.GetString(0));
        }

        return migrationIds;
    }

    private static bool HistoryTableExists(SqlConnection connection, SqlTransaction? transaction)
    {
        using var command = CreateCommand(connection, transaction, "SELECT CONVERT(bit, CASE WHEN OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL THEN 0 ELSE 1 END);");
        return (bool)command.ExecuteScalar()!;
    }

    private static void RecordMigrationIfMissing(SqlConnection connection, SqlTransaction transaction, SqlServerMigration migration)
    {
        using var command = CreateCommand(connection, transaction, """
            IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID)
            BEGIN
                INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
                VALUES (@MigrationID, @Description, SYSDATETIMEOFFSET());
            END;
            """);
        AddString(command, "@MigrationID", migration.MigrationId, 128);
        AddString(command, "@Description", migration.Description, 256);
        command.ExecuteNonQuery();
    }

    private static void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        using var command = CreateCommand(connection, transaction, sql);
        command.ExecuteNonQuery();
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction? transaction, string commandText)
    {
        var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.Transaction = transaction;
        return command;
    }

    private static void AddString(SqlCommand command, string name, string value, int length) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = value;

    private static void AddInt(SqlCommand command, string name, int value) =>
        command.Parameters.Add(name, SqlDbType.Int).Value = value;

    private static string GetRequiredDatabaseName(string connectionString)
    {
        var builder = new SqlConnectionStringBuilder(connectionString);
        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            throw new InvalidOperationException("SQL Server migrations require Database or Initial Catalog in the connection string.");
        }

        return builder.InitialCatalog;
    }

    private static void AddBatch(List<string> batches, string batch, int repeatCount)
    {
        if (string.IsNullOrWhiteSpace(batch))
        {
            return;
        }

        for (var i = 0; i < repeatCount; i++)
        {
            batches.Add(batch);
        }
    }

    private static int ParseBatchRepeatCount(Match match)
    {
        var value = match.Groups["count"].Value;
        return int.TryParse(value, out var repeatCount) && repeatCount > 0
            ? repeatCount
            : 1;
    }
}
