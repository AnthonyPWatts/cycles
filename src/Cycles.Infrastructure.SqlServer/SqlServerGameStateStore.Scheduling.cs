using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    public WorkerScheduleStatus GetWorkerScheduleStatus(
        DateTimeOffset now,
        TimeSpan runningAttemptSuspicionThreshold)
    {
        if (runningAttemptSuspicionThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(runningAttemptSuspicionThreshold),
                "The running-attempt suspicion threshold must be positive.");
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        using var command = CreateCommand(connection, transaction, """
            SELECT
                COUNT_BIG(CASE
                    WHEN game.Status = N'Active'
                     AND game.Purpose = N'Standard'
                     AND cycle.Status = N'Active'
                     AND cycle.SchedulingMode = N'Scheduled'
                     AND configuration.Status = N'Materialized'
                     AND configuration.SchedulingMode = cycle.SchedulingMode
                     AND cycle.NextTickAt IS NOT NULL
                    THEN 1 END) AS ActiveScheduledCycleCount,
                COUNT_BIG(CASE
                    WHEN game.Status = N'Active'
                     AND game.Purpose = N'Standard'
                     AND cycle.Status = N'RecoveryRequired'
                     AND cycle.SchedulingMode = N'Scheduled'
                    THEN 1 END) AS RecoveryBlockedCycleCount,
                MIN(CASE
                    WHEN game.Status = N'Active'
                     AND game.Purpose = N'Standard'
                     AND cycle.Status = N'Active'
                     AND cycle.SchedulingMode = N'Scheduled'
                     AND configuration.Status = N'Materialized'
                     AND configuration.SchedulingMode = cycle.SchedulingMode
                    THEN cycle.NextTickAt END) AS EarliestNextTickAt,
                (
                    SELECT COUNT_BIG(*)
                    FROM dbo.TickLogs AS tickLog
                    WHERE tickLog.Status = N'Running'
                      AND tickLog.StartedAt <= @SuspiciousBefore
                ) AS SuspiciousRunningAttemptCount
            FROM dbo.Games AS game
            INNER JOIN dbo.Cycles AS cycle ON cycle.GameID = game.GameID
            INNER JOIN dbo.CycleConfigurations AS configuration
                ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
               AND configuration.GameID = game.GameID;
            """);
        AddDateTimeOffset(command, "@SuspiciousBefore", now - runningAttemptSuspicionThreshold);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            throw new InvalidOperationException("The Worker schedule-status query returned no row.");
        }

        var result = new WorkerScheduleStatus(
            checked((int)reader.GetInt64(reader.GetOrdinal("ActiveScheduledCycleCount"))),
            checked((int)reader.GetInt64(reader.GetOrdinal("RecoveryBlockedCycleCount"))),
            checked((int)reader.GetInt64(reader.GetOrdinal("SuspiciousRunningAttemptCount"))),
            reader.IsDBNull(reader.GetOrdinal("EarliestNextTickAt"))
                ? null
                : GetDateTimeOffset(reader, "EarliestNextTickAt"));
        reader.Close();
        transaction.Commit();
        return result;
    }

    public DueCycleWorkItem? GetNextDue(DateTimeOffset now)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        using var command = CreateCommand(connection, transaction, """
            SELECT TOP (1)
                game.GameID,
                cycle.CycleID,
                cycle.NextTickAt
            FROM dbo.Games AS game
            INNER JOIN dbo.Cycles AS cycle ON cycle.GameID = game.GameID
            INNER JOIN dbo.CycleConfigurations AS configuration
                ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
               AND configuration.GameID = game.GameID
            WHERE game.Status = N'Active'
              AND game.Purpose = N'Standard'
              AND cycle.Status = N'Active'
              AND cycle.SchedulingMode = N'Scheduled'
              AND configuration.Status = N'Materialized'
              AND configuration.SchedulingMode = cycle.SchedulingMode
              AND cycle.NextTickAt IS NOT NULL
              AND cycle.NextTickAt <= @Now
            ORDER BY cycle.NextTickAt, cycle.CycleID;
            """);
        AddDateTimeOffset(command, "@Now", now);

        using var reader = command.ExecuteReader();
        DueCycleWorkItem? workItem = null;
        if (reader.Read())
        {
            workItem = new DueCycleWorkItem(
                new GameCycleScope(
                    GetGuid(reader, "GameID"),
                    GetGuid(reader, "CycleID")),
                GetDateTimeOffset(reader, "NextTickAt"));
        }

        reader.Close();
        transaction.Commit();
        return workItem;
    }

    public CycleResolutionResult ResolveIfDue(
        DueCycleWorkItem workItem,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        return Resolve(workItem.Scope, now, workItem);
    }

    public CycleResolutionResult ResolveExplicit(
        ExplicitCycleResolutionRequest request,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Resolve(request.Scope, now, dueWorkItem: null, request);
    }

    internal CycleResolutionResult ResolveExplicitOperator(
        GameCycleScope scope,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(scope);
        return Resolve(scope, now, dueWorkItem: null, explicitRequest: null);
    }

    private CycleResolutionResult Resolve(
        GameCycleScope scope,
        DateTimeOffset now,
        DueCycleWorkItem? dueWorkItem,
        ExplicitCycleResolutionRequest? explicitRequest = null)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        try
        {
            AcquireGameResolutionLock(connection, transaction, scope.GameId);
            AcquireCycleTickLock(connection, transaction, scope.CycleId);

            if (explicitRequest is not null)
            {
                var authority = LockExplicitResolutionAuthority(
                    connection,
                    transaction,
                    explicitRequest);
                if (authority == ExplicitResolutionAuthority.Unavailable)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.Unavailable();
                }

                if (authority == ExplicitResolutionAuthority.Forbidden)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.Forbidden();
                }
            }

            var snapshot = ReadResolutionSnapshot(connection, transaction, scope);
            if (snapshot is null
                || snapshot.GameStatus != GameLifecycleStatus.Active
                || snapshot.CycleStatus != CycleStatus.Active
                || snapshot.ConfigurationStatus != CycleConfigurationStatus.Materialized
                || snapshot.SchedulingMode != snapshot.ConfigurationSchedulingMode)
            {
                transaction.Commit();
                return new CycleResolutionResult.Unavailable();
            }

            if (explicitRequest?.ExpectedCurrentTickNumber is { } expectedCurrentTickNumber
                && snapshot.CurrentTickNumber != expectedCurrentTickNumber)
            {
                transaction.Commit();
                return new CycleResolutionResult.Stale();
            }

            if (explicitRequest is not null
                && !ExplicitPolicyMatchesSnapshot(explicitRequest.Policy, snapshot))
            {
                transaction.Commit();
                return new CycleResolutionResult.Unavailable();
            }

            if (dueWorkItem is not null)
            {
                if (snapshot.GamePurpose != GamePurpose.Standard
                    || snapshot.SchedulingMode != CycleSchedulingMode.Scheduled)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.Unavailable();
                }

                if (snapshot.NextTickAt != dueWorkItem.NextTickAt)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.Stale();
                }

                if (!snapshot.NextTickAt.HasValue || snapshot.NextTickAt.Value > now)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.NotDue();
                }
            }

            var tutorialRequest = explicitRequest is { Policy: ExplicitCycleResolutionPolicy.TutorialJourney }
                ? explicitRequest
                : null;
            var state = tutorialRequest is not null
                ? LoadFocusedViewStateUnsafe(connection, transaction, tutorialRequest.Context)
                : LoadFocusedTickStateUnsafe(connection, transaction, scope.CycleId);
            if (tutorialRequest is not null)
            {
                var run = ReadTutorialRun(
                    connection,
                    transaction,
                    tutorialRequest.Context.GameAccess.PlayerId,
                    scope.GameId,
                    forUpdate: true);
                if (run is null
                    || !TutorialJourneyEvaluator.Evaluate(
                        state,
                        tutorialRequest.Context,
                        run,
                        ReadAcknowledgements(connection, transaction, run.TutorialRunId)).CanResolve)
                {
                    transaction.Commit();
                    return new CycleResolutionResult.Unavailable();
                }
            }

            StrategicPriorityPolicy.Normalize(state);
            var result = new TickEngine().RunTick(state, scope.CycleId, now);
            SaveTickOutcomeUnsafe(connection, transaction, state, scope.CycleId);
            transaction.Commit();

            return result.Status == TickLogStatus.Completed
                ? new CycleResolutionResult.Completed(result)
                : new CycleResolutionResult.RecoveryRequired(result);
        }
        catch (TimeoutException)
        {
            return new CycleResolutionResult.Busy();
        }
    }

    private static ExplicitResolutionAuthority LockExplicitResolutionAuthority(
        SqlConnection connection,
        SqlTransaction transaction,
        ExplicitCycleResolutionRequest request)
    {
        var context = request.Context;
        using var command = CreateCommand(connection, transaction, """
            SET NOCOUNT ON;

            -- Keep this order aligned with the resolution application locks:
            -- Game -> Cycle -> player -> enrolment -> participant -> empire.
            -- UPDLOCK + HOLDLOCK keeps every live authority row stable until
            -- the tick transaction commits or rolls back.
            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.Games WITH (UPDLOCK, HOLDLOCK)
                WHERE GameID = @GameID
                  AND Status = @ActiveGameStatus
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.Cycles WITH (UPDLOCK, HOLDLOCK)
                WHERE CycleID = @CycleID
                  AND GameID = @GameID
                  AND Status = @ActiveCycleStatus
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF @ResolutionPolicy = @TutorialJourneyPolicy
               AND NOT EXISTS
               (
                   SELECT 1
                   FROM dbo.TutorialRuns WITH (UPDLOCK, HOLDLOCK)
                   WHERE GameID = @GameID
                     AND CycleID = @CycleID
                     AND PlayerID = @PlayerID
                     AND TutorialKey = @TutorialKey
                     AND DefinitionVersion = @DefinitionVersion
                     AND Status = @ActiveTutorialStatus
               )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF @ResolutionPolicy = @SelfPacedParticipantPolicy
               AND EXISTS
               (
                   SELECT 1
                   FROM dbo.TutorialRuns WITH (UPDLOCK, HOLDLOCK)
                   WHERE GameID = @GameID
                     AND CycleID = @CycleID
                     AND PlayerID = @PlayerID
                     AND Status IN (@ActiveTutorialStatus, @PausedTutorialStatus)
               )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            DECLARE @PlayerRole nvarchar(32);
            SELECT @PlayerRole = Role
            FROM dbo.Players WITH (UPDLOCK, HOLDLOCK)
            WHERE PlayerID = @PlayerID
              AND Status = @ActivePlayerStatus
              AND PlayerKind = @HumanPlayerKind;

            IF @PlayerRole IS NULL
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.GameEnrolments WITH (UPDLOCK, HOLDLOCK)
                WHERE GameEnrolmentID = @GameEnrolmentID
                  AND PlayerID = @PlayerID
                  AND GameID = @GameID
                  AND Status = @EnrolledStatus
                  AND EndedAt IS NULL
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.MatchParticipants WITH (UPDLOCK, HOLDLOCK)
                WHERE MatchParticipantID = @MatchParticipantID
                  AND GameID = @GameID
                  AND CycleID = @CycleID
                  AND PlayerID = @PlayerID
                  AND EmpireID = @EmpireID
                  AND Status = @ActiveParticipantStatus
                  AND EndedAt IS NULL
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM dbo.Empires WITH (UPDLOCK, HOLDLOCK)
                WHERE EmpireID = @EmpireID
                  AND CycleID = @CycleID
                  AND PlayerID = @PlayerID
                  AND Status = @ActiveEmpireStatus
            )
            BEGIN
                SELECT 0;
                RETURN;
            END;

            IF @ResolutionPolicy = @AdministratorPolicy
               AND @PlayerRole <> @AdminPlayerRole
            BEGIN
                SELECT 1;
                RETURN;
            END;

            SELECT 2;
            """);
        AddGuid(command, "@PlayerID", context.GameAccess.PlayerId);
        AddGuid(command, "@GameID", context.GameAccess.GameId);
        AddGuid(command, "@GameEnrolmentID", context.GameAccess.GameEnrolmentId!.Value);
        AddGuid(command, "@CycleID", context.CycleId);
        AddGuid(command, "@MatchParticipantID", context.MatchParticipantId);
        AddGuid(command, "@EmpireID", context.EmpireId);
        AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
        AddString(command, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
        AddString(command, "@EnrolledStatus", GameEnrolmentStatus.Enrolled.ToString(), 32);
        AddString(command, "@ActiveGameStatus", GameLifecycleStatus.Active.ToString(), 32);
        AddString(command, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
        AddString(command, "@ActiveParticipantStatus", MatchParticipantStatus.Active.ToString(), 32);
        AddString(command, "@ActiveEmpireStatus", EmpireStatus.Active.ToString(), 32);
        AddString(command, "@ResolutionPolicy", request.Policy.ToString(), 32);
        AddString(command, "@AdministratorPolicy", ExplicitCycleResolutionPolicy.Administrator.ToString(), 32);
        AddString(command, "@SelfPacedParticipantPolicy", ExplicitCycleResolutionPolicy.SelfPacedParticipant.ToString(), 32);
        AddString(command, "@TutorialJourneyPolicy", ExplicitCycleResolutionPolicy.TutorialJourney.ToString(), 32);
        AddString(command, "@TutorialKey", GameProfileCatalogue.TwinReachesProfileKey, 128);
        AddInt(command, "@DefinitionVersion", TutorialJourneyEvaluator.FoundationsDefinitionVersion);
        AddString(command, "@ActiveTutorialStatus", TutorialRunStatus.Active.ToString(), 32);
        AddString(command, "@PausedTutorialStatus", TutorialRunStatus.Paused.ToString(), 32);
        AddString(command, "@AdminPlayerRole", PlayerRole.Admin.ToString(), 32);

        return (ExplicitResolutionAuthority)Convert.ToInt32(command.ExecuteScalar(), null);
    }

    private static ResolutionSnapshot? ReadResolutionSnapshot(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCycleScope scope)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT
                game.Purpose AS GamePurpose,
                game.Status AS GameStatus,
                cycle.Status AS CycleStatus,
                cycle.CurrentTickNumber,
                cycle.SchedulingMode,
                cycle.NextTickAt,
                configuration.Status AS ConfigurationStatus,
                configuration.SchedulingMode AS ConfigurationSchedulingMode
            FROM dbo.Games AS game
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
            INNER JOIN dbo.CycleConfigurations AS configuration
                ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
               AND configuration.GameID = cycle.GameID
            WHERE game.GameID = @GameID
              AND cycle.CycleID = @CycleID;
            """);
        AddGuid(command, "@GameID", scope.GameId);
        AddGuid(command, "@CycleID", scope.CycleId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        return new ResolutionSnapshot(
            GetEnum<GamePurpose>(reader, "GamePurpose"),
            GetEnum<GameLifecycleStatus>(reader, "GameStatus"),
            GetEnum<CycleStatus>(reader, "CycleStatus"),
            GetInt(reader, "CurrentTickNumber"),
            GetEnum<CycleSchedulingMode>(reader, "SchedulingMode"),
            GetNullableDateTimeOffset(reader, "NextTickAt"),
            GetEnum<CycleConfigurationStatus>(reader, "ConfigurationStatus"),
            GetEnum<CycleSchedulingMode>(reader, "ConfigurationSchedulingMode"));
    }

    private sealed record ResolutionSnapshot(
        GamePurpose GamePurpose,
        GameLifecycleStatus GameStatus,
        CycleStatus CycleStatus,
        int CurrentTickNumber,
        CycleSchedulingMode SchedulingMode,
        DateTimeOffset? NextTickAt,
        CycleConfigurationStatus ConfigurationStatus,
        CycleSchedulingMode ConfigurationSchedulingMode);

    private static bool ExplicitPolicyMatchesSnapshot(
        ExplicitCycleResolutionPolicy policy,
        ResolutionSnapshot snapshot) => policy switch
        {
            ExplicitCycleResolutionPolicy.Administrator => true,
            ExplicitCycleResolutionPolicy.DevelopmentStandard =>
                snapshot.GamePurpose == GamePurpose.Standard,
            ExplicitCycleResolutionPolicy.SelfPacedParticipant =>
                snapshot.SchedulingMode == CycleSchedulingMode.SelfPaced,
            ExplicitCycleResolutionPolicy.TutorialJourney =>
                snapshot.GamePurpose == GamePurpose.Training
                && snapshot.SchedulingMode == CycleSchedulingMode.SelfPaced,
            _ => false
        };

    private enum ExplicitResolutionAuthority
    {
        Unavailable = 0,
        Forbidden = 1,
        Authorised = 2
    }
}
