using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore
{
    public PlayerAccountSnapshot? Get(Guid playerId)
    {
        if (playerId == Guid.Empty)
        {
            return null;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var rows = ReadRows(
            connection,
            transaction,
            """
            SELECT
                PlayerID,
                Username,
                PlayerKind,
                Role,
                Status,
                CreatedAt,
                LastLoginAt
            FROM dbo.Players
            WHERE PlayerID = @PlayerID;
            """,
            command => AddGuid(command, "@PlayerID", playerId),
            reader => new PlayerAccountSnapshot(
                GetGuid(reader, "PlayerID"),
                GetString(reader, "Username"),
                GetEnum<PlayerKind>(reader, "PlayerKind"),
                GetEnum<PlayerRole>(reader, "Role"),
                GetEnum<PlayerStatus>(reader, "Status"),
                GetDateTimeOffset(reader, "CreatedAt"),
                GetNullableDateTimeOffset(reader, "LastLoginAt")));

        transaction.Commit();
        return rows.SingleOrDefault();
    }

    public GameCataloguePage ListForPlayer(
        Guid playerId,
        GameCatalogueCursor? cursor,
        int pageSize)
    {
        GameCataloguePage.ValidatePageSize(pageSize);
        if (playerId == Guid.Empty)
        {
            return new GameCataloguePage([], nextCursor: null);
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var items = ReadRows(
            connection,
            transaction,
            """
            SELECT TOP (@Take)
                game.GameID,
                game.Name,
                game.Purpose,
                game.Status AS GameStatus,
                game.Visibility,
                game.CreatedAt,
                enrolment.GameEnrolmentID,
                enrolment.Status AS EnrolmentStatus,
                enrolment.StatusChangedAt,
                cycle.CycleID AS OperationalCycleID,
                cycle.Status AS OperationalCycleStatus,
                cycle.CurrentTickNumber,
                cycle.TurnStage
            FROM dbo.GameEnrolments AS enrolment
            INNER JOIN dbo.Players AS player
                ON player.PlayerID = enrolment.PlayerID
               AND player.Status = @ActivePlayerStatus
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
            LEFT JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.Status IN (@ActiveCycleStatus, @RecoveryCycleStatus)
            WHERE enrolment.PlayerID = @PlayerID
              AND
              (
                  @CursorSortAt IS NULL
                  OR enrolment.StatusChangedAt < @CursorSortAt
                  OR
                  (
                      enrolment.StatusChangedAt = @CursorSortAt
                      AND game.GameID > @CursorGameID
                  )
              )
            ORDER BY enrolment.StatusChangedAt DESC, game.GameID ASC;
            """,
            command =>
            {
                AddInt(command, "@Take", pageSize + 1);
                AddGuid(command, "@PlayerID", playerId);
                AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
                AddString(command, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
                AddString(command, "@RecoveryCycleStatus", CycleStatus.RecoveryRequired.ToString(), 32);
                AddNullableDateTimeOffset(command, "@CursorSortAt", cursor?.SortAt);
                AddNullableGuid(command, "@CursorGameID", cursor?.GameId);
            },
            ReadGameCatalogueItem);

        transaction.Commit();

        var hasMore = items.Count > pageSize;
        var pageItems = items.Take(pageSize).ToArray();
        var nextCursor = hasMore
            ? new GameCatalogueCursor(
                pageItems[^1].EnrolmentStatusChangedAt,
                pageItems[^1].GameId)
            : null;
        return new GameCataloguePage(pageItems, nextCursor);
    }

    public GameAccessSnapshot? Get(Guid playerId, Guid gameId)
    {
        if (playerId == Guid.Empty || gameId == Guid.Empty)
        {
            return null;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        var rows = ReadRows(
            connection,
            transaction,
            """
            SELECT
                player.PlayerID,
                game.GameID,
                game.Name,
                game.Purpose,
                game.Status AS GameStatus,
                game.Visibility,
                game.CreatedByPlayerID,
                enrolment.GameEnrolmentID,
                enrolment.Status AS EnrolmentStatus,
                cycle.CycleID AS OperationalCycleID,
                cycle.Status AS OperationalCycleStatus,
                cycle.CurrentTickNumber,
                cycle.TurnStage
            FROM dbo.Players AS player
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.PlayerID = player.PlayerID
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
            LEFT JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.Status IN (@ActiveCycleStatus, @RecoveryCycleStatus)
            WHERE player.PlayerID = @PlayerID
              AND player.Status = @ActivePlayerStatus
              AND game.GameID = @GameID;
            """,
            command =>
            {
                AddGuid(command, "@PlayerID", playerId);
                AddGuid(command, "@GameID", gameId);
                AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
                AddString(command, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
                AddString(command, "@RecoveryCycleStatus", CycleStatus.RecoveryRequired.ToString(), 32);
            },
            reader => new GameAccessSnapshot(
                GetGuid(reader, "PlayerID"),
                GetGuid(reader, "GameID"),
                GetString(reader, "Name"),
                GetEnum<GamePurpose>(reader, "Purpose"),
                GetEnum<GameLifecycleStatus>(reader, "GameStatus"),
                GetEnum<GameVisibility>(reader, "Visibility"),
                GetNullableGuid(reader, "CreatedByPlayerID"),
                GetNullableGuid(reader, "GameEnrolmentID"),
                GetNullableEnum<GameEnrolmentStatus>(reader, "EnrolmentStatus"),
                GetNullableGuid(reader, "OperationalCycleID"),
                GetNullableEnum<CycleStatus>(reader, "OperationalCycleStatus"),
                GetNullableInt(reader, "CurrentTickNumber"),
                GetNullableEnum<TurnResolutionStage>(reader, "TurnStage")));

        transaction.Commit();
        return rows.SingleOrDefault();
    }

    public GameCommandContext? Get(Guid playerId, GameCycleScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (playerId == Guid.Empty)
        {
            return null;
        }

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
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
               AND enrolment.Status <> @WithdrawnEnrolmentStatus
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
            INNER JOIN dbo.MatchParticipants AS participant
                ON participant.GameID = game.GameID
               AND participant.CycleID = cycle.CycleID
               AND participant.PlayerID = player.PlayerID
               AND participant.Status IN
                   (@ActiveParticipantStatus, @DefeatedParticipantStatus, @CompletedParticipantStatus)
            INNER JOIN dbo.Empires AS empire
                ON empire.EmpireID = participant.EmpireID
               AND empire.CycleID = cycle.CycleID
               AND empire.PlayerID = player.PlayerID
            WHERE player.PlayerID = @PlayerID
              AND player.Status = @ActivePlayerStatus
              AND player.PlayerKind = @HumanPlayerKind
              AND game.GameID = @GameID
              AND cycle.CycleID = @CycleID;
            """,
            command =>
            {
                AddGuid(command, "@PlayerID", playerId);
                AddGuid(command, "@GameID", scope.GameId);
                AddGuid(command, "@CycleID", scope.CycleId);
                AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
                AddString(command, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
                AddString(command, "@WithdrawnEnrolmentStatus", GameEnrolmentStatus.Withdrawn.ToString(), 32);
                AddString(command, "@ActiveParticipantStatus", MatchParticipantStatus.Active.ToString(), 32);
                AddString(command, "@DefeatedParticipantStatus", MatchParticipantStatus.Defeated.ToString(), 32);
                AddString(command, "@CompletedParticipantStatus", MatchParticipantStatus.Completed.ToString(), 32);
            },
            ReadGameCommandContext);

        transaction.Commit();
        return contexts.SingleOrDefault();
    }

    public ScopedQueryResult<T> Query<T>(
        GameCommandContext context,
        Func<GameState, T> projection)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(projection);

        GameState state;
        using (var connection = OpenConnection())
        using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
        {
            if (!ReadableContextExists(connection, transaction, context))
            {
                transaction.Commit();
                return new ScopedQueryResult<T>.Unavailable();
            }

            state = LoadFocusedViewStateUnsafe(connection, transaction, context);
            transaction.Commit();
        }

        return new ScopedQueryResult<T>.Success(projection(state));
    }

    public ScopedCommandResult<T> Execute<T>(
        GameCommandContext context,
        Func<GameState, T> command)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(command);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        try
        {
            AcquireCycleTickLock(connection, transaction, context.CycleId);
        }
        catch (TimeoutException)
        {
            return new ScopedCommandResult<T>.Busy();
        }

        if (!ActiveCommandContextExists(connection, transaction, context))
        {
            transaction.Commit();
            return new ScopedCommandResult<T>.Unavailable();
        }

        var scope = context.Scope;
        var state = LoadFocusedTickStateUnsafe(connection, transaction, context.CycleId);
        var before = state.DeepClone();
        var value = command(state);
        var changes = ValidateScopedCommandChanges(scope, before, state);

        EnsureAddedIdentifiersDoNotExist(
            connection,
            transaction,
            changes.AddedFleetOrderIds,
            ScopedEntityTable.FleetOrder);
        EnsureAddedIdentifiersDoNotExist(
            connection,
            transaction,
            changes.AddedEmpirePriorityIds,
            ScopedEntityTable.EmpirePriority);
        EnsureAddedIdentifiersDoNotExist(
            connection,
            transaction,
            changes.AddedEventIds,
            ScopedEntityTable.Event);

        foreach (var fleet in changes.Fleets)
        {
            UpsertFleet(connection, transaction, fleet);
        }

        UpsertFleetOrders(connection, transaction, changes.FleetOrders);

        foreach (var priority in changes.EmpirePriorities)
        {
            UpsertEmpirePriority(connection, transaction, priority);
        }

        foreach (var eventRecord in changes.Events)
        {
            InsertScopedEvent(connection, transaction, eventRecord);
        }

        transaction.Commit();
        return new ScopedCommandResult<T>.Success(value);
    }

    public GameCycleScope GetRequired()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        var cycleIds = ReadRows(
            connection,
            transaction,
            """
            SELECT cycle.CycleID
            FROM dbo.Games AS game
            LEFT JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.Status IN (@ActiveCycleStatus, @RecoveryCycleStatus)
            WHERE game.GameID = @GameID
            ORDER BY cycle.CycleID;
            """,
            command =>
            {
                AddGuid(command, "@GameID", GameFoundationConstants.LegacyGameId);
                AddString(command, "@ActiveCycleStatus", CycleStatus.Active.ToString(), 32);
                AddString(command, "@RecoveryCycleStatus", CycleStatus.RecoveryRequired.ToString(), 32);
            },
            reader => GetNullableGuid(reader, "CycleID"));
        transaction.Commit();

        if (cycleIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"The fixed legacy Game '{GameFoundationConstants.LegacyGameId:D}' does not exist. Apply the Game-foundation migrations and seed or import the runtime before using legacy routes.");
        }

        var operationalCycleIds = cycleIds
            .Where(item => item.HasValue)
            .Select(item => item!.Value)
            .ToArray();
        if (operationalCycleIds.Length == 0)
        {
            throw new InvalidOperationException(
                $"The fixed legacy Game '{GameFoundationConstants.LegacyGameId:D}' has no operational Active or RecoveryRequired Cycle. Select an explicit Game/Cycle or restore the legacy runtime before using legacy routes.");
        }

        if (operationalCycleIds.Length > 1)
        {
            throw new InvalidOperationException(
                $"The fixed legacy Game '{GameFoundationConstants.LegacyGameId:D}' has {operationalCycleIds.Length} operational Cycles. Repair the Game's operational-Cycle invariant before using legacy routes.");
        }

        return new GameCycleScope(GameFoundationConstants.LegacyGameId, operationalCycleIds[0]);
    }

    private static GameCatalogueItem ReadGameCatalogueItem(SqlDataReader reader) =>
        new(
            GetGuid(reader, "GameID"),
            GetString(reader, "Name"),
            GetEnum<GamePurpose>(reader, "Purpose"),
            GetEnum<GameLifecycleStatus>(reader, "GameStatus"),
            GetEnum<GameVisibility>(reader, "Visibility"),
            GetGuid(reader, "GameEnrolmentID"),
            GetEnum<GameEnrolmentStatus>(reader, "EnrolmentStatus"),
            GetDateTimeOffset(reader, "StatusChangedAt"),
            GetNullableGuid(reader, "OperationalCycleID"),
            GetNullableEnum<CycleStatus>(reader, "OperationalCycleStatus"),
            GetNullableInt(reader, "CurrentTickNumber"),
            GetNullableEnum<TurnResolutionStage>(reader, "TurnStage"),
            GetDateTimeOffset(reader, "CreatedAt"));

    private static GameCommandContext ReadGameCommandContext(SqlDataReader reader)
    {
        var playerId = GetGuid(reader, "PlayerID");
        var gameId = GetGuid(reader, "GameID");
        var permissions = GamePermission.Read;
        if (GetNullableGuid(reader, "CreatedByPlayerID") == playerId)
        {
            permissions |= GamePermission.Organise;
        }

        if (GetEnum<PlayerRole>(reader, "PlayerRole") == PlayerRole.Admin)
        {
            permissions |= GamePermission.Administer;
        }

        return new GameCommandContext(
            new GameAccessContext(
                playerId,
                gameId,
                GetGuid(reader, "GameEnrolmentID"),
                permissions),
            GetGuid(reader, "CycleID"),
            GetGuid(reader, "MatchParticipantID"),
            GetGuid(reader, "EmpireID"));
    }

    private static Player ReadScopedPlayer(SqlDataReader reader) => new()
    {
        PlayerId = GetGuid(reader, "PlayerID"),
        Username = GetString(reader, "Username"),
        Email = "",
        PasswordHash = "",
        ExternalIssuer = "",
        ExternalSubject = "",
        Kind = GetEnum<PlayerKind>(reader, "PlayerKind"),
        Role = GetEnum<PlayerRole>(reader, "Role"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        LastLoginAt = GetNullableDateTimeOffset(reader, "LastLoginAt"),
        Status = GetEnum<PlayerStatus>(reader, "Status")
    };

    private static bool ReadableContextExists(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCommandContext context)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT TOP (1) 1
            FROM dbo.Players AS player
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.GameEnrolmentID = @GameEnrolmentID
               AND enrolment.PlayerID = player.PlayerID
               AND enrolment.GameID = @GameID
               AND enrolment.Status <> @WithdrawnEnrolmentStatus
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.GameID = game.GameID
               AND cycle.CycleID = @CycleID
            INNER JOIN dbo.MatchParticipants AS participant
                ON participant.MatchParticipantID = @MatchParticipantID
               AND participant.GameID = game.GameID
               AND participant.CycleID = cycle.CycleID
               AND participant.PlayerID = player.PlayerID
               AND participant.EmpireID = @EmpireID
               AND participant.Status IN
                   (@ActiveParticipantStatus, @DefeatedParticipantStatus, @CompletedParticipantStatus)
            INNER JOIN dbo.Empires AS empire
                ON empire.EmpireID = participant.EmpireID
               AND empire.CycleID = cycle.CycleID
               AND empire.PlayerID = player.PlayerID
            WHERE player.PlayerID = @PlayerID
              AND player.Status = @ActivePlayerStatus
              AND player.PlayerKind = @HumanPlayerKind
              AND (@RequireOrganisePermission = 0 OR game.CreatedByPlayerID = player.PlayerID)
              AND (@RequireAdministerPermission = 0 OR player.Role = @AdminPlayerRole);
            """);
        AddGuid(command, "@PlayerID", context.GameAccess.PlayerId);
        AddGuid(command, "@GameID", context.GameAccess.GameId);
        AddGuid(command, "@GameEnrolmentID", context.GameAccess.GameEnrolmentId!.Value);
        AddGuid(command, "@CycleID", context.CycleId);
        AddGuid(command, "@MatchParticipantID", context.MatchParticipantId);
        AddGuid(command, "@EmpireID", context.EmpireId);
        AddString(command, "@ActivePlayerStatus", PlayerStatus.Active.ToString(), 32);
        AddString(command, "@HumanPlayerKind", PlayerKind.Human.ToString(), 32);
        AddString(command, "@WithdrawnEnrolmentStatus", GameEnrolmentStatus.Withdrawn.ToString(), 32);
        AddString(command, "@ActiveParticipantStatus", MatchParticipantStatus.Active.ToString(), 32);
        AddString(command, "@DefeatedParticipantStatus", MatchParticipantStatus.Defeated.ToString(), 32);
        AddString(command, "@CompletedParticipantStatus", MatchParticipantStatus.Completed.ToString(), 32);
        AddInt(
            command,
            "@RequireOrganisePermission",
            context.GameAccess.Permissions.HasFlag(GamePermission.Organise) ? 1 : 0);
        AddInt(
            command,
            "@RequireAdministerPermission",
            context.GameAccess.Permissions.HasFlag(GamePermission.Administer) ? 1 : 0);
        AddString(command, "@AdminPlayerRole", PlayerRole.Admin.ToString(), 32);
        return command.ExecuteScalar() is not null;
    }

    private static GameState LoadFocusedViewStateUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCommandContext context)
    {
        var state = LoadFocusedTickStateUnsafe(connection, transaction, context.CycleId);
        state.Games = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.Games WHERE GameID = @GameID",
            command => AddGuid(command, "@GameID", context.GameAccess.GameId),
            ReadGame);
        state.CycleConfigurations = ReadRows(
            connection,
            transaction,
            """
            SELECT configuration.*
            FROM dbo.CycleConfigurations AS configuration
            INNER JOIN dbo.Cycles AS cycle
                ON cycle.CycleConfigurationID = configuration.CycleConfigurationID
               AND cycle.GameID = configuration.GameID
            WHERE cycle.CycleID = @CycleID
              AND configuration.GameID = @GameID
            """,
            command =>
            {
                AddGuid(command, "@CycleID", context.CycleId);
                AddGuid(command, "@GameID", context.GameAccess.GameId);
            },
            ReadCycleConfiguration);
        state.GameEnrolments = ReadRows(
            connection,
            transaction,
            """
            SELECT *
            FROM dbo.GameEnrolments
            WHERE GameEnrolmentID = @GameEnrolmentID
              AND GameID = @GameID
              AND PlayerID = @PlayerID
            """,
            command =>
            {
                AddGuid(command, "@GameEnrolmentID", context.GameAccess.GameEnrolmentId!.Value);
                AddGuid(command, "@GameID", context.GameAccess.GameId);
                AddGuid(command, "@PlayerID", context.GameAccess.PlayerId);
            },
            ReadGameEnrolment);
        state.EmpireMetrics = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.EmpireMetrics WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadEmpireMetric);
        state.CycleRankings = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.CycleRankings WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadCycleRanking);
        state.CycleMajorEvents = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.CycleMajorEvents WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadCycleMajorEvent);
        state.SystemHistoricalSignals = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.SystemHistoricalSignals WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadSystemHistoricalSignal);
        state.AdmiralBattleHistories = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.AdmiralBattleHistories WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadAdmiralBattleHistory);
        state.FleetOrders = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.FleetOrders WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadFleetOrder);
        state.ShipConstructions = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.ShipConstructions WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadShipConstruction);
        state.TickLogs = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.TickLogs WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadTickLog);
        state.Events = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.Events WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadEvent);
        state.BattleRecords = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.BattleRecords WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadBattleRecord);
        state.BattleFleetParticipants = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.BattleFleetParticipants WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadBattleFleetParticipant);
        state.ChronicleEntries = ReadRows(
            connection,
            transaction,
            "SELECT * FROM dbo.ChronicleEntries WHERE CycleID = @CycleID",
            command => AddGuid(command, "@CycleID", context.CycleId),
            ReadChronicleEntry);

        BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
        return state;
    }

    private static bool ActiveCommandContextExists(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCommandContext context)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT TOP (1) 1
            FROM dbo.Players AS player
            INNER JOIN dbo.GameEnrolments AS enrolment
                ON enrolment.GameEnrolmentID = @GameEnrolmentID
               AND enrolment.PlayerID = player.PlayerID
               AND enrolment.GameID = @GameID
               AND enrolment.Status = @EnrolledStatus
               AND enrolment.EndedAt IS NULL
            INNER JOIN dbo.Games AS game
                ON game.GameID = enrolment.GameID
               AND game.Status = @ActiveGameStatus
            INNER JOIN dbo.Cycles AS cycle WITH (UPDLOCK, HOLDLOCK)
                ON cycle.GameID = game.GameID
               AND cycle.CycleID = @CycleID
               AND cycle.Status = @ActiveCycleStatus
            INNER JOIN dbo.MatchParticipants AS participant
                ON participant.MatchParticipantID = @MatchParticipantID
               AND participant.GameID = game.GameID
               AND participant.CycleID = cycle.CycleID
               AND participant.PlayerID = player.PlayerID
               AND participant.EmpireID = @EmpireID
               AND participant.Status = @ActiveParticipantStatus
               AND participant.EndedAt IS NULL
            INNER JOIN dbo.Empires AS empire
                ON empire.EmpireID = participant.EmpireID
               AND empire.CycleID = cycle.CycleID
               AND empire.PlayerID = player.PlayerID
               AND empire.Status = @ActiveEmpireStatus
            WHERE player.PlayerID = @PlayerID
              AND player.Status = @ActivePlayerStatus
              AND player.PlayerKind = @HumanPlayerKind
              AND (@RequireOrganisePermission = 0 OR game.CreatedByPlayerID = player.PlayerID)
              AND (@RequireAdministerPermission = 0 OR player.Role = @AdminPlayerRole);
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
        AddInt(
            command,
            "@RequireOrganisePermission",
            context.GameAccess.Permissions.HasFlag(GamePermission.Organise) ? 1 : 0);
        AddInt(
            command,
            "@RequireAdministerPermission",
            context.GameAccess.Permissions.HasFlag(GamePermission.Administer) ? 1 : 0);
        AddString(command, "@AdminPlayerRole", PlayerRole.Admin.ToString(), 32);
        return command.ExecuteScalar() is not null;
    }

    private static ScopedCommandChanges ValidateScopedCommandChanges(
        GameCycleScope scope,
        GameState before,
        GameState after)
    {
        EnsureOnlyCommandAggregatesChanged(before, after);

        var fleets = GetChangedRows(
            before.Fleets,
            after.Fleets,
            item => item.FleetId,
            "Fleet",
            allowDeletes: false);
        ValidateFleetCompatibilityChanges(scope, before, after, fleets);

        var fleetOrders = GetChangedRows(
            before.FleetOrders,
            after.FleetOrders,
            item => item.FleetOrderId,
            "Fleet order",
            allowDeletes: false);
        ValidateFleetOrders(scope, after, fleetOrders.AllRows);

        var empirePriorities = GetChangedRows(
            before.EmpirePriorities,
            after.EmpirePriorities,
            item => item.EmpirePriorityId,
            "Empire priority",
            allowDeletes: false);
        ValidateEmpirePriorities(scope, after, empirePriorities.AllRows);

        var events = GetChangedRows(
            before.Events,
            after.Events,
            item => item.EventId,
            "Event",
            allowDeletes: false);
        ValidateEvents(scope, after, events.AllRows);
        if (events.Changed.Count != events.Added.Count)
        {
            throw new InvalidOperationException(
                "Events are append-only on the scoped command path.");
        }

        return new ScopedCommandChanges(
            fleets.Changed,
            fleetOrders.Changed,
            fleetOrders.Added.Select(item => item.FleetOrderId).ToArray(),
            empirePriorities.Changed,
            empirePriorities.Added.Select(item => item.EmpirePriorityId).ToArray(),
            events.Changed,
            events.Added.Select(item => item.EventId).ToArray());
    }

    private static void EnsureOnlyCommandAggregatesChanged(GameState before, GameState after)
    {
        GameState beforeInvariant;
        GameState afterInvariant;
        try
        {
            beforeInvariant = CreateCommandInvariantProjection(before);
            afterInvariant = CreateCommandInvariantProjection(after);
        }
        catch (Exception exception) when (exception is NullReferenceException or ArgumentNullException)
        {
            throw new InvalidOperationException(
                "A scoped command cannot remove a GameState collection.",
                exception);
        }

        if (!RowsEqual(beforeInvariant, afterInvariant))
        {
            throw new InvalidOperationException(
                "A scoped command attempted to mutate state outside FleetOrders, EmpirePriorities, Events, or Fleet faction compatibility fields.");
        }
    }

    private static GameState CreateCommandInvariantProjection(GameState state)
    {
        var projection = state.DeepClone();
        projection.FleetOrders.Clear();
        projection.EmpirePriorities.Clear();
        projection.Events.Clear();
        foreach (var fleet in projection.Fleets)
        {
            fleet.FactionId = Guid.Empty;
        }

        return projection;
    }

    private static void ValidateFleetCompatibilityChanges(
        GameCycleScope scope,
        GameState before,
        GameState after,
        ChangedRows<Fleet> fleets)
    {
        if (fleets.AllRows.Count != before.Fleets.Count)
        {
            throw new InvalidOperationException("A scoped command cannot add or delete Fleets.");
        }

        foreach (var fleet in fleets.Changed)
        {
            if (fleet.CycleId != scope.CycleId)
            {
                throw new InvalidOperationException("A scoped command cannot mutate a Fleet outside its Cycle.");
            }

            var expectedFaction = after.Factions.SingleOrDefault(item =>
                item.CycleId == scope.CycleId
                && item.EmpireId == fleet.EmpireId
                && item.Kind == FactionKind.Empire);
            if (expectedFaction is null || fleet.FactionId != expectedFaction.FactionId)
            {
                throw new InvalidOperationException(
                    "A scoped command may only normalise a Fleet to its owning Empire's faction.");
            }
        }
    }

    private static void ValidateFleetOrders(
        GameCycleScope scope,
        GameState state,
        IReadOnlyList<FleetOrder> orders)
    {
        var fleetIds = state.Fleets.Select(item => item.FleetId).ToHashSet();
        var systemIds = state.Systems.Select(item => item.SystemId).ToHashSet();
        var empireIds = state.Empires.Select(item => item.EmpireId).ToHashSet();
        var factionIds = state.Factions.Select(item => item.FactionId).ToHashSet();
        var orderIds = orders.Select(item => item.FleetOrderId).ToHashSet();

        foreach (var order in orders)
        {
            if (order.CycleId != scope.CycleId
                || !fleetIds.Contains(order.FleetId)
                || order.TargetSystemId.HasValue && !systemIds.Contains(order.TargetSystemId.Value)
                || order.TargetEmpireId.HasValue && !empireIds.Contains(order.TargetEmpireId.Value)
                || order.TargetFactionId.HasValue && !factionIds.Contains(order.TargetFactionId.Value)
                || order.SupersededByOrderId.HasValue && !orderIds.Contains(order.SupersededByOrderId.Value))
            {
                throw new InvalidOperationException(
                    "A scoped command produced a Fleet order with an identifier outside its Cycle.");
            }
        }
    }

    private static void ValidateEmpirePriorities(
        GameCycleScope scope,
        GameState state,
        IReadOnlyList<EmpirePriority> priorities)
    {
        var empireIds = state.Empires
            .Where(item => item.CycleId == scope.CycleId)
            .Select(item => item.EmpireId)
            .ToHashSet();
        if (priorities.Any(item => !empireIds.Contains(item.EmpireId))
            || priorities.GroupBy(item => item.EmpireId).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException(
                "A scoped command produced Empire priorities outside its Cycle or duplicated an Empire's priorities.");
        }
    }

    private static void ValidateEvents(
        GameCycleScope scope,
        GameState state,
        IReadOnlyList<EventRecord> events)
    {
        var systemIds = state.Systems.Select(item => item.SystemId).ToHashSet();
        var empireIds = state.Empires.Select(item => item.EmpireId).ToHashSet();
        var factionIds = state.Factions.Select(item => item.FactionId).ToHashSet();
        foreach (var eventRecord in events)
        {
            if (eventRecord.CycleId != scope.CycleId
                || eventRecord.SystemId.HasValue && !systemIds.Contains(eventRecord.SystemId.Value)
                || eventRecord.EmpireId.HasValue && !empireIds.Contains(eventRecord.EmpireId.Value)
                || eventRecord.FactionId.HasValue && !factionIds.Contains(eventRecord.FactionId.Value))
            {
                throw new InvalidOperationException(
                    "A scoped command produced an Event with an identifier outside its Cycle.");
            }
        }
    }

    private static ChangedRows<T> GetChangedRows<T>(
        IReadOnlyList<T> before,
        IReadOnlyList<T> after,
        Func<T, Guid> getId,
        string entityName,
        bool allowDeletes)
    {
        var beforeById = IndexRows(before, getId, entityName);
        var afterById = IndexRows(after, getId, entityName);
        if (!allowDeletes && beforeById.Keys.Any(id => !afterById.ContainsKey(id)))
        {
            throw new InvalidOperationException($"A scoped command cannot delete a {entityName}.");
        }

        var changedRows = after
            .Where(item =>
            {
                var id = getId(item);
                return !beforeById.TryGetValue(id, out var original) || !RowsEqual(original, item);
            })
            .ToArray();
        var addedRows = after
            .Where(item => !beforeById.ContainsKey(getId(item)))
            .ToArray();
        return new ChangedRows<T>(after.ToArray(), changedRows, addedRows);
    }

    private static void EnsureAddedIdentifiersDoNotExist(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<Guid> identifiers,
        ScopedEntityTable table)
    {
        var (tableName, idColumn) = table switch
        {
            ScopedEntityTable.FleetOrder => ("FleetOrders", "FleetOrderID"),
            ScopedEntityTable.EmpirePriority => ("EmpirePriorities", "EmpirePriorityID"),
            ScopedEntityTable.Event => ("Events", "EventID"),
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, null)
        };

        foreach (var identifier in identifiers.OrderBy(item => item))
        {
            using var command = CreateCommand(
                connection,
                transaction,
                $"SELECT TOP (1) 1 FROM dbo.{tableName} WITH (UPDLOCK, HOLDLOCK) WHERE {idColumn} = @ID;");
            AddGuid(command, "@ID", identifier);
            if (command.ExecuteScalar() is not null)
            {
                throw new InvalidOperationException(
                    $"A scoped command attempted to add a {tableName} row whose identifier already exists.");
            }
        }
    }

    private static void InsertScopedEvent(
        SqlConnection connection,
        SqlTransaction transaction,
        EventRecord item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Events
                (EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES
                (@EventID, @CycleID, @TickNumber, @EventType, @SystemID, @EmpireID, @FactionID, @Severity, @FactJson, @DisplayText, @CreatedAt);
            """, command =>
        {
            AddGuid(command, "@EventID", item.EventId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddString(command, "@EventType", item.EventType.ToString(), 64);
            AddNullableGuid(command, "@SystemID", item.SystemId);
            AddNullableGuid(command, "@EmpireID", item.EmpireId);
            AddNullableGuid(command, "@FactionID", item.FactionId);
            AddString(command, "@Severity", item.Severity.ToString(), 32);
            AddMaxString(command, "@FactJson", item.FactJson);
            AddString(command, "@DisplayText", item.DisplayText, 1024);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static Dictionary<Guid, T> IndexRows<T>(
        IEnumerable<T> rows,
        Func<T, Guid> getId,
        string entityName)
    {
        var indexed = new Dictionary<Guid, T>();
        foreach (var row in rows)
        {
            var id = getId(row);
            if (id == Guid.Empty || !indexed.TryAdd(id, row))
            {
                throw new InvalidOperationException(
                    $"A scoped command produced an empty or duplicate {entityName} identifier.");
            }
        }

        return indexed;
    }

    private static bool RowsEqual<T>(T first, T second) =>
        JsonSerializer.Serialize(first, GameStateJson.Options) ==
        JsonSerializer.Serialize(second, GameStateJson.Options);

    private sealed record ChangedRows<T>(
        IReadOnlyList<T> AllRows,
        IReadOnlyList<T> Changed,
        IReadOnlyList<T> Added);

    private sealed record ScopedCommandChanges(
        IReadOnlyList<Fleet> Fleets,
        IReadOnlyList<FleetOrder> FleetOrders,
        IReadOnlyList<Guid> AddedFleetOrderIds,
        IReadOnlyList<EmpirePriority> EmpirePriorities,
        IReadOnlyList<Guid> AddedEmpirePriorityIds,
        IReadOnlyList<EventRecord> Events,
        IReadOnlyList<Guid> AddedEventIds);

    private enum ScopedEntityTable
    {
        FleetOrder,
        EmpirePriority,
        Event
    }
}
