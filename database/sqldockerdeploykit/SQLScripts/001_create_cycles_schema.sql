IF DB_ID(N'CyclesDb') IS NULL
BEGIN
    PRINT 'Creating CyclesDb database';
    CREATE DATABASE CyclesDb;
END
GO

USE CyclesDb;
GO

IF OBJECT_ID(N'dbo.ChronicleEntries', N'U') IS NOT NULL DROP TABLE dbo.ChronicleEntries;
IF OBJECT_ID(N'dbo.BattleRecords', N'U') IS NOT NULL DROP TABLE dbo.BattleRecords;
IF OBJECT_ID(N'dbo.Events', N'U') IS NOT NULL DROP TABLE dbo.Events;
IF OBJECT_ID(N'dbo.TickLogs', N'U') IS NOT NULL DROP TABLE dbo.TickLogs;
IF OBJECT_ID(N'dbo.FleetOrders', N'U') IS NOT NULL DROP TABLE dbo.FleetOrders;
IF OBJECT_ID(N'dbo.Fleets', N'U') IS NOT NULL DROP TABLE dbo.Fleets;
IF OBJECT_ID(N'dbo.SystemLinks', N'U') IS NOT NULL DROP TABLE dbo.SystemLinks;
IF OBJECT_ID(N'dbo.EmpirePriorities', N'U') IS NOT NULL DROP TABLE dbo.EmpirePriorities;
IF OBJECT_ID(N'dbo.EmpireResources', N'U') IS NOT NULL DROP TABLE dbo.EmpireResources;
IF OBJECT_ID(N'dbo.Empires', N'U') IS NOT NULL DROP TABLE dbo.Empires;
IF OBJECT_ID(N'dbo.Systems', N'U') IS NOT NULL DROP TABLE dbo.Systems;
IF OBJECT_ID(N'dbo.Cycles', N'U') IS NOT NULL DROP TABLE dbo.Cycles;
IF OBJECT_ID(N'dbo.Players', N'U') IS NOT NULL DROP TABLE dbo.Players;
GO

CREATE TABLE dbo.Players
(
    PlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Players PRIMARY KEY,
    Username NVARCHAR(80) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    PasswordHash NVARCHAR(512) NOT NULL,
    CreatedAt DATETIMEOFFSET NOT NULL,
    LastLoginAt DATETIMEOFFSET NULL,
    Status NVARCHAR(32) NOT NULL
);

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

CREATE TABLE dbo.EmpireResources
(
    EmpireResourceID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmpireResources PRIMARY KEY,
    EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireResources_Empires REFERENCES dbo.Empires(EmpireID),
    Industry DECIMAL(18, 2) NOT NULL,
    Research DECIMAL(18, 2) NOT NULL,
    Population DECIMAL(18, 2) NOT NULL,
    UpdatedAt DATETIMEOFFSET NOT NULL
);

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

CREATE TABLE dbo.SystemLinks
(
    SystemLinkID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SystemLinks PRIMARY KEY,
    CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_Cycles REFERENCES dbo.Cycles(CycleID),
    SystemAID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_SystemA REFERENCES dbo.Systems(SystemID),
    SystemBID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemLinks_SystemB REFERENCES dbo.Systems(SystemID),
    Distance DECIMAL(18, 2) NOT NULL,
    TravelTicks INT NOT NULL
);

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

CREATE TABLE dbo.TickLogs
(
    TickLogID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TickLogs PRIMARY KEY,
    CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_TickLogs_Cycles REFERENCES dbo.Cycles(CycleID),
    TickNumber INT NOT NULL,
    StartedAt DATETIMEOFFSET NOT NULL,
    CompletedAt DATETIMEOFFSET NULL,
    Status NVARCHAR(32) NOT NULL,
    DiagnosticLog NVARCHAR(MAX) NOT NULL,
    CONSTRAINT UQ_TickLogs_Cycle_Tick UNIQUE (CycleID, TickNumber)
);

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
    CreatedAt DATETIMEOFFSET NOT NULL
);
GO

CREATE INDEX IX_FleetOrders_Cycle_Status_ExecuteAfterTick ON dbo.FleetOrders(CycleID, Status, ExecuteAfterTick);
CREATE INDEX IX_Fleets_Cycle_Empire_Status_CurrentSystem ON dbo.Fleets(CycleID, EmpireID, Status, CurrentSystemID);
CREATE INDEX IX_Events_Cycle_Tick ON dbo.Events(CycleID, TickNumber);
CREATE INDEX IX_BattleRecords_Cycle_System ON dbo.BattleRecords(CycleID, SystemID);
CREATE INDEX IX_ChronicleEntries_Cycle_System ON dbo.ChronicleEntries(CycleID, SystemID);
GO
