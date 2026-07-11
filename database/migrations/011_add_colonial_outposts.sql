SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.ColonialOutposts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ColonialOutposts
    (
        ColonialOutpostID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ColonialOutposts PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ColonialOutposts_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ColonialOutposts_Empires REFERENCES dbo.Empires(EmpireID),
        SystemID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ColonialOutposts_Systems REFERENCES dbo.Systems(SystemID),
        EstablishedTick INT NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT CK_ColonialOutposts_EstablishedTick CHECK (EstablishedTick >= 0),
        CONSTRAINT UX_ColonialOutposts_Empire_System UNIQUE (EmpireID, SystemID)
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ColonialOutposts_Cycle_System' AND object_id = OBJECT_ID(N'dbo.ColonialOutposts'))
BEGIN
    CREATE INDEX IX_ColonialOutposts_Cycle_System ON dbo.ColonialOutposts(CycleID, SystemID);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'011_add_colonial_outposts')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'011_add_colonial_outposts', N'Add population-funded colonial outposts', SYSDATETIMEOFFSET());
END;
