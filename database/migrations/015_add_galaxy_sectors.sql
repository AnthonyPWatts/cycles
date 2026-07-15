SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.GalaxySectors', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GalaxySectors
    (
        SectorID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GalaxySectors PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_GalaxySectors_Cycles REFERENCES dbo.Cycles(CycleID),
        SectorName NVARCHAR(120) NOT NULL,
        CentreX INT NOT NULL,
        CentreY INT NOT NULL,
        SortOrder INT NOT NULL,
        CONSTRAINT UQ_GalaxySectors_Cycle_Sector UNIQUE (CycleID, SectorID),
        CONSTRAINT UQ_GalaxySectors_Cycle_Name UNIQUE (CycleID, SectorName),
        CONSTRAINT UQ_GalaxySectors_Cycle_SortOrder UNIQUE (CycleID, SortOrder)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.GalaxySectors') AND name = N'IX_GalaxySectors_CycleID')
BEGIN
    CREATE INDEX IX_GalaxySectors_CycleID ON dbo.GalaxySectors(CycleID);
END;

IF COL_LENGTH(N'dbo.Systems', N'SectorID') IS NULL
BEGIN
    ALTER TABLE dbo.Systems ADD SectorID UNIQUEIDENTIFIER NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE parent_object_id = OBJECT_ID(N'dbo.Systems') AND name = N'FK_Systems_GalaxySectors')
BEGIN
    ALTER TABLE dbo.Systems WITH CHECK
    ADD CONSTRAINT FK_Systems_GalaxySectors FOREIGN KEY (CycleID, SectorID) REFERENCES dbo.GalaxySectors(CycleID, SectorID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Systems') AND name = N'IX_Systems_SectorID')
BEGIN
    CREATE INDEX IX_Systems_SectorID ON dbo.Systems(SectorID);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'015_add_galaxy_sectors')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'015_add_galaxy_sectors', N'Add persisted galaxy sectors and system membership', SYSDATETIMEOFFSET());
END;

COMMIT TRANSACTION;
