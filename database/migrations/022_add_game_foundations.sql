SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

DECLARE @LegacyGameID UNIQUEIDENTIFIER = '01fcdded-9718-4436-b585-d97d504b1d57';
DECLARE @LegacyLifecycleEventID UNIQUEIDENTIFIER = 'b283628d-2899-475c-9c6e-5dd8e20c2e91';

DECLARE @UnknownCycleStatuses NVARCHAR(1800);
SELECT @UnknownCycleStatuses = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), invalid.CycleID), N'=', invalid.Status)),
    N', ')
FROM
(
    SELECT TOP (16) CycleID, Status
    FROM dbo.Cycles
    WHERE Status NOT IN (N'Active', N'Completed', N'RecoveryRequired')
    ORDER BY CycleID
) AS invalid;

IF @UnknownCycleStatuses IS NOT NULL
BEGIN
    DECLARE @UnknownStatusMessage NVARCHAR(2048) =
        CONCAT(N'Cannot add Game foundations because Cycles contain unknown statuses: ', @UnknownCycleStatuses, N'.');
    THROW 51022, @UnknownStatusMessage, 1;
END;

IF (SELECT COUNT_BIG(*) FROM dbo.Cycles WHERE Status IN (N'Active', N'RecoveryRequired')) > 1
BEGIN
    DECLARE @OperationalCycleIDs NVARCHAR(1800);
    SELECT @OperationalCycleIDs = STRING_AGG(
        CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), CycleID), N'=', Status)),
        N', ')
    FROM dbo.Cycles
    WHERE Status IN (N'Active', N'RecoveryRequired');

    DECLARE @OperationalCycleMessage NVARCHAR(2048) =
        CONCAT(N'Cannot add Game foundations while more than one operational Cycle exists: ', @OperationalCycleIDs, N'.');
    THROW 51023, @OperationalCycleMessage, 1;
END;

IF OBJECT_ID(N'dbo.Games', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Games
    (
        GameID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Games PRIMARY KEY,
        Name NVARCHAR(120) NOT NULL,
        Purpose NVARCHAR(32) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        Visibility NVARCHAR(32) NOT NULL,
        CreationSource NVARCHAR(32) NOT NULL,
        GamePolicyKey NVARCHAR(128) NOT NULL,
        GamePolicyVersion INT NOT NULL,
        GamePolicyContentHash CHAR(64) NULL,
        PolicyProvenanceStatus NVARCHAR(32) NOT NULL,
        CreatedByPlayerID UNIQUEIDENTIFIER NULL CONSTRAINT FK_Games_CreatedByPlayers REFERENCES dbo.Players(PlayerID),
        CreatedAt DATETIMEOFFSET NOT NULL,
        FirstStartedAt DATETIMEOFFSET NULL,
        CompletedAt DATETIMEOFFSET NULL,
        CancelledAt DATETIMEOFFSET NULL,
        TerminatedAt DATETIMEOFFSET NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT CK_Games_Name CHECK (LEN(LTRIM(RTRIM(Name))) > 0),
        CONSTRAINT CK_Games_Purpose CHECK (Purpose IN (N'Standard', N'Training')),
        CONSTRAINT CK_Games_Status CHECK
        (
            Status IN (N'Forming', N'Starting', N'Active', N'Intermission', N'Completed', N'Cancelled', N'Terminated')
        ),
        CONSTRAINT CK_Games_GamePolicyVersion CHECK (GamePolicyVersion > 0),
        CONSTRAINT CK_Games_PolicyProvenanceStatus CHECK
        (
            PolicyProvenanceStatus IN (N'Verified', N'LegacyUnverified')
        ),
        CONSTRAINT CK_Games_TerminalTimestamps CHECK
        (
            (Status = N'Completed' AND CompletedAt IS NOT NULL AND CancelledAt IS NULL AND TerminatedAt IS NULL)
            OR (Status = N'Cancelled' AND CompletedAt IS NULL AND CancelledAt IS NOT NULL AND TerminatedAt IS NULL)
            OR (Status = N'Terminated' AND CompletedAt IS NULL AND CancelledAt IS NULL AND TerminatedAt IS NOT NULL)
            OR (Status NOT IN (N'Completed', N'Cancelled', N'Terminated')
                AND CompletedAt IS NULL AND CancelledAt IS NULL AND TerminatedAt IS NULL)
        ),
        CONSTRAINT CK_Games_FirstStartedAt CHECK
        (
            Status IN (N'Forming', N'Starting', N'Cancelled') OR FirstStartedAt IS NOT NULL
        )
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Games')
      AND name = N'CK_Games_GamePolicyContentHash'
)
BEGIN
    ALTER TABLE dbo.Games WITH CHECK
        ADD CONSTRAINT CK_Games_GamePolicyContentHash CHECK
        (
            GamePolicyContentHash IS NULL
            OR
            (
                LEN(GamePolicyContentHash) = 64
                AND GamePolicyContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
            )
        );
END;

IF EXISTS (SELECT 1 FROM dbo.Games WHERE GameID <> @LegacyGameID)
   OR EXISTS (SELECT 1 FROM dbo.Games WHERE GameID = @LegacyGameID)
      AND NOT EXISTS (SELECT 1 FROM dbo.Cycles)
BEGIN
    THROW 51025, 'Cannot resume the legacy Game backfill because Games contains a non-legacy or orphaned foundation row.', 1;
END;

IF NOT EXISTS (SELECT 1 FROM dbo.Games WHERE GameID = @LegacyGameID)
   AND EXISTS (SELECT 1 FROM dbo.Cycles)
BEGIN
    INSERT INTO dbo.Games
    (
        GameID,
        Name,
        Purpose,
        Status,
        Visibility,
        CreationSource,
        GamePolicyKey,
        GamePolicyVersion,
        GamePolicyContentHash,
        PolicyProvenanceStatus,
        CreatedByPlayerID,
        CreatedAt,
        FirstStartedAt,
        CompletedAt,
        CancelledAt,
        TerminatedAt
    )
    SELECT
        @LegacyGameID,
        N'Legacy Standard Game',
        N'Standard',
        CASE
            WHEN SUM(CASE WHEN Status IN (N'Active', N'RecoveryRequired') THEN 1 ELSE 0 END) = 1
                THEN N'Active'
            ELSE N'Completed'
        END,
        N'Private',
        N'LegacyImport',
        N'legacy-single-lineage-v1',
        1,
        NULL,
        N'LegacyUnverified',
        NULL,
        MIN(CreatedAt),
        MIN(StartAt),
        CASE
            WHEN SUM(CASE WHEN Status IN (N'Active', N'RecoveryRequired') THEN 1 ELSE 0 END) = 0
                THEN MAX(EndAt)
            ELSE NULL
        END,
        NULL,
        NULL
    FROM dbo.Cycles;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.Games
    WHERE GameID = @LegacyGameID
      AND
      (
          Name <> N'Legacy Standard Game'
          OR Purpose <> N'Standard'
          OR Visibility <> N'Private'
          OR CreationSource <> N'LegacyImport'
          OR GamePolicyKey <> N'legacy-single-lineage-v1'
          OR GamePolicyVersion <> 1
          OR GamePolicyContentHash IS NOT NULL
          OR PolicyProvenanceStatus <> N'LegacyUnverified'
          OR CreatedByPlayerID IS NOT NULL
          OR CancelledAt IS NOT NULL
          OR TerminatedAt IS NOT NULL
      )
)
BEGIN
    THROW 51025, 'Cannot resume the legacy Game backfill because the fixed legacy Game has conflicting immutable fields.', 1;
