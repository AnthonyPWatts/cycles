SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Games', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CycleConfigurations', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Cycles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.TickLogs', N'U') IS NULL
BEGIN
    THROW 51040, 'Cannot add Cycle scheduling before the Game foundation and tick ledger exist.', 1;
END;

IF COL_LENGTH(N'dbo.CycleConfigurations', N'SchedulingMode') IS NULL
BEGIN
    ALTER TABLE dbo.CycleConfigurations ADD SchedulingMode NVARCHAR(32) NULL;
END;

IF COL_LENGTH(N'dbo.Cycles', N'SchedulingMode') IS NULL
BEGIN
    ALTER TABLE dbo.Cycles ADD SchedulingMode NVARCHAR(32) NULL;
END;

IF COL_LENGTH(N'dbo.Cycles', N'NextTickAt') IS NULL
BEGIN
    ALTER TABLE dbo.Cycles ADD NextTickAt DATETIMEOFFSET NULL;
END;

GO

UPDATE dbo.CycleConfigurations
SET SchedulingMode = N'Scheduled'
WHERE SchedulingMode IS NULL;

UPDATE cycle
SET SchedulingMode = configuration.SchedulingMode
FROM dbo.Cycles AS cycle
INNER JOIN dbo.CycleConfigurations AS configuration
    ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
   AND configuration.GameID = cycle.GameID
WHERE cycle.SchedulingMode IS NULL;

IF EXISTS
(
    SELECT 1
    FROM dbo.CycleConfigurations
    WHERE SchedulingMode NOT IN (N'Scheduled', N'SelfPaced')
)
BEGIN
    THROW 51041, 'Cycle configuration scheduling contains an unsupported capability.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.Cycles AS cycle
    INNER JOIN dbo.CycleConfigurations AS configuration
        ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
       AND configuration.GameID = cycle.GameID
    WHERE configuration.Status <> N'Materialized'
)
BEGIN
    THROW 51055, 'Every Cycle must reference a materialized Cycle configuration.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.Cycles AS cycle
    INNER JOIN dbo.CycleConfigurations AS configuration
        ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
       AND configuration.GameID = cycle.GameID
    WHERE cycle.SchedulingMode IS NULL
       OR cycle.SchedulingMode NOT IN (N'Scheduled', N'SelfPaced')
       OR cycle.SchedulingMode <> configuration.SchedulingMode
)
BEGIN
    THROW 51042, 'Cycle scheduling capability does not match its materialized configuration.', 1;
END;

UPDATE cycle
SET NextTickAt = CASE
    WHEN completed.LastCompletedAt IS NULL THEN cycle.StartAt
    ELSE DATEADD(MINUTE, cycle.TickLengthMinutes, completed.LastCompletedAt)
END
FROM dbo.Cycles AS cycle
OUTER APPLY
(
    SELECT MAX(log.CompletedAt) AS LastCompletedAt
    FROM dbo.TickLogs AS log
    WHERE log.CycleID = cycle.CycleID
      AND log.Status = N'Completed'
      AND log.CompletedAt IS NOT NULL
) AS completed
WHERE cycle.Status = N'Active'
  AND cycle.SchedulingMode = N'Scheduled'
  AND cycle.NextTickAt IS NULL;

UPDATE dbo.Cycles
SET NextTickAt = NULL
WHERE Status <> N'Active'
   OR SchedulingMode <> N'Scheduled';

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'SchedulingMode'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.CycleConfigurations ALTER COLUMN SchedulingMode NVARCHAR(32) NOT NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'SchedulingMode'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.Cycles ALTER COLUMN SchedulingMode NVARCHAR(32) NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'CK_CycleConfigurations_SchedulingMode'
)
BEGIN
    ALTER TABLE dbo.CycleConfigurations WITH CHECK
        ADD CONSTRAINT CK_CycleConfigurations_SchedulingMode CHECK
        (
            SchedulingMode IN (N'Scheduled', N'SelfPaced')
        );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_SchedulingMode'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_SchedulingMode CHECK
        (
            SchedulingMode IN (N'Scheduled', N'SelfPaced')
        );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_NextTickAt_Coherence'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_NextTickAt_Coherence CHECK
        (
            (Status = N'Active' AND SchedulingMode = N'Scheduled' AND NextTickAt IS NOT NULL)
            OR
            ((Status <> N'Active' OR SchedulingMode <> N'Scheduled') AND NextTickAt IS NULL)
        );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'IX_Cycles_Due'
)
BEGIN
    CREATE INDEX IX_Cycles_Due
        ON dbo.Cycles(NextTickAt, CycleID)
        INCLUDE(GameID)
        WHERE Status = N'Active'
          AND SchedulingMode = N'Scheduled'
          AND NextTickAt IS NOT NULL;
