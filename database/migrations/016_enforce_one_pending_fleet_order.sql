SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.FleetOrders', N'SupersededByOrderID') IS NULL
BEGIN
    ALTER TABLE dbo.FleetOrders
    ADD SupersededByOrderID UNIQUEIDENTIFIER NULL;
END;
GO

;WITH RankedPendingOrders AS
(
    SELECT
        FleetOrderID,
        CycleID,
        FleetID,
        ExecuteAfterTick,
        ROW_NUMBER() OVER
        (
            PARTITION BY CycleID, FleetID, ExecuteAfterTick
            ORDER BY CreatedAt DESC, FleetOrderID DESC
        ) AS PendingRank,
        FIRST_VALUE(FleetOrderID) OVER
        (
            PARTITION BY CycleID, FleetID, ExecuteAfterTick
            ORDER BY CreatedAt DESC, FleetOrderID DESC
        ) AS ReplacementOrderID
    FROM dbo.FleetOrders
    WHERE Status = N'Pending'
)
UPDATE orders
SET Status = N'Superseded',
    ProcessedTick = cycles.CurrentTickNumber,
    SupersededByOrderID = ranked.ReplacementOrderID
FROM dbo.FleetOrders orders
INNER JOIN RankedPendingOrders ranked ON ranked.FleetOrderID = orders.FleetOrderID
INNER JOIN dbo.Cycles cycles ON cycles.CycleID = orders.CycleID
WHERE ranked.PendingRank > 1;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_FleetOrders_Cycle_Fleet_ExecuteAfterTick_Pending' AND object_id = OBJECT_ID(N'dbo.FleetOrders'))
BEGIN
    CREATE UNIQUE INDEX UX_FleetOrders_Cycle_Fleet_ExecuteAfterTick_Pending
        ON dbo.FleetOrders(CycleID, FleetID, ExecuteAfterTick)
        WHERE Status = N'Pending';
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'016_enforce_one_pending_fleet_order')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'016_enforce_one_pending_fleet_order', N'Enforce one pending fleet order per execution tick', SYSDATETIMEOFFSET());
END;
