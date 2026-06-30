SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.Players', N'Role') IS NULL
BEGIN
    ALTER TABLE dbo.Players
    ADD Role NVARCHAR(32) NOT NULL
        CONSTRAINT DF_Players_Role DEFAULT N'Player';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'005_add_player_role')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'005_add_player_role', N'Add development player roles', SYSDATETIMEOFFSET());
END;
