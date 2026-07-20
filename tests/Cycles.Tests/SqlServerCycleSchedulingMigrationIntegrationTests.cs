using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleSchedulingMigrationIntegrationTests
{
    private const string MigrationId = "025_add_cycle_scheduling";
    private static readonly DateTimeOffset StartedAt =
        new(2026, 7, 20, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset LatestCompletedAt =
        new(2026, 7, 20, 11, 15, 0, TimeSpan.Zero);

    [Fact]
    public void Migration_backfills_the_latest_deadline_and_is_idempotent()
    {
        var serverConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(
            serverConnectionString,
            "024_enforce_external_identity_binary_collation");
        var ids = InsertCycle(database.ConnectionString, "Materialized");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Execute(
            connection,
            """
            INSERT INTO dbo.TickLogs
                (TickLogID, CycleID, TickNumber, StartedAt, CompletedAt, Status, DiagnosticLog)
            VALUES
                (NEWID(), @CycleID, 1, @FirstStartedAt, @FirstCompletedAt, N'Completed', N''),
                (NEWID(), @CycleID, 2, @LatestStartedAt, @LatestCompletedAt, N'Completed', N'');
            """,
            ("@CycleID", ids.CycleId),
            ("@FirstStartedAt", LatestCompletedAt.AddHours(-2).AddMinutes(-5)),
            ("@FirstCompletedAt", LatestCompletedAt.AddHours(-2)),
            ("@LatestStartedAt", LatestCompletedAt.AddMinutes(-5)),
            ("@LatestCompletedAt", LatestCompletedAt));
        var migrator = new SqlServerMigrator(database.ConnectionString);

        var applied = Assert.Single(migrator.MigrateThrough(MigrationId));

        Assert.Equal(MigrationId, applied.MigrationId);
        Assert.Equal(
            "Scheduled",
            Scalar<string>(
                connection,
                "SELECT SchedulingMode FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;",
                ("@ID", ids.ConfigurationId)));
        Assert.Equal(
            "Scheduled",
            Scalar<string>(
                connection,
                "SELECT SchedulingMode FROM dbo.Cycles WHERE CycleID = @ID;",
                ("@ID", ids.CycleId)));
        Assert.Equal(
            LatestCompletedAt.AddMinutes(60),
            Scalar<DateTimeOffset>(
                connection,
                "SELECT NextTickAt FROM dbo.Cycles WHERE CycleID = @ID;",
                ("@ID", ids.CycleId)));

        Execute(
            connection,
            "DELETE FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
            ("@MigrationID", MigrationId));
        Assert.Equal(MigrationId, Assert.Single(migrator.MigrateThrough(MigrationId)).MigrationId);

        var embedded = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == MigrationId);
        foreach (var batch in SqlServerMigrator.SplitBatches(embedded.Script))
        {
            Execute(connection, batch);
        }

        Assert.Equal(
            LatestCompletedAt.AddMinutes(60),
            Scalar<DateTimeOffset>(
                connection,
                "SELECT NextTickAt FROM dbo.Cycles WHERE CycleID = @ID;",
                ("@ID", ids.CycleId)));
        Assert.Equal(
            1,
            Scalar<int>(
                connection,
                "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
                ("@MigrationID", MigrationId)));
    }

    [Fact]
    public void Migration_rejects_a_cycle_bound_to_a_non_materialized_configuration_and_retries_cleanly()
    {
        var serverConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(
            serverConnectionString,
            "024_enforce_external_identity_binary_collation");
        var ids = InsertCycle(database.ConnectionString, "Draft");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        var migrator = new SqlServerMigrator(database.ConnectionString);

        var rejected = Assert.Throws<SqlException>(() => migrator.MigrateThrough(MigrationId));

        Assert.Equal(51055, rejected.Number);
        Assert.Contains("materialized Cycle configuration", rejected.Message, StringComparison.Ordinal);
        Assert.Equal(
            0,
            Scalar<int>(
                connection,
                "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
                ("@MigrationID", MigrationId)));
        Assert.Equal(
            DBNull.Value,
            ScalarObject(connection, "SELECT COL_LENGTH(N'dbo.Cycles', N'NextTickAt');"));

        Execute(
            connection,
            """
            UPDATE dbo.CycleConfigurations
            SET Status = N'Materialized',
                LockedAt = @Now,
                MaterializedAt = @Now
            WHERE CycleConfigurationID = @ID;
            """,
            ("@Now", StartedAt),
            ("@ID", ids.ConfigurationId));

        Assert.Equal(MigrationId, Assert.Single(migrator.MigrateThrough(MigrationId)).MigrationId);
        Assert.Equal(
            StartedAt,
            Scalar<DateTimeOffset>(
                connection,
                "SELECT NextTickAt FROM dbo.Cycles WHERE CycleID = @ID;",
                ("@ID", ids.CycleId)));
    }

    private static CycleIds InsertCycle(string connectionString, string configurationStatus)
    {
        var gameId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();
        var cycleId = Guid.NewGuid();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        Execute(
            connection,
            """
            INSERT INTO dbo.Games
            (
                GameID, Name, Purpose, Status, Visibility, CreationSource,
                GamePolicyKey, GamePolicyVersion, GamePolicyContentHash,
                PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt,
                FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt
            )
            VALUES
            (
                @GameID, N'Scheduling migration fixture', N'Standard', N'Active',
                N'Private', N'Operator', N'test-game-policy', 1, NULL,
                N'Verified', NULL, @StartAt, @StartAt, NULL, NULL, NULL
            );

            INSERT INTO dbo.CycleConfigurations
            (
                CycleConfigurationID, GameID, SequenceNumber, Status,
                ProvenanceStatus, MapProfileKey, MapProfileVersion,
                MapProfileContentHash, MapSeed, ScenarioProfileKey,
                ScenarioProfileVersion, ScenarioProfileContentHash,
                ScenarioSeed, CyclePolicyKey, CyclePolicyVersion,
                CyclePolicyContentHash, MinimumHumanSeats, MaximumHumanSeats,
                ScheduledStartAt, ScheduledEndAt, TickLengthMinutes,
                CreatedAt, LockedAt, MaterializedAt, CancelledAt
            )
            VALUES
            (
                @ConfigurationID, @GameID, 1, @ConfigurationStatus,
                N'Verified', N'test-map', 1, NULL, 17, N'test-scenario',
                1, NULL, 23, N'test-cycle-policy', 1, NULL, 1, 4,
                @StartAt, DATEADD(DAY, 10, @StartAt), 60, @StartAt,
                CASE WHEN @ConfigurationStatus = N'Materialized' THEN @StartAt ELSE NULL END,
                CASE WHEN @ConfigurationStatus = N'Materialized' THEN @StartAt ELSE NULL END,
                NULL
            );

            INSERT INTO dbo.Cycles
            (
                CycleID, GameID, CycleConfigurationID, PreviousCycleID,
                Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber,
                Status, TurnStage, MapProfileKey, MapProfileVersion,
                MapProfileContentHash, MapSeed, ScenarioProfileKey,
                ScenarioProfileVersion, ScenarioProfileContentHash,
                ScenarioSeed, CyclePolicyKey, CyclePolicyVersion,
                CyclePolicyContentHash, ProfileProvenanceStatus,
                CreatedByPlayerID, CreatedAt
            )
            VALUES
            (
                @CycleID, @GameID, @ConfigurationID, NULL,
                N'Scheduling migration Cycle', @StartAt, DATEADD(DAY, 10, @StartAt),
                60, 2, N'Active', N'CommandOpen', N'test-map', 1, NULL, 17,
                N'test-scenario', 1, NULL, 23, N'test-cycle-policy', 1, NULL,
                N'Verified', NULL, @StartAt
            );
            """,
            ("@GameID", gameId),
            ("@ConfigurationID", configurationId),
            ("@CycleID", cycleId),
            ("@ConfigurationStatus", configurationStatus),
            ("@StartAt", StartedAt));
        return new CycleIds(configurationId, cycleId);
    }

    private static void Execute(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        var value = ScalarObject(connection, sql, parameters);
        if (value is T typed)
        {
            return typed;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }

    private static object ScalarObject(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command.ExecuteScalar()!;
    }

    private sealed record CycleIds(Guid ConfigurationId, Guid CycleId);
}
