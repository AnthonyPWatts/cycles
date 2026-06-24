SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF EXISTS
(
    SELECT 1
    FROM sys.key_constraints
    WHERE name = N'UQ_TickLogs_Cycle_Tick'
        AND parent_object_id = OBJECT_ID(N'dbo.TickLogs')
)
BEGIN
    ALTER TABLE dbo.TickLogs DROP CONSTRAINT UQ_TickLogs_Cycle_Tick;
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TickLogs_Cycle_Tick_Completed' AND object_id = OBJECT_ID(N'dbo.TickLogs'))
BEGIN
    CREATE UNIQUE INDEX UX_TickLogs_Cycle_Tick_Completed
    ON dbo.TickLogs(CycleID, TickNumber)
    WHERE Status = N'Completed';
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_TickLogs_Cycle_Tick_Running' AND object_id = OBJECT_ID(N'dbo.TickLogs'))
BEGIN
    CREATE UNIQUE INDEX UX_TickLogs_Cycle_Tick_Running
    ON dbo.TickLogs(CycleID, TickNumber)
    WHERE Status = N'Running';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'002_allow_tick_retry_history')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'002_allow_tick_retry_history', N'Allow failed tick retry history', SYSDATETIMEOFFSET());
END;