END;

GO

CREATE OR ALTER TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance
ON dbo.CycleConfigurations
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT
            CycleConfigurationID,
            GameID,
            SequenceNumber,
            Status,
            ProvenanceStatus,
            MapProfileKey,
            MapProfileVersion,
            MapProfileContentHash,
            MapSeed,
            ScenarioProfileKey,
            ScenarioProfileVersion,
            ScenarioProfileContentHash,
            ScenarioSeed,
            CyclePolicyKey,
            CyclePolicyVersion,
            CyclePolicyContentHash,
            SchedulingMode,
            MinimumHumanSeats,
            MaximumHumanSeats,
            ScheduledStartAt,
            ScheduledEndAt,
            TickLengthMinutes,
            CreatedAt,
            LockedAt,
            MaterializedAt,
            CancelledAt
        FROM deleted
        WHERE Status = N'Materialized'

        EXCEPT

        SELECT
            CycleConfigurationID,
            GameID,
            SequenceNumber,
            Status,
            ProvenanceStatus,
            MapProfileKey,
            MapProfileVersion,
            MapProfileContentHash,
            MapSeed,
            ScenarioProfileKey,
            ScenarioProfileVersion,
            ScenarioProfileContentHash,
            ScenarioSeed,
            CyclePolicyKey,
            CyclePolicyVersion,
            CyclePolicyContentHash,
            SchedulingMode,
            MinimumHumanSeats,
            MaximumHumanSeats,
            ScheduledStartAt,
            ScheduledEndAt,
            TickLengthMinutes,
            CreatedAt,
            LockedAt,
            MaterializedAt,
            CancelledAt
        FROM inserted
    )
    BEGIN
        THROW 51030, 'Materialized Cycle configurations are immutable.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS configuration
        INNER JOIN dbo.Cycles AS cycle
            ON cycle.CycleConfigurationID = configuration.CycleConfigurationID
           AND cycle.GameID = configuration.GameID
        WHERE configuration.Status <> N'Materialized'
    )
    BEGIN
        THROW 51055, 'Every Cycle must reference a materialized Cycle configuration.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS configuration
        INNER JOIN dbo.Cycles AS cycle
            ON cycle.CycleConfigurationID = configuration.CycleConfigurationID
           AND cycle.GameID = configuration.GameID
        WHERE configuration.SchedulingMode <> cycle.SchedulingMode
    )
    BEGIN
        THROW 51042, 'Cycle scheduling capability does not match its materialized configuration.', 1;
    END;
END;

GO

CREATE OR ALTER TRIGGER dbo.TR_Cycles_EnforceSchedulingCapability
ON dbo.Cycles
AFTER INSERT, UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS cycle
        INNER JOIN dbo.CycleConfigurations AS configuration
            ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
           AND configuration.GameID = cycle.GameID
        WHERE configuration.Status <> N'Materialized'
    )
    BEGIN
        THROW 51055, 'Every Cycle must reference a materialized Cycle configuration.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM inserted AS cycle
        INNER JOIN dbo.CycleConfigurations AS configuration
            ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
           AND configuration.GameID = cycle.GameID
        WHERE cycle.SchedulingMode <> configuration.SchedulingMode
    )
    BEGIN
        THROW 51042, 'Cycle scheduling capability does not match its materialized configuration.', 1;
    END;
END;

GO
