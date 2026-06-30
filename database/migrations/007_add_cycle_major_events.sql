SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.CycleMajorEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CycleMajorEvents
    (
        CycleMajorEventID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CycleMajorEvents PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CycleMajorEvents_Cycles REFERENCES dbo.Cycles(CycleID),
        SourceBattleID UNIQUEIDENTIFIER NULL CONSTRAINT FK_CycleMajorEvents_BattleRecords REFERENCES dbo.BattleRecords(BattleID),
        SystemID UNIQUEIDENTIFIER NULL CONSTRAINT FK_CycleMajorEvents_Systems REFERENCES dbo.Systems(SystemID),
        EventType NVARCHAR(64) NOT NULL,
        TickNumber INT NOT NULL,
        SelectionRank INT NOT NULL,
        ImportanceScore INT NOT NULL,
        TotalLosses INT NOT NULL,
        Summary NVARCHAR(2048) NOT NULL,
        FactJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CycleMajorEvents_Cycle_SourceBattle' AND object_id = OBJECT_ID(N'dbo.CycleMajorEvents'))
BEGIN
    CREATE UNIQUE INDEX UX_CycleMajorEvents_Cycle_SourceBattle
    ON dbo.CycleMajorEvents(CycleID, SourceBattleID)
    WHERE SourceBattleID IS NOT NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CycleMajorEvents_Cycle_Rank' AND object_id = OBJECT_ID(N'dbo.CycleMajorEvents'))
BEGIN
    CREATE INDEX IX_CycleMajorEvents_Cycle_Rank ON dbo.CycleMajorEvents(CycleID, SelectionRank);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'007_add_cycle_major_events')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'007_add_cycle_major_events', N'Add selected major Cycle event persistence', SYSDATETIMEOFFSET());
END;
