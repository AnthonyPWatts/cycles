using Cycles.Application;
using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed partial class SqlServerGameStateStore :
    IGameStateStore,
    IPlayerAccountQuery,
    IGameCatalogueQuery,
    IGameAccessQuery,
    IGameCommandAccessQuery,
    ICycleViewQuery,
    ICycleCommandStore,
    IDueCycleQuery,
    IWorkerScheduleStatusQuery,
    ICycleResolutionStore,
    ILegacyRuntimeScopeQuery,
    IPlayerAccountCommandStore,
    ITrustedPlayerSelectionQuery,
    IAdminRoleCommandStore,
    ITrainingGameProvisioningStore,
    ITutorialAttemptStore
{
    private const string ApplicationLockName = "Cycles.GameState";
    private const string GameResolutionLockPrefix = "Cycles.Game.";
    private const string TickLockPrefix = "Cycles.Tick.";
    private const int ApplicationLockTimeoutMilliseconds = 5000;
    private const int ConnectionAttemptCount = 3;
    private static readonly TimeSpan ConnectionRetryDelay = TimeSpan.FromSeconds(5);

    internal static IReadOnlyList<int> TransientConnectionErrorNumbers { get; } =
        [-2, 40613];

    private readonly string _connectionString;
    private readonly Func<GameState> _seedFactory;

    public SqlServerGameStateStore(string connectionString, Func<GameState>? seedFactory = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("SQL Server connection string is required.", nameof(connectionString));
        }

        _connectionString = connectionString;
        _seedFactory = seedFactory ?? (() => GameSeeder.CreateDefault());
        Description = BuildDescription(connectionString);
    }

    public string Description { get; }

    public GameState LoadOrCreate()
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        var state = LoadUnsafe(connection, transaction);
        var createdState = state.Cycles.Count == 0;
        if (state.Cycles.Count == 0)
        {
            state = _seedFactory();
        }

        var prioritiesChanged = StrategicPriorityPolicy.Normalize(state);
        if (createdState || prioritiesChanged)
        {
            SaveUnsafe(connection, transaction, state);
        }

        transaction.Commit();
        return state;
    }

    public T Update<T>(Func<GameState, T> update)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        var state = LoadUnsafe(connection, transaction);
        if (state.Cycles.Count == 0)
        {
            state = _seedFactory();
        }

        StrategicPriorityPolicy.Normalize(state);
        var result = update(state);
        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
        return result;
    }

    public T UpdateActiveCycleExclusively<T>(Func<GameState, T> update)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        var activeCycleId = ReadActiveCycleIdUnsafe(connection, transaction)
            ?? throw new InvalidOperationException("No active cycle exists.");
        AcquireCycleTickLock(connection, transaction, activeCycleId);

        var state = LoadUnsafe(connection, transaction);
        StrategicPriorityPolicy.Normalize(state);
        var result = update(state);
        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
        return result;
    }

    public TickResult RunTick(DateTimeOffset now) =>
        RunTick(cycleId: null, now, requireDue: false)
        ?? throw new InvalidOperationException("The requested tick was not executed.");

    public TickResult RunTick(Guid cycleId, DateTimeOffset now) =>
        RunTick(cycleId, now, requireDue: false)
        ?? throw new InvalidOperationException("The requested tick was not executed.");

    public TickResult? RunTickIfDue(DateTimeOffset now) =>
        RunTick(cycleId: null, now, requireDue: true);

    public void Replace(GameState state)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        StrategicPriorityPolicy.Normalize(state);
        DeleteRowsMissingFromState(
            connection,
            transaction,
            new GameState(),
            deleteGameLifecycleEvents: true);
        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
    }

    private TickResult? RunTick(Guid? cycleId, DateTimeOffset now, bool requireDue)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);

        var activeCycleId = cycleId ?? ReadActiveCycleIdUnsafe(connection, transaction);
        GameState state;
        var createdState = false;

        if (activeCycleId.HasValue)
        {
            AcquireCycleTickLock(connection, transaction, activeCycleId.Value);
            state = LoadFocusedTickStateUnsafe(connection, transaction, activeCycleId.Value);
        }
        else
        {
            AcquireApplicationLock(connection, transaction);
            activeCycleId = ReadActiveCycleIdUnsafe(connection, transaction);
            if (activeCycleId.HasValue)
            {
                AcquireCycleTickLock(connection, transaction, activeCycleId.Value);
                state = LoadFocusedTickStateUnsafe(connection, transaction, activeCycleId.Value);
            }
            else if (AnyCycleExistsUnsafe(connection, transaction))
            {
                throw new InvalidOperationException("No active cycle exists.");
            }
            else
            {
                state = _seedFactory();
                createdState = true;
                activeCycleId = state.GetActiveCycle()?.CycleId
                    ?? throw new InvalidOperationException("No active cycle exists.");
                AcquireCycleTickLock(connection, transaction, activeCycleId.Value);
            }
        }

        StrategicPriorityPolicy.Normalize(state);
        if (requireDue)
        {
            var cycle = state.GetActiveCycle();
            var lastCompletedAt = createdState || cycle is null
                ? null
                : ReadLastCompletedTickAtUnsafe(connection, transaction, cycle.CycleId);
            if (cycle is null || !TickSchedule.IsDue(cycle, lastCompletedAt, now))
            {
                if (createdState)
                {
                    SaveUnsafe(connection, transaction, state);
                }

                transaction.Commit();
                return null;
            }
        }

        var result = new TickEngine().RunTick(state, activeCycleId.Value, now);
        if (createdState)
        {
            SaveUnsafe(connection, transaction, state);
        }
        else
        {
            SaveTickOutcomeUnsafe(connection, transaction, state, activeCycleId.Value);
        }

        transaction.Commit();
        return result;
    }

    private SqlConnection OpenConnection()
    {
        var connection = CreateConnection(_connectionString);
        try
        {
            connection.Open();
            return connection;
        }
        catch
        {
            connection.Dispose();
            throw;
        }
    }

    internal static SqlConnection CreateConnection(string connectionString) =>
        new(connectionString)
        {
            RetryLogicProvider = SqlConfigurableRetryFactory.CreateFixedRetryProvider(
                new SqlRetryLogicOption
                {
                    NumberOfTries = ConnectionAttemptCount,
                    DeltaTime = ConnectionRetryDelay,
                    MaxTimeInterval = ConnectionRetryDelay.Add(TimeSpan.FromSeconds(1)),
                    TransientErrors = TransientConnectionErrorNumbers
                })
        };

    private static void AcquireApplicationLock(SqlConnection connection, SqlTransaction transaction) =>
        AcquireSqlApplicationLock(connection, transaction, ApplicationLockName);

    private static void AcquireCycleTickLock(SqlConnection connection, SqlTransaction transaction, Guid cycleId) =>
        AcquireSqlApplicationLock(connection, transaction, $"{TickLockPrefix}{cycleId:D}");

    private static void AcquireGameResolutionLock(SqlConnection connection, SqlTransaction transaction, Guid gameId) =>
        AcquireSqlApplicationLock(connection, transaction, $"{GameResolutionLockPrefix}{gameId:D}");

    private static void AcquireSqlApplicationLock(SqlConnection connection, SqlTransaction transaction, string resourceName)
    {
        using var command = CreateCommand(connection, transaction, """
            DECLARE @Result int;
            EXEC @Result = sys.sp_getapplock
                @Resource = @Resource,
                @LockMode = N'Exclusive',
                @LockOwner = N'Transaction',
                @LockTimeout = @LockTimeout;
            SELECT @Result;
            """);
        AddString(command, "@Resource", resourceName, 255);
        AddInt(command, "@LockTimeout", ApplicationLockTimeoutMilliseconds);

        var result = Convert.ToInt32(command.ExecuteScalar(), null);
        if (result < 0)
        {
            throw new TimeoutException($"Could not acquire SQL Server application lock '{resourceName}'. Result code: {result}.");
        }
    }

    private static GameState LoadUnsafe(SqlConnection connection, SqlTransaction transaction)
    {
        var state = new GameState
        {
            Players = ReadRows(connection, transaction, "SELECT * FROM dbo.Players", ReadPlayer),
            AdminRoleAuditRecords = ReadRows(connection, transaction, "SELECT * FROM dbo.AdminRoleAuditRecords", ReadAdminRoleAuditRecord),
            Games = ReadRows(connection, transaction, "SELECT * FROM dbo.Games", ReadGame),
            CycleConfigurations = ReadRows(connection, transaction, "SELECT * FROM dbo.CycleConfigurations", ReadCycleConfiguration),
            Cycles = ReadRows(connection, transaction, "SELECT * FROM dbo.Cycles", ReadCycle),
            GameEnrolments = ReadRows(connection, transaction, "SELECT * FROM dbo.GameEnrolments", ReadGameEnrolment),
            GameLifecycleEvents = ReadRows(connection, transaction, "SELECT * FROM dbo.GameLifecycleEvents", ReadGameLifecycleEvent),
            Sectors = ReadRows(connection, transaction, "SELECT * FROM dbo.GalaxySectors", ReadSector),
            Systems = ReadRows(connection, transaction, "SELECT * FROM dbo.Systems", ReadSystem),
            Empires = ReadRows(connection, transaction, "SELECT * FROM dbo.Empires", ReadEmpire),
            Factions = ReadRows(connection, transaction, "SELECT * FROM dbo.Factions", ReadFaction),
            MatchParticipants = ReadRows(connection, transaction, "SELECT * FROM dbo.MatchParticipants", ReadMatchParticipant),
            EmpireResources = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireResources", ReadEmpireResource),
            EmpireDoctrineUnlocks = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireDoctrineUnlocks", ReadEmpireDoctrineUnlock),
            EmpirePriorities = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpirePriorities", ReadEmpirePriority),
            EmpireMetrics = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireMetrics", ReadEmpireMetric),
            CycleRankings = ReadRows(connection, transaction, "SELECT * FROM dbo.CycleRankings", ReadCycleRanking),
            CycleMajorEvents = ReadRows(connection, transaction, "SELECT * FROM dbo.CycleMajorEvents", ReadCycleMajorEvent),
            SystemHistoricalSignals = ReadRows(connection, transaction, "SELECT * FROM dbo.SystemHistoricalSignals", ReadSystemHistoricalSignal),
            ColonialOutposts = ReadRows(connection, transaction, "SELECT * FROM dbo.ColonialOutposts", ReadColonialOutpost),
            DiplomaticRelationships = ReadRows(connection, transaction, "SELECT * FROM dbo.DiplomaticRelationships", ReadDiplomaticRelationship),
            Admirals = ReadRows(connection, transaction, "SELECT * FROM dbo.Admirals", ReadAdmiral),
            AdmiralBattleHistories = ReadRows(connection, transaction, "SELECT * FROM dbo.AdmiralBattleHistories", ReadAdmiralBattleHistory),
            SystemLinks = ReadRows(connection, transaction, "SELECT * FROM dbo.SystemLinks", ReadSystemLink),
            Fleets = ReadRows(connection, transaction, "SELECT * FROM dbo.Fleets", ReadFleet),
            FleetOrders = ReadRows(connection, transaction, "SELECT * FROM dbo.FleetOrders", ReadFleetOrder),
            ShipConstructions = ReadRows(connection, transaction, "SELECT * FROM dbo.ShipConstructions", ReadShipConstruction),
            TickLogs = ReadRows(connection, transaction, "SELECT * FROM dbo.TickLogs", ReadTickLog),
            Events = ReadRows(connection, transaction, "SELECT * FROM dbo.Events", ReadEvent),
            BattleRecords = ReadRows(connection, transaction, "SELECT * FROM dbo.BattleRecords", ReadBattleRecord),
            BattleFleetParticipants = ReadRows(connection, transaction, "SELECT * FROM dbo.BattleFleetParticipants", ReadBattleFleetParticipant),
            ChronicleEntries = ReadRows(connection, transaction, "SELECT * FROM dbo.ChronicleEntries", ReadChronicleEntry)
        };

        BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
        return state;
    }

    private static GameState LoadFocusedTickStateUnsafe(SqlConnection connection, SqlTransaction transaction, Guid cycleId) =>
        new()
        {
            Players = ReadRows(
                connection,
                transaction,
                """
                SELECT DISTINCT
                    players.PlayerID,
                    players.Username,
                    players.PlayerKind,
                    players.Role,
                    players.CreatedAt,
                    players.LastLoginAt,
                    players.Status
                FROM dbo.Players players
                INNER JOIN dbo.Empires empires ON empires.PlayerID = players.PlayerID
                WHERE empires.CycleID = @CycleID
                """,
                command => AddGuid(command, "@CycleID", cycleId),
                ReadScopedPlayer),
            Cycles = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Cycles WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadCycle),
            Sectors = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.GalaxySectors WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadSector),
            Systems = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Systems WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadSystem),
            Empires = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Empires WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadEmpire),
            Factions = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Factions WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadFaction),
            MatchParticipants = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.MatchParticipants WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadMatchParticipant),
            EmpireResources = ReadRows(
                connection,
                transaction,
                """
                SELECT resources.*
                FROM dbo.EmpireResources resources
                INNER JOIN dbo.Empires empires ON empires.EmpireID = resources.EmpireID
                WHERE empires.CycleID = @CycleID
                """,
                command => AddGuid(command, "@CycleID", cycleId),
                ReadEmpireResource),
            EmpireDoctrineUnlocks = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.EmpireDoctrineUnlocks WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadEmpireDoctrineUnlock),
            EmpirePriorities = ReadRows(
                connection,
                transaction,
                """
                SELECT priorities.*
                FROM dbo.EmpirePriorities priorities
                INNER JOIN dbo.Empires empires ON empires.EmpireID = priorities.EmpireID
                WHERE empires.CycleID = @CycleID
                """,
                command => AddGuid(command, "@CycleID", cycleId),
                ReadEmpirePriority),
            ColonialOutposts = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.ColonialOutposts WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadColonialOutpost),
            DiplomaticRelationships = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.DiplomaticRelationships WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadDiplomaticRelationship),
            SystemLinks = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.SystemLinks WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadSystemLink),
            Admirals = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Admirals WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadAdmiral),
            Fleets = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Fleets WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadFleet),
            FleetOrders = ReadRows(
                connection,
                transaction,
                """
                SELECT orders.*
                FROM dbo.FleetOrders orders
                INNER JOIN dbo.Cycles cycles ON cycles.CycleID = orders.CycleID
                WHERE orders.CycleID = @CycleID
                    AND orders.Status = @Status
                    AND orders.ExecuteAfterTick <= cycles.CurrentTickNumber + 1
                """,
                command =>
                {
                    AddGuid(command, "@CycleID", cycleId);
                    AddString(command, "@Status", FleetOrderStatus.Pending.ToString(), 32);
                },
                ReadFleetOrder),
            ShipConstructions = ReadRows(
                connection,
                transaction,
                """
                SELECT constructions.*
                FROM dbo.ShipConstructions constructions
                INNER JOIN dbo.Cycles cycles ON cycles.CycleID = constructions.CycleID
                WHERE constructions.CycleID = @CycleID
                    AND constructions.Status = @Status
                    AND constructions.CompleteAfterTick <= cycles.CurrentTickNumber + 1
                """,
                command =>
                {
                    AddGuid(command, "@CycleID", cycleId);
                    AddString(command, "@Status", ShipConstructionStatus.Queued.ToString(), 32);
                },
                ReadShipConstruction),
            TickLogs = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.TickLogs WHERE CycleID = @CycleID AND Status = @Status",
                command =>
                {
                    AddGuid(command, "@CycleID", cycleId);
                    AddString(command, "@Status", TickLogStatus.Running.ToString(), 32);
                },
                ReadTickLog)
        };

    private static void EnsureGameFoundations(GameState state)
    {
        if (state.Cycles.Count == 0)
        {
            return;
        }

        var hasNoGame = state.Games.Count == 0;
        var hasOnlyLegacyGame = state.Games.Count == 1
            && state.Games[0].GameId == GameFoundationConstants.LegacyGameId;
        var hasOnlyLegacyCycleScope = state.Cycles.All(cycle =>
            cycle.GameId is null || cycle.GameId == GameFoundationConstants.LegacyGameId);
        var hasIncompleteLegacyFoundation = hasOnlyLegacyGame
            && (state.Cycles.Any(cycle =>
                    cycle.GameId is null
                    || cycle.CycleConfigurationId is null
                    || !state.CycleConfigurations.Any(configuration =>
                        configuration.CycleConfigurationId == cycle.CycleConfigurationId.Value))
                || state.MatchParticipants.Any(participant =>
                    participant.GameId == Guid.Empty
                    || !state.GameEnrolments.Any(enrolment =>
                        enrolment.GameId == GameFoundationConstants.LegacyGameId
                        && enrolment.PlayerId == participant.PlayerId))
                || state.GameLifecycleEvents.All(gameEvent =>
                    gameEvent.GameLifecycleEventId != GameFoundationConstants.LegacyLifecycleEventId));

        if (hasOnlyLegacyCycleScope && (hasNoGame || hasIncompleteLegacyFoundation))
        {
            LegacyGameFoundation.Apply(state);
            return;
        }

        if (hasOnlyLegacyGame && hasOnlyLegacyCycleScope)
        {
            return;
        }

        var incompleteCycle = state.Cycles.FirstOrDefault(cycle =>
            cycle.GameId is null || cycle.CycleConfigurationId is null);
        if (incompleteCycle is not null)
        {
            throw new InvalidOperationException(
                $"Cycle {incompleteCycle.CycleId} has no complete Game foundation and cannot be persisted outside the legacy adapter.");
        }
    }

    private static void SaveUnsafe(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        EnsureGameFoundations(state);
        CycleScheduling.NormalizePersistedSchedule(state);
        BattleFleetParticipantCompatibility.UpgradeLegacyMembership(state);
        BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
        DeleteRowsMissingFromState(connection, transaction, state);

        foreach (var item in state.Players)
        {
            UpsertPlayer(connection, transaction, item);
        }

        foreach (var item in state.AdminRoleAuditRecords)
        {
            UpsertAdminRoleAuditRecord(connection, transaction, item);
        }

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

        foreach (var item in state.EmpireDoctrineUnlocks)
        {
            UpsertEmpireDoctrineUnlock(connection, transaction, item);
        }

        foreach (var item in state.EmpirePriorities)
        {
            UpsertEmpirePriority(connection, transaction, item);
        }

        foreach (var item in state.EmpireMetrics)
        {
            UpsertEmpireMetric(connection, transaction, item);
        }

        foreach (var item in state.CycleRankings)
        {
            UpsertCycleRanking(connection, transaction, item);
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

        UpsertFleetOrders(connection, transaction, state.FleetOrders);

        foreach (var item in state.ShipConstructions)
        {
            UpsertShipConstruction(connection, transaction, item);
        }

        foreach (var item in state.TickLogs)
        {
            UpsertTickLog(connection, transaction, item);
        }

        foreach (var item in state.Events)
        {
            UpsertEvent(connection, transaction, item);
        }

        foreach (var item in state.BattleRecords)
        {
            UpsertBattleRecord(connection, transaction, item);
        }

        foreach (var item in state.BattleFleetParticipants)
        {
            UpsertBattleFleetParticipant(connection, transaction, item);
        }

        foreach (var item in state.AdmiralBattleHistories)
        {
            UpsertAdmiralBattleHistory(connection, transaction, item);
        }

        foreach (var item in state.SystemHistoricalSignals)
        {
            UpsertSystemHistoricalSignal(connection, transaction, item);
        }

        foreach (var item in state.ColonialOutposts)
        {
            UpsertColonialOutpost(connection, transaction, item);
        }

        foreach (var item in state.DiplomaticRelationships)
        {
            UpsertDiplomaticRelationship(connection, transaction, item);
        }

        foreach (var item in state.CycleMajorEvents)
        {
            UpsertCycleMajorEvent(connection, transaction, item);
        }

        foreach (var item in state.ChronicleEntries)
        {
            UpsertChronicleEntry(connection, transaction, item);
        }

    }

    private static void SaveTickOutcomeUnsafe(SqlConnection connection, SqlTransaction transaction, GameState state, Guid cycleId)
    {
        BattleFleetParticipantCompatibility.UpgradeLegacyMembership(state);
        BattleFleetParticipantCompatibility.SynchronizeLegacyFleetIds(state);
        var empireIds = state.Empires
            .Where(empire => empire.CycleId == cycleId)
            .Select(empire => empire.EmpireId)
            .ToHashSet();

        foreach (var item in state.Cycles.Where(cycle => cycle.CycleId == cycleId))
        {
            UpsertCycle(connection, transaction, item);
        }

        foreach (var item in state.EmpireResources.Where(resource => empireIds.Contains(resource.EmpireId)))
        {
            UpsertEmpireResource(connection, transaction, item);
        }

        foreach (var item in state.EmpireDoctrineUnlocks.Where(unlock => unlock.CycleId == cycleId))
        {
            UpsertEmpireDoctrineUnlock(connection, transaction, item);
        }

        foreach (var item in state.EmpirePriorities.Where(priority => empireIds.Contains(priority.EmpireId)))
        {
            UpsertEmpirePriority(connection, transaction, item);
        }

        foreach (var item in state.Admirals.Where(admiral => admiral.CycleId == cycleId))
        {
            UpsertAdmiral(connection, transaction, item);
        }

        foreach (var item in state.Fleets.Where(fleet => fleet.CycleId == cycleId))
        {
            UpsertFleet(connection, transaction, item);
        }

        UpsertFleetOrders(
            connection,
            transaction,
            state.FleetOrders.Where(order => order.CycleId == cycleId));

        foreach (var item in state.ColonialOutposts.Where(outpost => outpost.CycleId == cycleId))
        {
            UpsertColonialOutpost(connection, transaction, item);
        }

        foreach (var item in state.DiplomaticRelationships.Where(relationship => relationship.CycleId == cycleId))
        {
            UpsertDiplomaticRelationship(connection, transaction, item);
        }

        foreach (var item in state.ShipConstructions.Where(construction => construction.CycleId == cycleId))
        {
            UpsertShipConstruction(connection, transaction, item);
        }

        foreach (var item in state.EmpireMetrics.Where(metric => metric.CycleId == cycleId))
        {
            UpsertEmpireMetric(connection, transaction, item);
        }

        foreach (var item in state.TickLogs.Where(log => log.CycleId == cycleId))
        {
            UpsertTickLog(connection, transaction, item);
        }

        foreach (var item in state.Events.Where(item => item.CycleId == cycleId))
        {
            UpsertEvent(connection, transaction, item);
        }

        foreach (var item in state.BattleRecords.Where(item => item.CycleId == cycleId))
        {
            UpsertBattleRecord(connection, transaction, item);
        }

        foreach (var item in state.BattleFleetParticipants.Where(item => item.CycleId == cycleId))
        {
            UpsertBattleFleetParticipant(connection, transaction, item);
        }

        foreach (var item in state.AdmiralBattleHistories.Where(item => item.CycleId == cycleId))
        {
            UpsertAdmiralBattleHistory(connection, transaction, item);
        }

        foreach (var item in state.ChronicleEntries.Where(item => item.CycleId == cycleId))
        {
            UpsertChronicleEntry(connection, transaction, item);
        }
    }

    private static Player ReadPlayer(SqlDataReader reader) => new()
    {
        PlayerId = GetGuid(reader, "PlayerID"),
        Username = GetString(reader, "Username"),
        Email = GetString(reader, "Email"),
        PasswordHash = GetString(reader, "PasswordHash"),
        ExternalIssuer = GetString(reader, "ExternalIssuer"),
        ExternalSubject = GetString(reader, "ExternalSubject"),
        Kind = GetEnum<PlayerKind>(reader, "PlayerKind"),
        Role = GetEnum<PlayerRole>(reader, "Role"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        LastLoginAt = GetNullableDateTimeOffset(reader, "LastLoginAt"),
        Status = GetEnum<PlayerStatus>(reader, "Status")
    };

    private static AdminRoleAuditRecord ReadAdminRoleAuditRecord(SqlDataReader reader) => new()
    {
        AdminRoleAuditRecordId = GetGuid(reader, "AdminRoleAuditRecordID"),
        ActorPlayerId = GetNullableGuid(reader, "ActorPlayerID"),
        TargetPlayerId = GetGuid(reader, "TargetPlayerID"),
        Action = GetEnum<AdminRoleAuditAction>(reader, "Action"),
        Reason = GetString(reader, "Reason"),
        Source = GetString(reader, "Source"),
        Severity = GetEnum<EventSeverity>(reader, "Severity"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static Game ReadGame(SqlDataReader reader) => new()
    {
        GameId = GetGuid(reader, "GameID"),
        Name = GetString(reader, "Name"),
        Purpose = GetEnum<GamePurpose>(reader, "Purpose"),
        Status = GetEnum<GameLifecycleStatus>(reader, "Status"),
        Visibility = GetEnum<GameVisibility>(reader, "Visibility"),
        CreationSource = GetEnum<GameCreationSource>(reader, "CreationSource"),
        GamePolicyKey = GetString(reader, "GamePolicyKey"),
        GamePolicyVersion = GetInt(reader, "GamePolicyVersion"),
        GamePolicyContentHash = GetNullableString(reader, "GamePolicyContentHash"),
        PolicyProvenanceStatus = GetEnum<ProvenanceStatus>(reader, "PolicyProvenanceStatus"),
        CreatedByPlayerId = GetNullableGuid(reader, "CreatedByPlayerID"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        FirstStartedAt = GetNullableDateTimeOffset(reader, "FirstStartedAt"),
        CompletedAt = GetNullableDateTimeOffset(reader, "CompletedAt"),
        CancelledAt = GetNullableDateTimeOffset(reader, "CancelledAt"),
        TerminatedAt = GetNullableDateTimeOffset(reader, "TerminatedAt"),
        RowVersion = GetBytes(reader, "RowVersion")
    };

    private static CycleConfiguration ReadCycleConfiguration(SqlDataReader reader) => new()
    {
        CycleConfigurationId = GetGuid(reader, "CycleConfigurationID"),
        GameId = GetGuid(reader, "GameID"),
        SequenceNumber = GetInt(reader, "SequenceNumber"),
        Status = GetEnum<CycleConfigurationStatus>(reader, "Status"),
        ProvenanceStatus = GetEnum<ProvenanceStatus>(reader, "ProvenanceStatus"),
        MapProfileKey = GetNullableString(reader, "MapProfileKey"),
        MapProfileVersion = GetNullableInt(reader, "MapProfileVersion"),
        MapProfileContentHash = GetNullableString(reader, "MapProfileContentHash"),
        MapSeed = GetNullableInt(reader, "MapSeed"),
        ScenarioProfileKey = GetNullableString(reader, "ScenarioProfileKey"),
        ScenarioProfileVersion = GetNullableInt(reader, "ScenarioProfileVersion"),
        ScenarioProfileContentHash = GetNullableString(reader, "ScenarioProfileContentHash"),
        ScenarioSeed = GetNullableInt(reader, "ScenarioSeed"),
        CyclePolicyKey = GetString(reader, "CyclePolicyKey"),
        CyclePolicyVersion = GetInt(reader, "CyclePolicyVersion"),
        CyclePolicyContentHash = GetNullableString(reader, "CyclePolicyContentHash"),
        SchedulingMode = GetEnum<CycleSchedulingMode>(reader, "SchedulingMode"),
        MinimumHumanSeats = GetNullableInt(reader, "MinimumHumanSeats"),
        MaximumHumanSeats = GetNullableInt(reader, "MaximumHumanSeats"),
        ScheduledStartAt = GetNullableDateTimeOffset(reader, "ScheduledStartAt"),
        ScheduledEndAt = GetNullableDateTimeOffset(reader, "ScheduledEndAt"),
        TickLengthMinutes = GetNullableInt(reader, "TickLengthMinutes"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        LockedAt = GetNullableDateTimeOffset(reader, "LockedAt"),
        MaterializedAt = GetNullableDateTimeOffset(reader, "MaterializedAt"),
        CancelledAt = GetNullableDateTimeOffset(reader, "CancelledAt"),
        RowVersion = GetBytes(reader, "RowVersion")
    };

    private static Cycle ReadCycle(SqlDataReader reader) => new()
    {
        CycleId = GetGuid(reader, "CycleID"),
        GameId = GetNullableGuid(reader, "GameID"),
        CycleConfigurationId = GetNullableGuid(reader, "CycleConfigurationID"),
        PreviousCycleId = GetNullableGuid(reader, "PreviousCycleID"),
        Name = GetString(reader, "Name"),
        StartAt = GetDateTimeOffset(reader, "StartAt"),
        EndAt = GetDateTimeOffset(reader, "EndAt"),
        TickLengthMinutes = GetInt(reader, "TickLengthMinutes"),
        CurrentTickNumber = GetInt(reader, "CurrentTickNumber"),
        Status = GetEnum<CycleStatus>(reader, "Status"),
        TurnStage = GetEnum<TurnResolutionStage>(reader, "TurnStage"),
        MapProfileKey = GetNullableString(reader, "MapProfileKey"),
        MapProfileVersion = GetNullableInt(reader, "MapProfileVersion"),
        MapProfileContentHash = GetNullableString(reader, "MapProfileContentHash"),
        MapSeed = GetNullableInt(reader, "MapSeed"),
        ScenarioProfileKey = GetNullableString(reader, "ScenarioProfileKey"),
        ScenarioProfileVersion = GetNullableInt(reader, "ScenarioProfileVersion"),
        ScenarioProfileContentHash = GetNullableString(reader, "ScenarioProfileContentHash"),
        ScenarioSeed = GetNullableInt(reader, "ScenarioSeed"),
        CyclePolicyKey = GetNullableString(reader, "CyclePolicyKey"),
        CyclePolicyVersion = GetNullableInt(reader, "CyclePolicyVersion"),
        CyclePolicyContentHash = GetNullableString(reader, "CyclePolicyContentHash"),
        SchedulingMode = GetEnum<CycleSchedulingMode>(reader, "SchedulingMode"),
        NextTickAt = GetNullableDateTimeOffset(reader, "NextTickAt"),
        ProfileProvenanceStatus = GetNullableEnum<ProvenanceStatus>(reader, "ProfileProvenanceStatus"),
        CreatedByPlayerId = GetNullableGuid(reader, "CreatedByPlayerID"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static GameEnrolment ReadGameEnrolment(SqlDataReader reader) => new()
    {
        GameEnrolmentId = GetGuid(reader, "GameEnrolmentID"),
        GameId = GetGuid(reader, "GameID"),
        PlayerId = GetGuid(reader, "PlayerID"),
        Status = GetEnum<GameEnrolmentStatus>(reader, "Status"),
        Origin = GetEnum<GameEnrolmentOrigin>(reader, "Origin"),
        OriginatingRequestId = GetNullableString(reader, "OriginatingRequestID"),
        EnrolledAt = GetDateTimeOffset(reader, "EnrolledAt"),
        StatusChangedAt = GetDateTimeOffset(reader, "StatusChangedAt"),
        EndedAt = GetNullableDateTimeOffset(reader, "EndedAt"),
        RowVersion = GetBytes(reader, "RowVersion")
    };

    private static GameLifecycleEvent ReadGameLifecycleEvent(SqlDataReader reader) => new()
    {
        GameLifecycleEventId = GetGuid(reader, "GameLifecycleEventID"),
        GameId = GetGuid(reader, "GameID"),
        Type = GetEnum<GameLifecycleEventType>(reader, "EventType"),
        SubjectPlayerId = GetNullableGuid(reader, "SubjectPlayerID"),
        ActorPlayerId = GetNullableGuid(reader, "ActorPlayerID"),
        FromStatus = GetNullableString(reader, "FromStatus"),
        ToStatus = GetNullableString(reader, "ToStatus"),
        Reason = GetNullableString(reader, "Reason"),
        CorrelationId = GetNullableString(reader, "CorrelationID"),
        FactJson = GetString(reader, "FactJson"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static GalaxySector ReadSector(SqlDataReader reader) => new()
    {
        SectorId = GetGuid(reader, "SectorID"),
        CycleId = GetGuid(reader, "CycleID"),
        SectorName = GetString(reader, "SectorName"),
        CentreX = GetInt(reader, "CentreX"),
        CentreY = GetInt(reader, "CentreY"),
        SortOrder = GetInt(reader, "SortOrder")
    };

    private static GalaxySystem ReadSystem(SqlDataReader reader) => new()
    {
        SystemId = GetGuid(reader, "SystemID"),
        CycleId = GetGuid(reader, "CycleID"),
        SectorId = GetNullableGuid(reader, "SectorID") ?? Guid.Empty,
        SystemName = GetString(reader, "SystemName"),
        X = GetInt(reader, "X"),
        Y = GetInt(reader, "Y"),
        IndustryOutput = GetDecimal(reader, "IndustryOutput"),
        ResearchOutput = GetDecimal(reader, "ResearchOutput"),
        PopulationOutput = GetDecimal(reader, "PopulationOutput"),
        StrategicValue = GetInt(reader, "StrategicValue"),
        HistoricalSignificance = GetInt(reader, "HistoricalSignificance"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static Empire ReadEmpire(SqlDataReader reader) => new()
    {
        EmpireId = GetGuid(reader, "EmpireID"),
        CycleId = GetGuid(reader, "CycleID"),
        PlayerId = GetGuid(reader, "PlayerID"),
        EmpireName = GetString(reader, "EmpireName"),
        HomeSystemId = GetGuid(reader, "HomeSystemID"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        Status = GetEnum<EmpireStatus>(reader, "Status")
    };

    private static Faction ReadFaction(SqlDataReader reader) => new()
    {
        FactionId = GetGuid(reader, "FactionID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetNullableGuid(reader, "EmpireID"),
        FactionName = GetString(reader, "FactionName"),
        Kind = GetEnum<FactionKind>(reader, "Kind"),
        Status = GetEnum<FactionStatus>(reader, "Status"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static MatchParticipant ReadMatchParticipant(SqlDataReader reader) => new()
    {
        MatchParticipantId = GetGuid(reader, "MatchParticipantID"),
        GameId = GetGuid(reader, "GameID"),
        CycleId = GetGuid(reader, "CycleID"),
        PlayerId = GetGuid(reader, "PlayerID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        Status = GetEnum<MatchParticipantStatus>(reader, "Status"),
        JoinedAt = GetDateTimeOffset(reader, "JoinedAt"),
        EndedAt = GetNullableDateTimeOffset(reader, "EndedAt")
    };

    private static EmpireResource ReadEmpireResource(SqlDataReader reader) => new()
    {
        EmpireResourceId = GetGuid(reader, "EmpireResourceID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        Industry = GetDecimal(reader, "Industry"),
        Research = GetDecimal(reader, "Research"),
        Population = GetDecimal(reader, "Population"),
        LastGeneratedIndustry = GetDecimal(reader, "LastGeneratedIndustry"),
        LastGeneratedResearch = GetDecimal(reader, "LastGeneratedResearch"),
        LastGeneratedPopulation = GetDecimal(reader, "LastGeneratedPopulation"),
        LastSpentIndustry = GetDecimal(reader, "LastSpentIndustry"),
        LastSpentResearch = GetDecimal(reader, "LastSpentResearch"),
        LastSpentPopulation = GetDecimal(reader, "LastSpentPopulation"),
        UpdatedAt = GetDateTimeOffset(reader, "UpdatedAt")
    };

    private static EmpireDoctrineUnlock ReadEmpireDoctrineUnlock(SqlDataReader reader) => new()
    {
        EmpireDoctrineUnlockId = GetGuid(reader, "EmpireDoctrineUnlockID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        DoctrineKey = GetString(reader, "DoctrineKey"),
        UnlockedTickNumber = GetInt(reader, "UnlockedTickNumber"),
        UnlockedAt = GetDateTimeOffset(reader, "UnlockedAt")
    };

    private static EmpirePriority ReadEmpirePriority(SqlDataReader reader) => new()
    {
        EmpirePriorityId = GetGuid(reader, "EmpirePriorityID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        IndustryWeight = GetInt(reader, "IndustryWeight"),
        ResearchWeight = GetInt(reader, "ResearchWeight"),
        MilitaryWeight = GetInt(reader, "MilitaryWeight"),
        ExpansionWeight = GetInt(reader, "ExpansionWeight"),
        UpdatedAt = GetDateTimeOffset(reader, "UpdatedAt")
    };

    private static EmpireMetric ReadEmpireMetric(SqlDataReader reader) => new()
    {
        EmpireMetricId = GetGuid(reader, "EmpireMetricID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        TickNumber = GetInt(reader, "TickNumber"),
        Rank = GetInt(reader, "Rank"),
        IsWinner = GetBool(reader, "IsWinner"),
        MapControlPercent = GetDecimal(reader, "MapControlPercent"),
        TotalEffectivePresence = GetDecimal(reader, "TotalEffectivePresence"),
        ActiveShipCount = GetInt(reader, "ActiveShipCount"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static CycleRanking ReadCycleRanking(SqlDataReader reader) => new()
    {
        CycleRankingId = GetGuid(reader, "CycleRankingID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        Rank = GetInt(reader, "Rank"),
        IsWinner = GetBool(reader, "IsWinner"),
        MapControlPercent = GetDecimal(reader, "MapControlPercent"),
        TotalEffectivePresence = GetDecimal(reader, "TotalEffectivePresence"),
        ActiveShipCount = GetInt(reader, "ActiveShipCount"),
        CutoffTickNumber = GetInt(reader, "CutoffTickNumber"),
        CutoffAt = GetDateTimeOffset(reader, "CutoffAt")
    };

    private static CycleMajorEvent ReadCycleMajorEvent(SqlDataReader reader) => new()
    {
        CycleMajorEventId = GetGuid(reader, "CycleMajorEventID"),
        CycleId = GetGuid(reader, "CycleID"),
        SourceBattleId = GetNullableGuid(reader, "SourceBattleID"),
        SystemId = GetNullableGuid(reader, "SystemID"),
        EventType = GetEnum<CycleMajorEventType>(reader, "EventType"),
        TickNumber = GetInt(reader, "TickNumber"),
        SelectionRank = GetInt(reader, "SelectionRank"),
        ImportanceScore = GetInt(reader, "ImportanceScore"),
        TotalLosses = GetInt(reader, "TotalLosses"),
        Summary = GetString(reader, "Summary"),
        FactJson = GetString(reader, "FactJson"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static SystemHistoricalSignal ReadSystemHistoricalSignal(SqlDataReader reader) => new()
    {
        SystemHistoricalSignalId = GetGuid(reader, "SystemHistoricalSignalID"),
        CycleId = GetGuid(reader, "CycleID"),
        SystemId = GetGuid(reader, "SystemID"),
        SignalType = GetEnum<SystemHistoricalSignalType>(reader, "SignalType"),
        SourceBattleId = GetNullableGuid(reader, "SourceBattleID"),
        BattleCount = GetInt(reader, "BattleCount"),
        TotalLosses = GetInt(reader, "TotalLosses"),
        LargestBattleLosses = GetInt(reader, "LargestBattleLosses"),
        HostedCycleLargestBattle = GetBool(reader, "HostedCycleLargestBattle"),
        HistoricalSignificanceIncrease = GetInt(reader, "HistoricalSignificanceIncrease"),
        HistoricalSignificanceAfter = GetInt(reader, "HistoricalSignificanceAfter"),
        Summary = GetString(reader, "Summary"),
        FactJson = GetString(reader, "FactJson"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static ColonialOutpost ReadColonialOutpost(SqlDataReader reader) => new()
    {
        ColonialOutpostId = GetGuid(reader, "ColonialOutpostID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        SystemId = GetGuid(reader, "SystemID"),
        EstablishedTick = GetInt(reader, "EstablishedTick"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static DiplomaticRelationship ReadDiplomaticRelationship(SqlDataReader reader) => new()
    {
        DiplomaticRelationshipId = GetGuid(reader, "DiplomaticRelationshipID"),
        CycleId = GetGuid(reader, "CycleID"),
        FirstEmpireId = GetGuid(reader, "FirstEmpireID"),
        SecondEmpireId = GetGuid(reader, "SecondEmpireID"),
        State = GetEnum<DiplomaticRelationshipState>(reader, "State"),
        UpdatedTick = GetInt(reader, "UpdatedTick"),
        UpdatedAt = GetDateTimeOffset(reader, "UpdatedAt")
    };

    private static Admiral ReadAdmiral(SqlDataReader reader) => new()
    {
        AdmiralId = GetGuid(reader, "AdmiralID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        AdmiralName = GetString(reader, "AdmiralName"),
        ReputationScore = GetInt(reader, "ReputationScore"),
        Status = GetEnum<AdmiralStatus>(reader, "Status"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        UpdatedAt = GetDateTimeOffset(reader, "UpdatedAt")
    };

    private static AdmiralBattleHistory ReadAdmiralBattleHistory(SqlDataReader reader) => new()
    {
        AdmiralBattleHistoryId = GetGuid(reader, "AdmiralBattleHistoryID"),
        CycleId = GetGuid(reader, "CycleID"),
        AdmiralId = GetGuid(reader, "AdmiralID"),
        BattleId = GetGuid(reader, "BattleID"),
        SystemId = GetGuid(reader, "SystemID"),
        FleetId = GetGuid(reader, "FleetID"),
        Role = GetEnum<AdmiralBattleRole>(reader, "Role"),
        Outcome = GetEnum<AdmiralBattleOutcome>(reader, "Outcome"),
        ShipsCommandedBefore = GetInt(reader, "ShipsCommandedBefore"),
        ShipsLost = GetInt(reader, "ShipsLost"),
        ReputationChange = GetInt(reader, "ReputationChange"),
        ReputationScoreAfter = GetInt(reader, "ReputationScoreAfter"),
        AdmiralStatusAfter = GetEnum<AdmiralStatus>(reader, "AdmiralStatusAfter"),
        IsFamousSystemAssociation = GetBool(reader, "IsFamousSystemAssociation"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static SystemLink ReadSystemLink(SqlDataReader reader) => new()
    {
        SystemLinkId = GetGuid(reader, "SystemLinkID"),
        CycleId = GetGuid(reader, "CycleID"),
        SystemAId = GetGuid(reader, "SystemAID"),
        SystemBId = GetGuid(reader, "SystemBID"),
        Distance = GetDecimal(reader, "Distance"),
        TravelTicks = GetInt(reader, "TravelTicks")
    };

    private static Fleet ReadFleet(SqlDataReader reader) => new()
    {
        FleetId = GetGuid(reader, "FleetID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetNullableGuid(reader, "EmpireID") ?? Guid.Empty,
        FactionId = GetGuid(reader, "FactionID"),
        AdmiralId = GetNullableGuid(reader, "AdmiralID"),
        FleetName = GetString(reader, "FleetName"),
        CurrentSystemId = GetGuid(reader, "CurrentSystemID"),
        DestinationSystemId = GetNullableGuid(reader, "DestinationSystemID"),
        DepartureTickNumber = GetNullableInt(reader, "DepartureTickNumber"),
        ArrivalTickNumber = GetNullableInt(reader, "ArrivalTickNumber"),
        ShipCount = GetInt(reader, "ShipCount"),
        Status = GetEnum<FleetStatus>(reader, "Status"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static FleetOrder ReadFleetOrder(SqlDataReader reader) => new()
    {
        FleetOrderId = GetGuid(reader, "FleetOrderID"),
        CycleId = GetGuid(reader, "CycleID"),
        FleetId = GetGuid(reader, "FleetID"),
        OrderType = GetEnum<FleetOrderType>(reader, "OrderType"),
        TargetSystemId = GetNullableGuid(reader, "TargetSystemID"),
        TargetEmpireId = GetNullableGuid(reader, "TargetEmpireID"),
        TargetFactionId = GetNullableGuid(reader, "TargetFactionID"),
        SubmitTick = GetInt(reader, "SubmitTick"),
        ExecuteAfterTick = GetInt(reader, "ExecuteAfterTick"),
        ProcessedTick = GetNullableInt(reader, "ProcessedTick"),
        Status = GetEnum<FleetOrderStatus>(reader, "Status"),
        CommandSource = GetEnum<FleetOrderCommandSource>(reader, "CommandSource"),
        SealedTick = GetNullableInt(reader, "SealedTick"),
        SealedAt = GetNullableDateTimeOffset(reader, "SealedAt"),
        RejectionReason = GetNullableString(reader, "RejectionReason"),
        SupersededByOrderId = GetNullableGuid(reader, "SupersededByOrderID"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static ShipConstruction ReadShipConstruction(SqlDataReader reader) => new()
    {
        ShipConstructionId = GetGuid(reader, "ShipConstructionID"),
        CycleId = GetGuid(reader, "CycleID"),
        EmpireId = GetGuid(reader, "EmpireID"),
        ShipCount = GetInt(reader, "ShipCount"),
        IndustrySpent = GetDecimal(reader, "IndustrySpent"),
        StartedTick = GetInt(reader, "StartedTick"),
        CompleteAfterTick = GetInt(reader, "CompleteAfterTick"),
        CompletedTick = GetNullableInt(reader, "CompletedTick"),
        Status = GetEnum<ShipConstructionStatus>(reader, "Status"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        UpdatedAt = GetDateTimeOffset(reader, "UpdatedAt")
    };

    private static TickLog ReadTickLog(SqlDataReader reader) => new()
    {
        TickLogId = GetGuid(reader, "TickLogID"),
        CycleId = GetGuid(reader, "CycleID"),
        TickNumber = GetInt(reader, "TickNumber"),
        StartedAt = GetDateTimeOffset(reader, "StartedAt"),
        CompletedAt = GetNullableDateTimeOffset(reader, "CompletedAt"),
        Status = GetEnum<TickLogStatus>(reader, "Status"),
        DiagnosticLog = GetString(reader, "DiagnosticLog")
    };

    private static EventRecord ReadEvent(SqlDataReader reader) => new()
    {
        EventId = GetGuid(reader, "EventID"),
        CycleId = GetGuid(reader, "CycleID"),
        TickNumber = GetInt(reader, "TickNumber"),
        EventType = GetEnum<EventType>(reader, "EventType"),
        SystemId = GetNullableGuid(reader, "SystemID"),
        EmpireId = GetNullableGuid(reader, "EmpireID"),
        FactionId = GetNullableGuid(reader, "FactionID"),
        Severity = GetEnum<EventSeverity>(reader, "Severity"),
        FactJson = GetString(reader, "FactJson"),
        DisplayText = GetString(reader, "DisplayText"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static BattleRecord ReadBattleRecord(SqlDataReader reader) => new()
    {
        BattleId = GetGuid(reader, "BattleID"),
        CycleId = GetGuid(reader, "CycleID"),
        TickNumber = GetInt(reader, "TickNumber"),
        SystemId = GetGuid(reader, "SystemID"),
        AttackerEmpireId = GetNullableGuid(reader, "AttackerEmpireID") ?? Guid.Empty,
        DefenderEmpireId = GetNullableGuid(reader, "DefenderEmpireID") ?? Guid.Empty,
        AttackerFactionId = GetGuid(reader, "AttackerFactionID"),
        DefenderFactionId = GetGuid(reader, "DefenderFactionID"),
        AttackerFleetIds = GetString(reader, "AttackerFleetIDs"),
        DefenderFleetIds = GetString(reader, "DefenderFleetIDs"),
        AttackerShipsBefore = GetInt(reader, "AttackerShipsBefore"),
        DefenderShipsBefore = GetInt(reader, "DefenderShipsBefore"),
        AttackerLosses = GetInt(reader, "AttackerLosses"),
        DefenderLosses = GetInt(reader, "DefenderLosses"),
        Outcome = GetEnum<BattleOutcome>(reader, "Outcome"),
        FactJson = GetString(reader, "FactJson"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static BattleFleetParticipant ReadBattleFleetParticipant(SqlDataReader reader) => new()
    {
        BattleId = GetGuid(reader, "BattleID"),
        CycleId = GetGuid(reader, "CycleID"),
        FleetId = GetGuid(reader, "FleetID"),
        Side = GetEnum<BattleFleetSide>(reader, "Side")
    };

    private static ChronicleEntry ReadChronicleEntry(SqlDataReader reader) => new()
    {
        ChronicleEntryId = GetGuid(reader, "ChronicleEntryID"),
        SourceEventId = GetNullableGuid(reader, "SourceEventID"),
        SourceBattleId = GetNullableGuid(reader, "SourceBattleID"),
        CycleId = GetGuid(reader, "CycleID"),
        SystemId = GetNullableGuid(reader, "SystemID"),
        Title = GetString(reader, "Title"),
        EntryType = GetEnum<ChronicleEntryType>(reader, "EntryType"),
        ImportanceScore = GetInt(reader, "ImportanceScore"),
        FactualSummary = GetString(reader, "FactualSummary"),
        NarrativeText = GetString(reader, "NarrativeText"),
        NarrativeStatus = GetEnum<NarrativeGenerationStatus>(reader, "NarrativeStatus"),
        NarrativeContextJson = GetString(reader, "NarrativeContextJson"),
        NarrativeGeneratedAt = GetNullableDateTimeOffset(reader, "NarrativeGeneratedAt"),
        NarrativeFailureReason = GetNullableString(reader, "NarrativeFailureReason"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static void UpsertPlayer(SqlConnection connection, SqlTransaction transaction, Player item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Players
            SET Username = @Username,
                Email = @Email,
                PasswordHash = @PasswordHash,
                ExternalIssuer = @ExternalIssuer,
                ExternalSubject = @ExternalSubject,
                PlayerKind = @PlayerKind,
                Role = @Role,
                CreatedAt = @CreatedAt,
                LastLoginAt = @LastLoginAt,
                Status = @Status
            WHERE PlayerID = @PlayerID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES (@PlayerID, @Username, @Email, @PasswordHash, @ExternalIssuer, @ExternalSubject, @PlayerKind, @Role, @CreatedAt, @LastLoginAt, @Status);
            END;
            """, command =>
        {
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddString(command, "@Username", item.Username, 80);
            AddString(command, "@Email", item.Email, 256);
            AddString(command, "@PasswordHash", item.PasswordHash, 512);
            AddString(command, "@ExternalIssuer", item.ExternalIssuer, 256);
            AddString(command, "@ExternalSubject", item.ExternalSubject, 256);
            AddString(command, "@PlayerKind", item.Kind.ToString(), 32);
            AddString(command, "@Role", item.Role.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddNullableDateTimeOffset(command, "@LastLoginAt", item.LastLoginAt);
            AddString(command, "@Status", item.Status.ToString(), 32);
        });

    private static void UpsertAdminRoleAuditRecord(
        SqlConnection connection,
        SqlTransaction transaction,
        AdminRoleAuditRecord item) =>
        Execute(connection, transaction, """
            IF EXISTS (SELECT 1 FROM dbo.AdminRoleAuditRecords WHERE AdminRoleAuditRecordID = @AdminRoleAuditRecordID)
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.AdminRoleAuditRecords
                    WHERE AdminRoleAuditRecordID = @AdminRoleAuditRecordID
                      AND NOT
                      (
                          (ActorPlayerID = @ActorPlayerID OR (ActorPlayerID IS NULL AND @ActorPlayerID IS NULL))
                          AND TargetPlayerID = @TargetPlayerID
                          AND Action = @Action
                          AND Reason = @Reason
                          AND Source = @Source
                          AND Severity = @Severity
                          AND CreatedAt = @CreatedAt
                      )
                )
                BEGIN
                    THROW 51001, 'Admin role audit records are immutable.', 1;
                END;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.AdminRoleAuditRecords(AdminRoleAuditRecordID, ActorPlayerID, TargetPlayerID, Action, Reason, Source, Severity, CreatedAt)
                VALUES (@AdminRoleAuditRecordID, @ActorPlayerID, @TargetPlayerID, @Action, @Reason, @Source, @Severity, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@AdminRoleAuditRecordID", item.AdminRoleAuditRecordId);
            AddNullableGuid(command, "@ActorPlayerID", item.ActorPlayerId);
            AddGuid(command, "@TargetPlayerID", item.TargetPlayerId);
            AddString(command, "@Action", item.Action.ToString(), 32);
            AddString(command, "@Reason", item.Reason, 1024);
            AddString(command, "@Source", item.Source, 256);
            AddString(command, "@Severity", item.Severity.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertGame(SqlConnection connection, SqlTransaction transaction, Game item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Games
            SET Name = @Name,
                Purpose = @Purpose,
                Status = @Status,
                Visibility = @Visibility,
                CreationSource = @CreationSource,
                GamePolicyKey = @GamePolicyKey,
                GamePolicyVersion = @GamePolicyVersion,
                GamePolicyContentHash = @GamePolicyContentHash,
                PolicyProvenanceStatus = @PolicyProvenanceStatus,
                CreatedByPlayerID = @CreatedByPlayerID,
                CreatedAt = @CreatedAt,
                FirstStartedAt = @FirstStartedAt,
                CompletedAt = @CompletedAt,
                CancelledAt = @CancelledAt,
                TerminatedAt = @TerminatedAt
            WHERE GameID = @GameID;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.Games
                (
                    GameID, Name, Purpose, Status, Visibility, CreationSource,
                    GamePolicyKey, GamePolicyVersion, GamePolicyContentHash, PolicyProvenanceStatus,
                    CreatedByPlayerID, CreatedAt, FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt
                )
                VALUES
                (
                    @GameID, @Name, @Purpose, @Status, @Visibility, @CreationSource,
                    @GamePolicyKey, @GamePolicyVersion, @GamePolicyContentHash, @PolicyProvenanceStatus,
                    @CreatedByPlayerID, @CreatedAt, @FirstStartedAt, @CompletedAt, @CancelledAt, @TerminatedAt
                );
            END;
            """, command =>
        {
            AddGuid(command, "@GameID", item.GameId);
            AddString(command, "@Name", item.Name, 120);
            AddString(command, "@Purpose", item.Purpose.ToString(), 32);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddString(command, "@Visibility", item.Visibility.ToString(), 32);
            AddString(command, "@CreationSource", item.CreationSource.ToString(), 32);
            AddString(command, "@GamePolicyKey", item.GamePolicyKey, 128);
            AddInt(command, "@GamePolicyVersion", item.GamePolicyVersion);
            AddNullableString(command, "@GamePolicyContentHash", item.GamePolicyContentHash, 64);
            AddString(command, "@PolicyProvenanceStatus", item.PolicyProvenanceStatus.ToString(), 32);
            AddNullableGuid(command, "@CreatedByPlayerID", item.CreatedByPlayerId);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddNullableDateTimeOffset(command, "@FirstStartedAt", item.FirstStartedAt);
            AddNullableDateTimeOffset(command, "@CompletedAt", item.CompletedAt);
            AddNullableDateTimeOffset(command, "@CancelledAt", item.CancelledAt);
            AddNullableDateTimeOffset(command, "@TerminatedAt", item.TerminatedAt);
        });

    private static void UpsertCycleConfiguration(
        SqlConnection connection,
        SqlTransaction transaction,
        CycleConfiguration item) =>
        Execute(connection, transaction, """
            UPDATE dbo.CycleConfigurations
            SET GameID = @GameID,
                SequenceNumber = @SequenceNumber,
                Status = @Status,
                ProvenanceStatus = @ProvenanceStatus,
                MapProfileKey = @MapProfileKey,
                MapProfileVersion = @MapProfileVersion,
                MapProfileContentHash = @MapProfileContentHash,
                MapSeed = @MapSeed,
                ScenarioProfileKey = @ScenarioProfileKey,
                ScenarioProfileVersion = @ScenarioProfileVersion,
                ScenarioProfileContentHash = @ScenarioProfileContentHash,
                ScenarioSeed = @ScenarioSeed,
                CyclePolicyKey = @CyclePolicyKey,
                CyclePolicyVersion = @CyclePolicyVersion,
                CyclePolicyContentHash = @CyclePolicyContentHash,
                SchedulingMode = @SchedulingMode,
                MinimumHumanSeats = @MinimumHumanSeats,
                MaximumHumanSeats = @MaximumHumanSeats,
                ScheduledStartAt = @ScheduledStartAt,
                ScheduledEndAt = @ScheduledEndAt,
                TickLengthMinutes = @ConfigurationTickLengthMinutes,
                CreatedAt = @CreatedAt,
                LockedAt = @LockedAt,
                MaterializedAt = @MaterializedAt,
                CancelledAt = @CancelledAt
            WHERE CycleConfigurationID = @CycleConfigurationID;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.CycleConfigurations
                (
                    CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus,
                    MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed,
                    ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed,
                    CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash,
                    SchedulingMode,
                    MinimumHumanSeats, MaximumHumanSeats, ScheduledStartAt, ScheduledEndAt,
                    TickLengthMinutes, CreatedAt, LockedAt, MaterializedAt, CancelledAt
                )
                VALUES
                (
                    @CycleConfigurationID, @GameID, @SequenceNumber, @Status, @ProvenanceStatus,
                    @MapProfileKey, @MapProfileVersion, @MapProfileContentHash, @MapSeed,
                    @ScenarioProfileKey, @ScenarioProfileVersion, @ScenarioProfileContentHash, @ScenarioSeed,
                    @CyclePolicyKey, @CyclePolicyVersion, @CyclePolicyContentHash,
                    @SchedulingMode,
                    @MinimumHumanSeats, @MaximumHumanSeats, @ScheduledStartAt, @ScheduledEndAt,
                    @ConfigurationTickLengthMinutes, @CreatedAt, @LockedAt, @MaterializedAt, @CancelledAt
                );
            END;
            """, command =>
        {
            AddGuid(command, "@CycleConfigurationID", item.CycleConfigurationId);
            AddGuid(command, "@GameID", item.GameId);
            AddInt(command, "@SequenceNumber", item.SequenceNumber);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddString(command, "@ProvenanceStatus", item.ProvenanceStatus.ToString(), 32);
            AddNullableString(command, "@MapProfileKey", item.MapProfileKey, 128);
            AddNullableInt(command, "@MapProfileVersion", item.MapProfileVersion);
            AddNullableString(command, "@MapProfileContentHash", item.MapProfileContentHash, 64);
            AddNullableInt(command, "@MapSeed", item.MapSeed);
            AddNullableString(command, "@ScenarioProfileKey", item.ScenarioProfileKey, 128);
            AddNullableInt(command, "@ScenarioProfileVersion", item.ScenarioProfileVersion);
            AddNullableString(command, "@ScenarioProfileContentHash", item.ScenarioProfileContentHash, 64);
            AddNullableInt(command, "@ScenarioSeed", item.ScenarioSeed);
            AddString(command, "@CyclePolicyKey", item.CyclePolicyKey, 128);
            AddInt(command, "@CyclePolicyVersion", item.CyclePolicyVersion);
            AddNullableString(command, "@CyclePolicyContentHash", item.CyclePolicyContentHash, 64);
            AddString(command, "@SchedulingMode", item.SchedulingMode.ToString(), 32);
            AddNullableInt(command, "@MinimumHumanSeats", item.MinimumHumanSeats);
            AddNullableInt(command, "@MaximumHumanSeats", item.MaximumHumanSeats);
            AddNullableDateTimeOffset(command, "@ScheduledStartAt", item.ScheduledStartAt);
            AddNullableDateTimeOffset(command, "@ScheduledEndAt", item.ScheduledEndAt);
            AddNullableInt(command, "@ConfigurationTickLengthMinutes", item.TickLengthMinutes);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddNullableDateTimeOffset(command, "@LockedAt", item.LockedAt);
            AddNullableDateTimeOffset(command, "@MaterializedAt", item.MaterializedAt);
            AddNullableDateTimeOffset(command, "@CancelledAt", item.CancelledAt);
        });

    private static void UpsertCycle(SqlConnection connection, SqlTransaction transaction, Cycle item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Cycles
            SET GameID = @GameID,
                CycleConfigurationID = @CycleConfigurationID,
                PreviousCycleID = @PreviousCycleID,
                Name = @Name,
                StartAt = @StartAt,
                EndAt = @EndAt,
                TickLengthMinutes = @TickLengthMinutes,
                CurrentTickNumber = @CurrentTickNumber,
                Status = @Status,
                TurnStage = @TurnStage,
                MapProfileKey = @MapProfileKey,
                MapProfileVersion = @MapProfileVersion,
                MapProfileContentHash = @MapProfileContentHash,
                MapSeed = @MapSeed,
                ScenarioProfileKey = @ScenarioProfileKey,
                ScenarioProfileVersion = @ScenarioProfileVersion,
                ScenarioProfileContentHash = @ScenarioProfileContentHash,
                ScenarioSeed = @ScenarioSeed,
                CyclePolicyKey = @CyclePolicyKey,
                CyclePolicyVersion = @CyclePolicyVersion,
                CyclePolicyContentHash = @CyclePolicyContentHash,
                SchedulingMode = @SchedulingMode,
                NextTickAt = @NextTickAt,
                ProfileProvenanceStatus = @ProfileProvenanceStatus,
                CreatedByPlayerID = @CreatedByPlayerID,
                CreatedAt = @CreatedAt
            WHERE CycleID = @CycleID;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.Cycles
                (
                    CycleID, GameID, CycleConfigurationID, PreviousCycleID, Name, StartAt, EndAt,
                    TickLengthMinutes, CurrentTickNumber, Status, TurnStage,
                    MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed,
                    ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed,
                    CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, ProfileProvenanceStatus,
                    SchedulingMode, NextTickAt,
                    CreatedByPlayerID, CreatedAt
                )
                VALUES
                (
                    @CycleID, @GameID, @CycleConfigurationID, @PreviousCycleID, @Name, @StartAt, @EndAt,
                    @TickLengthMinutes, @CurrentTickNumber, @Status, @TurnStage,
                    @MapProfileKey, @MapProfileVersion, @MapProfileContentHash, @MapSeed,
                    @ScenarioProfileKey, @ScenarioProfileVersion, @ScenarioProfileContentHash, @ScenarioSeed,
                    @CyclePolicyKey, @CyclePolicyVersion, @CyclePolicyContentHash, @ProfileProvenanceStatus,
                    @SchedulingMode, @NextTickAt,
                    @CreatedByPlayerID, @CreatedAt
                );
            END;
            """, command =>
        {
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@GameID", item.GameId);
            AddNullableGuid(command, "@CycleConfigurationID", item.CycleConfigurationId);
            AddNullableGuid(command, "@PreviousCycleID", item.PreviousCycleId);
            AddString(command, "@Name", item.Name, 120);
            AddDateTimeOffset(command, "@StartAt", item.StartAt);
            AddDateTimeOffset(command, "@EndAt", item.EndAt);
            AddInt(command, "@TickLengthMinutes", item.TickLengthMinutes);
            AddInt(command, "@CurrentTickNumber", item.CurrentTickNumber);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddString(command, "@TurnStage", item.TurnStage.ToString(), 32);
            AddNullableString(command, "@MapProfileKey", item.MapProfileKey, 128);
            AddNullableInt(command, "@MapProfileVersion", item.MapProfileVersion);
            AddNullableString(command, "@MapProfileContentHash", item.MapProfileContentHash, 64);
            AddNullableInt(command, "@MapSeed", item.MapSeed);
            AddNullableString(command, "@ScenarioProfileKey", item.ScenarioProfileKey, 128);
            AddNullableInt(command, "@ScenarioProfileVersion", item.ScenarioProfileVersion);
            AddNullableString(command, "@ScenarioProfileContentHash", item.ScenarioProfileContentHash, 64);
            AddNullableInt(command, "@ScenarioSeed", item.ScenarioSeed);
            AddNullableString(command, "@CyclePolicyKey", item.CyclePolicyKey, 128);
            AddNullableInt(command, "@CyclePolicyVersion", item.CyclePolicyVersion);
            AddNullableString(command, "@CyclePolicyContentHash", item.CyclePolicyContentHash, 64);
            AddString(command, "@SchedulingMode", item.SchedulingMode.ToString(), 32);
            AddNullableDateTimeOffset(command, "@NextTickAt", item.NextTickAt);
            AddNullableString(command, "@ProfileProvenanceStatus", item.ProfileProvenanceStatus?.ToString(), 32);
            AddNullableGuid(command, "@CreatedByPlayerID", item.CreatedByPlayerId);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertGameEnrolment(
        SqlConnection connection,
        SqlTransaction transaction,
        GameEnrolment item) =>
        Execute(connection, transaction, """
            UPDATE dbo.GameEnrolments
            SET GameID = @GameID,
                PlayerID = @PlayerID,
                Status = @Status,
                Origin = @Origin,
                OriginatingRequestID = @OriginatingRequestID,
                EnrolledAt = @EnrolledAt,
                StatusChangedAt = @StatusChangedAt,
                EndedAt = @EndedAt
            WHERE GameEnrolmentID = @GameEnrolmentID;

            IF @@ROWCOUNT = 0
            BEGIN
                INSERT INTO dbo.GameEnrolments
                (
                    GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID,
                    EnrolledAt, StatusChangedAt, EndedAt
                )
                VALUES
                (
                    @GameEnrolmentID, @GameID, @PlayerID, @Status, @Origin, @OriginatingRequestID,
                    @EnrolledAt, @StatusChangedAt, @EndedAt
                );
            END;
            """, command =>
        {
            AddGuid(command, "@GameEnrolmentID", item.GameEnrolmentId);
            AddGuid(command, "@GameID", item.GameId);
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddString(command, "@Origin", item.Origin.ToString(), 32);
            AddNullableString(command, "@OriginatingRequestID", item.OriginatingRequestId, 128);
            AddDateTimeOffset(command, "@EnrolledAt", item.EnrolledAt);
            AddDateTimeOffset(command, "@StatusChangedAt", item.StatusChangedAt);
            AddNullableDateTimeOffset(command, "@EndedAt", item.EndedAt);
        });

    private static void UpsertGameLifecycleEvent(
        SqlConnection connection,
        SqlTransaction transaction,
        GameLifecycleEvent item) =>
        Execute(connection, transaction, """
            IF EXISTS
            (
                SELECT 1
                FROM dbo.GameLifecycleEvents
                WHERE GameLifecycleEventID = @GameLifecycleEventID
            )
            BEGIN
                IF EXISTS
                (
                    SELECT 1
                    FROM dbo.GameLifecycleEvents
                    WHERE GameLifecycleEventID = @GameLifecycleEventID
                      AND NOT
                      (
                          GameID = @GameID
                          AND EventType = @EventType
                          AND (SubjectPlayerID = @SubjectPlayerID OR (SubjectPlayerID IS NULL AND @SubjectPlayerID IS NULL))
                          AND (ActorPlayerID = @ActorPlayerID OR (ActorPlayerID IS NULL AND @ActorPlayerID IS NULL))
                          AND (FromStatus = @FromStatus OR (FromStatus IS NULL AND @FromStatus IS NULL))
                          AND (ToStatus = @ToStatus OR (ToStatus IS NULL AND @ToStatus IS NULL))
                          AND (Reason = @Reason OR (Reason IS NULL AND @Reason IS NULL))
                          AND (CorrelationID = @CorrelationID OR (CorrelationID IS NULL AND @CorrelationID IS NULL))
                          AND FactJson = @FactJson
                          AND CreatedAt = @CreatedAt
                      )
                )
                BEGIN
                    THROW 51025, 'Game lifecycle events are immutable.', 1;
                END;
            END
            ELSE
            BEGIN
                INSERT INTO dbo.GameLifecycleEvents
                (
                    GameLifecycleEventID, GameID, EventType, SubjectPlayerID, ActorPlayerID,
                    FromStatus, ToStatus, Reason, CorrelationID, FactJson, CreatedAt
                )
                VALUES
                (
                    @GameLifecycleEventID, @GameID, @EventType, @SubjectPlayerID, @ActorPlayerID,
                    @FromStatus, @ToStatus, @Reason, @CorrelationID, @FactJson, @CreatedAt
                );
            END;
            """, command =>
        {
            AddGuid(command, "@GameLifecycleEventID", item.GameLifecycleEventId);
            AddGuid(command, "@GameID", item.GameId);
            AddString(command, "@EventType", item.Type.ToString(), 64);
            AddNullableGuid(command, "@SubjectPlayerID", item.SubjectPlayerId);
            AddNullableGuid(command, "@ActorPlayerID", item.ActorPlayerId);
            AddNullableString(command, "@FromStatus", item.FromStatus, 32);
            AddNullableString(command, "@ToStatus", item.ToStatus, 32);
            AddNullableString(command, "@Reason", item.Reason, 512);
            AddNullableString(command, "@CorrelationID", item.CorrelationId, 128);
            AddMaxString(command, "@FactJson", item.FactJson);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertSector(SqlConnection connection, SqlTransaction transaction, GalaxySector item) =>
        Execute(connection, transaction, """
            UPDATE dbo.GalaxySectors
            SET CycleID = @CycleID,
                SectorName = @SectorName,
                CentreX = @CentreX,
                CentreY = @CentreY,
                SortOrder = @SortOrder
            WHERE SectorID = @SectorID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
            VALUES (@SectorID, @CycleID, @SectorName, @CentreX, @CentreY, @SortOrder);
            END;
            """, command =>
        {
            AddGuid(command, "@SectorID", item.SectorId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddString(command, "@SectorName", item.SectorName, 120);
            AddInt(command, "@CentreX", item.CentreX);
            AddInt(command, "@CentreY", item.CentreY);
            AddInt(command, "@SortOrder", item.SortOrder);
        });

    private static void UpsertSystem(SqlConnection connection, SqlTransaction transaction, GalaxySystem item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Systems
            SET CycleID = @CycleID,
                SectorID = @SectorID,
                SystemName = @SystemName,
                X = @X,
                Y = @Y,
                IndustryOutput = @IndustryOutput,
                ResearchOutput = @ResearchOutput,
                PopulationOutput = @PopulationOutput,
                StrategicValue = @StrategicValue,
                HistoricalSignificance = @HistoricalSignificance,
                CreatedAt = @CreatedAt
            WHERE SystemID = @SystemID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Systems(SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES (@SystemID, @CycleID, @SectorID, @SystemName, @X, @Y, @IndustryOutput, @ResearchOutput, @PopulationOutput, @StrategicValue, @HistoricalSignificance, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@SystemID", item.SystemId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@SectorID", item.SectorId == Guid.Empty ? null : item.SectorId);
            AddString(command, "@SystemName", item.SystemName, 120);
            AddInt(command, "@X", item.X);
            AddInt(command, "@Y", item.Y);
            AddDecimal(command, "@IndustryOutput", item.IndustryOutput);
            AddDecimal(command, "@ResearchOutput", item.ResearchOutput);
            AddDecimal(command, "@PopulationOutput", item.PopulationOutput);
            AddInt(command, "@StrategicValue", item.StrategicValue);
            AddInt(command, "@HistoricalSignificance", item.HistoricalSignificance);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertEmpire(SqlConnection connection, SqlTransaction transaction, Empire item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Empires
            SET CycleID = @CycleID,
                PlayerID = @PlayerID,
                EmpireName = @EmpireName,
                HomeSystemID = @HomeSystemID,
                CreatedAt = @CreatedAt,
                Status = @Status
            WHERE EmpireID = @EmpireID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES (@EmpireID, @CycleID, @PlayerID, @EmpireName, @HomeSystemID, @CreatedAt, @Status);
            END;
            """, command =>
        {
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddString(command, "@EmpireName", item.EmpireName, 120);
            AddGuid(command, "@HomeSystemID", item.HomeSystemId);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddString(command, "@Status", item.Status.ToString(), 32);
        });

    private static void UpsertFaction(SqlConnection connection, SqlTransaction transaction, Faction item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Factions
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                FactionName = @FactionName,
                Kind = @Kind,
                Status = @Status,
                CreatedAt = @CreatedAt
            WHERE FactionID = @FactionID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Factions(FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt)
            VALUES (@FactionID, @CycleID, @EmpireID, @FactionName, @Kind, @Status, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FactionID", item.FactionId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@EmpireID", item.EmpireId);
            AddString(command, "@FactionName", item.FactionName, 120);
            AddString(command, "@Kind", item.Kind.ToString(), 32);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertMatchParticipant(SqlConnection connection, SqlTransaction transaction, MatchParticipant item) =>
        Execute(connection, transaction, """
            UPDATE dbo.MatchParticipants
            SET GameID = @GameID,
                CycleID = @CycleID,
                PlayerID = @PlayerID,
                EmpireID = @EmpireID,
                Status = @Status,
                JoinedAt = @JoinedAt,
                EndedAt = @EndedAt
            WHERE MatchParticipantID = @MatchParticipantID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.MatchParticipants(MatchParticipantID, GameID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
            VALUES (@MatchParticipantID, @GameID, @CycleID, @PlayerID, @EmpireID, @Status, @JoinedAt, @EndedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@MatchParticipantID", item.MatchParticipantId);
            AddGuid(command, "@GameID", item.GameId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@JoinedAt", item.JoinedAt);
            AddNullableDateTimeOffset(command, "@EndedAt", item.EndedAt);
        });

    private static void UpsertEmpireResource(SqlConnection connection, SqlTransaction transaction, EmpireResource item) =>
        Execute(connection, transaction, """
            UPDATE dbo.EmpireResources
            SET EmpireID = @EmpireID,
                Industry = @Industry,
                Research = @Research,
                Population = @Population,
                LastGeneratedIndustry = @LastGeneratedIndustry,
                LastGeneratedResearch = @LastGeneratedResearch,
                LastGeneratedPopulation = @LastGeneratedPopulation,
                LastSpentIndustry = @LastSpentIndustry,
                LastSpentResearch = @LastSpentResearch,
                LastSpentPopulation = @LastSpentPopulation,
                UpdatedAt = @UpdatedAt
            WHERE EmpireResourceID = @EmpireResourceID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.EmpireResources(EmpireResourceID, EmpireID, Industry, Research, Population, LastGeneratedIndustry, LastGeneratedResearch, LastGeneratedPopulation, LastSpentIndustry, LastSpentResearch, LastSpentPopulation, UpdatedAt)
            VALUES (@EmpireResourceID, @EmpireID, @Industry, @Research, @Population, @LastGeneratedIndustry, @LastGeneratedResearch, @LastGeneratedPopulation, @LastSpentIndustry, @LastSpentResearch, @LastSpentPopulation, @UpdatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@EmpireResourceID", item.EmpireResourceId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddDecimal(command, "@Industry", item.Industry);
            AddDecimal(command, "@Research", item.Research);
            AddDecimal(command, "@Population", item.Population);
            AddDecimal(command, "@LastGeneratedIndustry", item.LastGeneratedIndustry);
            AddDecimal(command, "@LastGeneratedResearch", item.LastGeneratedResearch);
            AddDecimal(command, "@LastGeneratedPopulation", item.LastGeneratedPopulation);
            AddDecimal(command, "@LastSpentIndustry", item.LastSpentIndustry);
            AddDecimal(command, "@LastSpentResearch", item.LastSpentResearch);
            AddDecimal(command, "@LastSpentPopulation", item.LastSpentPopulation);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void UpsertEmpireDoctrineUnlock(SqlConnection connection, SqlTransaction transaction, EmpireDoctrineUnlock item) =>
        Execute(connection, transaction, """
            UPDATE dbo.EmpireDoctrineUnlocks
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                DoctrineKey = @DoctrineKey,
                UnlockedTickNumber = @UnlockedTickNumber,
                UnlockedAt = @UnlockedAt
            WHERE EmpireDoctrineUnlockID = @EmpireDoctrineUnlockID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.EmpireDoctrineUnlocks(EmpireDoctrineUnlockID, CycleID, EmpireID, DoctrineKey, UnlockedTickNumber, UnlockedAt)
            VALUES (@EmpireDoctrineUnlockID, @CycleID, @EmpireID, @DoctrineKey, @UnlockedTickNumber, @UnlockedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@EmpireDoctrineUnlockID", item.EmpireDoctrineUnlockId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddString(command, "@DoctrineKey", item.DoctrineKey, 128);
            AddInt(command, "@UnlockedTickNumber", item.UnlockedTickNumber);
            AddDateTimeOffset(command, "@UnlockedAt", item.UnlockedAt);
        });

    private static void UpsertEmpirePriority(SqlConnection connection, SqlTransaction transaction, EmpirePriority item) =>
        Execute(connection, transaction, """
            UPDATE dbo.EmpirePriorities
            SET EmpireID = @EmpireID,
                IndustryWeight = @IndustryWeight,
                ResearchWeight = @ResearchWeight,
                MilitaryWeight = @MilitaryWeight,
                ExpansionWeight = @ExpansionWeight,
                UpdatedAt = @UpdatedAt
            WHERE EmpirePriorityID = @EmpirePriorityID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.EmpirePriorities(EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt)
            VALUES (@EmpirePriorityID, @EmpireID, @IndustryWeight, @ResearchWeight, @MilitaryWeight, @ExpansionWeight, @UpdatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@EmpirePriorityID", item.EmpirePriorityId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddInt(command, "@IndustryWeight", item.IndustryWeight);
            AddInt(command, "@ResearchWeight", item.ResearchWeight);
            AddInt(command, "@MilitaryWeight", item.MilitaryWeight);
            AddInt(command, "@ExpansionWeight", item.ExpansionWeight);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void UpsertEmpireMetric(SqlConnection connection, SqlTransaction transaction, EmpireMetric item) =>
        Execute(connection, transaction, """
            UPDATE dbo.EmpireMetrics
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                TickNumber = @TickNumber,
                Rank = @Rank,
                IsWinner = @IsWinner,
                MapControlPercent = @MapControlPercent,
                TotalEffectivePresence = @TotalEffectivePresence,
                ActiveShipCount = @ActiveShipCount,
                CreatedAt = @CreatedAt
            WHERE EmpireMetricID = @EmpireMetricID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.EmpireMetrics(EmpireMetricID, CycleID, EmpireID, TickNumber, Rank, IsWinner, MapControlPercent, TotalEffectivePresence, ActiveShipCount, CreatedAt)
            VALUES (@EmpireMetricID, @CycleID, @EmpireID, @TickNumber, @Rank, @IsWinner, @MapControlPercent, @TotalEffectivePresence, @ActiveShipCount, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@EmpireMetricID", item.EmpireMetricId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddInt(command, "@Rank", item.Rank);
            AddBool(command, "@IsWinner", item.IsWinner);
            AddDecimal(command, "@MapControlPercent", item.MapControlPercent, scale: 6);
            AddDecimal(command, "@TotalEffectivePresence", item.TotalEffectivePresence);
            AddInt(command, "@ActiveShipCount", item.ActiveShipCount);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertCycleRanking(SqlConnection connection, SqlTransaction transaction, CycleRanking item) =>
        Execute(connection, transaction, """
            UPDATE dbo.CycleRankings
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                Rank = @Rank,
                IsWinner = @IsWinner,
                MapControlPercent = @MapControlPercent,
                TotalEffectivePresence = @TotalEffectivePresence,
                ActiveShipCount = @ActiveShipCount,
                CutoffTickNumber = @CutoffTickNumber,
                CutoffAt = @CutoffAt
            WHERE CycleRankingID = @CycleRankingID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.CycleRankings(CycleRankingID, CycleID, EmpireID, Rank, IsWinner, MapControlPercent, TotalEffectivePresence, ActiveShipCount, CutoffTickNumber, CutoffAt)
            VALUES (@CycleRankingID, @CycleID, @EmpireID, @Rank, @IsWinner, @MapControlPercent, @TotalEffectivePresence, @ActiveShipCount, @CutoffTickNumber, @CutoffAt);
            END;
            """, command =>
        {
            AddGuid(command, "@CycleRankingID", item.CycleRankingId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddInt(command, "@Rank", item.Rank);
            AddBool(command, "@IsWinner", item.IsWinner);
            AddDecimal(command, "@MapControlPercent", item.MapControlPercent, scale: 6);
            AddDecimal(command, "@TotalEffectivePresence", item.TotalEffectivePresence);
            AddInt(command, "@ActiveShipCount", item.ActiveShipCount);
            AddInt(command, "@CutoffTickNumber", item.CutoffTickNumber);
            AddDateTimeOffset(command, "@CutoffAt", item.CutoffAt);
        });

    private static void UpsertSystemLink(SqlConnection connection, SqlTransaction transaction, SystemLink item) =>
        Execute(connection, transaction, """
            UPDATE dbo.SystemLinks
            SET CycleID = @CycleID,
                SystemAID = @SystemAID,
                SystemBID = @SystemBID,
                Distance = @Distance,
                TravelTicks = @TravelTicks
            WHERE SystemLinkID = @SystemLinkID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
            VALUES (@SystemLinkID, @CycleID, @SystemAID, @SystemBID, @Distance, @TravelTicks);
            END;
            """, command =>
        {
            AddGuid(command, "@SystemLinkID", item.SystemLinkId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@SystemAID", item.SystemAId);
            AddGuid(command, "@SystemBID", item.SystemBId);
            AddDecimal(command, "@Distance", item.Distance);
            AddInt(command, "@TravelTicks", item.TravelTicks);
        });

    private static void UpsertAdmiral(SqlConnection connection, SqlTransaction transaction, Admiral item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Admirals
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                AdmiralName = @AdmiralName,
                ReputationScore = @ReputationScore,
                Status = @Status,
                CreatedAt = @CreatedAt,
                UpdatedAt = @UpdatedAt
            WHERE AdmiralID = @AdmiralID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Admirals(AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt)
            VALUES (@AdmiralID, @CycleID, @EmpireID, @AdmiralName, @ReputationScore, @Status, @CreatedAt, @UpdatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@AdmiralID", item.AdmiralId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddString(command, "@AdmiralName", item.AdmiralName, 120);
            AddInt(command, "@ReputationScore", item.ReputationScore);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void UpsertFleet(SqlConnection connection, SqlTransaction transaction, Fleet item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Fleets
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                FactionID = @FactionID,
                AdmiralID = @AdmiralID,
                FleetName = @FleetName,
                CurrentSystemID = @CurrentSystemID,
                DestinationSystemID = @DestinationSystemID,
                DepartureTickNumber = @DepartureTickNumber,
                ArrivalTickNumber = @ArrivalTickNumber,
                ShipCount = @ShipCount,
                Status = @Status,
                CreatedAt = @CreatedAt
            WHERE FleetID = @FleetID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES (@FleetID, @CycleID, @EmpireID, @FactionID, @AdmiralID, @FleetName, @CurrentSystemID, @DestinationSystemID, @DepartureTickNumber, @ArrivalTickNumber, @ShipCount, @Status, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FleetID", item.FleetId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@EmpireID", item.EmpireId == Guid.Empty ? null : item.EmpireId);
            AddGuid(command, "@FactionID", item.FactionId);
            AddNullableGuid(command, "@AdmiralID", item.AdmiralId);
            AddString(command, "@FleetName", item.FleetName, 120);
            AddGuid(command, "@CurrentSystemID", item.CurrentSystemId);
            AddNullableGuid(command, "@DestinationSystemID", item.DestinationSystemId);
            AddNullableInt(command, "@DepartureTickNumber", item.DepartureTickNumber);
            AddNullableInt(command, "@ArrivalTickNumber", item.ArrivalTickNumber);
            AddInt(command, "@ShipCount", item.ShipCount);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertFleetOrders(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<FleetOrder> items)
    {
        var orders = items.ToArray();
        foreach (var item in orders)
        {
            UpsertFleetOrder(connection, transaction, item, supersededByOrderId: null);
        }

        foreach (var item in orders.Where(item => item.SupersededByOrderId.HasValue))
        {
            Execute(
                connection,
                transaction,
                "UPDATE dbo.FleetOrders SET SupersededByOrderID = @SupersededByOrderID WHERE FleetOrderID = @FleetOrderID;",
                command =>
                {
                    AddGuid(command, "@FleetOrderID", item.FleetOrderId);
                    AddGuid(command, "@SupersededByOrderID", item.SupersededByOrderId!.Value);
                });
        }
    }

    private static void UpsertFleetOrder(
        SqlConnection connection,
        SqlTransaction transaction,
        FleetOrder item,
        Guid? supersededByOrderId) =>
        Execute(connection, transaction, """
            UPDATE dbo.FleetOrders
            SET CycleID = @CycleID,
                FleetID = @FleetID,
                OrderType = @OrderType,
                TargetSystemID = @TargetSystemID,
                TargetEmpireID = @TargetEmpireID,
                TargetFactionID = @TargetFactionID,
                SubmitTick = @SubmitTick,
                ExecuteAfterTick = @ExecuteAfterTick,
                ProcessedTick = @ProcessedTick,
                Status = @Status,
                CommandSource = @CommandSource,
                SealedTick = @SealedTick,
                SealedAt = @SealedAt,
                RejectionReason = @RejectionReason,
                SupersededByOrderID = @SupersededByOrderID,
                CreatedAt = @CreatedAt
            WHERE FleetOrderID = @FleetOrderID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.FleetOrders(FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, TargetFactionID, SubmitTick, ExecuteAfterTick, ProcessedTick, Status, CommandSource, SealedTick, SealedAt, RejectionReason, SupersededByOrderID, CreatedAt)
            VALUES (@FleetOrderID, @CycleID, @FleetID, @OrderType, @TargetSystemID, @TargetEmpireID, @TargetFactionID, @SubmitTick, @ExecuteAfterTick, @ProcessedTick, @Status, @CommandSource, @SealedTick, @SealedAt, @RejectionReason, @SupersededByOrderID, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FleetOrderID", item.FleetOrderId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@FleetID", item.FleetId);
            AddString(command, "@OrderType", item.OrderType.ToString(), 32);
            AddNullableGuid(command, "@TargetSystemID", item.TargetSystemId);
            AddNullableGuid(command, "@TargetEmpireID", item.TargetEmpireId);
            AddNullableGuid(command, "@TargetFactionID", item.TargetFactionId);
            AddInt(command, "@SubmitTick", item.SubmitTick);
            AddInt(command, "@ExecuteAfterTick", item.ExecuteAfterTick);
            AddNullableInt(command, "@ProcessedTick", item.ProcessedTick);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddString(command, "@CommandSource", item.CommandSource.ToString(), 32);
            AddNullableInt(command, "@SealedTick", item.SealedTick);
            AddNullableDateTimeOffset(command, "@SealedAt", item.SealedAt);
            AddNullableString(command, "@RejectionReason", item.RejectionReason, 512);
            AddNullableGuid(command, "@SupersededByOrderID", supersededByOrderId);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertShipConstruction(SqlConnection connection, SqlTransaction transaction, ShipConstruction item) =>
        Execute(connection, transaction, """
            UPDATE dbo.ShipConstructions
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                ShipCount = @ShipCount,
                IndustrySpent = @IndustrySpent,
                StartedTick = @StartedTick,
                CompleteAfterTick = @CompleteAfterTick,
                CompletedTick = @CompletedTick,
                Status = @Status,
                CreatedAt = @CreatedAt,
                UpdatedAt = @UpdatedAt
            WHERE ShipConstructionID = @ShipConstructionID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.ShipConstructions(ShipConstructionID, CycleID, EmpireID, ShipCount, IndustrySpent, StartedTick, CompleteAfterTick, CompletedTick, Status, CreatedAt, UpdatedAt)
            VALUES (@ShipConstructionID, @CycleID, @EmpireID, @ShipCount, @IndustrySpent, @StartedTick, @CompleteAfterTick, @CompletedTick, @Status, @CreatedAt, @UpdatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@ShipConstructionID", item.ShipConstructionId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddInt(command, "@ShipCount", item.ShipCount);
            AddDecimal(command, "@IndustrySpent", item.IndustrySpent);
            AddInt(command, "@StartedTick", item.StartedTick);
            AddInt(command, "@CompleteAfterTick", item.CompleteAfterTick);
            AddNullableInt(command, "@CompletedTick", item.CompletedTick);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void UpsertTickLog(SqlConnection connection, SqlTransaction transaction, TickLog item) =>
        Execute(connection, transaction, """
            UPDATE dbo.TickLogs
            SET CycleID = @CycleID,
                TickNumber = @TickNumber,
                StartedAt = @StartedAt,
                CompletedAt = @CompletedAt,
                Status = @Status,
                DiagnosticLog = @DiagnosticLog
            WHERE TickLogID = @TickLogID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.TickLogs(TickLogID, CycleID, TickNumber, StartedAt, CompletedAt, Status, DiagnosticLog)
            VALUES (@TickLogID, @CycleID, @TickNumber, @StartedAt, @CompletedAt, @Status, @DiagnosticLog);
            END;
            """, command =>
        {
            AddGuid(command, "@TickLogID", item.TickLogId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddDateTimeOffset(command, "@StartedAt", item.StartedAt);
            AddNullableDateTimeOffset(command, "@CompletedAt", item.CompletedAt);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddMaxString(command, "@DiagnosticLog", item.DiagnosticLog);
        });

    private static void UpsertEvent(SqlConnection connection, SqlTransaction transaction, EventRecord item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Events
            SET CycleID = @CycleID,
                TickNumber = @TickNumber,
                EventType = @EventType,
                SystemID = @SystemID,
                EmpireID = @EmpireID,
                FactionID = @FactionID,
                Severity = @Severity,
                FactJson = @FactJson,
                DisplayText = @DisplayText,
                CreatedAt = @CreatedAt
            WHERE EventID = @EventID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES (@EventID, @CycleID, @TickNumber, @EventType, @SystemID, @EmpireID, @FactionID, @Severity, @FactJson, @DisplayText, @CreatedAt);
            END;
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

    private static void UpsertBattleRecord(SqlConnection connection, SqlTransaction transaction, BattleRecord item) =>
        Execute(connection, transaction, """
            UPDATE dbo.BattleRecords
            SET CycleID = @CycleID,
                TickNumber = @TickNumber,
                SystemID = @SystemID,
                AttackerEmpireID = @AttackerEmpireID,
                DefenderEmpireID = @DefenderEmpireID,
                AttackerFactionID = @AttackerFactionID,
                DefenderFactionID = @DefenderFactionID,
                AttackerFleetIDs = @AttackerFleetIDs,
                DefenderFleetIDs = @DefenderFleetIDs,
                AttackerShipsBefore = @AttackerShipsBefore,
                DefenderShipsBefore = @DefenderShipsBefore,
                AttackerLosses = @AttackerLosses,
                DefenderLosses = @DefenderLosses,
                Outcome = @Outcome,
                FactJson = @FactJson,
                CreatedAt = @CreatedAt
            WHERE BattleID = @BattleID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.BattleRecords(BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFactionID, DefenderFactionID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore, AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES (@BattleID, @CycleID, @TickNumber, @SystemID, @AttackerEmpireID, @DefenderEmpireID, @AttackerFactionID, @DefenderFactionID, @AttackerFleetIDs, @DefenderFleetIDs, @AttackerShipsBefore, @DefenderShipsBefore, @AttackerLosses, @DefenderLosses, @Outcome, @FactJson, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@BattleID", item.BattleId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddGuid(command, "@SystemID", item.SystemId);
            AddNullableGuid(command, "@AttackerEmpireID", item.AttackerEmpireId == Guid.Empty ? null : item.AttackerEmpireId);
            AddNullableGuid(command, "@DefenderEmpireID", item.DefenderEmpireId == Guid.Empty ? null : item.DefenderEmpireId);
            AddGuid(command, "@AttackerFactionID", item.AttackerFactionId);
            AddGuid(command, "@DefenderFactionID", item.DefenderFactionId);
            AddMaxString(command, "@AttackerFleetIDs", item.AttackerFleetIds);
            AddMaxString(command, "@DefenderFleetIDs", item.DefenderFleetIds);
            AddInt(command, "@AttackerShipsBefore", item.AttackerShipsBefore);
            AddInt(command, "@DefenderShipsBefore", item.DefenderShipsBefore);
            AddInt(command, "@AttackerLosses", item.AttackerLosses);
            AddInt(command, "@DefenderLosses", item.DefenderLosses);
            AddString(command, "@Outcome", item.Outcome.ToString(), 64);
            AddMaxString(command, "@FactJson", item.FactJson);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertBattleFleetParticipant(
        SqlConnection connection,
        SqlTransaction transaction,
        BattleFleetParticipant item) =>
        Execute(connection, transaction, """
            UPDATE dbo.BattleFleetParticipants
            SET CycleID = @CycleID,
                Side = @Side
            WHERE BattleID = @BattleID
                AND FleetID = @FleetID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.BattleFleetParticipants(BattleID, CycleID, FleetID, Side)
            VALUES (@BattleID, @CycleID, @FleetID, @Side);
            END;
            """, command =>
        {
            AddGuid(command, "@BattleID", item.BattleId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@FleetID", item.FleetId);
            AddString(command, "@Side", item.Side.ToString(), 16);
        });

    private static void UpsertAdmiralBattleHistory(SqlConnection connection, SqlTransaction transaction, AdmiralBattleHistory item) =>
        Execute(connection, transaction, """
            UPDATE dbo.AdmiralBattleHistories
            SET CycleID = @CycleID,
                AdmiralID = @AdmiralID,
                BattleID = @BattleID,
                SystemID = @SystemID,
                FleetID = @FleetID,
                Role = @Role,
                Outcome = @Outcome,
                ShipsCommandedBefore = @ShipsCommandedBefore,
                ShipsLost = @ShipsLost,
                ReputationChange = @ReputationChange,
                ReputationScoreAfter = @ReputationScoreAfter,
                AdmiralStatusAfter = @AdmiralStatusAfter,
                IsFamousSystemAssociation = @IsFamousSystemAssociation,
                CreatedAt = @CreatedAt
            WHERE AdmiralBattleHistoryID = @AdmiralBattleHistoryID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.AdmiralBattleHistories(AdmiralBattleHistoryID, CycleID, AdmiralID, BattleID, SystemID, FleetID, Role, Outcome, ShipsCommandedBefore, ShipsLost, ReputationChange, ReputationScoreAfter, AdmiralStatusAfter, IsFamousSystemAssociation, CreatedAt)
            VALUES (@AdmiralBattleHistoryID, @CycleID, @AdmiralID, @BattleID, @SystemID, @FleetID, @Role, @Outcome, @ShipsCommandedBefore, @ShipsLost, @ReputationChange, @ReputationScoreAfter, @AdmiralStatusAfter, @IsFamousSystemAssociation, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@AdmiralBattleHistoryID", item.AdmiralBattleHistoryId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@AdmiralID", item.AdmiralId);
            AddGuid(command, "@BattleID", item.BattleId);
            AddGuid(command, "@SystemID", item.SystemId);
            AddGuid(command, "@FleetID", item.FleetId);
            AddString(command, "@Role", item.Role.ToString(), 32);
            AddString(command, "@Outcome", item.Outcome.ToString(), 32);
            AddInt(command, "@ShipsCommandedBefore", item.ShipsCommandedBefore);
            AddInt(command, "@ShipsLost", item.ShipsLost);
            AddInt(command, "@ReputationChange", item.ReputationChange);
            AddInt(command, "@ReputationScoreAfter", item.ReputationScoreAfter);
            AddString(command, "@AdmiralStatusAfter", item.AdmiralStatusAfter.ToString(), 32);
            AddBool(command, "@IsFamousSystemAssociation", item.IsFamousSystemAssociation);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertCycleMajorEvent(SqlConnection connection, SqlTransaction transaction, CycleMajorEvent item) =>
        Execute(connection, transaction, """
            UPDATE dbo.CycleMajorEvents
            SET CycleID = @CycleID,
                SourceBattleID = @SourceBattleID,
                SystemID = @SystemID,
                EventType = @EventType,
                TickNumber = @TickNumber,
                SelectionRank = @SelectionRank,
                ImportanceScore = @ImportanceScore,
                TotalLosses = @TotalLosses,
                Summary = @Summary,
                FactJson = @FactJson,
                CreatedAt = @CreatedAt
            WHERE CycleMajorEventID = @CycleMajorEventID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.CycleMajorEvents(CycleMajorEventID, CycleID, SourceBattleID, SystemID, EventType, TickNumber, SelectionRank, ImportanceScore, TotalLosses, Summary, FactJson, CreatedAt)
            VALUES (@CycleMajorEventID, @CycleID, @SourceBattleID, @SystemID, @EventType, @TickNumber, @SelectionRank, @ImportanceScore, @TotalLosses, @Summary, @FactJson, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@CycleMajorEventID", item.CycleMajorEventId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@SourceBattleID", item.SourceBattleId);
            AddNullableGuid(command, "@SystemID", item.SystemId);
            AddString(command, "@EventType", item.EventType.ToString(), 64);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddInt(command, "@SelectionRank", item.SelectionRank);
            AddInt(command, "@ImportanceScore", item.ImportanceScore);
            AddInt(command, "@TotalLosses", item.TotalLosses);
            AddString(command, "@Summary", item.Summary, 2048);
            AddMaxString(command, "@FactJson", item.FactJson);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertSystemHistoricalSignal(SqlConnection connection, SqlTransaction transaction, SystemHistoricalSignal item) =>
        Execute(connection, transaction, """
            UPDATE dbo.SystemHistoricalSignals
            SET CycleID = @CycleID,
                SystemID = @SystemID,
                SignalType = @SignalType,
                SourceBattleID = @SourceBattleID,
                BattleCount = @BattleCount,
                TotalLosses = @TotalLosses,
                LargestBattleLosses = @LargestBattleLosses,
                HostedCycleLargestBattle = @HostedCycleLargestBattle,
                HistoricalSignificanceIncrease = @HistoricalSignificanceIncrease,
                HistoricalSignificanceAfter = @HistoricalSignificanceAfter,
                Summary = @Summary,
                FactJson = @FactJson,
                CreatedAt = @CreatedAt
            WHERE SystemHistoricalSignalID = @SystemHistoricalSignalID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.SystemHistoricalSignals(SystemHistoricalSignalID, CycleID, SystemID, SignalType, SourceBattleID, BattleCount, TotalLosses, LargestBattleLosses, HostedCycleLargestBattle, HistoricalSignificanceIncrease, HistoricalSignificanceAfter, Summary, FactJson, CreatedAt)
            VALUES (@SystemHistoricalSignalID, @CycleID, @SystemID, @SignalType, @SourceBattleID, @BattleCount, @TotalLosses, @LargestBattleLosses, @HostedCycleLargestBattle, @HistoricalSignificanceIncrease, @HistoricalSignificanceAfter, @Summary, @FactJson, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@SystemHistoricalSignalID", item.SystemHistoricalSignalId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@SystemID", item.SystemId);
            AddString(command, "@SignalType", item.SignalType.ToString(), 64);
            AddNullableGuid(command, "@SourceBattleID", item.SourceBattleId);
            AddInt(command, "@BattleCount", item.BattleCount);
            AddInt(command, "@TotalLosses", item.TotalLosses);
            AddInt(command, "@LargestBattleLosses", item.LargestBattleLosses);
            AddBool(command, "@HostedCycleLargestBattle", item.HostedCycleLargestBattle);
            AddInt(command, "@HistoricalSignificanceIncrease", item.HistoricalSignificanceIncrease);
            AddInt(command, "@HistoricalSignificanceAfter", item.HistoricalSignificanceAfter);
            AddString(command, "@Summary", item.Summary, 2048);
            AddMaxString(command, "@FactJson", item.FactJson);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertColonialOutpost(SqlConnection connection, SqlTransaction transaction, ColonialOutpost item) =>
        Execute(connection, transaction, """
            UPDATE dbo.ColonialOutposts
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
                SystemID = @SystemID,
                EstablishedTick = @EstablishedTick,
                CreatedAt = @CreatedAt
            WHERE ColonialOutpostID = @ColonialOutpostID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.ColonialOutposts(ColonialOutpostID, CycleID, EmpireID, SystemID, EstablishedTick, CreatedAt)
            VALUES (@ColonialOutpostID, @CycleID, @EmpireID, @SystemID, @EstablishedTick, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@ColonialOutpostID", item.ColonialOutpostId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddGuid(command, "@SystemID", item.SystemId);
            AddInt(command, "@EstablishedTick", item.EstablishedTick);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertDiplomaticRelationship(SqlConnection connection, SqlTransaction transaction, DiplomaticRelationship item) =>
        Execute(connection, transaction, """
            UPDATE dbo.DiplomaticRelationships
            SET CycleID = @CycleID,
                FirstEmpireID = @FirstEmpireID,
                SecondEmpireID = @SecondEmpireID,
                State = @State,
                UpdatedTick = @UpdatedTick,
                UpdatedAt = @UpdatedAt
            WHERE DiplomaticRelationshipID = @DiplomaticRelationshipID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.DiplomaticRelationships(DiplomaticRelationshipID, CycleID, FirstEmpireID, SecondEmpireID, State, UpdatedTick, UpdatedAt)
            VALUES (@DiplomaticRelationshipID, @CycleID, @FirstEmpireID, @SecondEmpireID, @State, @UpdatedTick, @UpdatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@DiplomaticRelationshipID", item.DiplomaticRelationshipId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@FirstEmpireID", item.FirstEmpireId);
            AddGuid(command, "@SecondEmpireID", item.SecondEmpireId);
            AddString(command, "@State", item.State.ToString(), 32);
            AddInt(command, "@UpdatedTick", item.UpdatedTick);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void UpsertChronicleEntry(SqlConnection connection, SqlTransaction transaction, ChronicleEntry item) =>
        Execute(connection, transaction, """
            UPDATE dbo.ChronicleEntries
            SET SourceEventID = @SourceEventID,
                SourceBattleID = @SourceBattleID,
                CycleID = @CycleID,
                SystemID = @SystemID,
                Title = @Title,
                EntryType = @EntryType,
                ImportanceScore = @ImportanceScore,
                FactualSummary = @FactualSummary,
                NarrativeText = @NarrativeText,
                NarrativeStatus = @NarrativeStatus,
                NarrativeContextJson = @NarrativeContextJson,
                NarrativeGeneratedAt = @NarrativeGeneratedAt,
                NarrativeFailureReason = @NarrativeFailureReason,
                CreatedAt = @CreatedAt
            WHERE ChronicleEntryID = @ChronicleEntryID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.ChronicleEntries(ChronicleEntryID, SourceEventID, SourceBattleID, CycleID, SystemID, Title, EntryType, ImportanceScore, FactualSummary, NarrativeText, NarrativeStatus, NarrativeContextJson, NarrativeGeneratedAt, NarrativeFailureReason, CreatedAt)
            VALUES (@ChronicleEntryID, @SourceEventID, @SourceBattleID, @CycleID, @SystemID, @Title, @EntryType, @ImportanceScore, @FactualSummary, @NarrativeText, @NarrativeStatus, @NarrativeContextJson, @NarrativeGeneratedAt, @NarrativeFailureReason, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@ChronicleEntryID", item.ChronicleEntryId);
            AddNullableGuid(command, "@SourceEventID", item.SourceEventId);
            AddNullableGuid(command, "@SourceBattleID", item.SourceBattleId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddNullableGuid(command, "@SystemID", item.SystemId);
            AddString(command, "@Title", item.Title, 180);
            AddString(command, "@EntryType", item.EntryType.ToString(), 64);
            AddInt(command, "@ImportanceScore", item.ImportanceScore);
            AddString(command, "@FactualSummary", item.FactualSummary, 2048);
            AddMaxString(command, "@NarrativeText", item.NarrativeText);
            AddString(command, "@NarrativeStatus", item.NarrativeStatus.ToString(), 32);
            AddMaxString(command, "@NarrativeContextJson", item.NarrativeContextJson);
            AddNullableDateTimeOffset(command, "@NarrativeGeneratedAt", item.NarrativeGeneratedAt);
            AddNullableString(command, "@NarrativeFailureReason", item.NarrativeFailureReason, 1024);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static IReadOnlyList<Cycle> OrderCyclesForPersistence(IEnumerable<Cycle> cycles)
    {
        var cyclesById = cycles.ToDictionary(cycle => cycle.CycleId);
        var visitStates = new Dictionary<Guid, CycleVisitState>();
        var ordered = new List<Cycle>(cyclesById.Count);

        foreach (var cycle in cyclesById.Values
                     .OrderBy(item => item.StartAt)
                     .ThenBy(item => item.CreatedAt)
                     .ThenBy(item => item.CycleId))
        {
            Visit(cycle);
        }

        return ordered;

        void Visit(Cycle cycle)
        {
            if (visitStates.TryGetValue(cycle.CycleId, out var visitState))
            {
                if (visitState == CycleVisitState.Visiting)
                {
                    throw new InvalidOperationException(
                        $"Cycle lineage contains a predecessor loop involving Cycle {cycle.CycleId}.");
                }

                return;
            }

            visitStates[cycle.CycleId] = CycleVisitState.Visiting;
            if (cycle.PreviousCycleId is Guid previousCycleId
                && cyclesById.TryGetValue(previousCycleId, out var previousCycle))
            {
                Visit(previousCycle);
            }

            visitStates[cycle.CycleId] = CycleVisitState.Visited;
            ordered.Add(cycle);
        }
    }

    private static void DeleteRowsMissingFromState(
        SqlConnection connection,
        SqlTransaction transaction,
        GameState state,
        bool deleteGameLifecycleEvents = false)
    {
        DeleteMissingBattleFleetParticipants(connection, transaction, state.BattleFleetParticipants);
        DeleteMissingRows(connection, transaction, "dbo.AdminRoleAuditRecords", "AdminRoleAuditRecordID", state.AdminRoleAuditRecords.Select(item => item.AdminRoleAuditRecordId));
        if (deleteGameLifecycleEvents)
        {
            Execute(
                connection,
                transaction,
                """
                IF OBJECT_ID(N'dbo.TutorialCompletions', N'U') IS NOT NULL
                    DELETE FROM dbo.TutorialCompletions;
                IF OBJECT_ID(N'dbo.TutorialSkips', N'U') IS NOT NULL
                    DELETE FROM dbo.TutorialSkips;
                IF OBJECT_ID(N'dbo.TutorialAcknowledgements', N'U') IS NOT NULL
                    DELETE FROM dbo.TutorialAcknowledgements;
                IF OBJECT_ID(N'dbo.TutorialRuns', N'U') IS NOT NULL
                    DELETE FROM dbo.TutorialRuns;
                """,
                _ => { });
            DeleteMissingRows(connection, transaction, "dbo.GameLifecycleEvents", "GameLifecycleEventID", state.GameLifecycleEvents.Select(item => item.GameLifecycleEventId));
        }

        DeleteMissingRows(connection, transaction, "dbo.MatchParticipants", "MatchParticipantID", state.MatchParticipants.Select(item => item.MatchParticipantId));
        DeleteMissingRows(connection, transaction, "dbo.GameEnrolments", "GameEnrolmentID", state.GameEnrolments.Select(item => item.GameEnrolmentId));
        DeleteMissingRows(connection, transaction, "dbo.AdmiralBattleHistories", "AdmiralBattleHistoryID", state.AdmiralBattleHistories.Select(item => item.AdmiralBattleHistoryId));
        DeleteMissingRows(connection, transaction, "dbo.DiplomaticRelationships", "DiplomaticRelationshipID", state.DiplomaticRelationships.Select(item => item.DiplomaticRelationshipId));
        DeleteMissingRows(connection, transaction, "dbo.ColonialOutposts", "ColonialOutpostID", state.ColonialOutposts.Select(item => item.ColonialOutpostId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireDoctrineUnlocks", "EmpireDoctrineUnlockID", state.EmpireDoctrineUnlocks.Select(item => item.EmpireDoctrineUnlockId));
        DeleteMissingRows(connection, transaction, "dbo.SystemHistoricalSignals", "SystemHistoricalSignalID", state.SystemHistoricalSignals.Select(item => item.SystemHistoricalSignalId));
        DeleteMissingRows(connection, transaction, "dbo.CycleMajorEvents", "CycleMajorEventID", state.CycleMajorEvents.Select(item => item.CycleMajorEventId));
        DeleteMissingRows(connection, transaction, "dbo.ChronicleEntries", "ChronicleEntryID", state.ChronicleEntries.Select(item => item.ChronicleEntryId));
        DeleteMissingRows(connection, transaction, "dbo.BattleRecords", "BattleID", state.BattleRecords.Select(item => item.BattleId));
        DeleteMissingRows(connection, transaction, "dbo.Events", "EventID", state.Events.Select(item => item.EventId));
        DeleteMissingRows(connection, transaction, "dbo.TickLogs", "TickLogID", state.TickLogs.Select(item => item.TickLogId));
        DeleteMissingRows(connection, transaction, "dbo.ShipConstructions", "ShipConstructionID", state.ShipConstructions.Select(item => item.ShipConstructionId));
        ClearReferencesToMissingFleetOrders(connection, transaction, state.FleetOrders.Select(item => item.FleetOrderId));
        DeleteMissingRows(connection, transaction, "dbo.FleetOrders", "FleetOrderID", state.FleetOrders.Select(item => item.FleetOrderId));
        DeleteMissingRows(connection, transaction, "dbo.Fleets", "FleetID", state.Fleets.Select(item => item.FleetId));
        DeleteMissingRows(connection, transaction, "dbo.Admirals", "AdmiralID", state.Admirals.Select(item => item.AdmiralId));
        DeleteMissingRows(connection, transaction, "dbo.SystemLinks", "SystemLinkID", state.SystemLinks.Select(item => item.SystemLinkId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireMetrics", "EmpireMetricID", state.EmpireMetrics.Select(item => item.EmpireMetricId));
        DeleteMissingRows(connection, transaction, "dbo.CycleRankings", "CycleRankingID", state.CycleRankings.Select(item => item.CycleRankingId));
        DeleteMissingRows(connection, transaction, "dbo.EmpirePriorities", "EmpirePriorityID", state.EmpirePriorities.Select(item => item.EmpirePriorityId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireResources", "EmpireResourceID", state.EmpireResources.Select(item => item.EmpireResourceId));
        DeleteMissingRows(connection, transaction, "dbo.Factions", "FactionID", state.Factions.Select(item => item.FactionId));
        DeleteMissingRows(connection, transaction, "dbo.Empires", "EmpireID", state.Empires.Select(item => item.EmpireId));
        DeleteMissingRows(connection, transaction, "dbo.Systems", "SystemID", state.Systems.Select(item => item.SystemId));
        ClearReferencesToMissingSectors(connection, transaction, state.Sectors.Select(item => item.SectorId));
        DeleteMissingRows(connection, transaction, "dbo.GalaxySectors", "SectorID", state.Sectors.Select(item => item.SectorId));
        ClearReferencesToMissingCycles(connection, transaction, state.Cycles.Select(item => item.CycleId));
        DeleteMissingRows(connection, transaction, "dbo.Cycles", "CycleID", state.Cycles.Select(item => item.CycleId));
        DeleteMissingRows(connection, transaction, "dbo.CycleConfigurations", "CycleConfigurationID", state.CycleConfigurations.Select(item => item.CycleConfigurationId));
        DeleteMissingRows(connection, transaction, "dbo.Games", "GameID", state.Games.Select(item => item.GameId));
        DeleteMissingRows(connection, transaction, "dbo.Players", "PlayerID", state.Players.Select(item => item.PlayerId));
    }

    private enum CycleVisitState
    {
        Visiting,
        Visited
    }

    private static void DeleteMissingBattleFleetParticipants(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<BattleFleetParticipant> retainedParticipants)
    {
        var retainedKeys = retainedParticipants
            .Select(item => (item.BattleId, item.FleetId))
            .ToHashSet();
        var existingKeys = new List<(Guid BattleId, Guid FleetId)>();
        using (var command = CreateCommand(
                   connection,
                   transaction,
                   "SELECT BattleID, FleetID FROM dbo.BattleFleetParticipants;"))
        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                existingKeys.Add((reader.GetGuid(0), reader.GetGuid(1)));
            }
        }

        foreach (var key in existingKeys.Where(key => !retainedKeys.Contains(key)))
        {
            Execute(
                connection,
                transaction,
                "DELETE FROM dbo.BattleFleetParticipants WHERE BattleID = @BattleID AND FleetID = @FleetID;",
                command =>
                {
                    AddGuid(command, "@BattleID", key.BattleId);
                    AddGuid(command, "@FleetID", key.FleetId);
                });
        }
    }

    private static void ClearReferencesToMissingSectors(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<Guid> retainedSectorIds)
    {
        var retainedIdSet = retainedSectorIds.ToHashSet();
        foreach (var existingId in ReadIds(connection, transaction, "dbo.GalaxySectors", "SectorID"))
        {
            if (retainedIdSet.Contains(existingId))
            {
                continue;
            }

            Execute(
                connection,
                transaction,
                "UPDATE dbo.Systems SET SectorID = NULL WHERE SectorID = @SectorID;",
                command => AddGuid(command, "@SectorID", existingId));
        }
    }

    private static void ClearReferencesToMissingFleetOrders(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<Guid> retainedFleetOrderIds)
    {
        var retainedIdSet = retainedFleetOrderIds.ToHashSet();
        foreach (var existingId in ReadIds(connection, transaction, "dbo.FleetOrders", "FleetOrderID"))
        {
            if (retainedIdSet.Contains(existingId))
            {
                continue;
            }

            Execute(
                connection,
                transaction,
                "UPDATE dbo.FleetOrders SET SupersededByOrderID = NULL WHERE SupersededByOrderID = @FleetOrderID;",
                command => AddGuid(command, "@FleetOrderID", existingId));
        }
    }

    private static void ClearReferencesToMissingCycles(
        SqlConnection connection,
        SqlTransaction transaction,
        IEnumerable<Guid> retainedCycleIds)
    {
        var retainedIdSet = retainedCycleIds.ToHashSet();
        foreach (var existingId in ReadIds(connection, transaction, "dbo.Cycles", "CycleID"))
        {
            if (retainedIdSet.Contains(existingId))
            {
                continue;
            }

            Execute(
                connection,
                transaction,
                "UPDATE dbo.Cycles SET PreviousCycleID = NULL WHERE PreviousCycleID = @CycleID;",
                command => AddGuid(command, "@CycleID", existingId));
        }
    }

    private static void DeleteMissingRows(
        SqlConnection connection,
        SqlTransaction transaction,
        string tableName,
        string keyColumnName,
        IEnumerable<Guid> retainedIds)
    {
        var retainedIdSet = retainedIds.ToHashSet();
        foreach (var existingId in ReadIds(connection, transaction, tableName, keyColumnName))
        {
            if (!retainedIdSet.Contains(existingId))
            {
                DeleteRow(connection, transaction, tableName, keyColumnName, existingId);
            }
        }
    }

    private static IReadOnlyList<Guid> ReadIds(SqlConnection connection, SqlTransaction transaction, string tableName, string keyColumnName)
    {
        using var command = CreateCommand(connection, transaction, $"SELECT {keyColumnName} FROM {tableName};");
        using var reader = command.ExecuteReader();
        var ids = new List<Guid>();
        while (reader.Read())
        {
            ids.Add(reader.GetGuid(0));
        }

        return ids;
    }

    private static void DeleteRow(SqlConnection connection, SqlTransaction transaction, string tableName, string keyColumnName, Guid id)
    {
        using var command = CreateCommand(connection, transaction, $"DELETE FROM {tableName} WHERE {keyColumnName} = @Id;");
        AddGuid(command, "@Id", id);
        command.ExecuteNonQuery();
    }

    private static Guid? ReadActiveCycleIdUnsafe(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT TOP (2) CycleID
            FROM dbo.Cycles
            WHERE Status = @Status
            ORDER BY StartAt DESC, CycleID;
            """);
        AddString(command, "@Status", CycleStatus.Active.ToString(), 32);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
        {
            return null;
        }

        var cycleId = reader.GetGuid(0);
        if (reader.Read())
        {
            throw new InvalidOperationException(
                "Legacy active-Cycle selection is ambiguous because more than one active Cycle exists. Use an explicit Cycle identifier.");
        }

        return cycleId;
    }

    private static DateTimeOffset? ReadLastCompletedTickAtUnsafe(
        SqlConnection connection,
        SqlTransaction transaction,
        Guid cycleId)
    {
        using var command = CreateCommand(connection, transaction, """
            SELECT MAX(CompletedAt)
            FROM dbo.TickLogs
            WHERE CycleID = @CycleID
                AND Status = @Status
                AND CompletedAt IS NOT NULL;
            """);
        AddGuid(command, "@CycleID", cycleId);
        AddString(command, "@Status", TickLogStatus.Completed.ToString(), 32);

        var value = command.ExecuteScalar();
        return value is DateTimeOffset completedAt ? completedAt : null;
    }

    private static bool AnyCycleExistsUnsafe(SqlConnection connection, SqlTransaction transaction)
    {
        using var command = CreateCommand(connection, transaction, "SELECT TOP (1) 1 FROM dbo.Cycles;");
        return command.ExecuteScalar() is not null;
    }

    private static List<T> ReadRows<T>(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        Func<SqlDataReader, T> read)
    {
        using var command = CreateCommand(connection, transaction, sql);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static List<T> ReadRows<T>(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        Action<SqlCommand> configure,
        Func<SqlDataReader, T> read)
    {
        using var command = CreateCommand(connection, transaction, sql);
        configure(command);
        using var reader = command.ExecuteReader();
        var rows = new List<T>();
        while (reader.Read())
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private static void Execute(
        SqlConnection connection,
        SqlTransaction transaction,
        string sql,
        Action<SqlCommand> configure)
    {
        using var command = CreateCommand(connection, transaction, sql);
        configure(command);
        command.ExecuteNonQuery();
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlTransaction transaction, string commandText) =>
        new(commandText, connection, transaction);

    private static Guid GetGuid(SqlDataReader reader, string columnName) =>
        reader.GetGuid(reader.GetOrdinal(columnName));

    private static Guid? GetNullableGuid(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetGuid(ordinal);
    }

    private static int GetInt(SqlDataReader reader, string columnName) =>
        reader.GetInt32(reader.GetOrdinal(columnName));

    private static int? GetNullableInt(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static decimal GetDecimal(SqlDataReader reader, string columnName) =>
        reader.GetDecimal(reader.GetOrdinal(columnName));

    private static bool GetBool(SqlDataReader reader, string columnName) =>
        reader.GetBoolean(reader.GetOrdinal(columnName));

    private static string GetString(SqlDataReader reader, string columnName) =>
        reader.GetString(reader.GetOrdinal(columnName));

    private static string? GetNullableString(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static byte[] GetBytes(SqlDataReader reader, string columnName) =>
        reader.GetFieldValue<byte[]>(reader.GetOrdinal(columnName));

    private static DateTimeOffset GetDateTimeOffset(SqlDataReader reader, string columnName) =>
        reader.GetDateTimeOffset(reader.GetOrdinal(columnName));

    private static DateTimeOffset? GetNullableDateTimeOffset(SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTimeOffset(ordinal);
    }

    private static TEnum GetEnum<TEnum>(SqlDataReader reader, string columnName)
        where TEnum : struct, Enum
    {
        var value = GetString(reader, columnName);
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result)
            ? result
            : throw new InvalidOperationException($"Value '{value}' is not valid for {typeof(TEnum).Name}.");
    }

    private static TEnum? GetNullableEnum<TEnum>(SqlDataReader reader, string columnName)
        where TEnum : struct, Enum
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        var value = reader.GetString(ordinal);
        return Enum.TryParse<TEnum>(value, ignoreCase: true, out var result)
            ? result
            : throw new InvalidOperationException($"Value '{value}' is not valid for {typeof(TEnum).Name}.");
    }

    private static void AddGuid(SqlCommand command, string name, Guid value) =>
        command.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value;

    private static void AddNullableGuid(SqlCommand command, string name, Guid? value) =>
        command.Parameters.Add(name, SqlDbType.UniqueIdentifier).Value = value.HasValue ? value.Value : DBNull.Value;

    private static void AddInt(SqlCommand command, string name, int value) =>
        command.Parameters.Add(name, SqlDbType.Int).Value = value;

    private static void AddNullableInt(SqlCommand command, string name, int? value) =>
        command.Parameters.Add(name, SqlDbType.Int).Value = value.HasValue ? value.Value : DBNull.Value;

    private static void AddDecimal(SqlCommand command, string name, decimal value, byte scale = 2)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = scale;
        parameter.Value = value;
    }

    private static void AddBool(SqlCommand command, string name, bool value) =>
        command.Parameters.Add(name, SqlDbType.Bit).Value = value;

    private static void AddString(SqlCommand command, string name, string value, int length) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = value;

    private static void AddNullableString(SqlCommand command, string name, string? value, int length) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, length).Value = string.IsNullOrEmpty(value) ? DBNull.Value : value;

    private static void AddMaxString(SqlCommand command, string name, string value) =>
        command.Parameters.Add(name, SqlDbType.NVarChar, -1).Value = value;

    private static void AddDateTimeOffset(SqlCommand command, string name, DateTimeOffset value) =>
        command.Parameters.Add(name, SqlDbType.DateTimeOffset).Value = value;

    private static void AddNullableDateTimeOffset(SqlCommand command, string name, DateTimeOffset? value) =>
        command.Parameters.Add(name, SqlDbType.DateTimeOffset).Value = value.HasValue ? value.Value : DBNull.Value;

    private static string BuildDescription(string connectionString)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            var database = string.IsNullOrWhiteSpace(builder.InitialCatalog) ? "default database" : builder.InitialCatalog;
            return $"SQL Server {builder.DataSource}/{database}";
        }
        catch (ArgumentException)
        {
            return "SQL Server";
        }
    }
}