END;

;WITH DerivedLegacyGame AS
(
    SELECT
        CASE
            WHEN SUM(CASE WHEN Status IN (N'Active', N'RecoveryRequired') THEN 1 ELSE 0 END) = 1
                THEN N'Active'
            ELSE N'Completed'
        END AS Status,
        MIN(CreatedAt) AS CreatedAt,
        MIN(StartAt) AS FirstStartedAt,
        CASE
            WHEN SUM(CASE WHEN Status IN (N'Active', N'RecoveryRequired') THEN 1 ELSE 0 END) = 0
                THEN MAX(EndAt)
            ELSE NULL
        END AS CompletedAt
    FROM dbo.Cycles
)
UPDATE game
SET
    Status = derived.Status,
    CreatedAt = derived.CreatedAt,
    FirstStartedAt = derived.FirstStartedAt,
    CompletedAt = derived.CompletedAt
FROM dbo.Games AS game
CROSS JOIN DerivedLegacyGame AS derived
WHERE game.GameID = @LegacyGameID
  AND
  (
      game.Status <> derived.Status
      OR game.CreatedAt <> derived.CreatedAt
      OR game.FirstStartedAt <> derived.FirstStartedAt
      OR (game.CompletedAt <> derived.CompletedAt)
      OR (game.CompletedAt IS NULL AND derived.CompletedAt IS NOT NULL)
      OR (game.CompletedAt IS NOT NULL AND derived.CompletedAt IS NULL)
  );

