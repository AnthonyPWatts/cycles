SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.SystemHistoricalSignals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SystemHistoricalSignals
    (
        SystemHistoricalSignalID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SystemHistoricalSignals PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemHistoricalSignals_Cycles REFERENCES dbo.Cycles(CycleID),
        SystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_SystemHistoricalSignals_Systems REFERENCES dbo.Systems(SystemID),
        SignalType NVARCHAR(64) NOT NULL,
        SourceBattleID UNIQUEIDENTIFIER NULL CONSTRAINT FK_SystemHistoricalSignals_BattleRecords REFERENCES dbo.BattleRecords(BattleID),
        BattleCount INT NOT NULL,
        TotalLosses INT NOT NULL,
        LargestBattleLosses INT NOT NULL,
        HostedCycleLargestBattle BIT NOT NULL,
        HistoricalSignificanceIncrease INT NOT NULL,
        HistoricalSignificanceAfter INT NOT NULL,
        Summary NVARCHAR(2048) NOT NULL,
        FactJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_SystemHistoricalSignals_Cycle_System_Type' AND object_id = OBJECT_ID(N'dbo.SystemHistoricalSignals'))
BEGIN
    CREATE UNIQUE INDEX UX_SystemHistoricalSignals_Cycle_System_Type
    ON dbo.SystemHistoricalSignals(CycleID, SystemID, SignalType);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_SystemHistoricalSignals_Cycle_System' AND object_id = OBJECT_ID(N'dbo.SystemHistoricalSignals'))
BEGIN
    CREATE INDEX IX_SystemHistoricalSignals_Cycle_System ON dbo.SystemHistoricalSignals(CycleID, SystemID);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'008_add_system_historical_signals')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'008_add_system_historical_signals', N'Add system historical signal persistence', SYSDATETIMEOFFSET());
END;
