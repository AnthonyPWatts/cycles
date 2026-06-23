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

    public void Replace(GameState state)
    {
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction(IsolationLevel.ReadCommitted);
        AcquireApplicationLock(connection, transaction);

        SaveUnsafe(connection, transaction, state);
        transaction.Commit();
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
            SystemLinks = ReadRows(connection, transaction, "SELECT * FROM dbo.SystemLinks", ReadSystemLink),
            Fleets = ReadRows(connection, transaction, "SELECT * FROM dbo.Fleets", ReadFleet),
            FleetOrders = ReadRows(connection, transaction, "SELECT * FROM dbo.FleetOrders", ReadFleetOrder),
            TickLogs = ReadRows(connection, transaction, "SELECT * FROM dbo.TickLogs", ReadTickLog),
            Events = ReadRows(connection, transaction, "SELECT * FROM dbo.Events", ReadEvent),
            BattleRecords = ReadRows(connection, transaction, "SELECT * FROM dbo.BattleRecords", ReadBattleRecord),
            ChronicleEntries = ReadRows(connection, transaction, "SELECT * FROM dbo.ChronicleEntries", ReadChronicleEntry)
        };

    private static void SaveUnsafe(SqlConnection connection, SqlTransaction transaction, GameState state)
    {
        ExecuteNonQuery(connection, transaction, """
            DELETE FROM dbo.ChronicleEntries;
            DELETE FROM dbo.BattleRecords;
            DELETE FROM dbo.Events;
            DELETE FROM dbo.TickLogs;
            DELETE FROM dbo.FleetOrders;
            DELETE FROM dbo.Fleets;
            DELETE FROM dbo.SystemLinks;
            DELETE FROM dbo.EmpirePriorities;
            DELETE FROM dbo.EmpireResources;
            DELETE FROM dbo.Empires;
            DELETE FROM dbo.Systems;
            DELETE FROM dbo.Cycles;
            DELETE FROM dbo.Players;
            """);

        foreach (var item in state.Players)
        {
            InsertPlayer(connection, transaction, item);
        }

        foreach (var item in state.Cycles)
        {
            InsertCycle(connection, transaction, item);
        }

        foreach (var item in state.Systems)
        {
            InsertSystem(connection, transaction, item);
        }

        foreach (var item in state.Empires)
        {
            InsertEmpire(connection, transaction, item);
        }

        foreach (var item in state.EmpireResources)
        {
            InsertEmpireResource(connection, transaction, item);
        }

        foreach (var item in state.EmpirePriorities)
        {
            InsertEmpirePriority(connection, transaction, item);
        }

        foreach (var item in state.SystemLinks)
        {
            InsertSystemLink(connection, transaction, item);
        }

        foreach (var item in state.Fleets)
        {
            InsertFleet(connection, transaction, item);
        }

        foreach (var item in state.FleetOrders)
        {
            InsertFleetOrder(connection, transaction, item);
        }

        foreach (var item in state.TickLogs)
        {
            InsertTickLog(connection, transaction, item);
        }

        foreach (var item in state.Events)
        {
            InsertEvent(connection, transaction, item);
        }

        foreach (var item in state.BattleRecords)
        {
            InsertBattleRecord(connection, transaction, item);
        }

        foreach (var item in state.ChronicleEntries)
        {
            InsertChronicleEntry(connection, transaction, item);
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

    private static void InsertPlayer(SqlConnection connection, SqlTransaction transaction, Player item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, CreatedAt, LastLoginAt, Status)
            VALUES (@PlayerID, @Username, @Email, @PasswordHash, @CreatedAt, @LastLoginAt, @Status);
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

    private static void InsertCycle(SqlConnection connection, SqlTransaction transaction, Cycle item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
            VALUES (@CycleID, @Name, @StartAt, @EndAt, @TickLengthMinutes, @CurrentTickNumber, @Status, @CreatedAt);
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

    private static void InsertSystem(SqlConnection connection, SqlTransaction transaction, GalaxySystem item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
            VALUES (@SystemID, @CycleID, @SystemName, @X, @Y, @IndustryOutput, @ResearchOutput, @PopulationOutput, @StrategicValue, @HistoricalSignificance, @CreatedAt);
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

    private static void InsertEmpire(SqlConnection connection, SqlTransaction transaction, Empire item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
            VALUES (@EmpireID, @CycleID, @PlayerID, @EmpireName, @HomeSystemID, @CreatedAt, @Status);
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

    private static void InsertEmpireResource(SqlConnection connection, SqlTransaction transaction, EmpireResource item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.EmpireResources(EmpireResourceID, EmpireID, Industry, Research, Population, UpdatedAt)
            VALUES (@EmpireResourceID, @EmpireID, @Industry, @Research, @Population, @UpdatedAt);
            """, command =>
        {
            AddGuid(command, "@EmpireResourceID", item.EmpireResourceId);
            AddGuid(command, "@EmpireID", item.EmpireId);
            AddDecimal(command, "@Industry", item.Industry);
            AddDecimal(command, "@Research", item.Research);
            AddDecimal(command, "@Population", item.Population);
            AddDateTimeOffset(command, "@UpdatedAt", item.UpdatedAt);
        });

    private static void InsertEmpirePriority(SqlConnection connection, SqlTransaction transaction, EmpirePriority item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.EmpirePriorities(EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt)
            VALUES (@EmpirePriorityID, @EmpireID, @IndustryWeight, @ResearchWeight, @MilitaryWeight, @ExpansionWeight, @UpdatedAt);
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

    private static void InsertSystemLink(SqlConnection connection, SqlTransaction transaction, SystemLink item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
            VALUES (@SystemLinkID, @CycleID, @SystemAID, @SystemBID, @Distance, @TravelTicks);
            """, command =>
        {
            AddGuid(command, "@SystemLinkID", item.SystemLinkId);
            AddGuid(command, "@CycleID", item.CycleId);
            AddGuid(command, "@SystemAID", item.SystemAId);
            AddGuid(command, "@SystemBID", item.SystemBId);
            AddDecimal(command, "@Distance", item.Distance);
            AddInt(command, "@TravelTicks", item.TravelTicks);
        });

    private static void InsertFleet(SqlConnection connection, SqlTransaction transaction, Fleet item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt)
            VALUES (@FleetID, @CycleID, @EmpireID, @FleetName, @CurrentSystemID, @DestinationSystemID, @ArrivalTickNumber, @ShipCount, @Status, @CreatedAt);
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

    private static void InsertFleetOrder(SqlConnection connection, SqlTransaction transaction, FleetOrder item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.FleetOrders(FleetOrderID, CycleID, FleetID, OrderType, TargetSystemID, TargetEmpireID, SubmitTick, ExecuteAfterTick, ProcessedTick, Status, RejectionReason, CreatedAt)
            VALUES (@FleetOrderID, @CycleID, @FleetID, @OrderType, @TargetSystemID, @TargetEmpireID, @SubmitTick, @ExecuteAfterTick, @ProcessedTick, @Status, @RejectionReason, @CreatedAt);
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

    private static void InsertTickLog(SqlConnection connection, SqlTransaction transaction, TickLog item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.TickLogs(TickLogID, CycleID, TickNumber, StartedAt, CompletedAt, Status, DiagnosticLog)
            VALUES (@TickLogID, @CycleID, @TickNumber, @StartedAt, @CompletedAt, @Status, @DiagnosticLog);
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

    private static void InsertEvent(SqlConnection connection, SqlTransaction transaction, EventRecord item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
            VALUES (@EventID, @CycleID, @TickNumber, @EventType, @SystemID, @EmpireID, @Severity, @FactJson, @DisplayText, @CreatedAt);
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

    private static void InsertBattleRecord(SqlConnection connection, SqlTransaction transaction, BattleRecord item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.BattleRecords(BattleID, CycleID, TickNumber, SystemID, AttackerEmpireID, DefenderEmpireID, AttackerFleetIDs, DefenderFleetIDs, AttackerShipsBefore, DefenderShipsBefore, AttackerLosses, DefenderLosses, Outcome, FactJson, CreatedAt)
            VALUES (@BattleID, @CycleID, @TickNumber, @SystemID, @AttackerEmpireID, @DefenderEmpireID, @AttackerFleetIDs, @DefenderFleetIDs, @AttackerShipsBefore, @DefenderShipsBefore, @AttackerLosses, @DefenderLosses, @Outcome, @FactJson, @CreatedAt);
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

    private static void InsertChronicleEntry(SqlConnection connection, SqlTransaction transaction, ChronicleEntry item) =>
        Execute(connection, transaction, """
            INSERT INTO dbo.ChronicleEntries(ChronicleEntryID, SourceEventID, SourceBattleID, CycleID, SystemID, Title, EntryType, ImportanceScore, FactualSummary, NarrativeText, CreatedAt)
            VALUES (@ChronicleEntryID, @SourceEventID, @SourceBattleID, @CycleID, @SystemID, @Title, @EntryType, @ImportanceScore, @FactualSummary, @NarrativeText, @CreatedAt);
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

    private static void ExecuteNonQuery(SqlConnection connection, SqlTransaction transaction, string sql)
    {
        using var command = CreateCommand(connection, transaction, sql);
        command.ExecuteNonQuery();
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

    private static void AddDecimal(SqlCommand command, string name, decimal value)
    {
        var parameter = command.Parameters.Add(name, SqlDbType.Decimal);
        parameter.Precision = 18;
        parameter.Scale = 2;
        parameter.Value = value;
    }

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
