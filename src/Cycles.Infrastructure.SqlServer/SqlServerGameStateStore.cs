using Cycles.Core;
using Microsoft.Data.SqlClient;
using System.Data;

namespace Cycles.Infrastructure.SqlServer;

public sealed class SqlServerGameStateStore : IGameStateStore
{
    private const string ApplicationLockName = "Cycles.GameState";

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
        if (state.Cycles.Count == 0)
        {
            state = _seedFactory();
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

        var result = update(state);
        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
        return result;
    }

    public TickResult RunTick(DateTimeOffset now) =>
        RunTick(cycleId: null, now);

    public TickResult RunTick(Guid cycleId, DateTimeOffset now) =>
        RunTick((Guid?)cycleId, now);

    public void Replace(GameState state)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
    }

    private TickResult RunTick(Guid? cycleId, DateTimeOffset now)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        var activeCycleId = cycleId ?? ReadActiveCycleIdUnsafe(connection, transaction);
        GameState state;
        var createdState = false;

        if (activeCycleId.HasValue)
        {
            state = LoadTickStateUnsafe(connection, transaction, activeCycleId.Value);
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

    private static void AcquireApplicationLock(SqlConnection connection, SqlTransaction transaction)
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
        AddString(command, "@Resource", ApplicationLockName, 255);
        AddInt(command, "@LockTimeout", 5000);

        var result = Convert.ToInt32(command.ExecuteScalar(), null);
        if (result < 0)
        {
            throw new TimeoutException($"Could not acquire SQL Server application lock '{ApplicationLockName}'. Result code: {result}.");
        }
    }

    private static GameState LoadUnsafe(SqlConnection connection, SqlTransaction transaction) =>
        new()
        {
            Players = ReadRows(connection, transaction, "SELECT * FROM dbo.Players", ReadPlayer),
            Cycles = ReadRows(connection, transaction, "SELECT * FROM dbo.Cycles", ReadCycle),
            Systems = ReadRows(connection, transaction, "SELECT * FROM dbo.Systems", ReadSystem),
            Empires = ReadRows(connection, transaction, "SELECT * FROM dbo.Empires", ReadEmpire),
            EmpireResources = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireResources", ReadEmpireResource),
            EmpirePriorities = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpirePriorities", ReadEmpirePriority),
            EmpireMetrics = ReadRows(connection, transaction, "SELECT * FROM dbo.EmpireMetrics", ReadEmpireMetric),
            SystemLinks = ReadRows(connection, transaction, "SELECT * FROM dbo.SystemLinks", ReadSystemLink),
            Fleets = ReadRows(connection, transaction, "SELECT * FROM dbo.Fleets", ReadFleet),
            FleetOrders = ReadRows(connection, transaction, "SELECT * FROM dbo.FleetOrders", ReadFleetOrder),
            ShipConstructions = ReadRows(connection, transaction, "SELECT * FROM dbo.ShipConstructions", ReadShipConstruction),
            TickLogs = ReadRows(connection, transaction, "SELECT * FROM dbo.TickLogs", ReadTickLog),
            Events = ReadRows(connection, transaction, "SELECT * FROM dbo.Events", ReadEvent),
            BattleRecords = ReadRows(connection, transaction, "SELECT * FROM dbo.BattleRecords", ReadBattleRecord),
            ChronicleEntries = ReadRows(connection, transaction, "SELECT * FROM dbo.ChronicleEntries", ReadChronicleEntry)
        };

    private static GameState LoadTickStateUnsafe(SqlConnection connection, SqlTransaction transaction, Guid cycleId) =>
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
            SystemLinks = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.SystemLinks WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadSystemLink),
            Fleets = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Fleets WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadFleet),
            FleetOrders = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.FleetOrders WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadFleetOrder),
            ShipConstructions = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.ShipConstructions WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadShipConstruction),
            TickLogs = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.TickLogs WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadTickLog),
            Events = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.Events WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadEvent),
            BattleRecords = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.BattleRecords WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadBattleRecord),
            ChronicleEntries = ReadRows(
                connection,
                transaction,
                "SELECT * FROM dbo.ChronicleEntries WHERE CycleID = @CycleID",
                command => AddGuid(command, "@CycleID", cycleId),
                ReadChronicleEntry)
        };

    private static void SaveUnsafe(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        DeleteRowsMissingFromState(connection, transaction, state);

        foreach (var item in state.Players)
        {
            UpsertPlayer(connection, transaction, item);
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

        foreach (var item in state.SystemLinks)
        {
            UpsertSystemLink(connection, transaction, item);
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

        foreach (var item in state.Fleets.Where(fleet => fleet.CycleId == cycleId))
        {
            UpsertFleet(connection, transaction, item);
        }

        foreach (var item in state.FleetOrders.Where(order => order.CycleId == cycleId))
        {
            UpsertFleetOrder(connection, transaction, item);
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
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt"),
        LastLoginAt = GetNullableDateTimeOffset(reader, "LastLoginAt"),
        Status = GetEnum<PlayerStatus>(reader, "Status")
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
        CreatedAt = GetDateTimeOffset(reader, "CreatedAt")
    };

    private static void UpsertPlayer(SqlConnection connection, SqlTransaction transaction, Player item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Players
            SET Username = @Username,
                Email = @Email,
                PasswordHash = @PasswordHash,
                CreatedAt = @CreatedAt,
                LastLoginAt = @LastLoginAt,
                Status = @Status
            WHERE PlayerID = @PlayerID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, CreatedAt, LastLoginAt, Status)
            VALUES (@PlayerID, @Username, @Email, @PasswordHash, @CreatedAt, @LastLoginAt, @Status);
            END;
            """, command =>
        {
            AddGuid(command, "@PlayerID", item.PlayerId);
            AddString(command, "@Username", item.Username, 80);
            AddString(command, "@Email", item.Email, 256);
            AddString(command, "@PasswordHash", item.PasswordHash, 512);
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
            AddNullableDateTimeOffset(command, "@LastLoginAt", item.LastLoginAt);
            AddString(command, "@Status", item.Status.ToString(), 32);
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

    private static void UpsertFleet(SqlConnection connection, SqlTransaction transaction, Fleet item) =>
        Execute(connection, transaction, """
            UPDATE dbo.Fleets
            SET CycleID = @CycleID,
                EmpireID = @EmpireID,
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
            INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES (@FleetID, @CycleID, @EmpireID, @FleetName, @CurrentSystemID, @DestinationSystemID, @ArrivalTickNumber, @ShipCount, @Status, @CreatedAt);
            END;
            """, command =>
        {
            AddGuid(command, "@FleetID", item.FleetId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@EmpireID", item.EmpireId);
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
                CreatedAt = @CreatedAt
            WHERE ChronicleEntryID = @ChronicleEntryID;

            IF @@ROWCOUNT = 0
            BEGIN
            INSERT INTO dbo.ChronicleEntries(ChronicleEntryID, SourceEventID, SourceBattleID, CycleID, SystemID, Title, EntryType, ImportanceScore, FactualSummary, NarrativeText, CreatedAt)
            VALUES (@ChronicleEntryID, @SourceEventID, @SourceBattleID, @CycleID, @SystemID, @Title, @EntryType, @ImportanceScore, @FactualSummary, @NarrativeText, @CreatedAt);
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
            AddDateTimeOffset(command, "@CreatedAt", item.CreatedAt);
        });

    private static void DeleteRowsMissingFromState(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        DeleteMissingRows(connection, transaction, "dbo.ChronicleEntries", "ChronicleEntryID", state.ChronicleEntries.Select(item => item.ChronicleEntryId));
        DeleteMissingRows(connection, transaction, "dbo.BattleRecords", "BattleID", state.BattleRecords.Select(item => item.BattleId));
        DeleteMissingRows(connection, transaction, "dbo.Events", "EventID", state.Events.Select(item => item.EventId));
        DeleteMissingRows(connection, transaction, "dbo.TickLogs", "TickLogID", state.TickLogs.Select(item => item.TickLogId));
        DeleteMissingRows(connection, transaction, "dbo.ShipConstructions", "ShipConstructionID", state.ShipConstructions.Select(item => item.ShipConstructionId));
        DeleteMissingRows(connection, transaction, "dbo.FleetOrders", "FleetOrderID", state.FleetOrders.Select(item => item.FleetOrderId));
        DeleteMissingRows(connection, transaction, "dbo.Fleets", "FleetID", state.Fleets.Select(item => item.FleetId));
        DeleteMissingRows(connection, transaction, "dbo.SystemLinks", "SystemLinkID", state.SystemLinks.Select(item => item.SystemLinkId));
        DeleteMissingRows(connection, transaction, "dbo.EmpireMetrics", "EmpireMetricID", state.EmpireMetrics.Select(item => item.EmpireMetricId));
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