IF OBJECT_ID(N'dbo.CycleConfigurations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CycleConfigurations
    (
        CycleConfigurationID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CycleConfigurations PRIMARY KEY,
        GameID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_CycleConfigurations_Games REFERENCES dbo.Games(GameID),
        SequenceNumber INT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        ProvenanceStatus NVARCHAR(32) NOT NULL,
        MapProfileKey NVARCHAR(128) NULL,
        MapProfileVersion INT NULL,
        MapProfileContentHash CHAR(64) NULL,
        MapSeed INT NULL,
        ScenarioProfileKey NVARCHAR(128) NULL,
        ScenarioProfileVersion INT NULL,
        ScenarioProfileContentHash CHAR(64) NULL,
        ScenarioSeed INT NULL,
        CyclePolicyKey NVARCHAR(128) NOT NULL,
        CyclePolicyVersion INT NOT NULL,
        CyclePolicyContentHash CHAR(64) NULL,
        MinimumHumanSeats INT NULL,
        MaximumHumanSeats INT NULL,
        ScheduledStartAt DATETIMEOFFSET NULL,
        ScheduledEndAt DATETIMEOFFSET NULL,
        TickLengthMinutes INT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        LockedAt DATETIMEOFFSET NULL,
        MaterializedAt DATETIMEOFFSET NULL,
        CancelledAt DATETIMEOFFSET NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UX_CycleConfigurations_Game_Sequence UNIQUE (GameID, SequenceNumber),
        CONSTRAINT CK_CycleConfigurations_SequenceNumber CHECK (SequenceNumber > 0),
        CONSTRAINT CK_CycleConfigurations_Status CHECK
        (
            Status IN (N'Draft', N'Locked', N'Materialized', N'Cancelled')
        ),
        CONSTRAINT CK_CycleConfigurations_ProvenanceStatus CHECK
        (
            ProvenanceStatus IN (N'Verified', N'LegacyUnverified')
        ),
        CONSTRAINT CK_CycleConfigurations_ProfileVersions CHECK
        (
            (MapProfileVersion IS NULL OR MapProfileVersion > 0)
            AND (ScenarioProfileVersion IS NULL OR ScenarioProfileVersion > 0)
            AND CyclePolicyVersion > 0
        ),
        CONSTRAINT CK_CycleConfigurations_HumanSeats CHECK
        (
            (MinimumHumanSeats IS NULL AND MaximumHumanSeats IS NULL)
            OR
            (
                MinimumHumanSeats IS NOT NULL
                AND MaximumHumanSeats IS NOT NULL
                AND MinimumHumanSeats > 0
                AND MaximumHumanSeats >= MinimumHumanSeats
            )
        ),
        CONSTRAINT CK_CycleConfigurations_TickLength CHECK
        (
            TickLengthMinutes IS NULL OR TickLengthMinutes > 0
        ),
        CONSTRAINT CK_CycleConfigurations_StatusTimestamps CHECK
        (
            (Status = N'Draft' AND LockedAt IS NULL AND MaterializedAt IS NULL AND CancelledAt IS NULL)
            OR (Status = N'Locked' AND LockedAt IS NOT NULL AND MaterializedAt IS NULL AND CancelledAt IS NULL)
            OR (Status = N'Materialized' AND LockedAt IS NOT NULL AND MaterializedAt IS NOT NULL AND CancelledAt IS NULL)
            OR (Status = N'Cancelled' AND MaterializedAt IS NULL AND CancelledAt IS NOT NULL)
        )
    );
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'CK_CycleConfigurations_ContentHashes'
)
BEGIN
    ALTER TABLE dbo.CycleConfigurations WITH CHECK
        ADD CONSTRAINT CK_CycleConfigurations_ContentHashes CHECK
        (
            (
                MapProfileContentHash IS NULL
                OR
                (
                    LEN(MapProfileContentHash) = 64
                    AND MapProfileContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
            AND
            (
                ScenarioProfileContentHash IS NULL
                OR
                (
                    LEN(ScenarioProfileContentHash) = 64
                    AND ScenarioProfileContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
            AND
            (
                CyclePolicyContentHash IS NULL
                OR
                (
                    LEN(CyclePolicyContentHash) = 64
                    AND CyclePolicyContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
        );
END;

-- A partially applied earlier draft of this migration may already have the
-- original bounds constraint, which SQL Server treats as satisfied when one
-- side evaluates to UNKNOWN. This independently restartable constraint closes
-- that gap without depending on the original constraint definition.
IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'CK_CycleConfigurations_HumanSeatsCompleteness'
)
BEGIN
    ALTER TABLE dbo.CycleConfigurations WITH CHECK
        ADD CONSTRAINT CK_CycleConfigurations_HumanSeatsCompleteness CHECK
        (
            (MinimumHumanSeats IS NULL AND MaximumHumanSeats IS NULL)
            OR (MinimumHumanSeats IS NOT NULL AND MaximumHumanSeats IS NOT NULL)
        );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'IX_CycleConfigurations_Game_Status'
)
BEGIN
    CREATE INDEX IX_CycleConfigurations_Game_Status
        ON dbo.CycleConfigurations(GameID, Status, SequenceNumber);
END;

IF COL_LENGTH(N'dbo.Cycles', N'GameID') IS NULL
    ALTER TABLE dbo.Cycles ADD GameID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Cycles', N'CycleConfigurationID') IS NULL
    ALTER TABLE dbo.Cycles ADD CycleConfigurationID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Cycles', N'PreviousCycleID') IS NULL
    ALTER TABLE dbo.Cycles ADD PreviousCycleID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Cycles', N'MapProfileKey') IS NULL
    ALTER TABLE dbo.Cycles ADD MapProfileKey NVARCHAR(128) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'MapProfileVersion') IS NULL
    ALTER TABLE dbo.Cycles ADD MapProfileVersion INT NULL;
IF COL_LENGTH(N'dbo.Cycles', N'MapProfileContentHash') IS NULL
    ALTER TABLE dbo.Cycles ADD MapProfileContentHash CHAR(64) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'MapSeed') IS NULL
    ALTER TABLE dbo.Cycles ADD MapSeed INT NULL;
IF COL_LENGTH(N'dbo.Cycles', N'ScenarioProfileKey') IS NULL
    ALTER TABLE dbo.Cycles ADD ScenarioProfileKey NVARCHAR(128) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'ScenarioProfileVersion') IS NULL
    ALTER TABLE dbo.Cycles ADD ScenarioProfileVersion INT NULL;
IF COL_LENGTH(N'dbo.Cycles', N'ScenarioProfileContentHash') IS NULL
    ALTER TABLE dbo.Cycles ADD ScenarioProfileContentHash CHAR(64) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'ScenarioSeed') IS NULL
    ALTER TABLE dbo.Cycles ADD ScenarioSeed INT NULL;
IF COL_LENGTH(N'dbo.Cycles', N'CyclePolicyKey') IS NULL
    ALTER TABLE dbo.Cycles ADD CyclePolicyKey NVARCHAR(128) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'CyclePolicyVersion') IS NULL
    ALTER TABLE dbo.Cycles ADD CyclePolicyVersion INT NULL;
IF COL_LENGTH(N'dbo.Cycles', N'CyclePolicyContentHash') IS NULL
    ALTER TABLE dbo.Cycles ADD CyclePolicyContentHash CHAR(64) NULL;
IF COL_LENGTH(N'dbo.Cycles', N'ProfileProvenanceStatus') IS NULL
    ALTER TABLE dbo.Cycles ADD ProfileProvenanceStatus NVARCHAR(32) NULL;

IF COL_LENGTH(N'dbo.Cycles', N'OperationalSlot') IS NULL
BEGIN
    ALTER TABLE dbo.Cycles
        ADD OperationalSlot AS
        (
            CONVERT(TINYINT, CASE WHEN Status IN (N'Active', N'RecoveryRequired') THEN 1 ELSE NULL END)
        ) PERSISTED;
END;

GO

