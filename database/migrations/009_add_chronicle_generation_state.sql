SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.ChronicleEntries', N'NarrativeStatus') IS NULL
BEGIN
    ALTER TABLE dbo.ChronicleEntries
    ADD NarrativeStatus NVARCHAR(32) NOT NULL
        CONSTRAINT DF_ChronicleEntries_NarrativeStatus DEFAULT N'Generated';
END;

IF COL_LENGTH(N'dbo.ChronicleEntries', N'NarrativeContextJson') IS NULL
BEGIN
    ALTER TABLE dbo.ChronicleEntries
    ADD NarrativeContextJson NVARCHAR(MAX) NOT NULL
        CONSTRAINT DF_ChronicleEntries_NarrativeContextJson DEFAULT N'{}';
END;

IF COL_LENGTH(N'dbo.ChronicleEntries', N'NarrativeGeneratedAt') IS NULL
BEGIN
    ALTER TABLE dbo.ChronicleEntries
    ADD NarrativeGeneratedAt DATETIMEOFFSET NULL;
END;

IF COL_LENGTH(N'dbo.ChronicleEntries', N'NarrativeFailureReason') IS NULL
BEGIN
    ALTER TABLE dbo.ChronicleEntries
    ADD NarrativeFailureReason NVARCHAR(1024) NULL;
END;

EXEC(N'
UPDATE dbo.ChronicleEntries
SET NarrativeGeneratedAt = CreatedAt
WHERE NarrativeStatus = N''Generated''
    AND NarrativeGeneratedAt IS NULL
    AND LEN(NarrativeText) > 0;
');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ChronicleEntries_Cycle_NarrativeStatus' AND object_id = OBJECT_ID(N'dbo.ChronicleEntries'))
BEGIN
    CREATE INDEX IX_ChronicleEntries_Cycle_NarrativeStatus ON dbo.ChronicleEntries(CycleID, NarrativeStatus);
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'009_add_chronicle_generation_state')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'009_add_chronicle_generation_state', N'Add Chronicle narrative generation state', SYSDATETIMEOFFSET());
END;
