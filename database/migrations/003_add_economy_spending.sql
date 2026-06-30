SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastGeneratedIndustry') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastGeneratedIndustry DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastGeneratedIndustry DEFAULT 0;
END;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastGeneratedResearch') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastGeneratedResearch DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastGeneratedResearch DEFAULT 0;
END;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastGeneratedPopulation') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastGeneratedPopulation DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastGeneratedPopulation DEFAULT 0;
END;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastSpentIndustry') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastSpentIndustry DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastSpentIndustry DEFAULT 0;
END;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastSpentResearch') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastSpentResearch DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastSpentResearch DEFAULT 0;
END;

IF COL_LENGTH(N'dbo.EmpireResources', N'LastSpentPopulation') IS NULL
BEGIN
    ALTER TABLE dbo.EmpireResources ADD LastSpentPopulation DECIMAL(18, 2) NOT NULL CONSTRAINT DF_EmpireResources_LastSpentPopulation DEFAULT 0;
END;

IF OBJECT_ID(N'dbo.ShipConstructions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ShipConstructions
    (
        ShipConstructionID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ShipConstructions PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ShipConstructions_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_ShipConstructions_Empires REFERENCES dbo.Empires(EmpireID),
        ShipCount INT NOT NULL,
        IndustrySpent DECIMAL(18, 2) NOT NULL,
        StartedTick INT NOT NULL,
        CompleteAfterTick INT NOT NULL,
        CompletedTick INT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        UpdatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ShipConstructions_Cycle_Status_CompleteAfterTick' AND object_id = OBJECT_ID(N'dbo.ShipConstructions'))
BEGIN
    CREATE INDEX IX_ShipConstructions_Cycle_Status_CompleteAfterTick ON dbo.ShipConstructions(CycleID, Status, CompleteAfterTick);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'003_add_economy_spending')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'003_add_economy_spending', N'Add economy spending and ship construction queue', SYSDATETIMEOFFSET());
END;
