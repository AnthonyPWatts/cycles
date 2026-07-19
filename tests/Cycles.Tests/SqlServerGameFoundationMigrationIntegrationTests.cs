using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerGameFoundationMigrationIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = SqlIntegrationGuard.ConnectionStringEnvironmentVariable;
    private static readonly Guid LegacyGameId = Guid.Parse("01fcdded-9718-4436-b585-d97d504b1d57");
    private static readonly Guid LegacyLifecycleEventId = Guid.Parse("b283628d-2899-475c-9c6e-5dd8e20c2e91");

    [Fact]
    public void Migration_022_backfills_legacy_foundations_without_inventing_predecessors()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);

        var applied = new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");

        var migration = Assert.Single(applied);
        Assert.Equal("022_add_game_foundations", migration.MigrationId);
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal("Legacy Standard Game", Scalar<string>(connection,
            "SELECT Name FROM dbo.Games WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal("Active", Scalar<string>(connection,
            "SELECT Status FROM dbo.Games WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal("LegacyUnverified", Scalar<string>(connection,
            "SELECT PolicyProvenanceStatus FROM dbo.Games WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.CycleConfigurations WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.Cycles WHERE GameID = @ID AND CycleConfigurationID = CycleID;", LegacyGameId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.Cycles WHERE GameID = @ID AND PreviousCycleID IS NULL;", LegacyGameId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.Cycles WHERE GameID = @ID AND MapProfileKey = N'legacy-unclassified' AND ScenarioProfileKey = N'legacy-unclassified';", LegacyGameId));
        Assert.Equal(1, Scalar<int>(connection,
            "SELECT SequenceNumber FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;", legacy.CompletedCycleId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT SequenceNumber FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;", legacy.ActiveCycleId));
        Assert.Equal("Enrolled", Scalar<string>(connection,
            "SELECT Status FROM dbo.GameEnrolments WHERE PlayerID = @ID;", legacy.CurrentPlayerId));
        Assert.Equal("Historical", Scalar<string>(connection,
            "SELECT Status FROM dbo.GameEnrolments WHERE PlayerID = @ID;", legacy.HistoricalPlayerId));
        Assert.Equal("Withdrawn", Scalar<string>(connection,
            "SELECT Status FROM dbo.GameEnrolments WHERE PlayerID = @ID;", legacy.WithdrawnPlayerId));
        Assert.Equal(legacy.CurrentPlayerId, Scalar<Guid>(connection,
            "SELECT GameEnrolmentID FROM dbo.GameEnrolments WHERE PlayerID = @ID;", legacy.CurrentPlayerId));
        Assert.Equal("LegacyImported", Scalar<string>(connection,
            "SELECT EventType FROM dbo.GameLifecycleEvents WHERE GameLifecycleEventID = @ID;", LegacyLifecycleEventId));
        Assert.Equal("{\"source\":\"legacy-single-lineage\",\"schemaVersion\":1}", Scalar<string>(connection,
            "SELECT FactJson FROM dbo.GameLifecycleEvents WHERE GameLifecycleEventID = @ID;", LegacyLifecycleEventId));
        Assert.Equal(1, Scalar<int>(connection,
            "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Cycles') AND name = N'UX_Cycles_Game_OperationalSlot';"));
    }

    [Fact]
    public void Migration_022_classifies_canonical_profiles_only_from_consistent_authoritative_facts()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var cycleId = InsertCanonicalFactState(database.ConnectionString);

        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");

        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal("territorial-graph-v2", Scalar<string>(connection,
            "SELECT MapProfileKey FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
        Assert.Equal(71421, Scalar<int>(connection,
            "SELECT MapSeed FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
        Assert.Equal("development-match-v2", Scalar<string>(connection,
            "SELECT ScenarioProfileKey FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
        Assert.Equal(20260717, Scalar<int>(connection,
            "SELECT ScenarioSeed FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
        Assert.Equal(1, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.Cycles WHERE CycleID = @ID AND MapProfileVersion IS NULL AND ScenarioProfileVersion IS NULL AND ProfileProvenanceStatus = N'LegacyUnverified';", cycleId));
        Assert.Equal("territorial-graph-v2", Scalar<string>(connection,
            "SELECT MapProfileKey FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;", cycleId));
    }

    [Fact]
    public void Migration_022_rejects_conflicting_canonical_seed_facts()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var cycleId = InsertCanonicalFactState(database.ConnectionString);
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            Execute(connection, """
                INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, Severity, FactJson, DisplayText, CreatedAt)
                VALUES (NEWID(), @CycleID, 0, N'CycleSeeded', N'Normal',
                    N'{"topologyKey":"territorial-graph-v2","seed":99,"systemCount":64,"sectorCount":8}',
                    N'Conflicting canonical fact', SYSDATETIMEOFFSET());
                """, ("@CycleID", cycleId));
        }

        var error = Assert.Throws<SqlException>(() => new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations"));

        Assert.Equal(51024, error.Number);
        Assert.Contains(cycleId.ToString(), error.Message, StringComparison.OrdinalIgnoreCase);
        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(0, Scalar<int>(verification,
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Games');"));
    }

    [Theory]
    [InlineData("Unknown", 51022, "unknown statuses")]
    [InlineData("TwoOperational", 51023, "more than one operational")]
    public void Migration_022_rejects_contradictory_legacy_cycle_state_before_foundation_writes(
        string fixture,
        int expectedError,
        string expectedMessage)
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            InsertCycle(connection, Guid.NewGuid(), fixture == "Unknown" ? "Unexpected" : "Active", 0);
            if (fixture == "TwoOperational")
            {
                InsertCycle(connection, Guid.NewGuid(), "RecoveryRequired", 1);
            }
        }

        var error = Assert.Throws<SqlException>(() => new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations"));

        Assert.Equal(expectedError, error.Number);
        Assert.Contains(expectedMessage, error.Message, StringComparison.OrdinalIgnoreCase);
        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(0, Scalar<int>(verification,
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.Games');"));
    }

    [Fact]
    public void Migration_022_is_idempotent_and_can_resume_an_expanded_schema_without_backfill_rows()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);
        var migrator = new SqlServerMigrator(database.ConnectionString);
        migrator.MigrateThrough("022_add_game_foundations");
        byte[] firstRowVersion;
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            firstRowVersion = Scalar<byte[]>(connection,
                "SELECT RowVersion FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;", legacy.ActiveCycleId);
            Execute(connection,
                "DELETE FROM dbo.SchemaMigrations WHERE MigrationID = N'022_add_game_foundations';");
        }

        var reapplied = migrator.MigrateThrough("022_add_game_foundations");

        Assert.Equal("022_add_game_foundations", Assert.Single(reapplied).MigrationId);
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            Assert.Equal(firstRowVersion, Scalar<byte[]>(connection,
                "SELECT RowVersion FROM dbo.CycleConfigurations WHERE CycleConfigurationID = @ID;", legacy.ActiveCycleId));
            Execute(connection, """
                UPDATE dbo.Cycles
                SET GameID = NULL,
                    CycleConfigurationID = NULL,
                    MapProfileKey = NULL,
                    MapProfileVersion = NULL,
                    MapProfileContentHash = NULL,
                    MapSeed = NULL,
                    ScenarioProfileKey = NULL,
                    ScenarioProfileVersion = NULL,
                    ScenarioProfileContentHash = NULL,
                    ScenarioSeed = NULL,
                    CyclePolicyKey = NULL,
                    CyclePolicyVersion = NULL,
                    CyclePolicyContentHash = NULL,
                    ProfileProvenanceStatus = NULL;
                DELETE FROM dbo.GameLifecycleEvents;
                DELETE FROM dbo.GameEnrolments;
                DELETE FROM dbo.CycleConfigurations;
                DELETE FROM dbo.Games;
                DELETE FROM dbo.SchemaMigrations WHERE MigrationID = N'022_add_game_foundations';
                """);
        }

        var resumed = migrator.MigrateThrough("022_add_game_foundations");

        Assert.Equal("022_add_game_foundations", Assert.Single(resumed).MigrationId);
        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(1, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.Games WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal(2, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.CycleConfigurations WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal(3, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.GameEnrolments WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal(1, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.GameLifecycleEvents WHERE GameLifecycleEventID = @ID;", LegacyLifecycleEventId));
    }

    [Fact]
    public void Migration_022_enforces_one_operational_cycle_per_game_and_allows_different_games()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);
        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        var secondGameId = Guid.NewGuid();
        var secondCycleId = Guid.NewGuid();
        Execute(connection, """
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            INSERT INTO dbo.Games
            (
                GameID, Name, Purpose, Status, Visibility, CreationSource,
                GamePolicyKey, GamePolicyVersion, PolicyProvenanceStatus,
                CreatedAt, FirstStartedAt
            )
            VALUES
            (
                @GameID, N'Second Game', N'Standard', N'Active', N'Private', N'LegacyImport',
                N'test-policy', 1, N'Verified', @Now, @Now
            );
            INSERT INTO dbo.CycleConfigurations
            (
                CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus,
                MapProfileKey, ScenarioProfileKey, CyclePolicyKey, CyclePolicyVersion,
                CreatedAt, LockedAt, MaterializedAt
            )
            VALUES
            (
                @CycleID, @GameID, 1, N'Materialized', N'Verified',
                N'test-map', N'test-scenario', N'test-policy', 1, @Now, @Now, @Now
            );
            INSERT INTO dbo.Cycles
            (
                CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt,
                GameID, CycleConfigurationID, MapProfileKey, ScenarioProfileKey,
                CyclePolicyKey, CyclePolicyVersion, ProfileProvenanceStatus
            )
            VALUES
            (
                @CycleID, N'Second Cycle', @Now, DATEADD(DAY, 1, @Now), 60, 0, N'Active', @Now,
                @GameID, @CycleID, N'test-map', N'test-scenario', N'test-policy', 1, N'Verified'
            );
            """, ("@GameID", secondGameId), ("@CycleID", secondCycleId));
        Assert.Equal(2, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.Cycles WHERE Status IN (N'Active', N'RecoveryRequired');"));

        var error = Assert.Throws<SqlException>(() => Execute(connection,
            "UPDATE dbo.Cycles SET Status = N'RecoveryRequired' WHERE CycleID = @CycleID;",
            ("@CycleID", legacy.CompletedCycleId)));

        Assert.True(error.Number is 2601 or 2627, $"Unexpected SQL error {error.Number}: {error.Message}");
        Assert.Contains("UX_Cycles_Game_OperationalSlot", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_022_rejects_half_specified_human_seat_bounds()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        var gameId = Guid.NewGuid();
        Execute(connection, """
            INSERT INTO dbo.Games
            (
                GameID, Name, Purpose, Status, Visibility, CreationSource,
                GamePolicyKey, GamePolicyVersion, PolicyProvenanceStatus, CreatedAt
            )
            VALUES
            (
                @GameID, N'Seat bounds test', N'Standard', N'Forming', N'Private', N'Operator',
                N'test-policy', 1, N'Verified', SYSDATETIMEOFFSET()
            );
            """, ("@GameID", gameId));

        var missingMaximum = Assert.Throws<SqlException>(() => Execute(connection, """
            INSERT INTO dbo.CycleConfigurations
            (
                CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus,
                CyclePolicyKey, CyclePolicyVersion, MinimumHumanSeats, MaximumHumanSeats, CreatedAt
            )
            VALUES
            (
                NEWID(), @GameID, 1, N'Draft', N'Verified',
                N'test-policy', 1, 1, NULL, SYSDATETIMEOFFSET()
            );
            """, ("@GameID", gameId)));
        var missingMinimum = Assert.Throws<SqlException>(() => Execute(connection, """
            INSERT INTO dbo.CycleConfigurations
            (
                CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus,
                CyclePolicyKey, CyclePolicyVersion, MinimumHumanSeats, MaximumHumanSeats, CreatedAt
            )
            VALUES
            (
                NEWID(), @GameID, 2, N'Draft', N'Verified',
                N'test-policy', 1, NULL, 4, SYSDATETIMEOFFSET()
            );
            """, ("@GameID", gameId)));

        Assert.Equal(547, missingMaximum.Number);
        Assert.Equal(547, missingMinimum.Number);
        Assert.Contains("HumanSeats", missingMaximum.Message, StringComparison.Ordinal);
        Assert.Contains("HumanSeats", missingMinimum.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_022_rejects_short_content_hashes()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        Assert.Equal(3, Scalar<int>(connection, """
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE name IN
            (
                N'CK_Games_GamePolicyContentHash',
                N'CK_CycleConfigurations_ContentHashes',
                N'CK_Cycles_ContentHashes'
            );
            """));
        var error = Assert.Throws<SqlException>(() => Execute(connection, """
            INSERT INTO dbo.Games
            (
                GameID, Name, Purpose, Status, Visibility, CreationSource,
                GamePolicyKey, GamePolicyVersion, GamePolicyContentHash,
                PolicyProvenanceStatus, CreatedAt
            )
            VALUES
            (
                NEWID(), N'Invalid hash test', N'Standard', N'Forming', N'Private', N'Operator',
                N'test-policy', 1, N'abc123', N'Verified', SYSDATETIMEOFFSET()
            );
            """));

        Assert.Equal(547, error.Number);
        Assert.Contains("CK_Games_GamePolicyContentHash", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_022_keeps_materialized_configuration_snapshots_immutable()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);
        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();

        var provenanceError = Assert.Throws<SqlException>(() => Execute(connection, """
            UPDATE dbo.CycleConfigurations
            SET MapSeed = 42
            WHERE CycleConfigurationID = @CycleID;
            """, ("@CycleID", legacy.ActiveCycleId)));
        var timestampError = Assert.Throws<SqlException>(() => Execute(connection, """
            UPDATE dbo.CycleConfigurations
            SET LockedAt = DATEADD(MINUTE, 1, LockedAt)
            WHERE CycleConfigurationID = @CycleID;
            """, ("@CycleID", legacy.ActiveCycleId)));

        Assert.Equal(51030, provenanceError.Number);
        Assert.Equal(51030, timestampError.Number);
    }

    [Fact]
    public void Migration_022_prevents_cycle_lineage_forks()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);
        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Execute(connection, """
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            INSERT INTO dbo.Cycles
            (
                CycleID, GameID, PreviousCycleID, Name, StartAt, EndAt,
                TickLengthMinutes, CurrentTickNumber, Status, CreatedAt
            )
            VALUES
            (
                NEWID(), @GameID, @PreviousCycleID, N'First successor', @Now, DATEADD(DAY, 1, @Now),
                60, 0, N'Completed', @Now
            );
            """, ("@GameID", LegacyGameId), ("@PreviousCycleID", legacy.CompletedCycleId));

        var error = Assert.Throws<SqlException>(() => Execute(connection, """
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            INSERT INTO dbo.Cycles
            (
                CycleID, GameID, PreviousCycleID, Name, StartAt, EndAt,
                TickLengthMinutes, CurrentTickNumber, Status, CreatedAt
            )
            VALUES
            (
                NEWID(), @GameID, @PreviousCycleID, N'Second successor', @Now, DATEADD(DAY, 1, @Now),
                60, 0, N'Completed', @Now
            );
            """, ("@GameID", LegacyGameId), ("@PreviousCycleID", legacy.CompletedCycleId)));

        Assert.True(error.Number is 2601 or 2627, $"Unexpected SQL error {error.Number}: {error.Message}");
        Assert.Contains("UX_Cycles_PreviousCycleID", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_022_maps_a_recovery_required_only_lineage_to_an_active_game()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var cycleId = Guid.NewGuid();
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            InsertCycle(connection, cycleId, "RecoveryRequired", 0);
        }

        new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");

        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal("Active", Scalar<string>(verification,
            "SELECT Status FROM dbo.Games WHERE GameID = @ID;", LegacyGameId));
        Assert.Equal((byte)1, Scalar<byte>(verification,
            "SELECT OperationalSlot FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
        Assert.Equal("legacy-unclassified", Scalar<string>(verification,
            "SELECT MapProfileKey FROM dbo.Cycles WHERE CycleID = @ID;", cycleId));
    }

    [Theory]
    [InlineData("GameIdentity", 51025)]
    [InlineData("CyclePredecessor", 51026)]
    [InlineData("ConfigurationSchedule", 51027)]
    public void Migration_022_fails_closed_on_conflicting_partially_expanded_foundations(
        string conflict,
        int expectedError)
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");
        var legacy = InsertLegacyLineage(database.ConnectionString);
        var migrator = new SqlServerMigrator(database.ConnectionString);
        migrator.MigrateThrough("022_add_game_foundations");
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            switch (conflict)
            {
                case "GameIdentity":
                    Execute(connection,
                        "UPDATE dbo.Games SET Name = N'Conflicting Legacy Name' WHERE GameID = @GameID;",
                        ("@GameID", LegacyGameId));
                    break;
                case "CyclePredecessor":
                    Execute(connection,
                        "UPDATE dbo.Cycles SET PreviousCycleID = @PreviousCycleID WHERE CycleID = @CycleID;",
                        ("@PreviousCycleID", legacy.CompletedCycleId), ("@CycleID", legacy.ActiveCycleId));
                    break;
                case "ConfigurationSchedule":
                    Execute(connection, """
                        DISABLE TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance
                            ON dbo.CycleConfigurations;
                        UPDATE dbo.CycleConfigurations
                        SET ScheduledStartAt = DATEADD(DAY, 1, ScheduledStartAt)
                        WHERE CycleConfigurationID = @CycleID;
                        ENABLE TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance
                            ON dbo.CycleConfigurations;
                        """, ("@CycleID", legacy.ActiveCycleId));
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(conflict), conflict, null);
            }
            Execute(connection,
                "DELETE FROM dbo.SchemaMigrations WHERE MigrationID = N'022_add_game_foundations';");
        }

        var error = Assert.Throws<SqlException>(() => migrator.MigrateThrough("022_add_game_foundations"));

        Assert.Equal(expectedError, error.Number);
        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(0, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = N'022_add_game_foundations';"));
    }

    [Fact]
    public void Migration_022_supports_an_empty_database_without_inventing_a_legacy_game()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "021_add_empire_doctrine_unlocks");

        var applied = new SqlServerMigrator(database.ConnectionString).MigrateThrough("022_add_game_foundations");

        Assert.Equal("022_add_game_foundations", Assert.Single(applied).MigrationId);
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(0, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.Games;"));
        Assert.Equal(0, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.CycleConfigurations;"));
        Assert.Equal(0, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.GameEnrolments;"));
        Assert.Equal(0, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.GameLifecycleEvents;"));
    }

    private static LegacyLineage InsertLegacyLineage(string connectionString)
    {
        var ids = new LegacyLineage(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        Execute(connection, """
            DECLARE @CompletedAt DATETIMEOFFSET = '2026-01-01T00:00:00+00:00';
            DECLARE @ActiveAt DATETIMEOFFSET = '2026-02-01T00:00:00+00:00';
            DECLARE @CompletedSystemA UNIQUEIDENTIFIER = NEWID();
            DECLARE @CompletedSystemB UNIQUEIDENTIFIER = NEWID();
            DECLARE @ActiveSystemA UNIQUEIDENTIFIER = NEWID();
            DECLARE @ActiveSystemB UNIQUEIDENTIFIER = NEWID();
            DECLARE @CompletedEmpireA UNIQUEIDENTIFIER = NEWID();
            DECLARE @CompletedEmpireB UNIQUEIDENTIFIER = NEWID();
            DECLARE @ActiveEmpireA UNIQUEIDENTIFIER = NEWID();
            DECLARE @ActiveEmpireB UNIQUEIDENTIFIER = NEWID();

            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, Role, CreatedAt, Status)
            VALUES
                (@CurrentPlayerID, N'Current', N'', N'', N'Player', @CompletedAt, N'Active'),
                (@HistoricalPlayerID, N'Historical', N'', N'', N'Player', @CompletedAt, N'Active'),
                (@WithdrawnPlayerID, N'Withdrawn', N'', N'', N'Player', @ActiveAt, N'Active');
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES
                (@CompletedCycleID, N'Completed Legacy Cycle', @CompletedAt, DATEADD(DAY, 10, @CompletedAt), 60, 10, N'Completed', @CompletedAt),
                (@ActiveCycleID, N'Active Legacy Cycle', @ActiveAt, DATEADD(DAY, 90, @ActiveAt), 60, 2, N'Active', @ActiveAt);
            INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES
                (@CompletedSystemA, @CompletedCycleID, N'Completed A', 0, 0, 1, 1, 1, 1, 0, @CompletedAt),
                (@CompletedSystemB, @CompletedCycleID, N'Completed B', 1, 1, 1, 1, 1, 1, 0, @CompletedAt),
                (@ActiveSystemA, @ActiveCycleID, N'Active A', 0, 0, 1, 1, 1, 1, 0, @ActiveAt),
                (@ActiveSystemB, @ActiveCycleID, N'Active B', 1, 1, 1, 1, 1, 1, 0, @ActiveAt);
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES
                (@CompletedEmpireA, @CompletedCycleID, @CurrentPlayerID, N'Prior Current', @CompletedSystemA, @CompletedAt, N'Active'),
                (@CompletedEmpireB, @CompletedCycleID, @HistoricalPlayerID, N'Historical Empire', @CompletedSystemB, @CompletedAt, N'Active'),
                (@ActiveEmpireA, @ActiveCycleID, @CurrentPlayerID, N'Current Empire', @ActiveSystemA, @ActiveAt, N'Active'),
                (@ActiveEmpireB, @ActiveCycleID, @WithdrawnPlayerID, N'Withdrawn Empire', @ActiveSystemB, @ActiveAt, N'Active');
            INSERT INTO dbo.MatchParticipants(MatchParticipantID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
            VALUES
                (NEWID(), @CompletedCycleID, @CurrentPlayerID, @CompletedEmpireA, N'Completed', @CompletedAt, DATEADD(DAY, 10, @CompletedAt)),
                (NEWID(), @CompletedCycleID, @HistoricalPlayerID, @CompletedEmpireB, N'Completed', @CompletedAt, DATEADD(DAY, 10, @CompletedAt)),
                (NEWID(), @ActiveCycleID, @CurrentPlayerID, @ActiveEmpireA, N'Active', @ActiveAt, NULL),
                (NEWID(), @ActiveCycleID, @WithdrawnPlayerID, @ActiveEmpireB, N'Withdrawn', @ActiveAt, DATEADD(DAY, 1, @ActiveAt));
            """,
            ("@CurrentPlayerID", ids.CurrentPlayerId),
            ("@HistoricalPlayerID", ids.HistoricalPlayerId),
            ("@WithdrawnPlayerID", ids.WithdrawnPlayerId),
            ("@CompletedCycleID", ids.CompletedCycleId),
            ("@ActiveCycleID", ids.ActiveCycleId));
        return ids;
    }

    private static Guid InsertCanonicalFactState(string connectionString)
    {
        var cycleId = Guid.NewGuid();
        var playerId = Guid.NewGuid();
        var empireId = Guid.NewGuid();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        Execute(connection, """
            DECLARE @Now DATETIMEOFFSET = '2026-03-01T00:00:00+00:00';
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, Role, CreatedAt, Status)
            VALUES (@PlayerID, N'Canonical Player', N'', N'', N'Player', @Now, N'Active');
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, N'Canonical Cycle', @Now, DATEADD(DAY, 90, @Now), 60, 0, N'Active', @Now);

            ;WITH Numbers AS
            (
                SELECT TOP (8) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Number
                FROM sys.all_objects
            )
            INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
            SELECT NEWID(), @CycleID, CONCAT(N'Sector ', Number), Number * 10, Number * 10, Number
            FROM Numbers;

            ;WITH Numbers AS
            (
                SELECT TOP (64) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Number
                FROM sys.all_objects
            )
            INSERT INTO dbo.Systems
            (
                SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput,
                PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt, SectorID
            )
            SELECT
                NEWID(), @CycleID, CONCAT(N'System ', Number), Number, Number,
                1, 1, 1, 1, 0, @Now,
                (SELECT SectorID FROM dbo.GalaxySectors WHERE CycleID = @CycleID AND SortOrder = Number / 8)
            FROM Numbers;

            ;WITH OrderedSystems AS
            (
                SELECT SystemID, ROW_NUMBER() OVER (ORDER BY SystemName) - 1 AS Number
                FROM dbo.Systems
                WHERE CycleID = @CycleID
            ),
            LinkNumbers AS
            (
                SELECT TOP (91) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS Number
                FROM sys.all_objects
            )
            INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
            SELECT
                NEWID(), @CycleID, firstSystem.SystemID, secondSystem.SystemID, 1, 1
            FROM LinkNumbers AS link
            INNER JOIN OrderedSystems AS firstSystem ON firstSystem.Number = link.Number % 64
            INNER JOIN OrderedSystems AS secondSystem
                ON secondSystem.Number =
                    CASE
                        WHEN link.Number < 64 THEN (link.Number + 1) % 64
                        ELSE (link.Number - 64 + 2) % 64
                    END;

            DECLARE @HomeSystemID UNIQUEIDENTIFIER =
                (SELECT TOP (1) SystemID FROM dbo.Systems WHERE CycleID = @CycleID ORDER BY SystemName);
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES (@EmpireID, @CycleID, @PlayerID, N'Canonical Empire', @HomeSystemID, @Now, N'Active');
            INSERT INTO dbo.MatchParticipants(MatchParticipantID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
            VALUES (NEWID(), @CycleID, @PlayerID, @EmpireID, N'Active', @Now, NULL);
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES
                (NEWID(), @CycleID, 0, N'CycleSeeded', NULL, N'Normal',
                    N'{"topologyKey":"territorial-graph-v2","seed":71421,"empireCount":1,"systemCount":64,"sectorCount":8}',
                    N'Canonical seed fact', @Now),
                (NEWID(), @CycleID, 0, N'OpeningBriefingIssued', @EmpireID, N'High',
                    N'{"scenarioKey":"development-match-v2","scenarioSeed":20260717,"mapVersion":"territorial-graph-v2","setupAlgorithmVersion":1}',
                    N'Canonical briefing fact', @Now);
            """, ("@CycleID", cycleId), ("@PlayerID", playerId), ("@EmpireID", empireId));
        return cycleId;
    }

    private static void InsertCycle(SqlConnection connection, Guid cycleId, string status, int dayOffset) =>
        Execute(connection, """
            DECLARE @StartAt DATETIMEOFFSET = DATEADD(DAY, @DayOffset, CONVERT(DATETIMEOFFSET, '2026-01-01T00:00:00+00:00'));
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, N'Contradictory Cycle', @StartAt, DATEADD(DAY, 30, @StartAt), 60, 0, @Status, @StartAt);
            """, ("@CycleID", cycleId), ("@Status", status), ("@DayOffset", dayOffset));

    private static T Scalar<T>(SqlConnection connection, string sql, Guid? id = null)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        if (id.HasValue)
        {
            Add(command, "@ID", id.Value);
        }
        return (T)command.ExecuteScalar()!;
    }

    private static void Execute(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            Add(command, name, value);
        }
        command.ExecuteNonQuery();
    }

    private static void Add(SqlCommand command, string name, object value)
    {
        var parameter = command.Parameters.AddWithValue(name, value);
        if (value is Guid)
        {
            parameter.SqlDbType = System.Data.SqlDbType.UniqueIdentifier;
        }
    }

    private sealed record LegacyLineage(
        Guid CurrentPlayerId,
        Guid HistoricalPlayerId,
        Guid WithdrawnPlayerId,
        Guid CompletedCycleId,
        Guid ActiveCycleId);
}
