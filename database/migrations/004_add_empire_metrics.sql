SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

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

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_EmpireMetrics_Cycle_Empire_Tick' AND object_id = OBJECT_ID(N'dbo.EmpireMetrics'))
BEGIN
    CREATE UNIQUE INDEX UX_EmpireMetrics_Cycle_Empire_Tick ON dbo.EmpireMetrics(CycleID, EmpireID, TickNumber);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_EmpireMetrics_Cycle_Tick_Rank' AND object_id = OBJECT_ID(N'dbo.EmpireMetrics'))
BEGIN
    CREATE INDEX IX_EmpireMetrics_Cycle_Tick_Rank ON dbo.EmpireMetrics(CycleID, TickNumber, Rank);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'004_add_empire_metrics')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'004_add_empire_metrics', N'Add empire metric snapshots', SYSDATETIMEOFFSET());
END;
