using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleScopeMigrationIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = SqlIntegrationGuard.ConnectionStringEnvironmentVariable;
    private const string MigrationId = "023_enforce_cycle_scope_integrity";

    [Fact]
    public void Migration_023_backfills_participant_games_and_csv_and_json_battle_membership()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "022_add_game_foundations");
        var fixture = InsertVersion22Fixture(database.ConnectionString, Version22FixtureFault.None);

        var migration = Assert.Single(
            new SqlServerMigrator(database.ConnectionString).Migrate(),
            item => item.MigrationId == MigrationId);

        Assert.Equal(MigrationId, migration.MigrationId);
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(4, Scalar<int>(connection, """
            SELECT COUNT(*)
            FROM dbo.MatchParticipants AS participant
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.CycleID = participant.CycleID
               AND cycle.GameID = participant.GameID;
            """));

        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT BattleID, FleetID, Side
            FROM dbo.BattleFleetParticipants
            ORDER BY BattleID, Side, FleetID;
            """;
        using var reader = command.ExecuteReader();
        var memberships = new List<(Guid BattleId, Guid FleetId, string Side)>();
        while (reader.Read())
        {
            memberships.Add((reader.GetGuid(0), reader.GetGuid(1), reader.GetString(2)));
        }
        reader.Close();

        var expectedMemberships = new HashSet<(Guid BattleId, Guid FleetId, string Side)>
        {
            (fixture.CsvBattleId, fixture.AttackerFleetId, "Attacker"),
            (fixture.CsvBattleId, fixture.DefenderFleetId, "Defender"),
            (fixture.JsonBattleId, fixture.DefenderFleetId, "Attacker"),
            (fixture.JsonBattleId, fixture.AttackerFleetId, "Defender")
        };
        Assert.True(expectedMemberships.SetEquals(memberships));
        Assert.Equal(0, Scalar<int>(connection,
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants') AND name = N'GameID' AND is_nullable = 1;"));
    }

    [Fact]
    public void Migration_023_rejects_non_string_json_battle_membership_atomically()
        => AssertFailedMigrationRollsBack(Version22FixtureFault.NonStringJsonToken, 51038);

    [Fact]
    public void Migration_023_rejects_a_valid_guid_with_a_suffix_atomically()
        => AssertFailedMigrationRollsBack(Version22FixtureFault.SuffixedGuidToken, 51039);

    [Fact]
    public void Migration_023_rejects_cross_cycle_battle_membership_atomically()
        => AssertFailedMigrationRollsBack(Version22FixtureFault.CrossCycleFleet, 51042);

    [Fact]
    public void Migration_023_rejects_a_participant_without_same_game_enrolment_atomically()
        => AssertFailedMigrationRollsBack(Version22FixtureFault.MissingGameEnrolment, 51047);

    [Fact]
    public void Migration_023_is_restartable_without_duplicate_battle_membership()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "022_add_game_foundations");
        InsertVersion22Fixture(database.ConnectionString, Version22FixtureFault.None);
        Assert.Equal(
            MigrationId,
            Assert.Single(
                new SqlServerMigrator(database.ConnectionString).Migrate(),
                item => item.MigrationId == MigrationId).MigrationId);

        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            Assert.Equal(4, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.BattleFleetParticipants;"));
            Execute(connection, "DELETE FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;", ("@MigrationID", MigrationId));
        }

        Assert.Equal(
            MigrationId,
            Assert.Single(
                new SqlServerMigrator(database.ConnectionString).Migrate(),
                item => item.MigrationId == MigrationId).MigrationId);

        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(4, Scalar<int>(verification, "SELECT COUNT(*) FROM dbo.BattleFleetParticipants;"));
        Assert.Equal(4, Scalar<int>(verification, """
            SELECT COUNT(*)
            FROM
            (
                SELECT BattleID, FleetID
                FROM dbo.BattleFleetParticipants
                GROUP BY BattleID, FleetID
                HAVING COUNT(*) = 1
            ) AS uniqueMembership;
            """));
        Assert.Equal(1, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
            ("@MigrationID", MigrationId)));
    }

    [Fact]
    public void Migration_023_rejects_a_preexisting_non_zero_membership_subset_atomically()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "022_add_game_foundations");
        var fixture = InsertVersion22Fixture(database.ConnectionString, Version22FixtureFault.None);
        Assert.Equal(
            MigrationId,
            Assert.Single(
                new SqlServerMigrator(database.ConnectionString).Migrate(),
                item => item.MigrationId == MigrationId).MigrationId);
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            Execute(connection, """
                DELETE FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;
                DELETE FROM dbo.BattleFleetParticipants
                WHERE BattleID = @BattleID AND FleetID = @FleetID;
                """,
                ("@MigrationID", MigrationId),
                ("@BattleID", fixture.CsvBattleId),
                ("@FleetID", fixture.AttackerFleetId));
            Assert.Equal(1, Scalar<int>(connection,
                "SELECT COUNT(*) FROM dbo.BattleFleetParticipants WHERE BattleID = @BattleID;",
                ("@BattleID", fixture.CsvBattleId)));
        }

        var error = Assert.Throws<SqlException>(() => new SqlServerMigrator(database.ConnectionString).Migrate());

        Assert.Equal(51052, error.Number);
        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        Assert.Equal(1, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.BattleFleetParticipants WHERE BattleID = @BattleID;",
            ("@BattleID", fixture.CsvBattleId)));
        Assert.Equal(0, Scalar<int>(verification,
            "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
            ("@MigrationID", MigrationId)));
    }

    [Fact]
    public void Migration_023_rebuilds_wrongly_defined_named_battle_membership_objects()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "022_add_game_foundations");
        InsertVersion22Fixture(database.ConnectionString, Version22FixtureFault.None);
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            Execute(connection, """
                CREATE TABLE dbo.BattleFleetParticipants
                (
                    BattleID UNIQUEIDENTIFIER NOT NULL,
                    CycleID UNIQUEIDENTIFIER NOT NULL,
                    FleetID UNIQUEIDENTIFIER NOT NULL,
                    Side NVARCHAR(16) NOT NULL
                );
                ALTER TABLE dbo.BattleFleetParticipants WITH CHECK
                    ADD CONSTRAINT CK_BattleFleetParticipants_Side CHECK (Side <> N'');
                CREATE INDEX IX_BattleFleetParticipants_Fleet_Cycle_Battle
                    ON dbo.BattleFleetParticipants(CycleID, BattleID, FleetID);
                """);
        }

        Assert.Equal(
            MigrationId,
            Assert.Single(
                new SqlServerMigrator(database.ConnectionString).Migrate(),
                item => item.MigrationId == MigrationId).MigrationId);

        using var verification = new SqlConnection(database.ConnectionString);
        verification.Open();
        var checkDefinition = Scalar<string>(verification, """
            SELECT definition
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
              AND name = N'CK_BattleFleetParticipants_Side'
              AND is_disabled = 0
              AND is_not_trusted = 0;
            """);
        Assert.Contains("Attacker", checkDefinition, StringComparison.Ordinal);
        Assert.Contains("Defender", checkDefinition, StringComparison.Ordinal);

        using var indexCommand = verification.CreateCommand();
        indexCommand.CommandText = """
            SELECT columnMetadata.name
            FROM sys.indexes AS indexMetadata
            INNER JOIN sys.index_columns AS indexColumn
                ON indexColumn.object_id = indexMetadata.object_id
               AND indexColumn.index_id = indexMetadata.index_id
            INNER JOIN sys.columns AS columnMetadata
                ON columnMetadata.object_id = indexColumn.object_id
               AND columnMetadata.column_id = indexColumn.column_id
            WHERE indexMetadata.object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
              AND indexMetadata.name = N'IX_BattleFleetParticipants_Fleet_Cycle_Battle'
              AND indexMetadata.is_disabled = 0
              AND indexColumn.key_ordinal > 0
            ORDER BY indexColumn.key_ordinal;
            """;
        using var reader = indexCommand.ExecuteReader();
        var keyColumns = new List<string>();
        while (reader.Read())
        {
            keyColumns.Add(reader.GetString(0));
        }
        Assert.Equal(["FleetID", "CycleID", "BattleID"], keyColumns);
    }

    private static void AssertFailedMigrationRollsBack(Version22FixtureFault fault, int expectedErrorNumber)
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "022_add_game_foundations");
        var fixture = InsertVersion22Fixture(database.ConnectionString, fault);
        using var before = new SqlConnection(database.ConnectionString);
        before.Open();
        var originalAttackerFleetIds = Scalar<string>(before,
            "SELECT AttackerFleetIDs FROM dbo.BattleRecords WHERE BattleID = @BattleID;",
            ("@BattleID", fixture.CsvBattleId));

        var error = Assert.Throws<SqlException>(() => new SqlServerMigrator(database.ConnectionString).Migrate());

        Assert.Equal(expectedErrorNumber, error.Number);
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(0, Scalar<int>(connection,
            "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
            ("@MigrationID", MigrationId)));
        Assert.Equal(0, Scalar<int>(connection,
            "SELECT COUNT(*) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants') AND name = N'GameID';"));
        Assert.Equal(0, Scalar<int>(connection,
            "SELECT COUNT(*) FROM sys.tables WHERE object_id = OBJECT_ID(N'dbo.BattleFleetParticipants');"));
        Assert.Equal(originalAttackerFleetIds, Scalar<string>(connection,
            "SELECT AttackerFleetIDs FROM dbo.BattleRecords WHERE BattleID = @BattleID;",
            ("@BattleID", fixture.CsvBattleId)));
    }

    private static Version22Fixture InsertVersion22Fixture(string connectionString, Version22FixtureFault fault)
    {
        var fixture = Version22Fixture.Create();
        var csvAttackerFleetIds = fault switch
        {
            Version22FixtureFault.NonStringJsonToken => $"[\"{fixture.AttackerFleetId:D}\",7]",
            Version22FixtureFault.SuffixedGuidToken => $"{fixture.AttackerFleetId:D}JUNK",
            Version22FixtureFault.CrossCycleFleet => fixture.OtherCycleFleetId.ToString("D"),
            _ => fixture.AttackerFleetId.ToString("D")
        };
        var csvDefenderFleetIds = fixture.DefenderFleetId.ToString("D");
        var jsonAttackerFleetIds = $"[\"{fixture.DefenderFleetId:D}\"]";
        var jsonDefenderFleetIds = $"[\"{fixture.AttackerFleetId:D}\"]";

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        Execute(connection, """
            DECLARE @Now DATETIMEOFFSET = '2026-07-20T10:00:00+00:00';

            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@PlayerAID, N'Fixture A', N'', N'', N'', N'', N'Human', N'Player', @Now, NULL, N'Active'),
                (@PlayerBID, N'Fixture B', N'', N'', N'', N'', N'Human', N'Player', @Now, NULL, N'Active');

            INSERT INTO dbo.Games
                (GameID, Name, Purpose, Status, Visibility, CreationSource, GamePolicyKey, GamePolicyVersion, GamePolicyContentHash,
                 PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt, FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt)
            VALUES
                (@GameAID, N'Fixture Game A', N'Standard', N'Active', N'Private', N'Operator', N'fixture-policy', 1, NULL,
                 N'Verified', @PlayerAID, @Now, @Now, NULL, NULL, NULL),
                (@GameBID, N'Fixture Game B', N'Standard', N'Active', N'Private', N'Operator', N'fixture-policy', 1, NULL,
                 N'Verified', @PlayerBID, @Now, @Now, NULL, NULL, NULL);

            INSERT INTO dbo.CycleConfigurations
                (CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus, MapProfileKey, MapProfileVersion,
                 MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash,
                 ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, MinimumHumanSeats, MaximumHumanSeats,
                 ScheduledStartAt, ScheduledEndAt, TickLengthMinutes, CreatedAt, LockedAt, MaterializedAt, CancelledAt)
            VALUES
                (@ConfigurationAID, @GameAID, 1, N'Materialized', N'Verified', N'fixture-map', 1, NULL, 101,
                 N'fixture-scenario', 1, NULL, 201, N'fixture-cycle-policy', 1, NULL, 1, 2,
                 @Now, DATEADD(DAY, 30, @Now), 60, @Now, @Now, @Now, NULL),
                (@ConfigurationBID, @GameBID, 1, N'Materialized', N'Verified', N'fixture-map', 1, NULL, 102,
                 N'fixture-scenario', 1, NULL, 202, N'fixture-cycle-policy', 1, NULL, 1, 2,
                 @Now, DATEADD(DAY, 30, @Now), 60, @Now, @Now, @Now, NULL);

            INSERT INTO dbo.Cycles
                (CycleID, GameID, CycleConfigurationID, PreviousCycleID, Name, StartAt, EndAt, TickLengthMinutes,
                 CurrentTickNumber, Status, TurnStage, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed,
                 ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey,
                 CyclePolicyVersion, CyclePolicyContentHash, ProfileProvenanceStatus, CreatedByPlayerID, CreatedAt)
            VALUES
                (@CycleAID, @GameAID, @ConfigurationAID, NULL, N'Fixture Cycle A', @Now, DATEADD(DAY, 30, @Now), 60,
                 1, N'Active', N'CommandOpen', N'fixture-map', 1, NULL, 101, N'fixture-scenario', 1, NULL, 201,
                 N'fixture-cycle-policy', 1, NULL, N'Verified', @PlayerAID, @Now),
                (@CycleBID, @GameBID, @ConfigurationBID, NULL, N'Fixture Cycle B', @Now, DATEADD(DAY, 30, @Now), 60,
                 1, N'Active', N'CommandOpen', N'fixture-map', 1, NULL, 102, N'fixture-scenario', 1, NULL, 202,
                 N'fixture-cycle-policy', 1, NULL, N'Verified', @PlayerBID, @Now);

            INSERT INTO dbo.GameEnrolments
                (GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID, EnrolledAt, StatusChangedAt, EndedAt)
            SELECT @EnrolmentAAID, @GameAID, @PlayerAID, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL
            WHERE @OmitPrimaryEnrolment = 0;
            INSERT INTO dbo.GameEnrolments
                (GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID, EnrolledAt, StatusChangedAt, EndedAt)
            VALUES
                (@EnrolmentABID, @GameAID, @PlayerBID, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL),
                (@EnrolmentBAID, @GameBID, @PlayerAID, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL),
                (@EnrolmentBBID, @GameBID, @PlayerBID, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL);

            INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
            VALUES
                (@SectorAID, @CycleAID, N'Fixture Sector A', 0, 0, 0),
                (@SectorBID, @CycleBID, N'Fixture Sector B', 0, 0, 0);

            INSERT INTO dbo.Systems
                (SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput,
                 StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES
                (@SystemA1ID, @CycleAID, @SectorAID, N'Fixture A1', 0, 0, 1, 1, 1, 1, 0, @Now),
                (@SystemA2ID, @CycleAID, @SectorAID, N'Fixture A2', 1, 1, 1, 1, 1, 1, 0, @Now),
                (@SystemB1ID, @CycleBID, @SectorBID, N'Fixture B1', 0, 0, 1, 1, 1, 1, 0, @Now),
                (@SystemB2ID, @CycleBID, @SectorBID, N'Fixture B2', 1, 1, 1, 1, 1, 1, 0, @Now);

            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES
                (@EmpireA1ID, @CycleAID, @PlayerAID, N'Fixture Empire A1', @SystemA1ID, @Now, N'Active'),
                (@EmpireA2ID, @CycleAID, @PlayerBID, N'Fixture Empire A2', @SystemA2ID, @Now, N'Active'),
                (@EmpireB1ID, @CycleBID, @PlayerAID, N'Fixture Empire B1', @SystemB1ID, @Now, N'Active'),
                (@EmpireB2ID, @CycleBID, @PlayerBID, N'Fixture Empire B2', @SystemB2ID, @Now, N'Active');

            INSERT INTO dbo.Factions(FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt)
            VALUES
                (@EmpireA1ID, @CycleAID, @EmpireA1ID, N'Fixture Empire A1', N'Empire', N'Active', @Now),
                (@EmpireA2ID, @CycleAID, @EmpireA2ID, N'Fixture Empire A2', N'Empire', N'Active', @Now),
                (@EmpireB1ID, @CycleBID, @EmpireB1ID, N'Fixture Empire B1', N'Empire', N'Active', @Now),
                (@EmpireB2ID, @CycleBID, @EmpireB2ID, N'Fixture Empire B2', N'Empire', N'Active', @Now);

            INSERT INTO dbo.MatchParticipants(MatchParticipantID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
            VALUES
                (@ParticipantA1ID, @CycleAID, @PlayerAID, @EmpireA1ID, N'Active', @Now, NULL),
                (@ParticipantA2ID, @CycleAID, @PlayerBID, @EmpireA2ID, N'Active', @Now, NULL),
                (@ParticipantB1ID, @CycleBID, @PlayerAID, @EmpireB1ID, N'Active', @Now, NULL),
                (@ParticipantB2ID, @CycleBID, @PlayerBID, @EmpireB2ID, N'Active', @Now, NULL);

            INSERT INTO dbo.Fleets
                (FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID,
                 DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES
                (@FleetA1ID, @CycleAID, @EmpireA1ID, @EmpireA1ID, NULL, N'Fixture Fleet A1', @SystemA1ID, NULL, NULL, NULL, 10, N'Active', @Now),
                (@FleetA2ID, @CycleAID, @EmpireA2ID, @EmpireA2ID, NULL, N'Fixture Fleet A2', @SystemA2ID, NULL, NULL, NULL, 10, N'Active', @Now),
                (@FleetB1ID, @CycleBID, @EmpireB1ID, @EmpireB1ID, NULL, N'Fixture Fleet B1', @SystemB1ID, NULL, NULL, NULL, 10, N'Active', @Now),
                (@FleetB2ID, @CycleBID, @EmpireB2ID, @EmpireB2ID, NULL, N'Fixture Fleet B2', @SystemB2ID, NULL, NULL, NULL, 10, N'Active', @Now);

            INSERT INTO dbo.BattleRecords
                (BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFactionID,
                 DefenderFactionID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore,
                 AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES
                (@CsvBattleID, @CycleAID, 1, @SystemA1ID, @EmpireA1ID, @EmpireA2ID, @EmpireA1ID, @EmpireA2ID,
                 @CsvAttackerFleetIDs, @CsvDefenderFleetIDs, 10, 10, 1, 2, N'AttackerVictory', N'{}', @Now),
                (@JsonBattleID, @CycleAID, 1, @SystemA2ID, @EmpireA2ID, @EmpireA1ID, @EmpireA2ID, @EmpireA1ID,
                 @JsonAttackerFleetIDs, @JsonDefenderFleetIDs, 10, 10, 1, 2, N'DefenderVictory', N'{}', @Now);
            """,
            ("@PlayerAID", fixture.PlayerAId),
            ("@PlayerBID", fixture.PlayerBId),
            ("@GameAID", fixture.GameAId),
            ("@GameBID", fixture.GameBId),
            ("@ConfigurationAID", fixture.ConfigurationAId),
            ("@ConfigurationBID", fixture.ConfigurationBId),
            ("@CycleAID", fixture.CycleAId),
            ("@CycleBID", fixture.CycleBId),
            ("@EnrolmentAAID", fixture.EnrolmentAAId),
            ("@EnrolmentABID", fixture.EnrolmentABId),
            ("@EnrolmentBAID", fixture.EnrolmentBAId),
            ("@EnrolmentBBID", fixture.EnrolmentBBId),
            ("@OmitPrimaryEnrolment", fault == Version22FixtureFault.MissingGameEnrolment),
            ("@SectorAID", fixture.SectorAId),
            ("@SectorBID", fixture.SectorBId),
            ("@SystemA1ID", fixture.SystemA1Id),
            ("@SystemA2ID", fixture.SystemA2Id),
            ("@SystemB1ID", fixture.SystemB1Id),
            ("@SystemB2ID", fixture.SystemB2Id),
            ("@EmpireA1ID", fixture.EmpireA1Id),
            ("@EmpireA2ID", fixture.EmpireA2Id),
            ("@EmpireB1ID", fixture.EmpireB1Id),
            ("@EmpireB2ID", fixture.EmpireB2Id),
            ("@ParticipantA1ID", fixture.ParticipantA1Id),
            ("@ParticipantA2ID", fixture.ParticipantA2Id),
            ("@ParticipantB1ID", fixture.ParticipantB1Id),
            ("@ParticipantB2ID", fixture.ParticipantB2Id),
            ("@FleetA1ID", fixture.AttackerFleetId),
            ("@FleetA2ID", fixture.DefenderFleetId),
            ("@FleetB1ID", fixture.OtherCycleFleetId),
            ("@FleetB2ID", fixture.FleetB2Id),
            ("@CsvBattleID", fixture.CsvBattleId),
            ("@JsonBattleID", fixture.JsonBattleId),
            ("@CsvAttackerFleetIDs", csvAttackerFleetIds),
            ("@CsvDefenderFleetIDs", csvDefenderFleetIds),
            ("@JsonAttackerFleetIDs", jsonAttackerFleetIds),
            ("@JsonDefenderFleetIDs", jsonDefenderFleetIds));
        return fixture;
    }

    private static T Scalar<T>(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        return (T)command.ExecuteScalar()!;
    }

    private static void Execute(SqlConnection connection, string sql, params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        AddParameters(command, parameters);
        command.ExecuteNonQuery();
    }

    private static void AddParameters(SqlCommand command, IEnumerable<(string Name, object Value)> parameters)
    {
        foreach (var (name, value) in parameters)
        {
            var parameter = command.Parameters.AddWithValue(name, value);
            if (value is Guid)
            {
                parameter.SqlDbType = System.Data.SqlDbType.UniqueIdentifier;
            }
        }
    }

    private enum Version22FixtureFault
    {
        None,
        NonStringJsonToken,
        SuffixedGuidToken,
        CrossCycleFleet,
        MissingGameEnrolment
    }

    private sealed record Version22Fixture(
        Guid PlayerAId,
        Guid PlayerBId,
        Guid GameAId,
        Guid GameBId,
        Guid ConfigurationAId,
        Guid ConfigurationBId,
        Guid CycleAId,
        Guid CycleBId,
        Guid EnrolmentAAId,
        Guid EnrolmentABId,
        Guid EnrolmentBAId,
        Guid EnrolmentBBId,
        Guid SectorAId,
        Guid SectorBId,
        Guid SystemA1Id,
        Guid SystemA2Id,
        Guid SystemB1Id,
        Guid SystemB2Id,
        Guid EmpireA1Id,
        Guid EmpireA2Id,
        Guid EmpireB1Id,
        Guid EmpireB2Id,
        Guid ParticipantA1Id,
        Guid ParticipantA2Id,
        Guid ParticipantB1Id,
        Guid ParticipantB2Id,
        Guid AttackerFleetId,
        Guid DefenderFleetId,
        Guid OtherCycleFleetId,
        Guid FleetB2Id,
        Guid CsvBattleId,
        Guid JsonBattleId)
    {
        public static Version22Fixture Create() => new(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid());
    }
}
