using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed class SqlServerGameStateStore : IGameStateStore
{
    private const string ApplicationLockName = "Cycles.GameState";
    private const string TickLockPrefix = "Cycles.Tick.";
    private const int ApplicationLockTimeoutMilliseconds = 5000;

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
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }

    private static void AcquireApplicationLock(SqlConnection connection, SqlTransaction transaction) =>
        AcquireSqlApplicationLock(connection, transaction, ApplicationLockName);

    private static void AcquireCycleTickLock(SqlConnection connection, SqlTransaction transaction, Guid cycleId) =>
        AcquireSqlApplicationLock(connection, transaction, $"{TickLockPrefix}{cycleId:D}");

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

    private static GameState LoadUnsafe(SqlConnection connection, SqlTransaction transaction) =>
        new()
        {
            Players = ReadRows(connection, transaction, "SELECT * FROM dbo.Players", ReadPlayer),
            AdminRoleAuditRecords = ReadRows(connection, transaction, "SELECT * FROM dbo.AdminRoleAuditRecords", ReadAdminRoleAuditRecord),
            Cycles = ReadRows(connection, transaction, "SELECT * FROM dbo.Cycles", ReadCycle),
            Systems = ReadRows(connection, transaction, "SELECT * FROM dbo.Systems", ReadSystem),
            Empires = ReadRows(connection, transaction, "SELECT * FROM dbo.Empires", ReadEmpire),
            EmpireResources = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireResources", ReadEmpireResource),
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
            ChronicleEntries = ReadRows(connection, transaction, "SELECT * FROM dbo.ChronicleEntries", ReadChronicleEntry)
        };

    private static GameState LoadFocusedTickStateUnsafe(SqlConnection connection, SqlTransaction transaction, Guid cycleId) =>
        new()
        {
            Cycles = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Cycles WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadCycle),
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

    private static void SaveUnsafe(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        DeleteRowsMissingFromState(connection, transaction, state);

        foreach (var item in state.Players)
        {
            UpsertPlayer(connection, transaction, item);
        }

        foreach (var item in state.AdminRoleAuditRecords)
        {
            UpsertAdminRoleAuditRecord(connection, transaction, item);
        }

        foreach (var item in state.Cycles)
        {
            UpsertCycle(connection, transaction, item);
        }

        foreach (var item in state.Systems)
        {
            UpsertSystem(connection, transaction, item);
        }

        foreach (var item in state.Empires)
        {
            UpsertEmpire(connection, transaction, item);
        }

        foreach (var item in state.EmpireResources)
        {
            UpsertEmpireResource(connection, transaction, item);
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

        foreach (var item in state.FleetOrders)
        {
            UpsertFleetOrder(connection, transaction, item);
        }

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

        foreach (var item in state.FleetOrders.Where(order => order.CycleId == cycleId))
        {
            UpsertFleetOrder(connection, transaction, item);
        }

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

    private static Cycle ReadCycle(SqlDataReader reader) => new()
    {
        CycleId = GetGuid(reader, "CycleID"),
        Name = GetString(reader, "Name"),
        StartAt = GetDateTimeOffset(reader, "StartAt"),
        EndAt = GetDateTimeOffset(reader, "EndAt"),
        TickLengthMinutes = GetInt(reader, "TickLengthMinutes"),
        CurrentTickNumber = GetInt(reader, "CurrentTickNumber"),
        Status = GetEnum<CycleStatus>(reader, "Status"),
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static GalaxySystem ReadSystem(SqlDataReader reader) => new()
    {
        SystemId = GetGuid(reader, "SystemID"),
        CycleId = GetGuid(reader, "CycleID"),
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
        EmpireId = GetGuid(reader, "EmpireID"),
        AdmiralId = GetNullableGuid(reader, "AdmiralID"),
        FleetName = GetString(reader, "FleetName"),
        CurrentSystemId = GetGuid(reader, "CurrentSystemID"),
        DestinationSystemId = GetNullableGuid(reader, "DestinationSystemID"),
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
        SubmitTick = GetInt(reader, "SubmitTick"),
        ExecuteAfterTick = GetInt(reader, "ExecuteAfterTick"),
        ProcessedTick = GetNullableInt(reader, "ProcessedTick"),
        Status = GetEnum<FleetOrderStatus>(reader, "Status"),
        RejectionReason = GetNullableString(reader, "RejectionReason"),
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
        AttackerEmpireId = GetGuid(reader, "AttackerEmpireID"),
        DefenderEmpireId = GetGuid(reader, "DefenderEmpireID"),
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
                Role = @Role,
                CreatedAt = @CreatedAt,
                LastLoginAt = @LastLoginAt,
                Status = @Status
            WHERE PlayerID = @PlayerID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, Role, CreatedAt, LastLoginAt, Status)
            VALUES (@PlayerID, @Username, @Email, @PasswordHash, @ExternalIssuer, @ExternalSubject, @Role, @CreatedAt, @LastLoginAt, @Status);
            END;
            """, command =>
        {
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddString(command, "@Username", item.Username, 80);
            AddString(command, "@Email", item.Email, 256);
            AddString(command, "@PasswordHash", item.PasswordHash, 512);
            AddString(command, "@ExternalIssuer", item.ExternalIssuer, 256);
            AddString(command, "@ExternalSubject", item.ExternalSubject, 256);
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

    private static void UpsertCycle(SqlConnection connection, SqlTransaction transaction, Cycle item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Cycles
            SET Name = @Name,
                StartAt = @StartAt,
                EndAt = @EndAt,
                TickLengthMinutes = @TickLengthMinutes,
                CurrentTickNumber = @CurrentTickNumber,
                Status = @Status,
                CreatedAt = @CreatedAt
            WHERE CycleID = @CycleID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, @Name, @StartAt, @EndAt, @TickLengthMinutes, @CurrentTickNumber, @Status, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@CycleID", item.CycleId);
            AddString(command, "@Name", item.Name, 120);
            AddDateTimeOffset(command, "@StartAt", item.StartAt);
            AddDateTimeOffset(command, "@EndAt", item.EndAt);
            AddInt(command, "@TickLengthMinutes", item.TickLengthMinutes);
            AddInt(command, "@CurrentTickNumber", item.CurrentTickNumber);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertSystem(SqlConnection connection, SqlTransaction transaction, GalaxySystem item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Systems
            SET CycleID = @CycleID,
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
            INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES (@SystemID, @CycleID, @SystemName, @X, @Y, @IndustryOutput, @ResearchOutput, @PopulationOutput, @StrategicValue, @HistoricalSignificance, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@SystemID", item.SystemId);
            AddGuid(command, "@CycleID", item.CycleId);
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
                AdmiralID = @AdmiralID,
                FleetName = @FleetName,
                CurrentSystemID = @CurrentSystemID,
                DestinationSystemID = @DestinationSystemID,
                ArrivalTickNumber = @ArrivalTickNumber,
                ShipCount = @ShipCount,
                Status = @Status,
                CreatedAt = @CreatedAt
            WHERE FleetID = @FleetID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES (@FleetID, @CycleID, @EmpireID, @AdmiralID, @FleetName, @CurrentSystemID, @DestinationSystemID, @ArrivalTickNumber, @ShipCount, @Status, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FleetID", item.FleetId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddNullableGuid(command, "@AdmiralID", item.AdmiralId);
            AddString(command, "@FleetName", item.FleetName, 120);
            AddGuid(command, "@CurrentSystemID", item.CurrentSystemId);
            AddNullableGuid(command, "@DestinationSystemID", item.DestinationSystemId);
            AddNullableInt(command, "@ArrivalTickNumber", item.ArrivalTickNumber);
            AddInt(command, "@ShipCount", item.ShipCount);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void UpsertFleetOrder(SqlConnection connection, SqlTransaction transaction, FleetOrder item) =>
        Execute(connection, transaction, """
            UPDATE dbo.FleetOrders
            SET CycleID = @CycleID,
                FleetID = @FleetID,
                OrderType = @OrderType,
                TargetSystemID = @TargetSystemID,
                TargetEmpireID = @TargetEmpireID,
                SubmitTick = @SubmitTick,
                ExecuteAfterTick = @ExecuteAfterTick,
                ProcessedTick = @ProcessedTick,
                Status = @Status,
                RejectionReason = @RejectionReason,
                CreatedAt = @CreatedAt
            WHERE FleetOrderID = @FleetOrderID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.FleetOrders(FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, SubmitTick, ExecuteAfterTick, ProcessedTick, Status, RejectionReason, CreatedAt)
            VALUES (@FleetOrderID, @CycleID, @FleetID, @OrderType, @TargetSystemID, @TargetEmpireID, @SubmitTick, @ExecuteAfterTick, @ProcessedTick, @Status, @RejectionReason, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FleetOrderID", item.FleetOrderId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@FleetID", item.FleetId);
            AddString(command, "@OrderType", item.OrderType.ToString(), 32);
            AddNullableGuid(command, "@TargetSystemID", item.TargetSystemId);
            AddNullableGuid(command, "@TargetEmpireID", item.TargetEmpireId);
            AddInt(command, "@SubmitTick", item.SubmitTick);
            AddInt(command, "@ExecuteAfterTick", item.ExecuteAfterTick);
            AddNullableInt(command, "@ProcessedTick", item.ProcessedTick);
            AddString(command, "@Status", item.Status.ToString(), 32);
            AddNullableString(command, "@RejectionReason", item.RejectionReason, 512);
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
                Severity = @Severity,
                FactJson = @FactJson,
                DisplayText = @DisplayText,
                CreatedAt = @CreatedAt
            WHERE EventID = @EventID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES (@EventID, @CycleID, @TickNumber, @EventType, @SystemID, @EmpireID, @Severity, @FactJson, @DisplayText, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@EventID", item.EventId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddString(command, "@EventType", item.EventType.ToString(), 64);
            AddNullableGuid(command, "@SystemID", item.SystemId);
            AddNullableGuid(command, "@EmpireID", item.EmpireId);
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
            INSERT INTO dbo.BattleRecords(BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore, AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES (@BattleID, @CycleID, @TickNumber, @SystemID, @AttackerEmpireID, @DefenderEmpireID, @AttackerFleetIDs, @DefenderFleetIDs, @AttackerShipsBefore, @DefenderShipsBefore, @AttackerLosses, @DefenderLosses, @Outcome, @FactJson, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@BattleID", item.BattleId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddInt(command, "@TickNumber", item.TickNumber);
            AddGuid(command, "@SystemID", item.SystemId);
            AddGuid(command, "@AttackerEmpireID", item.AttackerEmpireId);
            AddGuid(command, "@DefenderEmpireID", item.DefenderEmpireId);
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

    private static void DeleteRowsMissingFromState(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        DeleteMissingRows(connection, transaction, "dbo.AdminRoleAuditRecords", "AdminRoleAuditRecordID", state.AdminRoleAuditRecords.Select(item => item.AdminRoleAuditRecordId));
        DeleteMissingRows(connection, transaction, "dbo.AdmiralBattleHistories", "AdmiralBattleHistoryID", state.AdmiralBattleHistories.Select(item => item.AdmiralBattleHistoryId));
        DeleteMissingRows(connection, transaction, "dbo.DiplomaticRelationships", "DiplomaticRelationshipID", state.DiplomaticRelationships.Select(item => item.DiplomaticRelationshipId));
        DeleteMissingRows(connection, transaction, "dbo.ColonialOutposts", "ColonialOutpostID", state.ColonialOutposts.Select(item => item.ColonialOutpostId));
        DeleteMissingRows(connection, transaction, "dbo.SystemHistoricalSignals", "SystemHistoricalSignalID", state.SystemHistoricalSignals.Select(item => item.SystemHistoricalSignalId));
        DeleteMissingRows(connection, transaction, "dbo.CycleMajorEvents", "CycleMajorEventID", state.CycleMajorEvents.Select(item => item.CycleMajorEventId));
        DeleteMissingRows(connection, transaction, "dbo.ChronicleEntries", "ChronicleEntryID", state.ChronicleEntries.Select(item => item.ChronicleEntryId));
        DeleteMissingRows(connection, transaction, "dbo.BattleRecords", "BattleID", state.BattleRecords.Select(item => item.BattleId));
        DeleteMissingRows(connection, transaction, "dbo.Events", "EventID", state.Events.Select(item => item.EventId));
        DeleteMissingRows(connection, transaction, "dbo.TickLogs", "TickLogID", state.TickLogs.Select(item => item.TickLogId));
        DeleteMissingRows(connection, transaction, "dbo.ShipConstructions", "ShipConstructionID", state.ShipConstructions.Select(item => item.ShipConstructionId));
        DeleteMissingRows(connection, transaction, "dbo.FleetOrders", "FleetOrderID", state.FleetOrders.Select(item => item.FleetOrderId));
        DeleteMissingRows(connection, transaction, "dbo.Fleets", "FleetID", state.Fleets.Select(item => item.FleetId));
        DeleteMissingRows(connection, transaction, "dbo.Admirals", "AdmiralID", state.Admirals.Select(item => item.AdmiralId));
        DeleteMissingRows(connection, transaction, "dbo.SystemLinks", "SystemLinkID", state.SystemLinks.Select(item => item.SystemLinkId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireMetrics", "EmpireMetricID", state.EmpireMetrics.Select(item => item.EmpireMetricId));
        DeleteMissingRows(connection, transaction, "dbo.CycleRankings", "CycleRankingID", state.CycleRankings.Select(item => item.CycleRankingId));
        DeleteMissingRows(connection, transaction, "dbo.EmpirePriorities", "EmpirePriorityID", state.EmpirePriorities.Select(item => item.EmpirePriorityId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireResources", "EmpireResourceID", state.EmpireResources.Select(item => item.EmpireResourceId));
        DeleteMissingRows(connection, transaction, "dbo.Empires", "EmpireID", state.Empires.Select(item => item.EmpireId));
        DeleteMissingRows(connection, transaction, "dbo.Systems", "SystemID", state.Systems.Select(item => item.SystemId));
        DeleteMissingRows(connection, transaction, "dbo.Cycles", "CycleID", state.Cycles.Select(item => item.CycleId));
        DeleteMissingRows(connection, transaction, "dbo.Players", "PlayerID", state.Players.Select(item => item.PlayerId));
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
            SELECT TOP (1) CycleID
            FROM dbo.Cycles
            WHERE Status = @Status
            ORDER BY StartAt DESC;
            """);
        AddString(command, "@Status", CycleStatus.Active.ToString(), 32);

        var value = command.ExecuteScalar();
        return value is Guid cycleId ? cycleId : null;
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
