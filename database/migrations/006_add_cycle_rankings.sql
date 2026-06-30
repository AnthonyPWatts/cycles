SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.CycleRankings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CycleRankings
    (
        CycleRankingID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CycleRankings PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CycleRankings_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CycleRankings_Empires REFERENCES dbo.Empires(EmpireID),
        Rank INT NOT NULL,
        IsWinner BIT NOT NULL,
        MapControlPercent DECIMAL(18, 6) NOT NULL,
        TotalEffectivePresence DECIMAL(18, 2) NOT NULL,
        ActiveShipCount INT NOT NULL,
        CutoffTickNumber INT NOT NULL,
        CutoffAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_CycleRankings_Cycle_Empire' AND object_id = OBJECT_ID(N'dbo.CycleRankings'))
BEGIN
    CREATE UNIQUE INDEX UX_CycleRankings_Cycle_Empire ON dbo.CycleRankings(CycleID, EmpireID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_CycleRankings_Cycle_Rank' AND object_id = OBJECT_ID(N'dbo.CycleRankings'))
BEGIN
    CREATE INDEX IX_CycleRankings_Cycle_Rank ON dbo.CycleRankings(CycleID, Rank);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'006_add_cycle_rankings')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'006_add_cycle_rankings', N'Add final Cycle ranking persistence', SYSDATETIMEOFFSET());
END;
