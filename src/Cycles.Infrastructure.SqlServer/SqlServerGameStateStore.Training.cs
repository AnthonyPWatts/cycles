using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    private const string TrainingLockPrefix = "cycles:training:";
    private const int TwinReachesMapSeed = 71421;
    private const int TwinReachesScenarioSeed = 20260720;

    public TrainingGameProvisioningResult ProvisionTwinReaches(
        TrainingGameProvisioningCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        var profile = GameProfileCatalogue.TwinReaches;

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            AcquireSqlApplicationLock(
                connection,
                transaction,
                BuildTrainingLockName(command.PlayerId, profile.Version));
        }
        catch (TimeoutException)
        {
            return new TrainingGameProvisioningResult.Busy();
        }

        var existing = ReadCurrentTwinReachesAttempt(
            connection,
            transaction,
            command.PlayerId,
            profile);
        if (existing is not null)
        {
            transaction.Commit();
            return new TrainingGameProvisioningResult.Success(
                new TrainingGameProvisioningSnapshot(
                    existing.GameId,
                    existing.CycleId,
                    Created: false));
        }

        var player = ReadTrainingPlayerForUpdate(
            connection,
            transaction,
            command.PlayerId);
        if (player is null || player.Kind != PlayerKind.Human || player.Status != PlayerStatus.Active)
        {
            transaction.Commit();
            return new TrainingGameProvisioningResult.Unavailable();
        }

        var state = CreateTwinReachesStartingState(player, command, profile);
        var configuration = state.CycleConfigurations.Single();
        var materialized = RosterAwareCycleFactory.Materialize(
            state,
            configuration.CycleConfigurationId,
            command.RequestedAt);

        SaveNewMaterializedGameUnsafe(connection, transaction, state);
        transaction.Commit();

        return new TrainingGameProvisioningResult.Success(
            new TrainingGameProvisioningSnapshot(
                materialized.GameId,
                materialized.CycleId,
                Created: true));
    }

    internal static string BuildTrainingLockName(Guid playerId, int tutorialVersion) =>
        $"{TrainingLockPrefix}{playerId:N}:{tutorialVersion}";

    private static CurrentTrainingAttempt? ReadCurrentTwinReachesAttempt(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId,
        GameProfileDefinition profile)
    {
        var rows = ReadRows(
            connection,
            transaction,
            """
            SELECT TOP (2)
                game.GameID,
                cycle.CycleID
            FROM dbo.GameEnrolments AS enrolment WITH (UPDLOCK, HOLDLOCK)
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
            INNER JOIN dbo.CycleConfigurations AS configuration
                ON configuration.GameID = game.GameID
               AND configuration.SequenceNumber = 1
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.CycleConfigurationID = configuration.CycleConfigurationID
            WHERE enrolment.PlayerID = @PlayerID
              AND enrolment.Status = @EnrolledStatus
              AND game.Purpose = @TrainingPurpose
              AND game.Status IN (@ActiveGameStatus, @IntermissionGameStatus)
              AND configuration.ScenarioProfileKey = @ProfileKey
              AND configuration.ScenarioProfileVersion = @ProfileVersion
              AND cycle.Status IN (@ActiveCycleStatus, @RecoveryCycleStatus)
            ORDER BY game.CreatedAt DESC, game.GameID;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", playerId);
                AddString(sqlCommand, "@EnrolledStatus", GameEnrolmentStatus.Enrolled.ToString(), 32);
                AddString(sqlCommand, "@TrainingPurpose", GamePurpose.Training.ToString(), 32);
                AddString(sqlCommand, "@ActiveGameStatus", GameLifecycleStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@IntermissionGameStatus", GameLifecycleStatus.Intermission.ToString(), 32);
                AddString(sqlCommand, "@ProfileKey", profile.Key, 128);
                AddInt(sqlCommand, "@ProfileVersion", profile.Version);
                AddString(sqlCommand, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@RecoveryCycleStatus", CycleStatus.RecoveryRequired.ToString(), 32);
            },
            reader => new CurrentTrainingAttempt(
                GetGuid(reader, "GameID"),
                GetGuid(reader, "CycleID")));

        return rows.Count switch
        {
            0 => null,
            1 => rows[0],
            _ => throw new InvalidOperationException(
                "The Player has more than one current Twin Reaches Training attempt.")
        };
    }

    private static Player? ReadTrainingPlayerForUpdate(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId)
    {
        var players = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.Players WITH (UPDLOCK, HOLDLOCK) WHERE PlayerID = @PlayerID;",
            sqlCommand => AddGuid(sqlCommand, "@PlayerID", playerId),
            ReadPlayer);
        return players.SingleOrDefault();
    }

    private static GameState CreateTwinReachesStartingState(
        Player player,
        TrainingGameProvisioningCommand command,
        GameProfileDefinition profile)
    {
        var gameId = Guid.NewGuid();
        var configurationId = Guid.NewGuid();
        var requestId = command.RequestId.ToString("N");
        return new GameState
        {
            Players = [player],
            Games =
            [
                new Game
                {
                    GameId = gameId,
                    Name = profile.DisplayName,
                    Purpose = profile.Purpose,
                    Status = GameLifecycleStatus.Starting,
                    Visibility = profile.GamePolicy.Visibility,
                    CreationSource = GameCreationSource.TrainingProvisioning,
                    GamePolicyKey = profile.GamePolicy.Key,
                    GamePolicyVersion = profile.GamePolicy.Version,
                    GamePolicyContentHash = profile.GamePolicy.ContentHash,
                    PolicyProvenanceStatus = ProvenanceStatus.Verified,
                    CreatedByPlayerId = player.PlayerId,
                    CreatedAt = command.RequestedAt
                }
            ],
            CycleConfigurations =
            [
                new CycleConfiguration
                {
                    CycleConfigurationId = configurationId,
                    GameId = gameId,
                    SequenceNumber = 1,
                    Status = CycleConfigurationStatus.Locked,
                    ProvenanceStatus = ProvenanceStatus.Verified,
                    MapProfileKey = profile.Map.Key,
                    MapProfileVersion = profile.Map.Version,
                    MapProfileContentHash = profile.Map.ContentHash,
                    MapSeed = TwinReachesMapSeed,
                    ScenarioProfileKey = profile.Scenario.Key,
                    ScenarioProfileVersion = profile.Scenario.Version,
                    ScenarioProfileContentHash = profile.Scenario.ContentHash,
                    ScenarioSeed = TwinReachesScenarioSeed,
                    CyclePolicyKey = profile.CyclePolicy.Key,
                    CyclePolicyVersion = profile.CyclePolicy.Version,
                    CyclePolicyContentHash = profile.CyclePolicy.ContentHash,
                    SchedulingMode = profile.CyclePolicy.SchedulingMode,
                    MinimumHumanSeats = profile.MinimumHumanSeats,
                    MaximumHumanSeats = profile.MaximumHumanSeats,
                    TickLengthMinutes = profile.CyclePolicy.TickLengthMinutes,
                    CreatedAt = command.RequestedAt,
                    LockedAt = command.RequestedAt
                }
            ],
            GameEnrolments =
            [
                new GameEnrolment
                {
                    GameEnrolmentId = Guid.NewGuid(),
                    GameId = gameId,
                    PlayerId = player.PlayerId,
                    Status = GameEnrolmentStatus.Enrolled,
                    Origin = GameEnrolmentOrigin.Direct,
                    OriginatingRequestId = requestId,
                    EnrolledAt = command.RequestedAt,
                    StatusChangedAt = command.RequestedAt
                }
            ],
            GameLifecycleEvents =
            [
                new GameLifecycleEvent
                {
                    GameLifecycleEventId = Guid.NewGuid(),
                    GameId = gameId,
                    Type = GameLifecycleEventType.Created,
                    SubjectPlayerId = player.PlayerId,
                    ActorPlayerId = player.PlayerId,
                    ToStatus = GameLifecycleStatus.Starting.ToString(),
                    Reason = "A private Twin Reaches Training attempt was requested.",
                    CorrelationId = requestId,
                    FactJson = "{}",
                    CreatedAt = command.RequestedAt
                },
                new GameLifecycleEvent
                {
                    GameLifecycleEventId = Guid.NewGuid(),
                    GameId = gameId,
                    Type = GameLifecycleEventType.EnrolmentChanged,
                    SubjectPlayerId = player.PlayerId,
                    ActorPlayerId = player.PlayerId,
                    Reason = "The requesting Player was enrolled in the private Training Game.",
                    CorrelationId = requestId,
                    FactJson = "{}",
                    CreatedAt = command.RequestedAt
                }
            ]
        };
    }

    private static void SaveNewMaterializedGameUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        GameState state)
    {
        EnsureGameFoundations(state);
        CycleScheduling.NormalizePersistedSchedule(state);

        foreach (var item in state.Games)
        {
            UpsertGame(connection, transaction, item);
        }
        foreach (var item in state.CycleConfigurations)
        {
            UpsertCycleConfiguration(connection, transaction, item);
        }
        foreach (var item in OrderCyclesForPersistence(state.Cycles))
        {
            UpsertCycle(connection, transaction, item);
        }
        foreach (var item in state.GameEnrolments)
        {
            UpsertGameEnrolment(connection, transaction, item);
        }
        foreach (var item in state.GameLifecycleEvents)
        {
            UpsertGameLifecycleEvent(connection, transaction, item);
        }
        foreach (var item in state.Sectors)
        {
            UpsertSector(connection, transaction, item);
        }
        foreach (var item in state.Systems)
        {
            UpsertSystem(connection, transaction, item);
        }
        foreach (var item in state.Empires)
        {
            UpsertEmpire(connection, transaction, item);
        }
        foreach (var item in state.Factions)
        {
            UpsertFaction(connection, transaction, item);
        }
        foreach (var item in state.MatchParticipants)
        {
            UpsertMatchParticipant(connection, transaction, item);
        }
        foreach (var item in state.EmpireResources)
        {
            UpsertEmpireResource(connection, transaction, item);
        }
        foreach (var item in state.EmpirePriorities)
        {
            UpsertEmpirePriority(connection, transaction, item);
        }
        foreach (var item in state.SystemLinks)
        {
            UpsertSystemLink(connection, transaction, item);
        }
        foreach (var item in state.Admirals)
        {
            UpsertAdmiral(connection, transaction, item);
        }
        foreach (var item in state.Fleets)
        {
            UpsertFleet(connection, transaction, item);
        }
        foreach (var item in state.Events)
        {
            UpsertEvent(connection, transaction, item);
        }
    }

    private sealed record CurrentTrainingAttempt(Guid GameId, Guid CycleId);
}
