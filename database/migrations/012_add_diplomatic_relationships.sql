SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF OBJECT_ID(N'dbo.DiplomaticRelationships', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DiplomaticRelationships
    (
        DiplomaticRelationshipID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_DiplomaticRelationships PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DiplomaticRelationships_Cycles REFERENCES dbo.Cycles(CycleID),
        FirstEmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DiplomaticRelationships_FirstEmpire REFERENCES dbo.Empires(EmpireID),
        SecondEmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_DiplomaticRelationships_SecondEmpire REFERENCES dbo.Empires(EmpireID),
        State NVARCHAR(32) NOT NULL,
        UpdatedTick INT NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT CK_DiplomaticRelationships_DifferentEmpires CHECK (FirstEmpireID <> SecondEmpireID),
        CONSTRAINT CK_DiplomaticRelationships_UpdatedTick CHECK (UpdatedTick >= 0),
        CONSTRAINT CK_DiplomaticRelationships_State CHECK (State IN (N'Neutral', N'War', N'NonAggressionPact', N'Alliance')),
        CONSTRAINT UX_DiplomaticRelationships_Cycle_Empires UNIQUE (CycleID, FirstEmpireID, SecondEmpireID)
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'012_add_diplomatic_relationships')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'012_add_diplomatic_relationships', N'Add initial diplomatic relationship states', SYSDATETIMEOFFSET());
END;
