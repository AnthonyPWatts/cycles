IF COL_LENGTH(N'dbo.Cycles', N'TurnStage') IS NULL
BEGIN
    ALTER TABLE dbo.Cycles
        ADD TurnStage NVARCHAR(32) NOT NULL
            CONSTRAINT DF_Cycles_TurnStage DEFAULT N'CommandOpen';
END;
GO

IF COL_LENGTH(N'dbo.FleetOrders', N'CommandSource') IS NULL
BEGIN
    ALTER TABLE dbo.FleetOrders
        ADD CommandSource NVARCHAR(32) NOT NULL
            CONSTRAINT DF_FleetOrders_CommandSource DEFAULT N'Human';
END;
GO

IF COL_LENGTH(N'dbo.FleetOrders', N'SealedTick') IS NULL
BEGIN
    ALTER TABLE dbo.FleetOrders ADD SealedTick INT NULL;
END;
GO

IF COL_LENGTH(N'dbo.FleetOrders', N'SealedAt') IS NULL
BEGIN
    ALTER TABLE dbo.FleetOrders ADD SealedAt DATETIMEOFFSET NULL;
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE name = N'CK_FleetOrders_SealedTogether'
      AND parent_object_id = OBJECT_ID(N'dbo.FleetOrders')
)
BEGIN
    ALTER TABLE dbo.FleetOrders WITH CHECK
        ADD CONSTRAINT CK_FleetOrders_SealedTogether CHECK
        (
            (SealedTick IS NULL AND SealedAt IS NULL)
            OR (SealedTick IS NOT NULL AND SealedAt IS NOT NULL)
        );
END;
GO

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FleetOrders')
      AND name = N'IX_FleetOrders_Cycle_SealedTick'
)
BEGIN
    CREATE INDEX IX_FleetOrders_Cycle_SealedTick
        ON dbo.FleetOrders(CycleID, SealedTick, FleetID)
        WHERE SealedTick IS NOT NULL;
END;
GO
