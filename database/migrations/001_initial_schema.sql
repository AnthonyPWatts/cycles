SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.SchemaMigrations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigrations
    (
        MigrationID NVARCHAR(128) NOT NULL CONSTRAINT PK_SchemaMigrations PRIMARY KEY,
        Description NVARCHAR(256) NOT NULL,
        AppliedAt DATETIMEOFFSET NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAt DEFAULT SYSDATETIMEOFFSET()
    );
END;

IF OBJECT_ID(N'dbo.Players', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Players
    (
        PlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Players PRIMARY KEY,
        Username NVARCHAR(80) NOT NULL,
        Email NVARCHAR(256) NOT NULL,
        PasswordHash NVARCHAR(512) NOT NULL,
        Role NVARCHAR(32) NOT NULL CONSTRAINT DF_Players_Role DEFAULT N'Player',
        CreatedAt DATETIMEOFFSET NOT NULL,
        LastLoginAt DATETIMEOFFSET NULL,
        Status NVARCHAR(32) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Cycles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Cycles
    (
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Cycles PRIMARY KEY,
        Name NVARCHAR(120) NOT NULL,
        StartAt DATETIMEOFFSET NOT NULL,
        EndAt DATETIMEOFFSET NOT NULL,
        TickLengthMinutes INT NOT NULL,
        CurrentTickNumber INT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Systems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Systems
    (
        SystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Systems PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Systems_Cycles REFERENCES dbo.Cycles(CycleID),
        SystemName NVARCHAR(120) NOT NULL,
        X INT NOT NULL,
        Y INT NOT NULL,
        IndustryOutput DECIMAL(18, 2) NOT NULL,
        ResearchOutput DECIMAL(18, 2) NOT NULL,
        PopulationOutput DECIMAL(18, 2) NOT NULL,
        StrategicValue INT NOT NULL,
        HistoricalSignificance INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Empires', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Empires
    (
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Empires PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Empires_Cycles REFERENCES dbo.Cycles(CycleID),
        PlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Empires_Players REFERENCES dbo.Players(PlayerID),
        EmpireName NVARCHAR(120) NOT NULL,
        HomeSystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Empires_HomeSystems REFERENCES dbo.Systems(SystemID),
        CreatedAt DATETIMEOFFSET NOT NULL,
        Status NVARCHAR(32) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.EmpireResources', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmpireResources
    (
        EmpireResourceID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmpireResources PRIMARY KEY,
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireResources_Empires REFERENCES dbo.Empires(EmpireID),
        Industry DECIMAL(18, 2) NOT NULL,
        Research DECIMAL(18, 2) NOT NULL,
        Population DECIMAL(18, 2) NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.EmpirePriorities', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmpirePriorities
    (
        EmpirePriorityID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmpirePriorities PRIMARY KEY,
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpirePriorities_Empires REFERENCES dbo.Empires(EmpireID),
        IndustryWeight INT NOT NULL,
        ResearchWeight INT NOT NULL,
        MilitaryWeight INT NOT NULL,
        ExpansionWeight INT NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.EmpireMetrics', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmpireMetrics
    (
        EmpireMetricID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmpireMetrics PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireMetrics_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireMetrics_Empires REFERENCES dbo.Empires(EmpireID),
        TickNumber INT NOT NULL,
        Rank INT NOT NULL,
        IsWinner BIT NOT NULL,
        MapControlPercent DECIMAL(18, 6) NOT NULL,
        TotalEffectivePresence DECIMAL(18, 2) NOT NULL,
        ActiveShipCount INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.SystemLinks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemLinks
    (
        SystemLinkID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SystemLinks PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_Cycles REFERENCES dbo.Cycles(CycleID),
        SystemAID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_SystemA REFERENCES dbo.Systems(SystemID),
        SystemBID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_SystemB REFERENCES dbo.Systems(SystemID),
        Distance DECIMAL(18, 2) NOT NULL,
        TravelTicks INT NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Fleets', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Fleets
    (
        FleetID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Fleets PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Fleets_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Fleets_Empires REFERENCES dbo.Empires(EmpireID),
        FleetName NVARCHAR(120) NOT NULL,
        CurrentSystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Fleets_CurrentSystems REFERENCES dbo.Systems(SystemID),
        DestinationSystemID UNIQUEIDENTIFIER NULL CONSTRAINT FK_Fleets_DestinationSystems REFERENCES dbo.Systems(SystemID),
        ArrivalTickNumber INT NULL,
        ShipCount INT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.FleetOrders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FleetOrders
    (
        FleetOrderID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_FleetOrders PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_FleetOrders_Cycles REFERENCES dbo.Cycles(CycleID),
        FleetID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_FleetOrders_Fleets REFERENCES dbo.Fleets(FleetID),
        OrderType NVARCHAR(32) NOT NULL,
        TargetSystemID UNIQUEIDENTIFIER NULL CONSTRAINT FK_FleetOrders_TargetSystems REFERENCES dbo.Systems(SystemID),
        TargetEmpireID UNIQUEIDENTIFIER NULL CONSTRAINT FK_FleetOrders_TargetEmpires REFERENCES dbo.Empires(EmpireID),
        SubmitTick INT NOT NULL,
        ExecuteAfterTick INT NOT NULL,
        ProcessedTick INT NULL,
        Status NVARCHAR(32) NOT NULL,
        RejectionReason NVARCHAR(512) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.TickLogs', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TickLogs
    (
        TickLogID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TickLogs PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_TickLogs_Cycles REFERENCES dbo.Cycles(CycleID),
        TickNumber INT NOT NULL,
        StartedAt DATETIMEOFFSET NOT NULL,
        CompletedAt DATETIMEOFFSET NULL,
        Status NVARCHAR(32) NOT NULL,
        DiagnosticLog NVARCHAR(MAX) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.Events', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Events
    (
        EventID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Events PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Events_Cycles REFERENCES dbo.Cycles(CycleID),
        TickNumber INT NOT NULL,
        EventType NVARCHAR(64) NOT NULL,
        SystemID UNIQUEIDENTIFIER NULL CONSTRAINT FK_Events_Systems REFERENCES dbo.Systems(SystemID),
        EmpireID UNIQUEIDENTIFIER NULL CONSTRAINT FK_Events_Empires REFERENCES dbo.Empires(EmpireID),
        Severity NVARCHAR(32) NOT NULL,
        FactJson NVARCHAR(MAX) NOT NULL,
        DisplayText NVARCHAR(1024) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.BattleRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BattleRecords
    (
        BattleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_BattleRecords PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BattleRecords_Cycles REFERENCES dbo.Cycles(CycleID),
        TickNumber INT NOT NULL,
        SystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BattleRecords_Systems REFERENCES dbo.Systems(SystemID),
        AttackerEmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BattleRecords_AttackerEmpires REFERENCES dbo.Empires(EmpireID),
        DefenderEmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_BattleRecords_DefenderEmpires REFERENCES dbo.Empires(EmpireID),
        AttackerFleetIDs NVARCHAR(MAX) NOT NULL,
        DefenderFleetIDs NVARCHAR(MAX) NOT NULL,
        AttackerShipsBefore INT NOT NULL,
        DefenderShipsBefore INT NOT NULL,
        AttackerLosses INT NOT NULL,
        DefenderLosses INT NOT NULL,
        Outcome NVARCHAR(64) NOT NULL,
        FactJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.ChronicleEntries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ChronicleEntries
    (
        ChronicleEntryID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ChronicleEntries PRIMARY KEY,
        SourceEventID UNIQUEIDENTIFIER NULL CONSTRAINT FK_ChronicleEntries_Events REFERENCES dbo.Events(EventID),
        SourceBattleID UNIQUEIDENTIFIER NULL CONSTRAINT FK_ChronicleEntries_BattleRecords REFERENCES dbo.BattleRecords(BattleID),
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ChronicleEntries_Cycles REFERENCES dbo.Cycles(CycleID),
        SystemID UNIQUEIDENTIFIER NULL CONSTRAINT FK_ChronicleEntries_Systems REFERENCES dbo.Systems(SystemID),
        Title NVARCHAR(180) NOT NULL,
        EntryType NVARCHAR(64) NOT NULL,
        ImportanceScore INT NOT NULL,
        FactualSummary NVARCHAR(2048) NOT NULL,
        NarrativeText NVARCHAR(MAX) NOT NULL,
        NarrativeStatus NVARCHAR(32) NOT NULL CONSTRAINT DF_ChronicleEntries_NarrativeStatus DEFAULT N'Generated',
        NarrativeContextJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_ChronicleEntries_NarrativeContextJson DEFAULT N'{}',
        NarrativeGeneratedAt DATETIMEOFFSET NULL,
        NarrativeFailureReason NVARCHAR(1024) NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FleetOrders_Cycle_Status_ExecuteAfterTick' AND object_id = OBJECT_ID(N'dbo.FleetOrders'))
BEGIN
    CREATE INDEX IX_FleetOrders_Cycle_Status_ExecuteAfterTick ON dbo.FleetOrders(CycleID, Status, ExecuteAfterTick);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_EmpireMetrics_Cycle_Empire_Tick' AND object_id = OBJECT_ID(N'dbo.EmpireMetrics'))
BEGIN
    CREATE UNIQUE INDEX UX_EmpireMetrics_Cycle_Empire_Tick ON dbo.EmpireMetrics(CycleID, EmpireID, TickNumber);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmpireMetrics_Cycle_Tick_Rank' AND object_id = OBJECT_ID(N'dbo.EmpireMetrics'))
BEGIN
    CREATE INDEX IX_EmpireMetrics_Cycle_Tick_Rank ON dbo.EmpireMetrics(CycleID, TickNumber, Rank);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Fleets_Cycle_Empire_Status_CurrentSystem' AND object_id = OBJECT_ID(N'dbo.Fleets'))
BEGIN
    CREATE INDEX IX_Fleets_Cycle_Empire_Status_CurrentSystem ON dbo.Fleets(CycleID, EmpireID, Status, CurrentSystemID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Events_Cycle_Tick' AND object_id = OBJECT_ID(N'dbo.Events'))
BEGIN
    CREATE INDEX IX_Events_Cycle_Tick ON dbo.Events(CycleID, TickNumber);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TickLogs_Cycle_Tick_Completed' AND object_id = OBJECT_ID(N'dbo.TickLogs'))
BEGIN
    CREATE UNIQUE INDEX UX_TickLogs_Cycle_Tick_Completed
    ON dbo.TickLogs(CycleID, TickNumber)
    WHERE Status = N'Completed';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TickLogs_Cycle_Tick_Running' AND object_id = OBJECT_ID(N'dbo.TickLogs'))
BEGIN
    CREATE UNIQUE INDEX UX_TickLogs_Cycle_Tick_Running
    ON dbo.TickLogs(CycleID, TickNumber)
    WHERE Status = N'Running';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_BattleRecords_Cycle_System' AND object_id = OBJECT_ID(N'dbo.BattleRecords'))
BEGIN
    CREATE INDEX IX_BattleRecords_Cycle_System ON dbo.BattleRecords(CycleID, SystemID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChronicleEntries_Cycle_System' AND object_id = OBJECT_ID(N'dbo.ChronicleEntries'))
BEGIN
    CREATE INDEX IX_ChronicleEntries_Cycle_System ON dbo.ChronicleEntries(CycleID, SystemID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChronicleEntries_Cycle_NarrativeStatus' AND object_id = OBJECT_ID(N'dbo.ChronicleEntries'))
BEGIN
    CREATE INDEX IX_ChronicleEntries_Cycle_NarrativeStatus ON dbo.ChronicleEntries(CycleID, NarrativeStatus);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'001_initial_schema')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'001_initial_schema', N'Initial Cycles relational schema', SYSDATETIMEOFFSET());
END;
