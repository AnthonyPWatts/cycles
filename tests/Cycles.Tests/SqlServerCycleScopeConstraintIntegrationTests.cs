using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerCycleScopeConstraintIntegrationTests
{
    private const string ConnectionStringEnvironmentVariable = SqlIntegrationGuard.ConnectionStringEnvironmentVariable;

    [Fact]
    public void Latest_schema_has_every_exact_cycle_scope_foreign_key_enabled_and_trusted()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var actual = ReadForeignKeys(connection);

        Assert.Equal(50, RequiredForeignKeys.Length);
        foreach (var expected in RequiredForeignKeys)
        {
            var foreignKey = Assert.Single(actual, item => item.Name == expected.Name);
            Assert.Equal(expected.ChildTable, foreignKey.ChildTable);
            Assert.Equal(expected.ChildColumns, foreignKey.ChildColumns);
            Assert.Equal(expected.ParentTable, foreignKey.ParentTable);
            Assert.Equal(expected.ParentColumns, foreignKey.ParentColumns);
            Assert.False(foreignKey.IsDisabled, $"{expected.Name} must be enabled.");
            Assert.False(foreignKey.IsNotTrusted, $"{expected.Name} must be trusted.");
        }
    }

    [Theory]
    [InlineData("FK_Cycles_CycleConfigurationsInGame")]
    [InlineData("FK_Cycles_PreviousCyclesInGame")]
    [InlineData("FK_Systems_GalaxySectors")]
    [InlineData("FK_Empires_HomeSystemsInCycle")]
    [InlineData("FK_Factions_EmpiresInCycle")]
    [InlineData("FK_MatchParticipants_EmpiresInCycle")]
    [InlineData("FK_MatchParticipants_CyclesInGame")]
    [InlineData("FK_MatchParticipants_GameEnrolments")]
    [InlineData("FK_EmpireMetrics_EmpiresInCycle")]
    [InlineData("FK_ShipConstructions_EmpiresInCycle")]
    [InlineData("FK_CycleRankings_EmpiresInCycle")]
    [InlineData("FK_Admirals_EmpiresInCycle")]
    [InlineData("FK_ColonialOutposts_EmpiresInCycle")]
    [InlineData("FK_ColonialOutposts_SystemsInCycle")]
    [InlineData("FK_EmpireDoctrineUnlocks_EmpiresInCycle")]
    [InlineData("FK_DiplomaticRelationships_FirstEmpiresInCycle")]
    [InlineData("FK_DiplomaticRelationships_SecondEmpiresInCycle")]
    [InlineData("FK_SystemLinks_SystemAInCycle")]
    [InlineData("FK_SystemLinks_SystemBInCycle")]
    [InlineData("FK_Fleets_EmpiresInCycle")]
    [InlineData("FK_Fleets_FactionsInCycle")]
    [InlineData("FK_Fleets_CurrentSystemsInCycle")]
    [InlineData("FK_Fleets_DestinationSystemsInCycle")]
    [InlineData("FK_Fleets_AdmiralsInCycle")]
    [InlineData("FK_FleetOrders_FleetsInCycle")]
    [InlineData("FK_FleetOrders_TargetSystemsInCycle")]
    [InlineData("FK_FleetOrders_TargetEmpiresInCycle")]
    [InlineData("FK_FleetOrders_TargetFactionsInCycle")]
    [InlineData("FK_FleetOrders_SupersededOrdersInCycle")]
    [InlineData("FK_Events_SystemsInCycle")]
    [InlineData("FK_Events_EmpiresInCycle")]
    [InlineData("FK_Events_FactionsInCycle")]
    [InlineData("FK_BattleRecords_SystemsInCycle")]
    [InlineData("FK_BattleRecords_AttackerEmpiresInCycle")]
    [InlineData("FK_BattleRecords_DefenderEmpiresInCycle")]
    [InlineData("FK_BattleRecords_AttackerFactionsInCycle")]
    [InlineData("FK_BattleRecords_DefenderFactionsInCycle")]
    [InlineData("FK_BattleFleetParticipants_BattlesInCycle")]
    [InlineData("FK_BattleFleetParticipants_FleetsInCycle")]
    [InlineData("FK_ChronicleEntries_EventsInCycle")]
    [InlineData("FK_ChronicleEntries_BattlesInCycle")]
    [InlineData("FK_ChronicleEntries_SystemsInCycle")]
    [InlineData("FK_CycleMajorEvents_BattlesInCycle")]
    [InlineData("FK_CycleMajorEvents_SystemsInCycle")]
    [InlineData("FK_SystemHistoricalSignals_SystemsInCycle")]
    [InlineData("FK_SystemHistoricalSignals_BattlesInCycle")]
    [InlineData("FK_AdmiralBattleHistories_AdmiralsInCycle")]
    [InlineData("FK_AdmiralBattleHistories_BattlesInCycle")]
    [InlineData("FK_AdmiralBattleHistories_SystemsInCycle")]
    [InlineData("FK_AdmiralBattleHistories_FleetsInCycle")]
    public void Latest_schema_rejects_each_cross_scope_relationship(string foreignKeyName)
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var transaction = connection.BeginTransaction();
        var ids = SqlServerCycleScopeFixture.Insert(connection, transaction);
        var mutation = CreateCrossScopeMutation(foreignKeyName, ids);
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = mutation.Sql;
        foreach (var (name, value) in mutation.Parameters)
        {
            command.Parameters.Add(new SqlParameter(name, System.Data.SqlDbType.UniqueIdentifier) { Value = value });
        }

        var error = Assert.Throws<SqlException>(() => command.ExecuteNonQuery());

        Assert.Equal(547, error.Number);
        Assert.Contains(foreignKeyName, error.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static ScopeMutation CreateCrossScopeMutation(string foreignKeyName, SqlServerCycleScopeFixtureIds ids)
        => foreignKeyName switch
        {
            "FK_Cycles_CycleConfigurationsInGame" => Mutation("UPDATE dbo.Cycles SET CycleConfigurationID = @OtherID WHERE CycleID = @ChildID;", ids.CycleA, ids.ConfigurationBUnused),
            "FK_Cycles_PreviousCyclesInGame" => Mutation("UPDATE dbo.Cycles SET PreviousCycleID = @OtherID WHERE CycleID = @ChildID;", ids.CycleA, ids.CycleB),
            "FK_Systems_GalaxySectors" => Mutation("UPDATE dbo.Systems SET SectorID = @OtherID WHERE SystemID = @ChildID;", ids.SystemA1, ids.SectorB),
            "FK_Empires_HomeSystemsInCycle" => Mutation("UPDATE dbo.Empires SET HomeSystemID = @OtherID WHERE EmpireID = @ChildID;", ids.EmpireA1, ids.SystemB1),
            "FK_Factions_EmpiresInCycle" => Mutation("UPDATE dbo.Factions SET EmpireID = @OtherID WHERE FactionID = @ChildID;", ids.EmpireA1, ids.EmpireB3),
            "FK_MatchParticipants_EmpiresInCycle" => Mutation("UPDATE dbo.MatchParticipants SET EmpireID = @OtherID WHERE MatchParticipantID = @ChildID;", ids.ParticipantA1, ids.EmpireB3),
            "FK_MatchParticipants_CyclesInGame" => Mutation("UPDATE dbo.MatchParticipants SET GameID = @OtherID WHERE MatchParticipantID = @ChildID;", ids.ParticipantA1, ids.GameB),
            "FK_MatchParticipants_GameEnrolments" => new ScopeMutation(
                "UPDATE dbo.MatchParticipants SET PlayerID = @OtherID, EmpireID = @OtherRelatedID WHERE MatchParticipantID = @ChildID;",
                ("@ChildID", ids.ParticipantA1), ("@OtherID", ids.PlayerC), ("@OtherRelatedID", ids.EmpireA3)),
            "FK_EmpireMetrics_EmpiresInCycle" => Mutation("UPDATE dbo.EmpireMetrics SET EmpireID = @OtherID WHERE EmpireMetricID = @ChildID;", ids.MetricA, ids.EmpireB1),
            "FK_ShipConstructions_EmpiresInCycle" => Mutation("UPDATE dbo.ShipConstructions SET EmpireID = @OtherID WHERE ShipConstructionID = @ChildID;", ids.ConstructionA, ids.EmpireB1),
            "FK_CycleRankings_EmpiresInCycle" => Mutation("UPDATE dbo.CycleRankings SET EmpireID = @OtherID WHERE CycleRankingID = @ChildID;", ids.RankingA, ids.EmpireB1),
            "FK_Admirals_EmpiresInCycle" => Mutation("UPDATE dbo.Admirals SET EmpireID = @OtherID WHERE AdmiralID = @ChildID;", ids.AdmiralA, ids.EmpireB1),
            "FK_ColonialOutposts_EmpiresInCycle" => Mutation("UPDATE dbo.ColonialOutposts SET EmpireID = @OtherID WHERE ColonialOutpostID = @ChildID;", ids.OutpostA, ids.EmpireB1),
            "FK_ColonialOutposts_SystemsInCycle" => Mutation("UPDATE dbo.ColonialOutposts SET SystemID = @OtherID WHERE ColonialOutpostID = @ChildID;", ids.OutpostA, ids.SystemB1),
            "FK_EmpireDoctrineUnlocks_EmpiresInCycle" => Mutation("UPDATE dbo.EmpireDoctrineUnlocks SET EmpireID = @OtherID WHERE EmpireDoctrineUnlockID = @ChildID;", ids.DoctrineA, ids.EmpireB1),
            "FK_DiplomaticRelationships_FirstEmpiresInCycle" => Mutation("UPDATE dbo.DiplomaticRelationships SET FirstEmpireID = @OtherID WHERE DiplomaticRelationshipID = @ChildID;", ids.DiplomacyA, ids.EmpireB1),
            "FK_DiplomaticRelationships_SecondEmpiresInCycle" => Mutation("UPDATE dbo.DiplomaticRelationships SET SecondEmpireID = @OtherID WHERE DiplomaticRelationshipID = @ChildID;", ids.DiplomacyA, ids.EmpireB2),
            "FK_SystemLinks_SystemAInCycle" => Mutation("UPDATE dbo.SystemLinks SET SystemAID = @OtherID WHERE SystemLinkID = @ChildID;", ids.LinkA, ids.SystemB1),
            "FK_SystemLinks_SystemBInCycle" => Mutation("UPDATE dbo.SystemLinks SET SystemBID = @OtherID WHERE SystemLinkID = @ChildID;", ids.LinkA, ids.SystemB2),
            "FK_Fleets_EmpiresInCycle" => Mutation("UPDATE dbo.Fleets SET EmpireID = @OtherID WHERE FleetID = @ChildID;", ids.FleetA1, ids.EmpireB1),
            "FK_Fleets_FactionsInCycle" => Mutation("UPDATE dbo.Fleets SET FactionID = @OtherID WHERE FleetID = @ChildID;", ids.FleetA1, ids.EmpireB1),
            "FK_Fleets_CurrentSystemsInCycle" => Mutation("UPDATE dbo.Fleets SET CurrentSystemID = @OtherID WHERE FleetID = @ChildID;", ids.FleetA1, ids.SystemB1),
            "FK_Fleets_DestinationSystemsInCycle" => Mutation("UPDATE dbo.Fleets SET DestinationSystemID = @OtherID WHERE FleetID = @ChildID;", ids.FleetA1, ids.SystemB2),
            "FK_Fleets_AdmiralsInCycle" => Mutation("UPDATE dbo.Fleets SET AdmiralID = @OtherID WHERE FleetID = @ChildID;", ids.FleetA1, ids.AdmiralBUnused),
            "FK_FleetOrders_FleetsInCycle" => Mutation("UPDATE dbo.FleetOrders SET FleetID = @OtherID WHERE FleetOrderID = @ChildID;", ids.OrderA1, ids.FleetB1),
            "FK_FleetOrders_TargetSystemsInCycle" => Mutation("UPDATE dbo.FleetOrders SET TargetSystemID = @OtherID WHERE FleetOrderID = @ChildID;", ids.OrderA1, ids.SystemB1),
            "FK_FleetOrders_TargetEmpiresInCycle" => Mutation("UPDATE dbo.FleetOrders SET TargetEmpireID = @OtherID WHERE FleetOrderID = @ChildID;", ids.OrderA1, ids.EmpireB1),
            "FK_FleetOrders_TargetFactionsInCycle" => Mutation("UPDATE dbo.FleetOrders SET TargetFactionID = @OtherID WHERE FleetOrderID = @ChildID;", ids.OrderA1, ids.EmpireB1),
            "FK_FleetOrders_SupersededOrdersInCycle" => Mutation("UPDATE dbo.FleetOrders SET SupersededByOrderID = @OtherID WHERE FleetOrderID = @ChildID;", ids.OrderA1, ids.OrderB2),
            "FK_Events_SystemsInCycle" => Mutation("UPDATE dbo.Events SET SystemID = @OtherID WHERE EventID = @ChildID;", ids.EventA, ids.SystemB1),
            "FK_Events_EmpiresInCycle" => Mutation("UPDATE dbo.Events SET EmpireID = @OtherID WHERE EventID = @ChildID;", ids.EventA, ids.EmpireB1),
            "FK_Events_FactionsInCycle" => Mutation("UPDATE dbo.Events SET FactionID = @OtherID WHERE EventID = @ChildID;", ids.EventA, ids.EmpireB1),
            "FK_BattleRecords_SystemsInCycle" => Mutation("UPDATE dbo.BattleRecords SET SystemID = @OtherID WHERE BattleID = @ChildID;", ids.BattleA, ids.SystemB1),
            "FK_BattleRecords_AttackerEmpiresInCycle" => Mutation("UPDATE dbo.BattleRecords SET AttackerEmpireID = @OtherID WHERE BattleID = @ChildID;", ids.BattleA, ids.EmpireB1),
            "FK_BattleRecords_DefenderEmpiresInCycle" => Mutation("UPDATE dbo.BattleRecords SET DefenderEmpireID = @OtherID WHERE BattleID = @ChildID;", ids.BattleA, ids.EmpireB2),
            "FK_BattleRecords_AttackerFactionsInCycle" => Mutation("UPDATE dbo.BattleRecords SET AttackerFactionID = @OtherID WHERE BattleID = @ChildID;", ids.BattleA, ids.EmpireB1),
            "FK_BattleRecords_DefenderFactionsInCycle" => Mutation("UPDATE dbo.BattleRecords SET DefenderFactionID = @OtherID WHERE BattleID = @ChildID;", ids.BattleA, ids.EmpireB2),
            "FK_BattleFleetParticipants_BattlesInCycle" => new ScopeMutation(
                "UPDATE dbo.BattleFleetParticipants SET BattleID = @OtherID WHERE BattleID = @ChildID AND FleetID = @RelatedID;",
                ("@ChildID", ids.BattleA), ("@RelatedID", ids.FleetA1), ("@OtherID", ids.BattleB)),
            "FK_BattleFleetParticipants_FleetsInCycle" => new ScopeMutation(
                "UPDATE dbo.BattleFleetParticipants SET FleetID = @OtherID WHERE BattleID = @ChildID AND FleetID = @RelatedID;",
                ("@ChildID", ids.BattleA), ("@RelatedID", ids.FleetA1), ("@OtherID", ids.FleetB1)),
            "FK_ChronicleEntries_EventsInCycle" => Mutation("UPDATE dbo.ChronicleEntries SET SourceEventID = @OtherID WHERE ChronicleEntryID = @ChildID;", ids.ChronicleA, ids.EventB),
            "FK_ChronicleEntries_BattlesInCycle" => Mutation("UPDATE dbo.ChronicleEntries SET SourceBattleID = @OtherID WHERE ChronicleEntryID = @ChildID;", ids.ChronicleA, ids.BattleB),
            "FK_ChronicleEntries_SystemsInCycle" => Mutation("UPDATE dbo.ChronicleEntries SET SystemID = @OtherID WHERE ChronicleEntryID = @ChildID;", ids.ChronicleA, ids.SystemB1),
            "FK_CycleMajorEvents_BattlesInCycle" => Mutation("UPDATE dbo.CycleMajorEvents SET SourceBattleID = @OtherID WHERE CycleMajorEventID = @ChildID;", ids.MajorEventA, ids.BattleB),
            "FK_CycleMajorEvents_SystemsInCycle" => Mutation("UPDATE dbo.CycleMajorEvents SET SystemID = @OtherID WHERE CycleMajorEventID = @ChildID;", ids.MajorEventA, ids.SystemB1),
            "FK_SystemHistoricalSignals_SystemsInCycle" => Mutation("UPDATE dbo.SystemHistoricalSignals SET SystemID = @OtherID WHERE SystemHistoricalSignalID = @ChildID;", ids.SignalA, ids.SystemB1),
            "FK_SystemHistoricalSignals_BattlesInCycle" => Mutation("UPDATE dbo.SystemHistoricalSignals SET SourceBattleID = @OtherID WHERE SystemHistoricalSignalID = @ChildID;", ids.SignalA, ids.BattleB),
            "FK_AdmiralBattleHistories_AdmiralsInCycle" => Mutation("UPDATE dbo.AdmiralBattleHistories SET AdmiralID = @OtherID WHERE AdmiralBattleHistoryID = @ChildID;", ids.HistoryA, ids.AdmiralB),
            "FK_AdmiralBattleHistories_BattlesInCycle" => Mutation("UPDATE dbo.AdmiralBattleHistories SET BattleID = @OtherID WHERE AdmiralBattleHistoryID = @ChildID;", ids.HistoryA, ids.BattleB),
            "FK_AdmiralBattleHistories_SystemsInCycle" => Mutation("UPDATE dbo.AdmiralBattleHistories SET SystemID = @OtherID WHERE AdmiralBattleHistoryID = @ChildID;", ids.HistoryA, ids.SystemB1),
            "FK_AdmiralBattleHistories_FleetsInCycle" => Mutation("UPDATE dbo.AdmiralBattleHistories SET FleetID = @OtherID WHERE AdmiralBattleHistoryID = @ChildID;", ids.HistoryA, ids.FleetB1),
            _ => throw new ArgumentOutOfRangeException(nameof(foreignKeyName), foreignKeyName, "Unknown scope relationship.")
        };

    private static ScopeMutation Mutation(string sql, Guid childId, Guid otherId)
        => new(sql, ("@ChildID", childId), ("@OtherID", otherId));

    private static List<ForeignKeyMetadata> ReadForeignKeys(SqlConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT
                foreignKey.name,
                OBJECT_NAME(foreignKey.parent_object_id),
                childColumn.name,
                OBJECT_NAME(foreignKey.referenced_object_id),
                parentColumn.name,
                foreignKeyColumn.constraint_column_id,
                foreignKey.is_disabled,
                foreignKey.is_not_trusted
            FROM sys.foreign_keys AS foreignKey
            INNER JOIN sys.foreign_key_columns AS foreignKeyColumn
                ON foreignKeyColumn.constraint_object_id = foreignKey.object_id
            INNER JOIN sys.columns AS childColumn
                ON childColumn.object_id = foreignKeyColumn.parent_object_id
               AND childColumn.column_id = foreignKeyColumn.parent_column_id
            INNER JOIN sys.columns AS parentColumn
                ON parentColumn.object_id = foreignKeyColumn.referenced_object_id
               AND parentColumn.column_id = foreignKeyColumn.referenced_column_id
            ORDER BY foreignKey.name, foreignKeyColumn.constraint_column_id;
            """;
        using var reader = command.ExecuteReader();
        var rows = new List<ForeignKeyColumnMetadata>();
        while (reader.Read())
        {
            rows.Add(new ForeignKeyColumnMetadata(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetBoolean(6),
                reader.GetBoolean(7)));
        }

        return rows
            .GroupBy(item => item.Name, StringComparer.Ordinal)
            .Select(group => new ForeignKeyMetadata(
                group.Key,
                group.First().ChildTable,
                group.Select(item => item.ChildColumn).ToArray(),
                group.First().ParentTable,
                group.Select(item => item.ParentColumn).ToArray(),
                group.First().IsDisabled,
                group.First().IsNotTrusted))
            .ToList();
    }

    private static readonly RequiredForeignKey[] RequiredForeignKeys =
    [
        Fk("FK_Cycles_CycleConfigurationsInGame", "Cycles", "CycleConfigurationID", "GameID", "CycleConfigurations", "CycleConfigurationID", "GameID"),
        Fk("FK_Cycles_PreviousCyclesInGame", "Cycles", "PreviousCycleID", "GameID", "Cycles", "CycleID", "GameID"),
        Fk("FK_Systems_GalaxySectors", "Systems", "CycleID", "SectorID", "GalaxySectors", "CycleID", "SectorID"),
        Fk("FK_Empires_HomeSystemsInCycle", "Empires", "HomeSystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_Factions_EmpiresInCycle", "Factions", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_MatchParticipants_EmpiresInCycle", "MatchParticipants", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_MatchParticipants_CyclesInGame", "MatchParticipants", "CycleID", "GameID", "Cycles", "CycleID", "GameID"),
        Fk("FK_MatchParticipants_GameEnrolments", "MatchParticipants", "GameID", "PlayerID", "GameEnrolments", "GameID", "PlayerID"),
        Fk("FK_EmpireMetrics_EmpiresInCycle", "EmpireMetrics", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_ShipConstructions_EmpiresInCycle", "ShipConstructions", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_CycleRankings_EmpiresInCycle", "CycleRankings", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_Admirals_EmpiresInCycle", "Admirals", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_ColonialOutposts_EmpiresInCycle", "ColonialOutposts", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_ColonialOutposts_SystemsInCycle", "ColonialOutposts", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_EmpireDoctrineUnlocks_EmpiresInCycle", "EmpireDoctrineUnlocks", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_DiplomaticRelationships_FirstEmpiresInCycle", "DiplomaticRelationships", "FirstEmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_DiplomaticRelationships_SecondEmpiresInCycle", "DiplomaticRelationships", "SecondEmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_SystemLinks_SystemAInCycle", "SystemLinks", "SystemAID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_SystemLinks_SystemBInCycle", "SystemLinks", "SystemBID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_Fleets_EmpiresInCycle", "Fleets", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_Fleets_FactionsInCycle", "Fleets", "CycleID", "FactionID", "Factions", "CycleID", "FactionID"),
        Fk("FK_Fleets_CurrentSystemsInCycle", "Fleets", "CurrentSystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_Fleets_DestinationSystemsInCycle", "Fleets", "DestinationSystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_Fleets_AdmiralsInCycle", "Fleets", "AdmiralID", "CycleID", "Admirals", "AdmiralID", "CycleID"),
        Fk("FK_FleetOrders_FleetsInCycle", "FleetOrders", "FleetID", "CycleID", "Fleets", "FleetID", "CycleID"),
        Fk("FK_FleetOrders_TargetSystemsInCycle", "FleetOrders", "TargetSystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_FleetOrders_TargetEmpiresInCycle", "FleetOrders", "TargetEmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_FleetOrders_TargetFactionsInCycle", "FleetOrders", "CycleID", "TargetFactionID", "Factions", "CycleID", "FactionID"),
        Fk("FK_FleetOrders_SupersededOrdersInCycle", "FleetOrders", "SupersededByOrderID", "CycleID", "FleetOrders", "FleetOrderID", "CycleID"),
        Fk("FK_Events_SystemsInCycle", "Events", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_Events_EmpiresInCycle", "Events", "EmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_Events_FactionsInCycle", "Events", "CycleID", "FactionID", "Factions", "CycleID", "FactionID"),
        Fk("FK_BattleRecords_SystemsInCycle", "BattleRecords", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_BattleRecords_AttackerEmpiresInCycle", "BattleRecords", "AttackerEmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_BattleRecords_DefenderEmpiresInCycle", "BattleRecords", "DefenderEmpireID", "CycleID", "Empires", "EmpireID", "CycleID"),
        Fk("FK_BattleRecords_AttackerFactionsInCycle", "BattleRecords", "CycleID", "AttackerFactionID", "Factions", "CycleID", "FactionID"),
        Fk("FK_BattleRecords_DefenderFactionsInCycle", "BattleRecords", "CycleID", "DefenderFactionID", "Factions", "CycleID", "FactionID"),
        Fk("FK_BattleFleetParticipants_BattlesInCycle", "BattleFleetParticipants", "BattleID", "CycleID", "BattleRecords", "BattleID", "CycleID"),
        Fk("FK_BattleFleetParticipants_FleetsInCycle", "BattleFleetParticipants", "FleetID", "CycleID", "Fleets", "FleetID", "CycleID"),
        Fk("FK_ChronicleEntries_EventsInCycle", "ChronicleEntries", "SourceEventID", "CycleID", "Events", "EventID", "CycleID"),
        Fk("FK_ChronicleEntries_BattlesInCycle", "ChronicleEntries", "SourceBattleID", "CycleID", "BattleRecords", "BattleID", "CycleID"),
        Fk("FK_ChronicleEntries_SystemsInCycle", "ChronicleEntries", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_CycleMajorEvents_BattlesInCycle", "CycleMajorEvents", "SourceBattleID", "CycleID", "BattleRecords", "BattleID", "CycleID"),
        Fk("FK_CycleMajorEvents_SystemsInCycle", "CycleMajorEvents", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_SystemHistoricalSignals_SystemsInCycle", "SystemHistoricalSignals", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_SystemHistoricalSignals_BattlesInCycle", "SystemHistoricalSignals", "SourceBattleID", "CycleID", "BattleRecords", "BattleID", "CycleID"),
        Fk("FK_AdmiralBattleHistories_AdmiralsInCycle", "AdmiralBattleHistories", "AdmiralID", "CycleID", "Admirals", "AdmiralID", "CycleID"),
        Fk("FK_AdmiralBattleHistories_BattlesInCycle", "AdmiralBattleHistories", "BattleID", "CycleID", "BattleRecords", "BattleID", "CycleID"),
        Fk("FK_AdmiralBattleHistories_SystemsInCycle", "AdmiralBattleHistories", "SystemID", "CycleID", "Systems", "SystemID", "CycleID"),
        Fk("FK_AdmiralBattleHistories_FleetsInCycle", "AdmiralBattleHistories", "FleetID", "CycleID", "Fleets", "FleetID", "CycleID")
    ];

    private static RequiredForeignKey Fk(
        string name,
        string childTable,
        string childColumn1,
        string childColumn2,
        string parentTable,
        string parentColumn1,
        string parentColumn2)
        => new(name, childTable, [childColumn1, childColumn2], parentTable, [parentColumn1, parentColumn2]);

    private sealed record ScopeMutation(string Sql, params (string Name, Guid Value)[] Parameters);
    private sealed record RequiredForeignKey(
        string Name,
        string ChildTable,
        string[] ChildColumns,
        string ParentTable,
        string[] ParentColumns);
    private sealed record ForeignKeyColumnMetadata(
        string Name,
        string ChildTable,
        string ChildColumn,
        string ParentTable,
        string ParentColumn,
        bool IsDisabled,
        bool IsNotTrusted);
    private sealed record ForeignKeyMetadata(
        string Name,
        string ChildTable,
        string[] ChildColumns,
        string ParentTable,
        string[] ParentColumns,
        bool IsDisabled,
        bool IsNotTrusted);
}
