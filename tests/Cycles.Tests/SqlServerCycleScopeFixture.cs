using Microsoft.Data.SqlClient;
using System.Reflection;

namespace Cycles.Tests;

internal static class SqlServerCycleScopeFixture
{
    public static SqlServerCycleScopeFixtureIds Insert(SqlConnection connection, SqlTransaction transaction)
    {
        var ids = new SqlServerCycleScopeFixtureIds();
        using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            DECLARE @Now DATETIMEOFFSET = '2026-07-20T12:00:00+00:00';

            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@PlayerA, CONCAT(N'Scope A ', CONVERT(NVARCHAR(36), @PlayerA)), N'', N'', N'', N'', N'Human', N'Player', @Now, NULL, N'Active'),
                (@PlayerB, CONCAT(N'Scope B ', CONVERT(NVARCHAR(36), @PlayerB)), N'', N'', N'', N'', N'Human', N'Player', @Now, NULL, N'Active'),
                (@PlayerC, CONCAT(N'Scope C ', CONVERT(NVARCHAR(36), @PlayerC)), N'', N'', N'', N'', N'Human', N'Player', @Now, NULL, N'Active');

            INSERT INTO dbo.Games
                (GameID, Name, Purpose, Status, Visibility, CreationSource, GamePolicyKey, GamePolicyVersion, GamePolicyContentHash,
                 PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt, FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt)
            VALUES
                (@GameA, N'Scope Game A', N'Standard', N'Active', N'Private', N'Operator', N'scope-fixture-policy', 1, NULL,
                 N'Verified', @PlayerA, @Now, @Now, NULL, NULL, NULL),
                (@GameB, N'Scope Game B', N'Standard', N'Active', N'Private', N'Operator', N'scope-fixture-policy', 1, NULL,
                 N'Verified', @PlayerB, @Now, @Now, NULL, NULL, NULL);

            INSERT INTO dbo.CycleConfigurations
                (CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus, MapProfileKey, MapProfileVersion,
                 MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash,
                 ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, MinimumHumanSeats, MaximumHumanSeats,
                 ScheduledStartAt, ScheduledEndAt, TickLengthMinutes, CreatedAt, LockedAt, MaterializedAt, CancelledAt)
            VALUES
                (@ConfigurationA, @GameA, 1, N'Materialized', N'Verified', N'scope-map', 1, NULL, 101,
                 N'scope-scenario', 1, NULL, 201, N'scope-cycle-policy', 1, NULL, 1, 3,
                 @Now, DATEADD(DAY, 30, @Now), 60, @Now, @Now, @Now, NULL),
                (@ConfigurationB, @GameB, 1, N'Materialized', N'Verified', N'scope-map', 1, NULL, 102,
                 N'scope-scenario', 1, NULL, 202, N'scope-cycle-policy', 1, NULL, 1, 2,
                 @Now, DATEADD(DAY, 30, @Now), 60, @Now, @Now, @Now, NULL),
                (@ConfigurationBUnused, @GameB, 2, N'Draft', N'Verified', N'scope-map', 1, NULL, 103,
                 N'scope-scenario', 1, NULL, 203, N'scope-cycle-policy', 1, NULL, 1, 2,
                 NULL, NULL, 60, @Now, NULL, NULL, NULL);

            INSERT INTO dbo.Cycles
                (CycleID, GameID, CycleConfigurationID, PreviousCycleID, Name, StartAt, EndAt, TickLengthMinutes,
                 CurrentTickNumber, Status, TurnStage, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed,
                 ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey,
                 CyclePolicyVersion, CyclePolicyContentHash, ProfileProvenanceStatus, CreatedByPlayerID, CreatedAt)
            VALUES
                (@CycleA, @GameA, @ConfigurationA, NULL, N'Scope Cycle A', @Now, DATEADD(DAY, 30, @Now), 60,
                 1, N'Active', N'CommandOpen', N'scope-map', 1, NULL, 101, N'scope-scenario', 1, NULL, 201,
                 N'scope-cycle-policy', 1, NULL, N'Verified', @PlayerA, @Now),
                (@CycleB, @GameB, @ConfigurationB, NULL, N'Scope Cycle B', @Now, DATEADD(DAY, 30, @Now), 60,
                 1, N'Active', N'CommandOpen', N'scope-map', 1, NULL, 102, N'scope-scenario', 1, NULL, 202,
                 N'scope-cycle-policy', 1, NULL, N'Verified', @PlayerB, @Now);

