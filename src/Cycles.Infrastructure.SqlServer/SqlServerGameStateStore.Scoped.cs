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

    public ScopedCommandResult<T> Execute<T>(
        GameCycleScope scope,
        Func<GameState, T> command)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(command);

        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        try
        {
            AcquireCycleTickLock(connection, transaction, scope.CycleId);
        }
        catch (TimeoutException)
        {
            return new ScopedCommandResult<T>.Busy();
        }

        if (!GameCycleScopeExists(connection, transaction, scope))
        {
            transaction.Commit();
            return new ScopedCommandResult<T>.Unavailable();
        }

        var state = LoadFocusedTickStateUnsafe(connection, transaction, scope.CycleId);
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

    private static bool GameCycleScopeExists(
        SqlConnection connection,
        SqlTransaction transaction,
        GameCycleScope scope)
    {
        using var command = CreateCommand(
            connection,
            transaction,
            """
            SELECT TOP (1) 1
            FROM dbo.Cycles WITH (UPDLOCK, HOLDLOCK)
            WHERE CycleID = @CycleID
              AND GameID = @GameID;
            """);
        AddGuid(command, "@CycleID", scope.CycleId);
        AddGuid(command, "@GameID", scope.GameId);
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
