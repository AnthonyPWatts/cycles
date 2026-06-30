SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.Admirals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Admirals
    (
        AdmiralID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Admirals PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Admirals_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Admirals_Empires REFERENCES dbo.Empires(EmpireID),
        AdmiralName NVARCHAR(120) NOT NULL,
        ReputationScore INT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF COL_LENGTH(N'dbo.Fleets', N'AdmiralID') IS NULL
BEGIN
    ALTER TABLE dbo.Fleets
    ADD AdmiralID UNIQUEIDENTIFIER NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Fleets_Admirals')
BEGIN
    EXEC(N'
    ALTER TABLE dbo.Fleets
    ADD CONSTRAINT FK_Fleets_Admirals
        FOREIGN KEY (AdmiralID)
        REFERENCES dbo.Admirals(AdmiralID)
        ON DELETE SET NULL;
    ');
END;

IF OBJECT_ID(N'dbo.AdmiralBattleHistories', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdmiralBattleHistories
    (
        AdmiralBattleHistoryID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AdmiralBattleHistories PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdmiralBattleHistories_Cycles REFERENCES dbo.Cycles(CycleID),
        AdmiralID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdmiralBattleHistories_Admirals REFERENCES dbo.Admirals(AdmiralID),
        BattleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdmiralBattleHistories_BattleRecords REFERENCES dbo.BattleRecords(BattleID),
        SystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdmiralBattleHistories_Systems REFERENCES dbo.Systems(SystemID),
        FleetID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdmiralBattleHistories_Fleets REFERENCES dbo.Fleets(FleetID),
        Role NVARCHAR(32) NOT NULL,
        Outcome NVARCHAR(32) NOT NULL,
        ShipsCommandedBefore INT NOT NULL,
        ShipsLost INT NOT NULL,
        ReputationChange INT NOT NULL,
        ReputationScoreAfter INT NOT NULL,
        AdmiralStatusAfter NVARCHAR(32) NOT NULL,
        IsFamousSystemAssociation BIT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Admirals_Cycle_Empire_Status' AND object_id = OBJECT_ID(N'dbo.Admirals'))
BEGIN
    CREATE INDEX IX_Admirals_Cycle_Empire_Status ON dbo.Admirals(CycleID, EmpireID, Status);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_Fleets_AdmiralID' AND object_id = OBJECT_ID(N'dbo.Fleets'))
BEGIN
    EXEC(N'
    CREATE UNIQUE INDEX UX_Fleets_AdmiralID
    ON dbo.Fleets(AdmiralID)
    WHERE AdmiralID IS NOT NULL;
    ');
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdmiralBattleHistories_Battle' AND object_id = OBJECT_ID(N'dbo.AdmiralBattleHistories'))
BEGIN
    CREATE INDEX IX_AdmiralBattleHistories_Battle ON dbo.AdmiralBattleHistories(BattleID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_AdmiralBattleHistories_Admiral_CreatedAt' AND object_id = OBJECT_ID(N'dbo.AdmiralBattleHistories'))
BEGIN
    CREATE INDEX IX_AdmiralBattleHistories_Admiral_CreatedAt ON dbo.AdmiralBattleHistories(AdmiralID, CreatedAt DESC);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'010_add_admirals')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'010_add_admirals', N'Add admirals and battle history', SYSDATETIMEOFFSET());
END;