            INSERT INTO dbo.GameEnrolments
                (GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID, EnrolledAt, StatusChangedAt, EndedAt)
            VALUES
                (@EnrolmentAA, @GameA, @PlayerA, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL),
                (@EnrolmentAB, @GameA, @PlayerB, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL),
                (@EnrolmentBA, @GameB, @PlayerA, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL),
                (@EnrolmentBB, @GameB, @PlayerB, N'Enrolled', N'Direct', NULL, @Now, @Now, NULL);

            INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
            VALUES
                (@SectorA, @CycleA, N'Scope Sector A', 0, 0, 0),
                (@SectorB, @CycleB, N'Scope Sector B', 0, 0, 0);

            INSERT INTO dbo.Systems
                (SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput,
                 StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES
                (@SystemA1, @CycleA, @SectorA, N'Scope A1', 0, 0, 1, 1, 1, 1, 0, @Now),
                (@SystemA2, @CycleA, @SectorA, N'Scope A2', 1, 1, 1, 1, 1, 1, 0, @Now),
                (@SystemB1, @CycleB, @SectorB, N'Scope B1', 0, 0, 1, 1, 1, 1, 0, @Now),
                (@SystemB2, @CycleB, @SectorB, N'Scope B2', 1, 1, 1, 1, 1, 1, 0, @Now);

            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES
                (@EmpireA1, @CycleA, @PlayerA, N'Scope Empire A1', @SystemA1, @Now, N'Active'),
                (@EmpireA2, @CycleA, @PlayerB, N'Scope Empire A2', @SystemA2, @Now, N'Active'),
                (@EmpireA3, @CycleA, @PlayerC, N'Scope Empire A3', @SystemA1, @Now, N'Active'),
                (@EmpireB1, @CycleB, @PlayerA, N'Scope Empire B1', @SystemB1, @Now, N'Active'),
                (@EmpireB2, @CycleB, @PlayerB, N'Scope Empire B2', @SystemB2, @Now, N'Active'),
                (@EmpireB3, @CycleB, @PlayerA, N'Scope Empire B3', @SystemB1, @Now, N'Active');

            INSERT INTO dbo.Factions(FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt)
            VALUES
                (@EmpireA1, @CycleA, @EmpireA1, N'Scope Empire A1', N'Empire', N'Active', @Now),
                (@EmpireA2, @CycleA, @EmpireA2, N'Scope Empire A2', N'Empire', N'Active', @Now),
                (@EmpireA3, @CycleA, @EmpireA3, N'Scope Empire A3', N'Empire', N'Active', @Now),
                (@EmpireB1, @CycleB, @EmpireB1, N'Scope Empire B1', N'Empire', N'Active', @Now),
                (@EmpireB2, @CycleB, @EmpireB2, N'Scope Empire B2', N'Empire', N'Active', @Now);

            INSERT INTO dbo.MatchParticipants(MatchParticipantID, GameID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
            VALUES
                (@ParticipantA1, @GameA, @CycleA, @PlayerA, @EmpireA1, N'Active', @Now, NULL),
                (@ParticipantA2, @GameA, @CycleA, @PlayerB, @EmpireA2, N'Active', @Now, NULL),
                (@ParticipantB1, @GameB, @CycleB, @PlayerA, @EmpireB1, N'Active', @Now, NULL),
                (@ParticipantB2, @GameB, @CycleB, @PlayerB, @EmpireB2, N'Active', @Now, NULL);

            INSERT INTO dbo.EmpireMetrics
                (EmpireMetricID, CycleID, EmpireID, TickNumber, Rank, IsWinner, MapControlPercent,
                 TotalEffectivePresence, ActiveShipCount, CreatedAt)
            VALUES
                (@MetricA, @CycleA, @EmpireA1, 1, 1, 0, 50, 10, 10, @Now),
                (@MetricB, @CycleB, @EmpireB1, 1, 1, 0, 50, 10, 10, @Now);

            INSERT INTO dbo.ShipConstructions
                (ShipConstructionID, CycleID, EmpireID, ShipCount, IndustrySpent, StartedTick, CompleteAfterTick,
                 CompletedTick, Status, CreatedAt, UpdatedAt)
            VALUES
                (@ConstructionA, @CycleA, @EmpireA1, 1, 1, 0, 1, 1, N'Completed', @Now, @Now),
                (@ConstructionB, @CycleB, @EmpireB1, 1, 1, 0, 1, 1, N'Completed', @Now, @Now);

            INSERT INTO dbo.CycleRankings
                (CycleRankingID, CycleID, EmpireID, Rank, IsWinner, MapControlPercent, TotalEffectivePresence,
                 ActiveShipCount, CutoffTickNumber, CutoffAt)
            VALUES
                (@RankingA, @CycleA, @EmpireA1, 1, 1, 50, 10, 10, 1, @Now),
                (@RankingB, @CycleB, @EmpireB1, 1, 1, 50, 10, 10, 1, @Now);

            INSERT INTO dbo.Admirals(AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt)
            VALUES
                (@AdmiralA, @CycleA, @EmpireA1, N'Scope Admiral A', 0, N'Active', @Now, @Now),
                (@AdmiralB, @CycleB, @EmpireB1, N'Scope Admiral B', 0, N'Active', @Now, @Now),
                (@AdmiralBUnused, @CycleB, @EmpireB1, N'Scope Admiral B unused', 0, N'Active', @Now, @Now);

            INSERT INTO dbo.ColonialOutposts(ColonialOutpostID, CycleID, EmpireID, SystemID, EstablishedTick, CreatedAt)
            VALUES
                (@OutpostA, @CycleA, @EmpireA1, @SystemA2, 1, @Now),
                (@OutpostB, @CycleB, @EmpireB1, @SystemB2, 1, @Now);

            INSERT INTO dbo.EmpireDoctrineUnlocks
                (EmpireDoctrineUnlockID, CycleID, EmpireID, DoctrineKey, UnlockedTickNumber, UnlockedAt)
            VALUES
                (@DoctrineA, @CycleA, @EmpireA1, N'scope-a', 1, @Now),
                (@DoctrineB, @CycleB, @EmpireB1, N'scope-b', 1, @Now);

            INSERT INTO dbo.DiplomaticRelationships
                (DiplomaticRelationshipID, CycleID, FirstEmpireID, SecondEmpireID, State, UpdatedTick, UpdatedAt)
            VALUES
                (@DiplomacyA, @CycleA, @EmpireA1, @EmpireA2, N'Neutral', 1, @Now),
                (@DiplomacyB, @CycleB, @EmpireB1, @EmpireB2, N'Neutral', 1, @Now);

            INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
            VALUES
                (@LinkA, @CycleA, @SystemA1, @SystemA2, 1, 1),
                (@LinkB, @CycleB, @SystemB1, @SystemB2, 1, 1);

            INSERT INTO dbo.Fleets
                (FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID,
                 DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES
                (@FleetA1, @CycleA, @EmpireA1, @EmpireA1, @AdmiralA, N'Scope Fleet A1', @SystemA1, @SystemA2, 1, 2, 10, N'InTransit', @Now),
                (@FleetA2, @CycleA, @EmpireA2, @EmpireA2, NULL, N'Scope Fleet A2', @SystemA2, NULL, NULL, NULL, 10, N'Active', @Now),
                (@FleetB1, @CycleB, @EmpireB1, @EmpireB1, @AdmiralB, N'Scope Fleet B1', @SystemB1, @SystemB2, 1, 2, 10, N'InTransit', @Now),
                (@FleetB2, @CycleB, @EmpireB2, @EmpireB2, NULL, N'Scope Fleet B2', @SystemB2, NULL, NULL, NULL, 10, N'Active', @Now);

            INSERT INTO dbo.FleetOrders
                (FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, TargetFactionID,
                 SubmitTick, ExecuteAfterTick, ProcessedTick, Status, CommandSource, SealedTick, SealedAt,
                 RejectionReason, SupersededByOrderID, CreatedAt)
            VALUES
                (@OrderA1, @CycleA, @FleetA1, N'Attack', @SystemA2, @EmpireA2, @EmpireA2,
                 0, 1, 1, N'Superseded', N'Human', 1, @Now, NULL, @OrderA2, @Now),
                (@OrderA2, @CycleA, @FleetA1, N'Move', @SystemA2, @EmpireA2, @EmpireA2,
                 0, 1, 1, N'Processed', N'Human', 1, @Now, NULL, NULL, @Now),
                (@OrderB1, @CycleB, @FleetB1, N'Attack', @SystemB2, @EmpireB2, @EmpireB2,
                 0, 1, 1, N'Superseded', N'Human', 1, @Now, NULL, @OrderB2, @Now),
                (@OrderB2, @CycleB, @FleetB1, N'Move', @SystemB2, @EmpireB2, @EmpireB2,
                 0, 1, 1, N'Processed', N'Human', 1, @Now, NULL, NULL, @Now);

            INSERT INTO dbo.Events
                (EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES
                (@EventA, @CycleA, 1, N'BattleResolved', @SystemA1, @EmpireA1, @EmpireA1, N'Normal', N'{}', N'Scope event A', @Now),
                (@EventB, @CycleB, 1, N'BattleResolved', @SystemB1, @EmpireB1, @EmpireB1, N'Normal', N'{}', N'Scope event B', @Now);

            INSERT INTO dbo.BattleRecords
                (BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFactionID,
                 DefenderFactionID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore,
                 AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES
                (@BattleA, @CycleA, 1, @SystemA1, @EmpireA1, @EmpireA2, @EmpireA1, @EmpireA2,
                 CONVERT(NVARCHAR(36), @FleetA1), CONVERT(NVARCHAR(36), @FleetA2), 10, 10, 1, 2, N'AttackerVictory', N'{}', @Now),
                (@BattleB, @CycleB, 1, @SystemB1, @EmpireB1, @EmpireB2, @EmpireB1, @EmpireB2,
                 CONVERT(NVARCHAR(36), @FleetB1), CONVERT(NVARCHAR(36), @FleetB2), 10, 10, 1, 2, N'AttackerVictory', N'{}', @Now);

            INSERT INTO dbo.BattleFleetParticipants(BattleID, CycleID, FleetID, Side)
            VALUES
                (@BattleA, @CycleA, @FleetA1, N'Attacker'),
                (@BattleA, @CycleA, @FleetA2, N'Defender'),
                (@BattleB, @CycleB, @FleetB1, N'Attacker'),
                (@BattleB, @CycleB, @FleetB2, N'Defender');

            INSERT INTO dbo.ChronicleEntries
                (ChronicleEntryID, SourceEventID, SourceBattleID, CycleID, SystemID, Title, EntryType, ImportanceScore,
                 FactualSummary, NarrativeText, NarrativeStatus, NarrativeContextJson, NarrativeGeneratedAt,
                 NarrativeFailureReason, CreatedAt)
            VALUES
                (@ChronicleA, @EventA, @BattleA, @CycleA, @SystemA1, N'Scope Chronicle A', N'Battle', 1,
                 N'Summary', N'Narrative', N'Generated', N'{}', @Now, NULL, @Now),
                (@ChronicleB, @EventB, @BattleB, @CycleB, @SystemB1, N'Scope Chronicle B', N'Battle', 1,
                 N'Summary', N'Narrative', N'Generated', N'{}', @Now, NULL, @Now);

            INSERT INTO dbo.CycleMajorEvents
                (CycleMajorEventID, CycleID, SourceBattleID, SystemID, EventType, TickNumber, SelectionRank,
                 ImportanceScore, TotalLosses, Summary, FactJson, CreatedAt)
            VALUES
                (@MajorEventA, @CycleA, @BattleA, @SystemA1, N'Battle', 1, 1, 1, 3, N'Summary', N'{}', @Now),
                (@MajorEventB, @CycleB, @BattleB, @SystemB1, N'Battle', 1, 1, 1, 3, N'Summary', N'{}', @Now);

            INSERT INTO dbo.SystemHistoricalSignals
                (SystemHistoricalSignalID, CycleID, SystemID, SignalType, SourceBattleID, BattleCount, TotalLosses,
                 LargestBattleLosses, HostedCycleLargestBattle, HistoricalSignificanceIncrease,
                 HistoricalSignificanceAfter, Summary, FactJson, CreatedAt)
            VALUES
                (@SignalA, @CycleA, @SystemA1, N'BattleSite', @BattleA, 1, 3, 3, 1, 1, 1, N'Summary', N'{}', @Now),
                (@SignalB, @CycleB, @SystemB1, N'BattleSite', @BattleB, 1, 3, 3, 1, 1, 1, N'Summary', N'{}', @Now);

            INSERT INTO dbo.AdmiralBattleHistories
                (AdmiralBattleHistoryID, CycleID, AdmiralID, BattleID, SystemID, FleetID, Role, Outcome,
                 ShipsCommandedBefore, ShipsLost, ReputationChange, ReputationScoreAfter, AdmiralStatusAfter,
                 IsFamousSystemAssociation, CreatedAt)
            VALUES
                (@HistoryA, @CycleA, @AdmiralA, @BattleA, @SystemA1, @FleetA1, N'Attacker', N'Victory', 10, 1, 1, 1, N'Active', 0, @Now),
                (@HistoryB, @CycleB, @AdmiralB, @BattleB, @SystemB1, @FleetB1, N'Attacker', N'Victory', 10, 1, 1, 1, N'Active', 0, @Now);
            """;

        foreach (var property in typeof(SqlServerCycleScopeFixtureIds).GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            var value = (Guid)property.GetValue(ids)!;
            command.Parameters.Add(new SqlParameter($"@{property.Name}", System.Data.SqlDbType.UniqueIdentifier) { Value = value });
        }

        command.ExecuteNonQuery();
        return ids;
    }
}

internal sealed class SqlServerCycleScopeFixtureIds
{
    public Guid PlayerA { get; } = Guid.NewGuid();
    public Guid PlayerB { get; } = Guid.NewGuid();
    public Guid PlayerC { get; } = Guid.NewGuid();
    public Guid GameA { get; } = Guid.NewGuid();
    public Guid GameB { get; } = Guid.NewGuid();
    public Guid ConfigurationA { get; } = Guid.NewGuid();
    public Guid ConfigurationB { get; } = Guid.NewGuid();
    public Guid ConfigurationBUnused { get; } = Guid.NewGuid();
    public Guid CycleA { get; } = Guid.NewGuid();
    public Guid CycleB { get; } = Guid.NewGuid();
    public Guid EnrolmentAA { get; } = Guid.NewGuid();
    public Guid EnrolmentAB { get; } = Guid.NewGuid();
    public Guid EnrolmentBA { get; } = Guid.NewGuid();
    public Guid EnrolmentBB { get; } = Guid.NewGuid();
    public Guid SectorA { get; } = Guid.NewGuid();
    public Guid SectorB { get; } = Guid.NewGuid();
    public Guid SystemA1 { get; } = Guid.NewGuid();
    public Guid SystemA2 { get; } = Guid.NewGuid();
    public Guid SystemB1 { get; } = Guid.NewGuid();
    public Guid SystemB2 { get; } = Guid.NewGuid();
    public Guid EmpireA1 { get; } = Guid.NewGuid();
    public Guid EmpireA2 { get; } = Guid.NewGuid();
    public Guid EmpireA3 { get; } = Guid.NewGuid();
    public Guid EmpireB1 { get; } = Guid.NewGuid();
    public Guid EmpireB2 { get; } = Guid.NewGuid();
    public Guid EmpireB3 { get; } = Guid.NewGuid();
    public Guid ParticipantA1 { get; } = Guid.NewGuid();
    public Guid ParticipantA2 { get; } = Guid.NewGuid();
    public Guid ParticipantB1 { get; } = Guid.NewGuid();
    public Guid ParticipantB2 { get; } = Guid.NewGuid();
    public Guid MetricA { get; } = Guid.NewGuid();
    public Guid MetricB { get; } = Guid.NewGuid();
    public Guid ConstructionA { get; } = Guid.NewGuid();
    public Guid ConstructionB { get; } = Guid.NewGuid();
    public Guid RankingA { get; } = Guid.NewGuid();
    public Guid RankingB { get; } = Guid.NewGuid();
    public Guid AdmiralA { get; } = Guid.NewGuid();
    public Guid AdmiralB { get; } = Guid.NewGuid();
    public Guid AdmiralBUnused { get; } = Guid.NewGuid();
    public Guid OutpostA { get; } = Guid.NewGuid();
    public Guid OutpostB { get; } = Guid.NewGuid();
    public Guid DoctrineA { get; } = Guid.NewGuid();
    public Guid DoctrineB { get; } = Guid.NewGuid();
    public Guid DiplomacyA { get; } = Guid.NewGuid();
    public Guid DiplomacyB { get; } = Guid.NewGuid();
    public Guid LinkA { get; } = Guid.NewGuid();
    public Guid LinkB { get; } = Guid.NewGuid();
    public Guid FleetA1 { get; } = Guid.NewGuid();
    public Guid FleetA2 { get; } = Guid.NewGuid();
    public Guid FleetB1 { get; } = Guid.NewGuid();
    public Guid FleetB2 { get; } = Guid.NewGuid();
    public Guid OrderA1 { get; } = Guid.NewGuid();
    public Guid OrderA2 { get; } = Guid.NewGuid();
    public Guid OrderB1 { get; } = Guid.NewGuid();
    public Guid OrderB2 { get; } = Guid.NewGuid();
    public Guid EventA { get; } = Guid.NewGuid();
    public Guid EventB { get; } = Guid.NewGuid();
    public Guid BattleA { get; } = Guid.NewGuid();
    public Guid BattleB { get; } = Guid.NewGuid();
    public Guid ChronicleA { get; } = Guid.NewGuid();
    public Guid ChronicleB { get; } = Guid.NewGuid();
    public Guid MajorEventA { get; } = Guid.NewGuid();
    public Guid MajorEventB { get; } = Guid.NewGuid();
    public Guid SignalA { get; } = Guid.NewGuid();
    public Guid SignalB { get; } = Guid.NewGuid();
    public Guid HistoryA { get; } = Guid.NewGuid();
    public Guid HistoryB { get; } = Guid.NewGuid();
}
