using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerMatchMigrationIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = SqlIntegrationGuard.ConnectionStringEnvironmentVariable;

    [Fact]
    public void Migration_017_backfills_populated_legacy_match_ownership()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "016_enforce_one_pending_fleet_order");
        var legacy = InsertLegacyMatch(database.ConnectionString);

        var applied = new SqlServerMigrator(database.ConnectionString).Migrate();

        Assert.Equal(
            [
                "017_add_match_participants_and_factions",
                "018_enforce_match_faction_integrity",
                "019_add_turn_resolution_ledger",
                "020_add_fleet_departure_tick",
                "021_add_empire_doctrine_unlocks",
                "022_add_game_foundations",
                "023_enforce_cycle_scope_integrity",
                "024_enforce_external_identity_binary_collation"
            ],
            applied.Take(8).Select(item => item.MigrationId));
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(2, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.Factions WHERE Kind = N'Empire';"));
        Assert.Equal(2, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.MatchParticipants WHERE Status = N'Active';"));
        Assert.Equal("Human", Scalar<string>(connection, "SELECT PlayerKind FROM dbo.Players WHERE PlayerID = @ID;", legacy.FirstPlayerId));
        Assert.Equal(legacy.FirstEmpireId, Scalar<Guid>(connection, "SELECT FactionID FROM dbo.Fleets WHERE FleetID = @ID;", legacy.FleetId));
        Assert.Equal(legacy.SecondEmpireId, Scalar<Guid>(connection, "SELECT TargetFactionID FROM dbo.FleetOrders WHERE FleetOrderID = @ID;", legacy.OrderId));
        Assert.Equal(legacy.FirstEmpireId, Scalar<Guid>(connection, "SELECT FactionID FROM dbo.Events WHERE EventID = @ID;", legacy.EventId));
        Assert.Equal(legacy.FirstEmpireId, Scalar<Guid>(connection, "SELECT AttackerFactionID FROM dbo.BattleRecords WHERE BattleID = @ID;", legacy.BattleId));
        Assert.Equal(legacy.SecondEmpireId, Scalar<Guid>(connection, "SELECT DefenderFactionID FROM dbo.BattleRecords WHERE BattleID = @ID;", legacy.BattleId));
        Assert.Equal("CommandOpen", Scalar<string>(connection, "SELECT TurnStage FROM dbo.Cycles WHERE CycleID = @ID;", legacy.CycleId));
        Assert.Equal("Human", Scalar<string>(connection, "SELECT CommandSource FROM dbo.FleetOrders WHERE FleetOrderID = @ID;", legacy.OrderId));
        Assert.Equal(1, Scalar<int>(connection, "SELECT CAST(is_nullable AS int) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Fleets') AND name = N'EmpireID';"));
        Assert.Equal(1, Scalar<int>(connection, "SELECT CAST(is_nullable AS int) FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.BattleRecords') AND name = N'DefenderEmpireID';"));
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_MatchParticipants_EmpireOwnership';"));
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM sys.foreign_keys WHERE name = N'FK_Fleets_FactionsInCycle';"));
    }

    [Fact]
    public void Migration_017_rejects_a_player_controlling_two_legacy_empires_in_one_cycle()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "016_enforce_one_pending_fleet_order");
        var legacy = InsertLegacyMatch(database.ConnectionString);
        using (var connection = new SqlConnection(database.ConnectionString))
        {
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE dbo.Empires SET PlayerID = @PlayerID WHERE EmpireID = @EmpireID;";
            command.Parameters.AddWithValue("@PlayerID", legacy.FirstPlayerId);
            command.Parameters.AddWithValue("@EmpireID", legacy.SecondEmpireId);
            command.ExecuteNonQuery();
        }

        var error = Assert.Throws<SqlException>(() => new SqlServerMigrator(database.ConnectionString).Migrate());

        Assert.Equal(51018, error.Number);
        Assert.Contains("more than one Empire", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_021_backfills_one_doctrine_record_without_rewriting_unlock_events()
    {
        var serverConnectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(serverConnectionString, "020_add_fleet_departure_tick");
        var legacy = InsertLegacyDoctrineEvents(database.ConnectionString);

        var applied = new SqlServerMigrator(database.ConnectionString).Migrate();

        Assert.Equal(
            [
                "021_add_empire_doctrine_unlocks",
                "022_add_game_foundations",
                "023_enforce_cycle_scope_integrity",
                "024_enforce_external_identity_binary_collation"
            ],
            applied.Take(4).Select(item => item.MigrationId));
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        Assert.Equal(1, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.EmpireDoctrineUnlocks WHERE CycleID = @ID;", legacy.CycleId));
        Assert.Equal(1, Scalar<int>(connection, "SELECT UnlockedTickNumber FROM dbo.EmpireDoctrineUnlocks WHERE EmpireID = @ID;", legacy.EmpireId));
        Assert.Equal(2, Scalar<int>(connection, "SELECT COUNT(*) FROM dbo.Events WHERE CycleID = @ID AND EventType = N'DoctrineUnlocked';", legacy.CycleId));
    }

    private static LegacyMatch InsertLegacyMatch(string connectionString)
    {
        var ids = new LegacyMatch(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            Guid.NewGuid());
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, Role, CreatedAt, Status)
            VALUES (@FirstPlayerID, N'Legacy One', N'', N'', N'Player', @Now, N'Active'),
                   (@SecondPlayerID, N'Legacy Two', N'', N'', N'Player', @Now, N'Active');
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, N'Legacy Cycle', @Now, DATEADD(DAY, 90, @Now), 60, 0, N'Active', @Now);
            INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES (@FirstSystemID, @CycleID, N'First Home', 0, 0, 100, 100, 100, 20, 0, @Now),
                   (@SecondSystemID, @CycleID, N'Second Home', 10, 10, 100, 100, 100, 20, 0, @Now);
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES (@FirstEmpireID, @CycleID, @FirstPlayerID, N'First Empire', @FirstSystemID, @Now, N'Active'),
                   (@SecondEmpireID, @CycleID, @SecondPlayerID, N'Second Empire', @SecondSystemID, @Now, N'Active');
            INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, FleetName, CurrentSystemID, ShipCount, Status, CreatedAt)
            VALUES (@FleetID, @CycleID, @FirstEmpireID, N'Legacy Fleet', @FirstSystemID, 10, N'Active', @Now),
                   (@DefenderFleetID, @CycleID, @SecondEmpireID, N'Legacy Defender', @SecondSystemID, 8, N'Active', @Now);
            INSERT INTO dbo.FleetOrders(FleetOrderID, CycleID, FleetID, OrderType, TargetEmpireID, SubmitTick, ExecuteAfterTick, Status, CreatedAt)
            VALUES (@OrderID, @CycleID, @FleetID, N'Attack', @SecondEmpireID, 0, 1, N'Pending', @Now);
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES (@EventID, @CycleID, 0, N'CycleSeeded', @FirstEmpireID, N'Low', N'{}', N'Legacy event', @Now);
            INSERT INTO dbo.BattleRecords(BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore, AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES (@BattleID, @CycleID, 0, @FirstSystemID, @FirstEmpireID, @SecondEmpireID,
                    CONVERT(NVARCHAR(36), @FleetID), CONVERT(NVARCHAR(36), @DefenderFleetID),
                    10, 8, 1, 2, N'AttackerVictory', N'{}', @Now);
            """;
        Add(command, "@FirstPlayerID", ids.FirstPlayerId);
        Add(command, "@SecondPlayerID", ids.SecondPlayerId);
        Add(command, "@CycleID", ids.CycleId);
        Add(command, "@FirstSystemID", ids.FirstSystemId);
        Add(command, "@SecondSystemID", ids.SecondSystemId);
        Add(command, "@FirstEmpireID", ids.FirstEmpireId);
        Add(command, "@SecondEmpireID", ids.SecondEmpireId);
        Add(command, "@FleetID", ids.FleetId);
        Add(command, "@DefenderFleetID", ids.DefenderFleetId);
        Add(command, "@OrderID", ids.OrderId);
        Add(command, "@EventID", ids.EventId);
        Add(command, "@BattleID", ids.BattleId);
        command.ExecuteNonQuery();
        return ids;
    }

    private static LegacyDoctrineState InsertLegacyDoctrineEvents(string connectionString)
    {
        var ids = new LegacyDoctrineState(Guid.NewGuid(), Guid.NewGuid());
        var playerId = Guid.NewGuid();
        var systemId = Guid.NewGuid();
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, Role, CreatedAt, Status)
            VALUES (@PlayerID, N'Legacy Researcher', N'', N'', N'Player', @Now, N'Active');
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, N'Legacy Doctrine Cycle', @Now, DATEADD(DAY, 90, @Now), 60, 2, N'Active', @Now);
            INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES (@SystemID, @CycleID, N'Research Home', 0, 0, 0, 0, 0, 10, 0, @Now);
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES (@EmpireID, @CycleID, @PlayerID, N'Legacy Researchers', @SystemID, @Now, N'Active');
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES (NEWID(), @CycleID, 1, N'DoctrineUnlocked', @EmpireID, N'Normal', N'{"doctrine":"survey-projection"}', N'First unlock', @Now),
                   (NEWID(), @CycleID, 2, N'DoctrineUnlocked', @EmpireID, N'Normal', N'{"doctrine":"survey-projection"}', N'Duplicate unlock', DATEADD(HOUR, 1, @Now));
            """;
        Add(command, "@PlayerID", playerId);
        Add(command, "@CycleID", ids.CycleId);
        Add(command, "@SystemID", systemId);
        Add(command, "@EmpireID", ids.EmpireId);
        command.ExecuteNonQuery();
        return ids;
    }

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

    private static void Add(SqlCommand command, string name, Guid value) =>
        command.Parameters.Add(new SqlParameter(name, System.Data.SqlDbType.UniqueIdentifier) { Value = value });

    private sealed record LegacyMatch(
        Guid FirstPlayerId,
        Guid SecondPlayerId,
        Guid CycleId,
        Guid FirstSystemId,
        Guid SecondSystemId,
        Guid FirstEmpireId,
        Guid SecondEmpireId,
        Guid FleetId,
        Guid DefenderFleetId,
        Guid OrderId,
        Guid EventId,
        Guid BattleId);

    private sealed record LegacyDoctrineState(Guid CycleId, Guid EmpireId);
}