DECLARE @LegacyGameID UNIQUEIDENTIFIER = '01fcdded-9718-4436-b585-d97d504b1d57';
DECLARE @LegacyLifecycleEventID UNIQUEIDENTIFIER = 'b283628d-2899-475c-9c6e-5dd8e20c2e91';

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_ContentHashes'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_ContentHashes CHECK
        (
            (
                MapProfileContentHash IS NULL
                OR
                (
                    LEN(MapProfileContentHash) = 64
                    AND MapProfileContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
            AND
            (
                ScenarioProfileContentHash IS NULL
                OR
                (
                    LEN(ScenarioProfileContentHash) = 64
                    AND ScenarioProfileContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
            AND
            (
                CyclePolicyContentHash IS NULL
                OR
                (
                    LEN(CyclePolicyContentHash) = 64
                    AND CyclePolicyContentHash COLLATE Latin1_General_100_BIN2 NOT LIKE '%[^0-9A-Fa-f]%'
                )
            )
        );
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.Cycles
    WHERE (GameID IS NOT NULL AND GameID <> @LegacyGameID)
       OR (CycleConfigurationID IS NOT NULL AND CycleConfigurationID <> CycleID)
       OR PreviousCycleID IS NOT NULL
       OR (CyclePolicyKey IS NOT NULL AND CyclePolicyKey <> N'legacy-cycle-policy-v1')
       OR (CyclePolicyVersion IS NOT NULL AND CyclePolicyVersion <> 1)
       OR CyclePolicyContentHash IS NOT NULL
       OR (ProfileProvenanceStatus IS NOT NULL AND ProfileProvenanceStatus <> N'LegacyUnverified')
)
BEGIN
    THROW 51026, 'Cannot resume the legacy Game backfill because a Cycle has conflicting Game, configuration, predecessor or policy provenance.', 1;
END;

DECLARE @ConflictingCanonicalCycleIDs NVARCHAR(1800);
SELECT @ConflictingCanonicalCycleIDs = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONVERT(NVARCHAR(36), conflict.CycleID)),
    N', ')
FROM
(
    SELECT events.CycleID
    FROM dbo.Events AS events
    WHERE events.EventType = N'CycleSeeded'
      AND ISJSON(events.FactJson) = 1
      AND JSON_VALUE(events.FactJson, N'$.topologyKey') = N'territorial-graph-v2'
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.seed')) IS NOT NULL
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.sectorCount')) = 8
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.systemCount')) = 64
    GROUP BY events.CycleID
    HAVING MIN(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.seed')))
        <> MAX(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.seed')))
) AS conflict;

IF @ConflictingCanonicalCycleIDs IS NOT NULL
BEGIN
    DECLARE @ConflictingCanonicalMessage NVARCHAR(2048) = CONCAT(
        N'Cannot classify canonical map provenance because qualifying CycleSeeded facts disagree on seed for Cycles: ',
        @ConflictingCanonicalCycleIDs,
        N'.');
    THROW 51024, @ConflictingCanonicalMessage, 1;
END;

CREATE TABLE #LegacyProfileClassification
(
    CycleID UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    MapProfileKey NVARCHAR(128) NOT NULL,
    MapSeed INT NULL,
    ScenarioProfileKey NVARCHAR(128) NOT NULL,
    ScenarioSeed INT NULL
);

INSERT INTO #LegacyProfileClassification
(
    CycleID,
    MapProfileKey,
    MapSeed,
    ScenarioProfileKey,
    ScenarioSeed
)
SELECT
    CycleID,
    N'legacy-unclassified',
    NULL,
    N'legacy-unclassified',
    NULL
FROM dbo.Cycles;

;WITH CanonicalMapFacts AS
(
    SELECT
        events.CycleID,
        MIN(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.seed'))) AS MapSeed
    FROM dbo.Events AS events
    WHERE events.EventType = N'CycleSeeded'
      AND ISJSON(events.FactJson) = 1
      AND JSON_VALUE(events.FactJson, N'$.topologyKey') = N'territorial-graph-v2'
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.seed')) IS NOT NULL
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.sectorCount')) = 8
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.systemCount')) = 64
      AND (SELECT COUNT(*) FROM dbo.GalaxySectors AS sector WHERE sector.CycleID = events.CycleID) = 8
      AND (SELECT COUNT(*) FROM dbo.Systems AS system WHERE system.CycleID = events.CycleID) = 64
      AND (SELECT COUNT(*) FROM dbo.SystemLinks AS link WHERE link.CycleID = events.CycleID) = 91
    GROUP BY events.CycleID
)
UPDATE classification
SET
    MapProfileKey = N'territorial-graph-v2',
    MapSeed = facts.MapSeed
FROM #LegacyProfileClassification AS classification
INNER JOIN CanonicalMapFacts AS facts ON facts.CycleID = classification.CycleID;

;WITH ParticipantScenarioSeeds AS
(
    SELECT
        participant.CycleID,
        participant.MatchParticipantID,
        MIN(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.scenarioSeed'))) AS MinimumSeed,
        MAX(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.scenarioSeed'))) AS MaximumSeed
    FROM dbo.MatchParticipants AS participant
    INNER JOIN dbo.Events AS events
        ON events.CycleID = participant.CycleID
       AND events.EmpireID = participant.EmpireID
    WHERE events.EventType = N'OpeningBriefingIssued'
      AND ISJSON(events.FactJson) = 1
      AND JSON_VALUE(events.FactJson, N'$.scenarioKey') = N'development-match-v2'
      AND JSON_VALUE(events.FactJson, N'$.mapVersion') = N'territorial-graph-v2'
      AND TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.scenarioSeed')) IS NOT NULL
    GROUP BY participant.CycleID, participant.MatchParticipantID
    HAVING MIN(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.scenarioSeed')))
        = MAX(TRY_CONVERT(INT, JSON_VALUE(events.FactJson, N'$.scenarioSeed')))
),
CanonicalScenarioFacts AS
(
    SELECT
        participantSeed.CycleID,
        MIN(participantSeed.MinimumSeed) AS ScenarioSeed,
        COUNT(*) AS ParticipantCount
    FROM ParticipantScenarioSeeds AS participantSeed
    GROUP BY participantSeed.CycleID
    HAVING MIN(participantSeed.MinimumSeed) = MAX(participantSeed.MaximumSeed)
)
UPDATE classification
SET
    ScenarioProfileKey = N'development-match-v2',
    ScenarioSeed = facts.ScenarioSeed
FROM #LegacyProfileClassification AS classification
INNER JOIN CanonicalScenarioFacts AS facts ON facts.CycleID = classification.CycleID
WHERE classification.MapProfileKey = N'territorial-graph-v2'
  AND facts.ParticipantCount =
  (
      SELECT COUNT(*)
      FROM dbo.MatchParticipants AS participant
      WHERE participant.CycleID = classification.CycleID
  )
  AND facts.ParticipantCount > 0;

IF EXISTS
(
    SELECT 1
    FROM dbo.Cycles AS cycle
    INNER JOIN #LegacyProfileClassification AS profile ON profile.CycleID = cycle.CycleID
    WHERE (cycle.MapProfileKey IS NOT NULL AND cycle.MapProfileKey <> profile.MapProfileKey)
       OR cycle.MapProfileVersion IS NOT NULL
       OR cycle.MapProfileContentHash IS NOT NULL
       OR (cycle.MapSeed IS NOT NULL AND (profile.MapSeed IS NULL OR cycle.MapSeed <> profile.MapSeed))
       OR (cycle.ScenarioProfileKey IS NOT NULL AND cycle.ScenarioProfileKey <> profile.ScenarioProfileKey)
       OR cycle.ScenarioProfileVersion IS NOT NULL
       OR cycle.ScenarioProfileContentHash IS NOT NULL
       OR (cycle.ScenarioSeed IS NOT NULL AND
           (profile.ScenarioSeed IS NULL OR cycle.ScenarioSeed <> profile.ScenarioSeed))
)
BEGIN
    THROW 51026, 'Cannot resume the legacy Game backfill because a Cycle has conflicting map or scenario provenance.', 1;
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.CycleConfigurations AS configuration
    LEFT JOIN
    (
        SELECT
            CycleID,
            CONVERT(INT, ROW_NUMBER() OVER (ORDER BY StartAt, CreatedAt, CycleID)) AS SequenceNumber,
            StartAt,
            EndAt,
            TickLengthMinutes,
            CreatedAt
        FROM dbo.Cycles
    ) AS cycle ON cycle.CycleID = configuration.CycleConfigurationID
    LEFT JOIN #LegacyProfileClassification AS profile ON profile.CycleID = cycle.CycleID
    WHERE cycle.CycleID IS NULL
       OR configuration.GameID <> @LegacyGameID
       OR configuration.SequenceNumber <> cycle.SequenceNumber
       OR configuration.Status <> N'Materialized'
       OR configuration.ProvenanceStatus <> N'LegacyUnverified'
       OR (configuration.MapProfileKey IS NOT NULL AND configuration.MapProfileKey <> profile.MapProfileKey)
       OR configuration.MapProfileVersion IS NOT NULL
       OR configuration.MapProfileContentHash IS NOT NULL
       OR (configuration.MapSeed IS NOT NULL AND
           (profile.MapSeed IS NULL OR configuration.MapSeed <> profile.MapSeed))
       OR (configuration.ScenarioProfileKey IS NOT NULL AND configuration.ScenarioProfileKey <> profile.ScenarioProfileKey)
       OR configuration.ScenarioProfileVersion IS NOT NULL
       OR configuration.ScenarioProfileContentHash IS NOT NULL
       OR (configuration.ScenarioSeed IS NOT NULL AND
           (profile.ScenarioSeed IS NULL OR configuration.ScenarioSeed <> profile.ScenarioSeed))
       OR configuration.CyclePolicyKey <> N'legacy-cycle-policy-v1'
       OR configuration.CyclePolicyVersion <> 1
       OR configuration.CyclePolicyContentHash IS NOT NULL
       OR configuration.MinimumHumanSeats IS NOT NULL
       OR configuration.MaximumHumanSeats IS NOT NULL
       OR (configuration.ScheduledStartAt IS NOT NULL AND configuration.ScheduledStartAt <> cycle.StartAt)
       OR (configuration.ScheduledEndAt IS NOT NULL AND configuration.ScheduledEndAt <> cycle.EndAt)
       OR (configuration.TickLengthMinutes IS NOT NULL AND configuration.TickLengthMinutes <> cycle.TickLengthMinutes)
       OR configuration.CreatedAt <> cycle.CreatedAt
       OR (configuration.LockedAt IS NOT NULL AND configuration.LockedAt <> cycle.CreatedAt)
       OR (configuration.MaterializedAt IS NOT NULL AND configuration.MaterializedAt <> cycle.CreatedAt)
       OR configuration.CancelledAt IS NOT NULL
)
BEGIN
    THROW 51027, 'Cannot resume the legacy Game backfill because a Cycle configuration has conflicting identity, sequence or immutable provenance.', 1;
END;

;WITH RankedLegacyCycles AS
(
    SELECT
        CycleID,
        CONVERT(INT, ROW_NUMBER() OVER (ORDER BY StartAt, CreatedAt, CycleID)) AS SequenceNumber,
        StartAt,
        EndAt,
        TickLengthMinutes,
        CreatedAt
    FROM dbo.Cycles
)
INSERT INTO dbo.CycleConfigurations
(
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
    MinimumHumanSeats,
    MaximumHumanSeats,
    ScheduledStartAt,
    ScheduledEndAt,
    TickLengthMinutes,
    CreatedAt,
    LockedAt,
    MaterializedAt,
    CancelledAt
)
SELECT
    cycle.CycleID,
    @LegacyGameID,
    cycle.SequenceNumber,
    N'Materialized',
    N'LegacyUnverified',
    profile.MapProfileKey,
    NULL,
    NULL,
    profile.MapSeed,
    profile.ScenarioProfileKey,
    NULL,
    NULL,
    profile.ScenarioSeed,
    N'legacy-cycle-policy-v1',
    1,
    NULL,
    NULL,
    NULL,
    cycle.StartAt,
    cycle.EndAt,
    cycle.TickLengthMinutes,
    cycle.CreatedAt,
    cycle.CreatedAt,
    cycle.CreatedAt,
    NULL
FROM RankedLegacyCycles AS cycle
INNER JOIN #LegacyProfileClassification AS profile ON profile.CycleID = cycle.CycleID
WHERE EXISTS (SELECT 1 FROM dbo.Games WHERE GameID = @LegacyGameID)
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.CycleConfigurations AS existing
      WHERE existing.CycleConfigurationID = cycle.CycleID
  );

UPDATE configuration
SET
    MapProfileKey = profile.MapProfileKey,
    MapProfileVersion = NULL,
    MapProfileContentHash = NULL,
    MapSeed = profile.MapSeed,
    ScenarioProfileKey = profile.ScenarioProfileKey,
    ScenarioProfileVersion = NULL,
    ScenarioProfileContentHash = NULL,
    ScenarioSeed = profile.ScenarioSeed,
    ScheduledStartAt = COALESCE(configuration.ScheduledStartAt, cycle.StartAt),
    ScheduledEndAt = COALESCE(configuration.ScheduledEndAt, cycle.EndAt),
    TickLengthMinutes = COALESCE(configuration.TickLengthMinutes, cycle.TickLengthMinutes),
    LockedAt = COALESCE(configuration.LockedAt, cycle.CreatedAt),
    MaterializedAt = COALESCE(configuration.MaterializedAt, cycle.CreatedAt)
FROM dbo.CycleConfigurations AS configuration
INNER JOIN #LegacyProfileClassification AS profile
    ON profile.CycleID = configuration.CycleConfigurationID
INNER JOIN dbo.Cycles AS cycle ON cycle.CycleID = configuration.CycleConfigurationID
WHERE configuration.GameID = @LegacyGameID
  AND configuration.ProvenanceStatus = N'LegacyUnverified'
  AND
  (
      configuration.MapProfileKey IS NULL
      OR configuration.MapProfileKey <> profile.MapProfileKey
      OR configuration.MapProfileVersion IS NOT NULL
      OR configuration.MapProfileContentHash IS NOT NULL
      OR (configuration.MapSeed <> profile.MapSeed)
      OR (configuration.MapSeed IS NULL AND profile.MapSeed IS NOT NULL)
      OR (configuration.MapSeed IS NOT NULL AND profile.MapSeed IS NULL)
      OR configuration.ScenarioProfileKey IS NULL
      OR configuration.ScenarioProfileKey <> profile.ScenarioProfileKey
      OR configuration.ScenarioProfileVersion IS NOT NULL
      OR configuration.ScenarioProfileContentHash IS NOT NULL
      OR (configuration.ScenarioSeed <> profile.ScenarioSeed)
      OR (configuration.ScenarioSeed IS NULL AND profile.ScenarioSeed IS NOT NULL)
      OR (configuration.ScenarioSeed IS NOT NULL AND profile.ScenarioSeed IS NULL)
      OR configuration.ScheduledStartAt IS NULL
      OR configuration.ScheduledEndAt IS NULL
      OR configuration.TickLengthMinutes IS NULL
      OR configuration.LockedAt IS NULL
      OR configuration.MaterializedAt IS NULL
  );

UPDATE cycle
SET
    GameID = COALESCE(cycle.GameID, @LegacyGameID),
    CycleConfigurationID = COALESCE(cycle.CycleConfigurationID, cycle.CycleID),
    MapProfileKey = profile.MapProfileKey,
    MapProfileVersion = NULL,
    MapProfileContentHash = NULL,
    MapSeed = profile.MapSeed,
    ScenarioProfileKey = profile.ScenarioProfileKey,
    ScenarioProfileVersion = NULL,
    ScenarioProfileContentHash = NULL,
    ScenarioSeed = profile.ScenarioSeed,
    CyclePolicyKey = COALESCE(cycle.CyclePolicyKey, N'legacy-cycle-policy-v1'),
    CyclePolicyVersion = COALESCE(cycle.CyclePolicyVersion, 1),
    ProfileProvenanceStatus = COALESCE(cycle.ProfileProvenanceStatus, N'LegacyUnverified')
FROM dbo.Cycles AS cycle
INNER JOIN #LegacyProfileClassification AS profile ON profile.CycleID = cycle.CycleID
WHERE EXISTS (SELECT 1 FROM dbo.Games WHERE GameID = @LegacyGameID)
  AND (cycle.GameID IS NULL OR cycle.GameID = @LegacyGameID)
  AND (cycle.ProfileProvenanceStatus IS NULL OR cycle.ProfileProvenanceStatus = N'LegacyUnverified')
  AND
  (
      cycle.GameID IS NULL
      OR cycle.CycleConfigurationID IS NULL
      OR cycle.MapProfileKey IS NULL
      OR cycle.MapProfileKey <> profile.MapProfileKey
      OR cycle.MapProfileVersion IS NOT NULL
      OR cycle.MapProfileContentHash IS NOT NULL
      OR (cycle.MapSeed <> profile.MapSeed)
      OR (cycle.MapSeed IS NULL AND profile.MapSeed IS NOT NULL)
      OR (cycle.MapSeed IS NOT NULL AND profile.MapSeed IS NULL)
      OR cycle.ScenarioProfileKey IS NULL
      OR cycle.ScenarioProfileKey <> profile.ScenarioProfileKey
      OR cycle.ScenarioProfileVersion IS NOT NULL
      OR cycle.ScenarioProfileContentHash IS NOT NULL
      OR (cycle.ScenarioSeed <> profile.ScenarioSeed)
      OR (cycle.ScenarioSeed IS NULL AND profile.ScenarioSeed IS NOT NULL)
      OR (cycle.ScenarioSeed IS NOT NULL AND profile.ScenarioSeed IS NULL)
      OR cycle.CyclePolicyKey IS NULL
      OR cycle.CyclePolicyVersion IS NULL
      OR cycle.ProfileProvenanceStatus IS NULL
  );

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_Games')
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT FK_Cycles_Games FOREIGN KEY (GameID) REFERENCES dbo.Games(GameID);
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_CycleConfigurations')
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT FK_Cycles_CycleConfigurations
            FOREIGN KEY (CycleConfigurationID) REFERENCES dbo.CycleConfigurations(CycleConfigurationID);
END;
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_PreviousCycles')
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT FK_Cycles_PreviousCycles FOREIGN KEY (PreviousCycleID) REFERENCES dbo.Cycles(CycleID);
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_Status'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_Status CHECK
        (
            Status IN (N'Active', N'Completed', N'RecoveryRequired')
        );
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_ProfileProvenanceStatus'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_ProfileProvenanceStatus CHECK
        (
            ProfileProvenanceStatus IS NULL
            OR ProfileProvenanceStatus IN (N'Verified', N'LegacyUnverified')
        );
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CK_Cycles_ProfileVersions'
)
BEGIN
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT CK_Cycles_ProfileVersions CHECK
        (
            (MapProfileVersion IS NULL OR MapProfileVersion > 0)
            AND (ScenarioProfileVersion IS NULL OR ScenarioProfileVersion > 0)
            AND (CyclePolicyVersion IS NULL OR CyclePolicyVersion > 0)
        );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_CycleConfigurationID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Cycles_CycleConfigurationID
        ON dbo.Cycles(CycleConfigurationID)
        WHERE CycleConfigurationID IS NOT NULL;
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_PreviousCycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Cycles_PreviousCycleID
        ON dbo.Cycles(PreviousCycleID)
        WHERE PreviousCycleID IS NOT NULL;
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_Game_OperationalSlot'
)
BEGIN
    CREATE UNIQUE INDEX UX_Cycles_Game_OperationalSlot
        ON dbo.Cycles(GameID, OperationalSlot)
        WHERE GameID IS NOT NULL AND Status IN (N'Active', N'RecoveryRequired');
END;
IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'IX_Cycles_Game_Status'
)
BEGIN
    CREATE INDEX IX_Cycles_Game_Status ON dbo.Cycles(GameID, Status, StartAt);
END;

IF OBJECT_ID(N'dbo.GameEnrolments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GameEnrolments
    (
        GameEnrolmentID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GameEnrolments PRIMARY KEY,
        GameID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_GameEnrolments_Games REFERENCES dbo.Games(GameID),
        PlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_GameEnrolments_Players REFERENCES dbo.Players(PlayerID),
        Status NVARCHAR(32) NOT NULL,
        Origin NVARCHAR(32) NOT NULL,
        OriginatingRequestID NVARCHAR(128) NULL,
        EnrolledAt DATETIMEOFFSET NOT NULL,
        StatusChangedAt DATETIMEOFFSET NOT NULL,
        EndedAt DATETIMEOFFSET NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT UX_GameEnrolments_Game_Player UNIQUE (GameID, PlayerID),
        CONSTRAINT CK_GameEnrolments_Status CHECK
        (
            Status IN (N'Enrolled', N'Historical', N'Completed', N'Withdrawn')
        ),
        CONSTRAINT CK_GameEnrolments_Origin CHECK
        (
            Origin IN (N'Direct', N'Invitation', N'ManualOrganiser', N'Matchmaking', N'LegacyImport')
        ),
        CONSTRAINT CK_GameEnrolments_EndedAt CHECK
        (
            (Status IN (N'Completed', N'Withdrawn') AND EndedAt IS NOT NULL)
            OR (Status NOT IN (N'Completed', N'Withdrawn') AND EndedAt IS NULL)
        )
    );
END;

CREATE TABLE #LegacyEnrolmentClassification
(
    PlayerID UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
    Status NVARCHAR(32) NOT NULL,
    EnrolledAt DATETIMEOFFSET NOT NULL,
    StatusChangedAt DATETIMEOFFSET NOT NULL,
    EndedAt DATETIMEOFFSET NULL
);

;WITH ParticipantHistory AS
(
    SELECT
        participant.PlayerID,
        MIN(participant.JoinedAt) AS EnrolledAt,
        MAX(COALESCE(participant.EndedAt, participant.JoinedAt)) AS LatestParticipationAt,
        MAX(CASE
            WHEN cycle.Status IN (N'Active', N'RecoveryRequired') AND participant.Status <> N'Withdrawn' THEN 1
            ELSE 0
        END) AS HasCurrentEnrolment,
        MAX(CASE
            WHEN cycle.Status IN (N'Active', N'RecoveryRequired') AND participant.Status = N'Withdrawn' THEN 1
            ELSE 0
        END) AS HasCurrentWithdrawal,
        MAX(CASE
            WHEN cycle.Status IN (N'Active', N'RecoveryRequired')
                THEN COALESCE(participant.EndedAt, participant.JoinedAt)
            ELSE NULL
        END) AS CurrentParticipationAt
    FROM dbo.MatchParticipants AS participant
    INNER JOIN dbo.Cycles AS cycle ON cycle.CycleID = participant.CycleID
    WHERE cycle.GameID = @LegacyGameID
    GROUP BY participant.PlayerID
),
ClassifiedEnrolments AS
(
    SELECT
        history.PlayerID,
        history.EnrolledAt,
        CASE
            WHEN game.Status = N'Completed' THEN N'Completed'
            WHEN history.HasCurrentEnrolment = 1 THEN N'Enrolled'
            WHEN history.HasCurrentWithdrawal = 1 THEN N'Withdrawn'
            ELSE N'Historical'
        END AS Status,
        CASE
            WHEN game.Status = N'Completed'
                THEN COALESCE(game.CompletedAt, history.LatestParticipationAt)
            WHEN history.HasCurrentEnrolment = 1 OR history.HasCurrentWithdrawal = 1
                THEN COALESCE(history.CurrentParticipationAt, history.LatestParticipationAt)
            ELSE history.LatestParticipationAt
        END AS StatusChangedAt
    FROM ParticipantHistory AS history
    CROSS JOIN dbo.Games AS game
    WHERE game.GameID = @LegacyGameID
)
INSERT INTO #LegacyEnrolmentClassification
(
    PlayerID,
    Status,
    EnrolledAt,
    StatusChangedAt,
    EndedAt
)
SELECT
    enrolment.PlayerID,
    enrolment.Status,
    enrolment.EnrolledAt,
    enrolment.StatusChangedAt,
    CASE WHEN enrolment.Status IN (N'Completed', N'Withdrawn') THEN enrolment.StatusChangedAt ELSE NULL END
FROM ClassifiedEnrolments AS enrolment;

IF EXISTS
(
    SELECT 1
    FROM dbo.GameEnrolments AS existing
    LEFT JOIN #LegacyEnrolmentClassification AS expected ON expected.PlayerID = existing.PlayerID
    WHERE existing.GameID <> @LegacyGameID
       OR existing.GameEnrolmentID <> existing.PlayerID
       OR existing.Origin <> N'LegacyImport'
       OR existing.OriginatingRequestID IS NOT NULL
       OR expected.PlayerID IS NULL
)
BEGIN
    THROW 51028, 'Cannot resume the legacy Game backfill because a Game enrolment has conflicting identity, origin or participant history.', 1;
END;

UPDATE existing
SET
    Status = expected.Status,
    EnrolledAt = expected.EnrolledAt,
    StatusChangedAt = expected.StatusChangedAt,
    EndedAt = expected.EndedAt
FROM dbo.GameEnrolments AS existing
INNER JOIN #LegacyEnrolmentClassification AS expected ON expected.PlayerID = existing.PlayerID
WHERE existing.GameID = @LegacyGameID
  AND
  (
      existing.Status <> expected.Status
      OR existing.EnrolledAt <> expected.EnrolledAt
      OR existing.StatusChangedAt <> expected.StatusChangedAt
      OR (existing.EndedAt <> expected.EndedAt)
      OR (existing.EndedAt IS NULL AND expected.EndedAt IS NOT NULL)
      OR (existing.EndedAt IS NOT NULL AND expected.EndedAt IS NULL)
  );

INSERT INTO dbo.GameEnrolments
(
    GameEnrolmentID,
    GameID,
    PlayerID,
    Status,
    Origin,
    OriginatingRequestID,
    EnrolledAt,
    StatusChangedAt,
    EndedAt
)
SELECT
    expected.PlayerID,
    @LegacyGameID,
    expected.PlayerID,
    expected.Status,
    N'LegacyImport',
    NULL,
    expected.EnrolledAt,
    expected.StatusChangedAt,
    expected.EndedAt
FROM #LegacyEnrolmentClassification AS expected
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.GameEnrolments AS existing
    WHERE existing.GameID = @LegacyGameID
      AND existing.PlayerID = expected.PlayerID
);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GameEnrolments')
      AND name = N'IX_GameEnrolments_Player_Status_Game'
)
BEGIN
    CREATE INDEX IX_GameEnrolments_Player_Status_Game
        ON dbo.GameEnrolments(PlayerID, Status, GameID)
        INCLUDE (EnrolledAt, StatusChangedAt, EndedAt);
END;

IF OBJECT_ID(N'dbo.GameLifecycleEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.GameLifecycleEvents
    (
        GameLifecycleEventID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_GameLifecycleEvents PRIMARY KEY,
        GameID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_GameLifecycleEvents_Games REFERENCES dbo.Games(GameID),
        EventType NVARCHAR(64) NOT NULL,
        SubjectPlayerID UNIQUEIDENTIFIER NULL CONSTRAINT FK_GameLifecycleEvents_SubjectPlayers REFERENCES dbo.Players(PlayerID),
        ActorPlayerID UNIQUEIDENTIFIER NULL CONSTRAINT FK_GameLifecycleEvents_ActorPlayers REFERENCES dbo.Players(PlayerID),
        FromStatus NVARCHAR(32) NULL,
        ToStatus NVARCHAR(32) NULL,
        Reason NVARCHAR(512) NULL,
        CorrelationID NVARCHAR(128) NULL,
        FactJson NVARCHAR(MAX) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT CK_GameLifecycleEvents_EventType CHECK (LEN(LTRIM(RTRIM(EventType))) > 0),
        CONSTRAINT CK_GameLifecycleEvents_FactJson CHECK (ISJSON(FactJson) = 1)
    );
END;

IF EXISTS
(
    SELECT 1
    FROM dbo.GameLifecycleEvents AS gameEvent
    LEFT JOIN dbo.Games AS game ON game.GameID = @LegacyGameID
    WHERE (gameEvent.GameLifecycleEventID = @LegacyLifecycleEventID
           OR (gameEvent.GameID = @LegacyGameID AND gameEvent.EventType = N'LegacyImported'))
      AND
      (
          gameEvent.GameLifecycleEventID <> @LegacyLifecycleEventID
          OR gameEvent.GameID <> @LegacyGameID
          OR gameEvent.EventType <> N'LegacyImported'
          OR gameEvent.SubjectPlayerID IS NOT NULL
          OR gameEvent.ActorPlayerID IS NOT NULL
          OR gameEvent.FromStatus IS NOT NULL
          OR gameEvent.ToStatus <> game.Status
          OR gameEvent.Reason IS NOT NULL
          OR gameEvent.CorrelationID IS NOT NULL
          OR gameEvent.FactJson <> N'{"source":"legacy-single-lineage","schemaVersion":1}'
          OR gameEvent.CreatedAt <> game.CreatedAt
      )
)
BEGIN
    THROW 51029, 'Cannot resume the legacy Game backfill because the fixed lifecycle event has conflicting audit facts.', 1;
END;

IF EXISTS (SELECT 1 FROM dbo.Games WHERE GameID = @LegacyGameID)
   AND NOT EXISTS
   (
       SELECT 1
       FROM dbo.GameLifecycleEvents
       WHERE GameLifecycleEventID = @LegacyLifecycleEventID
   )
BEGIN
    INSERT INTO dbo.GameLifecycleEvents
    (
        GameLifecycleEventID,
        GameID,
        EventType,
        SubjectPlayerID,
        ActorPlayerID,
        FromStatus,
        ToStatus,
        Reason,
        CorrelationID,
        FactJson,
        CreatedAt
    )
    SELECT
        @LegacyLifecycleEventID,
        GameID,
        N'LegacyImported',
        NULL,
        NULL,
        NULL,
        Status,
        NULL,
        NULL,
        N'{"source":"legacy-single-lineage","schemaVersion":1}',
        CreatedAt
    FROM dbo.Games
    WHERE GameID = @LegacyGameID;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GameLifecycleEvents')
      AND name = N'IX_GameLifecycleEvents_Game_CreatedAt'
)
BEGIN
    CREATE INDEX IX_GameLifecycleEvents_Game_CreatedAt
        ON dbo.GameLifecycleEvents(GameID, CreatedAt, GameLifecycleEventID);
END;

GO

CREATE OR ALTER TRIGGER dbo.TR_CycleConfigurations_ProtectMaterializedProvenance
ON dbo.CycleConfigurations
AFTER UPDATE
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
END;

GO
