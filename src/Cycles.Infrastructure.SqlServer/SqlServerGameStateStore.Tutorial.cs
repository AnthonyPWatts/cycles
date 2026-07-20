using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    public TutorialAttemptResult<TutorialJourneySnapshot> GetJourney(Guid playerId, Guid gameId)
    {
        if (playerId == Guid.Empty || gameId == Guid.Empty)
        {
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var run = ReadTutorialRun(connection, transaction, playerId, gameId, forUpdate: false);
        if (run is null)
        {
            transaction.Commit();
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
        }

        var journey = ReadTutorialJourney(connection, transaction, run);
        transaction.Commit();
        return journey is null
            ? new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable()
            : new TutorialAttemptResult<TutorialJourneySnapshot>.Success(journey);
    }

    public TutorialAttemptResult<TutorialJourneySnapshot> Acknowledge(
        TutorialAcknowledgementCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (string.IsNullOrWhiteSpace(command.AcknowledgementKey))
        {
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Conflict(
                "An acknowledgement key is required.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            AcquireSqlApplicationLock(
                connection,
                transaction,
                BuildTrainingLockName(
                    command.PlayerId,
                    TutorialJourneyEvaluator.FoundationsDefinitionVersion));
            var unlocked = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: false);
            if (unlocked is null)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
            }

            AcquireGameResolutionLock(connection, transaction, unlocked.GameId);
            AcquireCycleTickLock(connection, transaction, unlocked.CycleId);
            var run = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: true);
            if (run is null)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
            }

            var acknowledgements = ReadAcknowledgements(connection, transaction, run.TutorialRunId);
            if (acknowledgements.Contains(command.AcknowledgementKey))
            {
                var duplicateJourney = ReadTutorialJourney(connection, transaction, run);
                transaction.Commit();
                return duplicateJourney is null
                    ? new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable()
                    : new TutorialAttemptResult<TutorialJourneySnapshot>.Success(duplicateJourney);
            }

            if (run.Status != TutorialRunStatus.Active)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Conflict(
                    "Only an active tutorial journey can record a new acknowledgement.");
            }

            var journey = ReadTutorialJourney(connection, transaction, run);
            if (journey?.CurrentLesson is not { } lesson
                || !lesson.MechanicalEvidence.Satisfied
                || !string.Equals(
                    lesson.PresentationAcknowledgement.Key,
                    command.AcknowledgementKey,
                    StringComparison.Ordinal))
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Conflict(
                    "That explanation is not ready to acknowledge from the current authoritative state.");
            }

            InsertAcknowledgement(connection, transaction, run.TutorialRunId, command);
            acknowledgements.Add(command.AcknowledgementKey);
            var updatedJourney = ReadTutorialJourney(
                connection,
                transaction,
                run,
                acknowledgements);
            if (updatedJourney?.CoreCompleted == true)
            {
                CompleteTutorialRun(connection, transaction, run, command.AcknowledgedAt);
                run = run with
                {
                    Status = TutorialRunStatus.Completed,
                    EndedAt = command.AcknowledgedAt
                };
                updatedJourney = ReadTutorialJourney(
                    connection,
                    transaction,
                    run,
                    acknowledgements);
            }

            transaction.Commit();
            return updatedJourney is null
                ? new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable()
                : new TutorialAttemptResult<TutorialJourneySnapshot>.Success(updatedJourney);
        }
        catch (TimeoutException)
        {
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Busy();
        }
    }

    public TutorialAttemptResult<TutorialJourneySnapshot> ChangeStatus(
        TutorialStatusCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.Status is not (TutorialRunStatus.Active or TutorialRunStatus.Paused or TutorialRunStatus.Skipped))
        {
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Conflict(
                "The requested tutorial status transition is not player-controlled.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            AcquireSqlApplicationLock(
                connection,
                transaction,
                BuildTrainingLockName(
                    command.PlayerId,
                    TutorialJourneyEvaluator.FoundationsDefinitionVersion));
            var unlocked = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: false);
            if (unlocked is null)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
            }

            AcquireGameResolutionLock(connection, transaction, unlocked.GameId);
            AcquireCycleTickLock(connection, transaction, unlocked.CycleId);
            var run = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: true);
            if (run is null)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable();
            }

            var valid = (run.Status, command.Status) switch
            {
                (TutorialRunStatus.Active, TutorialRunStatus.Paused) => true,
                (TutorialRunStatus.Paused, TutorialRunStatus.Active) => true,
                (TutorialRunStatus.Active, TutorialRunStatus.Skipped) => true,
                (TutorialRunStatus.Paused, TutorialRunStatus.Skipped) => true,
                _ when run.Status == command.Status => true,
                _ => false
            };
            if (!valid)
            {
                transaction.Commit();
                return new TutorialAttemptResult<TutorialJourneySnapshot>.Conflict(
                    $"A {run.Status} tutorial journey cannot transition to {command.Status}.");
            }

            if (run.Status != command.Status)
            {
                Execute(
                    connection,
                    transaction,
                    """
                    UPDATE dbo.TutorialRuns
                    SET Status = @Status,
                        StatusChangedAt = @ChangedAt,
                        EndedAt = @EndedAt
                    WHERE TutorialRunID = @TutorialRunID;
                    """,
                    sqlCommand =>
                    {
                        AddGuid(sqlCommand, "@TutorialRunID", run.TutorialRunId);
                        AddString(sqlCommand, "@Status", command.Status.ToString(), 32);
                        AddDateTimeOffset(sqlCommand, "@ChangedAt", command.ChangedAt);
                        AddNullableDateTimeOffset(
                            sqlCommand,
                            "@EndedAt",
                            command.Status == TutorialRunStatus.Skipped ? command.ChangedAt : null);
                    });
                if (command.Status == TutorialRunStatus.Skipped)
                {
                    RecordTutorialSkip(connection, transaction, run, command.ChangedAt);
                }
                run = run with
                {
                    Status = command.Status,
                    EndedAt = command.Status == TutorialRunStatus.Skipped ? command.ChangedAt : null
                };
            }

            var journey = ReadTutorialJourney(connection, transaction, run);
            transaction.Commit();
            return journey is null
                ? new TutorialAttemptResult<TutorialJourneySnapshot>.Unavailable()
                : new TutorialAttemptResult<TutorialJourneySnapshot>.Success(journey);
        }
        catch (TimeoutException)
        {
            return new TutorialAttemptResult<TutorialJourneySnapshot>.Busy();
        }
    }

    public TutorialAttemptResult<FreshTrainingAttemptSnapshot> StartFresh(
        FreshTrainingAttemptCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.RequestId == Guid.Empty)
        {
            return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Conflict(
                "A fresh-attempt request identifier is required.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            AcquireSqlApplicationLock(
                connection,
                transaction,
                BuildTrainingLockName(
                    command.PlayerId,
                    TutorialJourneyEvaluator.FoundationsDefinitionVersion));

            var replay = ReadFreshAttemptByRequest(
                connection,
                transaction,
                command.PlayerId,
                command.RequestId);
            if (replay is not null)
            {
                transaction.Commit();
                return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Success(
                    replay with { Created = false });
            }

            var oldRun = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: false);
            if (oldRun is null || oldRun.Status == TutorialRunStatus.Superseded)
            {
                transaction.Commit();
                return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Unavailable();
            }

            AcquireGameResolutionLock(connection, transaction, oldRun.GameId);
            AcquireCycleTickLock(connection, transaction, oldRun.CycleId);
            oldRun = ReadTutorialRun(
                connection,
                transaction,
                command.PlayerId,
                command.GameId,
                forUpdate: true);
            if (oldRun is null || oldRun.Status == TutorialRunStatus.Superseded)
            {
                transaction.Commit();
                return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Unavailable();
            }

            var player = ReadTrainingPlayerForUpdate(
                connection,
                transaction,
                command.PlayerId);
            if (player is null || player.Kind != PlayerKind.Human || player.Status != PlayerStatus.Active)
            {
                transaction.Commit();
                return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Unavailable();
            }

            // Vacate the one-current-run slot before inserting the replacement.
            Execute(
                connection,
                transaction,
                """
                UPDATE dbo.TutorialRuns
                SET Status = @SkippedStatus,
                    SupersededByTutorialRunID = NULL,
                    StatusChangedAt = @RequestedAt,
                    EndedAt = COALESCE(EndedAt, @RequestedAt)
                WHERE TutorialRunID = @TutorialRunID;
                """,
                sqlCommand =>
                {
                    AddGuid(sqlCommand, "@TutorialRunID", oldRun.TutorialRunId);
                    AddString(sqlCommand, "@SkippedStatus", TutorialRunStatus.Skipped.ToString(), 32);
                    AddDateTimeOffset(sqlCommand, "@RequestedAt", command.RequestedAt);
                });

            var profile = GameProfileCatalogue.TwinReaches;
            var state = CreateTwinReachesStartingState(
                player,
                new TrainingGameProvisioningCommand(
                    player.PlayerId,
                    command.RequestId,
                    command.RequestedAt),
                profile);
            var materialized = RosterAwareCycleFactory.Materialize(
                state,
                state.CycleConfigurations.Single().CycleConfigurationId,
                command.RequestedAt);
            SaveNewMaterializedGameUnsafe(connection, transaction, state);
            var newRunId = Guid.NewGuid();
            InsertTutorialRun(
                connection,
                transaction,
                newRunId,
                materialized.GameId,
                materialized.CycleId,
                player.PlayerId,
                command.RequestId,
                command.RequestedAt);

            SupersedeOldTrainingAttempt(
                connection,
                transaction,
                oldRun,
                newRunId,
                command.RequestId,
                command.RequestedAt);
            transaction.Commit();

            return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Success(
                new FreshTrainingAttemptSnapshot(
                    newRunId,
                    materialized.GameId,
                    materialized.CycleId,
                    Created: true));
        }
        catch (TimeoutException)
        {
            return new TutorialAttemptResult<FreshTrainingAttemptSnapshot>.Busy();
        }
    }

    private static TutorialJourneySnapshot? ReadTutorialJourney(
        SqlConnection connection,
        SqlTransaction transaction,
        TutorialRunSnapshot run,
        HashSet<string>? acknowledgements = null)
    {
        var context = ReadTutorialCommandContext(connection, transaction, run);
        if (context is null)
        {
            return null;
        }

        var state = LoadFocusedViewStateUnsafe(connection, transaction, context);
        return TutorialJourneyEvaluator.Evaluate(
            state,
            context,
            run,
            acknowledgements ?? ReadAcknowledgements(connection, transaction, run.TutorialRunId));
    }

    private static TutorialRunSnapshot? ReadTutorialRun(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId,
        Guid gameId,
        bool forUpdate)
    {
        var lockHint = forUpdate ? " WITH (UPDLOCK, HOLDLOCK)" : "";
        var runs = ReadRows(
            connection,
            transaction,
            $"""
            SELECT run.*
            FROM dbo.TutorialRuns AS run{lockHint}
            WHERE run.PlayerID = @PlayerID
              AND run.GameID = @GameID
              AND run.TutorialKey = @TutorialKey
              AND run.DefinitionVersion = @DefinitionVersion;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", playerId);
                AddGuid(sqlCommand, "@GameID", gameId);
                AddString(sqlCommand, "@TutorialKey", GameProfileCatalogue.TwinReachesProfileKey, 128);
                AddInt(sqlCommand, "@DefinitionVersion", TutorialJourneyEvaluator.FoundationsDefinitionVersion);
            },
            ReadTutorialRun);
        return runs.SingleOrDefault();
    }

    private static TutorialRunSnapshot ReadTutorialRun(SqlDataReader reader) => new(
        GetGuid(reader, "TutorialRunID"),
        GetGuid(reader, "GameID"),
        GetGuid(reader, "CycleID"),
        GetGuid(reader, "PlayerID"),
        GetString(reader, "TutorialKey"),
        GetInt(reader, "DefinitionVersion"),
        GetEnum<TutorialRunStatus>(reader, "Status"),
        GetDateTimeOffset(reader, "StartedAt"),
        GetNullableGuid(reader, "SupersededByTutorialRunID"),
        GetNullableDateTimeOffset(reader, "EndedAt"));

    private static GameCommandContext? ReadTutorialCommandContext(
        SqlConnection connection,
        SqlTransaction transaction,
        TutorialRunSnapshot run)
    {
        var contexts = ReadRows(
            connection,
            transaction,
            """
            SELECT
                player.PlayerID,
                player.Role AS PlayerRole,
                game.GameID,
                game.CreatedByPlayerID,
                enrolment.GameEnrolmentID,
                cycle.CycleID,
                participant.MatchParticipantID,
                empire.EmpireID
            FROM dbo.Players AS player
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.PlayerID = player.PlayerID
               AND enrolment.GameID = @GameID
               AND enrolment.Status <> @WithdrawnEnrolmentStatus
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
               AND game.Status = @ActiveGameStatus
               AND game.Purpose = @TrainingPurpose
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.CycleID = @CycleID
               AND cycle.Status IN (@ActiveCycleStatus, @RecoveryCycleStatus)
            INNER JOIN dbo.MatchParticipants AS participant
                ON participant.GameID = game.GameID
               AND participant.CycleID = cycle.CycleID
               AND participant.PlayerID = player.PlayerID
               AND participant.Status IN (@ActiveParticipantStatus, @DefeatedParticipantStatus)
            INNER JOIN dbo.Empires AS empire
                ON empire.EmpireID = participant.EmpireID
               AND empire.CycleID = cycle.CycleID
               AND empire.PlayerID = player.PlayerID
            WHERE player.PlayerID = @PlayerID
              AND player.Status = @ActivePlayerStatus
              AND player.PlayerKind = @HumanPlayerKind;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", run.PlayerId);
                AddGuid(sqlCommand, "@GameID", run.GameId);
                AddGuid(sqlCommand, "@CycleID", run.CycleId);
                AddString(sqlCommand, "@WithdrawnEnrolmentStatus", GameEnrolmentStatus.Withdrawn.ToString(), 32);
                AddString(sqlCommand, "@ActiveGameStatus", GameLifecycleStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@TrainingPurpose", GamePurpose.Training.ToString(), 32);
                AddString(sqlCommand, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@RecoveryCycleStatus", CycleStatus.RecoveryRequired.ToString(), 32);
                AddString(sqlCommand, "@ActiveParticipantStatus", MatchParticipantStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@DefeatedParticipantStatus", MatchParticipantStatus.Defeated.ToString(), 32);
                AddString(sqlCommand, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
            },
            ReadGameCommandContext);
        return contexts.SingleOrDefault();
    }

    private static HashSet<string> ReadAcknowledgements(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid tutorialRunId) =>
        ReadRows(
                connection,
                transaction,
                """
                SELECT AcknowledgementKey
                FROM dbo.TutorialAcknowledgements
                WHERE TutorialRunID = @TutorialRunID;
                """,
                sqlCommand => AddGuid(sqlCommand, "@TutorialRunID", tutorialRunId),
                reader => GetString(reader, "AcknowledgementKey"))
            .ToHashSet(StringComparer.Ordinal);

    private static void InsertAcknowledgement(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid tutorialRunId,
        TutorialAcknowledgementCommand command) =>
        Execute(
            connection,
            transaction,
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.TutorialAcknowledgements WITH (UPDLOCK, HOLDLOCK)
                WHERE TutorialRunID = @TutorialRunID
                  AND AcknowledgementKey = @AcknowledgementKey
            )
            BEGIN
                INSERT dbo.TutorialAcknowledgements
                    (TutorialRunID, AcknowledgementKey, AcknowledgedAt)
                VALUES
                    (@TutorialRunID, @AcknowledgementKey, @AcknowledgedAt);
            END;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@TutorialRunID", tutorialRunId);
                AddString(sqlCommand, "@AcknowledgementKey", command.AcknowledgementKey, 128);
                AddDateTimeOffset(sqlCommand, "@AcknowledgedAt", command.AcknowledgedAt);
            });

    private static void CompleteTutorialRun(
        SqlConnection connection,
        SqlTransaction transaction,
        TutorialRunSnapshot run,
        DateTimeOffset completedAt) =>
        Execute(
            connection,
            transaction,
            """
            UPDATE dbo.TutorialRuns
            SET Status = @CompletedStatus,
                StatusChangedAt = @CompletedAt,
                EndedAt = @CompletedAt
            WHERE TutorialRunID = @TutorialRunID
              AND Status = @ActiveStatus;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.TutorialCompletions WITH (UPDLOCK, HOLDLOCK)
                WHERE PlayerID = @PlayerID
                  AND TutorialKey = @TutorialKey
                  AND DefinitionVersion = @DefinitionVersion
            )
            BEGIN
                INSERT dbo.TutorialCompletions
                    (PlayerID, TutorialKey, DefinitionVersion, FirstCompletedRunID, FirstCompletedAt)
                VALUES
                    (@PlayerID, @TutorialKey, @DefinitionVersion, @TutorialRunID, @CompletedAt);
            END;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@TutorialRunID", run.TutorialRunId);
                AddGuid(sqlCommand, "@PlayerID", run.PlayerId);
                AddString(sqlCommand, "@TutorialKey", run.TutorialKey, 128);
                AddInt(sqlCommand, "@DefinitionVersion", run.DefinitionVersion);
                AddString(sqlCommand, "@ActiveStatus", TutorialRunStatus.Active.ToString(), 32);
                AddString(sqlCommand, "@CompletedStatus", TutorialRunStatus.Completed.ToString(), 32);
                AddDateTimeOffset(sqlCommand, "@CompletedAt", completedAt);
            });

    private static void RecordTutorialSkip(
        SqlConnection connection,
        SqlTransaction transaction,
        TutorialRunSnapshot run,
        DateTimeOffset skippedAt) =>
        Execute(
            connection,
            transaction,
            """
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.TutorialSkips WITH (UPDLOCK, HOLDLOCK)
                WHERE PlayerID = @PlayerID
                  AND TutorialKey = @TutorialKey
                  AND DefinitionVersion = @DefinitionVersion
            )
            BEGIN
                INSERT dbo.TutorialSkips
                    (PlayerID, TutorialKey, DefinitionVersion, FirstSkippedRunID, FirstSkippedAt)
                VALUES
                    (@PlayerID, @TutorialKey, @DefinitionVersion, @TutorialRunID, @SkippedAt);
            END;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@TutorialRunID", run.TutorialRunId);
                AddGuid(sqlCommand, "@PlayerID", run.PlayerId);
                AddString(sqlCommand, "@TutorialKey", run.TutorialKey, 128);
                AddInt(sqlCommand, "@DefinitionVersion", run.DefinitionVersion);
                AddDateTimeOffset(sqlCommand, "@SkippedAt", skippedAt);
            });

    private static FreshTrainingAttemptSnapshot? ReadFreshAttemptByRequest(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid playerId,
        Guid requestId)
    {
        var attempts = ReadRows(
            connection,
            transaction,
            """
            SELECT TutorialRunID, GameID, CycleID
            FROM dbo.TutorialRuns WITH (UPDLOCK, HOLDLOCK)
            WHERE PlayerID = @PlayerID
              AND TutorialKey = @TutorialKey
              AND DefinitionVersion = @DefinitionVersion
              AND OriginatingRequestID = @OriginatingRequestID;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@PlayerID", playerId);
                AddString(sqlCommand, "@TutorialKey", GameProfileCatalogue.TwinReachesProfileKey, 128);
                AddInt(sqlCommand, "@DefinitionVersion", TutorialJourneyEvaluator.FoundationsDefinitionVersion);
                AddGuid(sqlCommand, "@OriginatingRequestID", requestId);
            },
            reader => new FreshTrainingAttemptSnapshot(
                GetGuid(reader, "TutorialRunID"),
                GetGuid(reader, "GameID"),
                GetGuid(reader, "CycleID"),
                Created: false));
        return attempts.SingleOrDefault();
    }

    private static void SupersedeOldTrainingAttempt(
        SqlConnection connection,
        SqlTransaction transaction,
        TutorialRunSnapshot oldRun,
        Guid newRunId,
        Guid requestId,
        DateTimeOffset requestedAt)
    {
        Execute(
            connection,
            transaction,
            """
            UPDATE dbo.TutorialRuns
            SET Status = @SupersededStatus,
                SupersededByTutorialRunID = @NewTutorialRunID,
                StatusChangedAt = @RequestedAt,
                EndedAt = @RequestedAt
            WHERE TutorialRunID = @OldTutorialRunID;

            UPDATE dbo.MatchParticipants
            SET Status = @CompletedParticipantStatus,
                EndedAt = COALESCE(EndedAt, @RequestedAt)
            WHERE GameID = @OldGameID
              AND CycleID = @OldCycleID
              AND PlayerID = @PlayerID;

            UPDATE dbo.GameEnrolments
            SET Status = @HistoricalEnrolmentStatus,
                StatusChangedAt = @RequestedAt,
                EndedAt = NULL
            WHERE GameID = @OldGameID
              AND PlayerID = @PlayerID;

            UPDATE dbo.Cycles
            SET Status = @CompletedCycleStatus,
                NextTickAt = NULL
            WHERE CycleID = @OldCycleID
              AND GameID = @OldGameID;

            UPDATE dbo.Games
            SET Status = @TerminatedGameStatus,
                CompletedAt = NULL,
                CancelledAt = NULL,
                TerminatedAt = @RequestedAt
            WHERE GameID = @OldGameID;
            """,
            sqlCommand =>
            {
                AddGuid(sqlCommand, "@OldTutorialRunID", oldRun.TutorialRunId);
                AddGuid(sqlCommand, "@NewTutorialRunID", newRunId);
                AddGuid(sqlCommand, "@OldGameID", oldRun.GameId);
                AddGuid(sqlCommand, "@OldCycleID", oldRun.CycleId);
                AddGuid(sqlCommand, "@PlayerID", oldRun.PlayerId);
                AddString(sqlCommand, "@SupersededStatus", TutorialRunStatus.Superseded.ToString(), 32);
                AddString(sqlCommand, "@CompletedParticipantStatus", MatchParticipantStatus.Completed.ToString(), 32);
                AddString(sqlCommand, "@HistoricalEnrolmentStatus", GameEnrolmentStatus.Historical.ToString(), 32);
                AddString(sqlCommand, "@CompletedCycleStatus", CycleStatus.Completed.ToString(), 32);
                AddString(sqlCommand, "@TerminatedGameStatus", GameLifecycleStatus.Terminated.ToString(), 32);
                AddDateTimeOffset(sqlCommand, "@RequestedAt", requestedAt);
            });

        UpsertGameLifecycleEvent(
            connection,
            transaction,
            new GameLifecycleEvent
            {
                GameLifecycleEventId = Guid.NewGuid(),
                GameId = oldRun.GameId,
                Type = GameLifecycleEventType.StatusChanged,
                SubjectPlayerId = oldRun.PlayerId,
                ActorPlayerId = oldRun.PlayerId,
                FromStatus = GameLifecycleStatus.Active.ToString(),
                ToStatus = GameLifecycleStatus.Terminated.ToString(),
                Reason = "The Player started a fresh private Training attempt.",
                CorrelationId = requestId.ToString("N"),
                FactJson = "{}",
                CreatedAt = requestedAt
            });
    }
}
