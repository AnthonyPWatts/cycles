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
        var economySpending = Assert.Single(migrations, migration => migration.MigrationId == "003_add_economy_spending");
        var empireMetrics = Assert.Single(migrations, migration => migration.MigrationId == "004_add_empire_metrics");
        var playerRole = Assert.Single(migrations, migration => migration.MigrationId == "005_add_player_role");
        var cycleRankings = Assert.Single(migrations, migration => migration.MigrationId == "006_add_cycle_rankings");
        var cycleMajorEvents = Assert.Single(migrations, migration => migration.MigrationId == "007_add_cycle_major_events");
        var systemHistoricalSignals = Assert.Single(migrations, migration => migration.MigrationId == "008_add_system_historical_signals");
        var chronicleGenerationState = Assert.Single(migrations, migration => migration.MigrationId == "009_add_chronicle_generation_state");
        var admirals = Assert.Single(migrations, migration => migration.MigrationId == "010_add_admirals");
        var colonialOutposts = Assert.Single(migrations, migration => migration.MigrationId == "011_add_colonial_outposts");
        var diplomaticRelationships = Assert.Single(migrations, migration => migration.MigrationId == "012_add_diplomatic_relationships");
        var externalIdentityAndAdminAudit = Assert.Single(migrations, migration => migration.MigrationId == "013_add_external_identity_and_admin_audit");
        var inactivePriorities = Assert.Single(migrations, migration => migration.MigrationId == "014_lock_inactive_priorities");
        var galaxySectors = Assert.Single(migrations, migration => migration.MigrationId == "015_add_galaxy_sectors");
        var pendingFleetOrder = Assert.Single(migrations, migration => migration.MigrationId == "016_enforce_one_pending_fleet_order");
        var turnResolutionLedger = Assert.Single(migrations, migration => migration.MigrationId == "019_add_turn_resolution_ledger");
        var fleetDepartureTick = Assert.Single(migrations, migration => migration.MigrationId == "020_add_fleet_departure_tick");
        var doctrineUnlocks = Assert.Single(migrations, migration => migration.MigrationId == "021_add_empire_doctrine_unlocks");
        var gameScopeIntegrity = Assert.Single(migrations, migration => migration.MigrationId == "023_enforce_cycle_scope_integrity");

        Assert.Contains("SchemaMigrations", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.Players", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.TickLogs", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("NarrativeStatus", initialSchema.Script, StringComparison.Ordinal);
        Assert.Contains("UX_TickLogs_Cycle_Tick_Completed", retryHistory.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.ShipConstructions", economySpending.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.EmpireMetrics", empireMetrics.Script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE dbo.Players", playerRole.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.CycleRankings", cycleRankings.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.CycleMajorEvents", cycleMajorEvents.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.SystemHistoricalSignals", systemHistoricalSignals.Script, StringComparison.Ordinal);
        Assert.Contains("NarrativeStatus", chronicleGenerationState.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.Admirals", admirals.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.AdmiralBattleHistories", admirals.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.ColonialOutposts", colonialOutposts.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.DiplomaticRelationships", diplomaticRelationships.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE UNIQUE INDEX UX_Players_ExternalIdentity", externalIdentityAndAdminAudit.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.AdminRoleAuditRecords", externalIdentityAndAdminAudit.Script, StringComparison.Ordinal);
        Assert.Contains("CK_EmpirePriorities_ActiveProgrammes", inactivePriorities.Script, StringComparison.Ordinal);
        Assert.Contains("CAST(MilitaryWeight AS BIGINT) + ExpansionWeight = 100", inactivePriorities.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.GalaxySectors", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("ADD SectorID UNIQUEIDENTIFIER NULL", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("FK_Systems_GalaxySectors", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("sys.foreign_keys", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("IX_Systems_SectorID", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("BEGIN TRANSACTION", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("COMMIT TRANSACTION", galaxySectors.Script, StringComparison.Ordinal);
        Assert.Contains("SupersededByOrderID", pendingFleetOrder.Script, StringComparison.Ordinal);
        Assert.Contains("UX_FleetOrders_Cycle_Fleet_ExecuteAfterTick_Pending", pendingFleetOrder.Script, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER()", pendingFleetOrder.Script, StringComparison.Ordinal);
        Assert.Matches(@"END;\r?\nGO\r?\n", pendingFleetOrder.Script);
        Assert.Contains("TurnStage", turnResolutionLedger.Script, StringComparison.Ordinal);
        Assert.Contains("CommandSource", turnResolutionLedger.Script, StringComparison.Ordinal);
        Assert.Contains("SealedTick", turnResolutionLedger.Script, StringComparison.Ordinal);
        Assert.Contains("CK_FleetOrders_SealedTogether", turnResolutionLedger.Script, StringComparison.Ordinal);
        Assert.Contains("DepartureTickNumber", fleetDepartureTick.Script, StringComparison.Ordinal);
        Assert.Contains("link.TravelTicks", fleetDepartureTick.Script, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE dbo.EmpireDoctrineUnlocks", doctrineUnlocks.Script, StringComparison.Ordinal);
        Assert.Contains("ROW_NUMBER()", doctrineUnlocks.Script, StringComparison.Ordinal);
        Assert.Contains("DoctrineUnlocked", doctrineUnlocks.Script, StringComparison.Ordinal);
        Assert.Contains("BattleFleetParticipants", gameScopeIntegrity.Script, StringComparison.Ordinal);
        Assert.Contains("MatchParticipants", gameScopeIntegrity.Script, StringComparison.Ordinal);
        Assert.Contains("GameID", gameScopeIntegrity.Script, StringComparison.Ordinal);
        Assert.Contains("WITH CHECK", gameScopeIntegrity.Script, StringComparison.Ordinal);
    }
}
