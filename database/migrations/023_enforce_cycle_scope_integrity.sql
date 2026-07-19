SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

-- Deployment assumptions:
-- * migration 022 has completed and gameplay writes are quiesced for the
--   expand/backfill/audit/contract boundary;
-- * BattleRecords fleet compatibility columns contain either comma-separated
--   GUIDs or JSON arrays of GUID strings and remain available for old readers;
-- * battle Side is the historical authority. A Fleet's current FactionID is
--   intentionally not compared with the historical attacking/defending side.

IF OBJECT_ID(N'dbo.Cycles', N'U') IS NULL
   OR OBJECT_ID(N'dbo.CycleConfigurations', N'U') IS NULL
   OR OBJECT_ID(N'dbo.GameEnrolments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.MatchParticipants', N'U') IS NULL
   OR COL_LENGTH(N'dbo.Cycles', N'GameID') IS NULL
   OR COL_LENGTH(N'dbo.Cycles', N'CycleConfigurationID') IS NULL
BEGIN
    THROW 51031, 'Cannot enforce Cycle scope integrity before migration 022 has established the Game foundation.', 1;
END;

IF COL_LENGTH(N'dbo.MatchParticipants', N'GameID') IS NULL
BEGIN
    ALTER TABLE dbo.MatchParticipants ADD GameID UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'dbo.BattleFleetParticipants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.BattleFleetParticipants
    (
        BattleID UNIQUEIDENTIFIER NOT NULL,
        CycleID UNIQUEIDENTIFIER NOT NULL,
        FleetID UNIQUEIDENTIFIER NOT NULL,
        Side NVARCHAR(16) NOT NULL
    );
END;
GO

IF EXISTS
(
    SELECT 1
    FROM sys.columns AS columnMetadata
    INNER JOIN sys.types AS typeMetadata ON typeMetadata.user_type_id = columnMetadata.user_type_id
    WHERE columnMetadata.object_id = OBJECT_ID(N'dbo.MatchParticipants')
      AND columnMetadata.name = N'GameID'
      AND typeMetadata.name <> N'uniqueidentifier'
)
BEGIN
    THROW 51032, 'MatchParticipants.GameID exists with an incompatible SQL type.', 1;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.CycleConfigurations')
      AND name = N'UX_CycleConfigurations_CycleConfigurationID_GameID'
)
BEGIN
    CREATE UNIQUE INDEX UX_CycleConfigurations_CycleConfigurationID_GameID
        ON dbo.CycleConfigurations(CycleConfigurationID, GameID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_CycleID_GameID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Cycles_CycleID_GameID ON dbo.Cycles(CycleID, GameID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.GalaxySectors')
      AND name = N'UX_GalaxySectors_SectorID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_GalaxySectors_SectorID_CycleID
        ON dbo.GalaxySectors(SectorID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Systems')
      AND name = N'UX_Systems_SystemID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Systems_SystemID_CycleID ON dbo.Systems(SystemID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Factions')
      AND name = N'UX_Factions_FactionID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Factions_FactionID_CycleID ON dbo.Factions(FactionID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Admirals')
      AND name = N'UX_Admirals_AdmiralID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Admirals_AdmiralID_CycleID ON dbo.Admirals(AdmiralID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Fleets')
      AND name = N'UX_Fleets_FleetID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Fleets_FleetID_CycleID ON dbo.Fleets(FleetID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.FleetOrders')
      AND name = N'UX_FleetOrders_FleetOrderID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_FleetOrders_FleetOrderID_CycleID
        ON dbo.FleetOrders(FleetOrderID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Events')
      AND name = N'UX_Events_EventID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Events_EventID_CycleID ON dbo.Events(EventID, CycleID);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BattleRecords')
      AND name = N'UX_BattleRecords_BattleID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_BattleRecords_BattleID_CycleID
        ON dbo.BattleRecords(BattleID, CycleID);
END;

CREATE TABLE #RequiredUniqueIndexes
(
    TableName SYSNAME NOT NULL,
    IndexName SYSNAME NOT NULL,
    FirstColumn SYSNAME NOT NULL,
    SecondColumn SYSNAME NOT NULL
);

INSERT INTO #RequiredUniqueIndexes(TableName, IndexName, FirstColumn, SecondColumn)
VALUES
    (N'dbo.CycleConfigurations', N'UX_CycleConfigurations_CycleConfigurationID_GameID', N'CycleConfigurationID', N'GameID'),
    (N'dbo.Cycles', N'UX_Cycles_CycleID_GameID', N'CycleID', N'GameID'),
    (N'dbo.GalaxySectors', N'UX_GalaxySectors_SectorID_CycleID', N'SectorID', N'CycleID'),
    (N'dbo.Systems', N'UX_Systems_SystemID_CycleID', N'SystemID', N'CycleID'),
    (N'dbo.Empires', N'UX_Empires_EmpireID_CycleID', N'EmpireID', N'CycleID'),
    (N'dbo.Factions', N'UX_Factions_FactionID_CycleID', N'FactionID', N'CycleID'),
    (N'dbo.GameEnrolments', N'UX_GameEnrolments_Game_Player', N'GameID', N'PlayerID'),
    (N'dbo.Admirals', N'UX_Admirals_AdmiralID_CycleID', N'AdmiralID', N'CycleID'),
    (N'dbo.Fleets', N'UX_Fleets_FleetID_CycleID', N'FleetID', N'CycleID'),
    (N'dbo.FleetOrders', N'UX_FleetOrders_FleetOrderID_CycleID', N'FleetOrderID', N'CycleID'),
    (N'dbo.Events', N'UX_Events_EventID_CycleID', N'EventID', N'CycleID'),
    (N'dbo.BattleRecords', N'UX_BattleRecords_BattleID_CycleID', N'BattleID', N'CycleID');

DECLARE @InvalidSupportingIndexes NVARCHAR(1800);
SELECT @InvalidSupportingIndexes = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(invalid.TableName, N'.', invalid.IndexName)),
    N', ')
FROM
(
    SELECT TOP (20) expected.TableName, expected.IndexName
    FROM #RequiredUniqueIndexes AS expected
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM sys.indexes AS indexMetadata
        INNER JOIN sys.index_columns AS firstIndexColumn
            ON firstIndexColumn.object_id = indexMetadata.object_id
           AND firstIndexColumn.index_id = indexMetadata.index_id
           AND firstIndexColumn.key_ordinal = 1
        INNER JOIN sys.columns AS firstColumn
            ON firstColumn.object_id = firstIndexColumn.object_id
           AND firstColumn.column_id = firstIndexColumn.column_id
        INNER JOIN sys.index_columns AS secondIndexColumn
            ON secondIndexColumn.object_id = indexMetadata.object_id
           AND secondIndexColumn.index_id = indexMetadata.index_id
           AND secondIndexColumn.key_ordinal = 2
        INNER JOIN sys.columns AS secondColumn
            ON secondColumn.object_id = secondIndexColumn.object_id
           AND secondColumn.column_id = secondIndexColumn.column_id
        WHERE indexMetadata.object_id = OBJECT_ID(expected.TableName)
          AND indexMetadata.name = expected.IndexName
          AND indexMetadata.is_unique = 1
          AND indexMetadata.is_disabled = 0
          AND firstColumn.name = expected.FirstColumn
          AND secondColumn.name = expected.SecondColumn
          AND 2 =
          (
              SELECT COUNT(*)
              FROM sys.index_columns AS keyColumn
              WHERE keyColumn.object_id = indexMetadata.object_id
                AND keyColumn.index_id = indexMetadata.index_id
                AND keyColumn.key_ordinal > 0
          )
    )
    ORDER BY expected.TableName, expected.IndexName
) AS invalid;

IF @InvalidSupportingIndexes IS NOT NULL
BEGIN
    DECLARE @InvalidSupportingIndexMessage NVARCHAR(2048) = CONCAT(
        N'Cannot enforce Cycle scope because a required supporting unique index is missing or has an unexpected definition: ',
        @InvalidSupportingIndexes, N'.');
    THROW 51046, @InvalidSupportingIndexMessage, 1;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants')
      AND name = N'IX_MatchParticipants_Game_Player_Cycle'
)
BEGIN
    CREATE INDEX IX_MatchParticipants_Game_Player_Cycle
        ON dbo.MatchParticipants(GameID, PlayerID, CycleID);
END;
GO

IF COL_LENGTH(N'dbo.BattleFleetParticipants', N'BattleID') IS NULL
   OR COL_LENGTH(N'dbo.BattleFleetParticipants', N'CycleID') IS NULL
   OR COL_LENGTH(N'dbo.BattleFleetParticipants', N'FleetID') IS NULL
   OR COL_LENGTH(N'dbo.BattleFleetParticipants', N'Side') IS NULL
   OR EXISTS
   (
       SELECT 1
       FROM sys.columns AS columnMetadata
       INNER JOIN sys.types AS typeMetadata ON typeMetadata.user_type_id = columnMetadata.user_type_id
       WHERE columnMetadata.object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
         AND
         (
             (columnMetadata.name IN (N'BattleID', N'CycleID', N'FleetID')
              AND (typeMetadata.name <> N'uniqueidentifier' OR columnMetadata.is_nullable = 1))
             OR (columnMetadata.name = N'Side'
                 AND (typeMetadata.name <> N'nvarchar' OR columnMetadata.max_length <> 32 OR columnMetadata.is_nullable = 1))
         )
   )
BEGIN
    THROW 51033, 'BattleFleetParticipants exists with an incompatible or nullable column definition.', 1;
END;

DECLARE @ConflictingParticipantGames NVARCHAR(1800);
SELECT @ConflictingParticipantGames = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.MatchParticipantID), N'=',
        CONVERT(NVARCHAR(36), invalid.GameID), N'/',
        CONVERT(NVARCHAR(36), invalid.ExpectedGameID))),
    N', ')
FROM
(
    SELECT TOP (12)
        participant.MatchParticipantID,
        participant.GameID,
        cycle.GameID AS ExpectedGameID
    FROM dbo.MatchParticipants AS participant
    LEFT JOIN dbo.Cycles AS cycle ON cycle.CycleID = participant.CycleID
    WHERE cycle.CycleID IS NULL
       OR (participant.GameID IS NOT NULL AND participant.GameID <> cycle.GameID)
    ORDER BY participant.MatchParticipantID
) AS invalid;

IF @ConflictingParticipantGames IS NOT NULL
BEGIN
    DECLARE @ConflictingParticipantGameMessage NVARCHAR(2048) = CONCAT(
        N'Cannot backfill MatchParticipants.GameID because participant Cycle ownership conflicts: ',
        @ConflictingParticipantGames, N'.');
    THROW 51034, @ConflictingParticipantGameMessage, 1;
END;

UPDATE participant
SET GameID = cycle.GameID
FROM dbo.MatchParticipants AS participant
INNER JOIN dbo.Cycles AS cycle ON cycle.CycleID = participant.CycleID
WHERE participant.GameID IS NULL;

IF EXISTS (SELECT 1 FROM dbo.MatchParticipants WHERE GameID IS NULL)
BEGIN
    THROW 51035, 'Cannot contract MatchParticipants.GameID because at least one participant has no owning Game.', 1;
END;

CREATE TABLE #BattleFleetSources
(
    BattleID UNIQUEIDENTIFIER NOT NULL,
    CycleID UNIQUEIDENTIFIER NOT NULL,
    Side NVARCHAR(16) NOT NULL,
    RawValue NVARCHAR(MAX) NOT NULL,
    IsJsonArray BIT NOT NULL
);

INSERT INTO #BattleFleetSources(BattleID, CycleID, Side, RawValue, IsJsonArray)
SELECT
    battle.BattleID,
    battle.CycleID,
    source.Side,
    source.RawValue,
    CONVERT(BIT, CASE WHEN LEFT(LTRIM(RTRIM(source.RawValue)), 1) = N'[' THEN 1 ELSE 0 END)
FROM dbo.BattleRecords AS battle
CROSS APPLY
(
    VALUES
        (N'Attacker', battle.AttackerFleetIDs),
        (N'Defender', battle.DefenderFleetIDs)
) AS source(Side, RawValue);

DECLARE @EmptyBattleSides NVARCHAR(1800);
SELECT @EmptyBattleSides = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side)),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, Side
    FROM #BattleFleetSources
    WHERE LEN(LTRIM(RTRIM(RawValue))) = 0
    ORDER BY BattleID, Side
) AS invalid;

IF @EmptyBattleSides IS NOT NULL
BEGIN
    DECLARE @EmptyBattleSideMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because each battle side must contain at least one fleet: ',
        @EmptyBattleSides, N'.');
    THROW 51036, @EmptyBattleSideMessage, 1;
END;

DECLARE @MalformedBattleLists NVARCHAR(1800);
SELECT @MalformedBattleLists = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side)),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, Side
    FROM #BattleFleetSources
    WHERE (IsJsonArray = 1 AND ISJSON(RawValue) <> 1)
       OR (IsJsonArray = 0 AND ISJSON(RawValue) = 1)
    ORDER BY BattleID, Side
) AS invalid;

IF @MalformedBattleLists IS NOT NULL
BEGIN
    DECLARE @MalformedBattleListMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a fleet list is neither strict CSV nor a JSON array: ',
        @MalformedBattleLists, N'.');
    THROW 51037, @MalformedBattleListMessage, 1;
END;

CREATE TABLE #ParsedBattleFleetTokens
(
    BattleID UNIQUEIDENTIFIER NOT NULL,
    CycleID UNIQUEIDENTIFIER NOT NULL,
    Side NVARCHAR(16) NOT NULL,
    TokenOrdinal INT NOT NULL,
    RawToken NVARCHAR(MAX) NULL,
    TokenType INT NOT NULL,
    FleetID UNIQUEIDENTIFIER NULL
);

INSERT INTO #ParsedBattleFleetTokens
(
    BattleID,
    CycleID,
    Side,
    TokenOrdinal,
    RawToken,
    TokenType,
    FleetID
)
SELECT
    source.BattleID,
    source.CycleID,
    source.Side,
    TRY_CONVERT(INT, token.[key]),
    token.[value],
    token.[type],
    TRY_CONVERT(UNIQUEIDENTIFIER, LTRIM(RTRIM(token.[value])))
FROM #BattleFleetSources AS source
CROSS APPLY OPENJSON(source.RawValue) AS token
WHERE source.IsJsonArray = 1;

INSERT INTO #ParsedBattleFleetTokens
(
    BattleID,
    CycleID,
    Side,
    TokenOrdinal,
    RawToken,
    TokenType,
    FleetID
)
SELECT
    source.BattleID,
    source.CycleID,
    source.Side,
    CONVERT(INT, ROW_NUMBER() OVER
    (
        PARTITION BY source.BattleID, source.Side
        ORDER BY (SELECT NULL)
    ) - 1),
    token.[value],
    1,
    TRY_CONVERT(UNIQUEIDENTIFIER, LTRIM(RTRIM(token.[value])))
FROM #BattleFleetSources AS source
CROSS APPLY STRING_SPLIT(source.RawValue, N',') AS token
WHERE source.IsJsonArray = 0;

DECLARE @MalformedCsvFleetLists NVARCHAR(1800);
SELECT @MalformedCsvFleetLists = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side)),
    N', ')
FROM
(
    SELECT TOP (12) source.BattleID, source.Side
    FROM #BattleFleetSources AS source
    WHERE source.IsJsonArray = 0
      AND
      (
          SELECT COUNT_BIG(*)
          FROM #ParsedBattleFleetTokens AS token
          WHERE token.BattleID = source.BattleID
            AND token.Side = source.Side
      ) <> CONVERT(BIGINT, LEN(source.RawValue) - LEN(REPLACE(source.RawValue, N',', N'')) + 1)
    ORDER BY source.BattleID, source.Side
) AS invalid;

IF @MalformedCsvFleetLists IS NOT NULL
BEGIN
    DECLARE @MalformedCsvFleetListMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a CSV fleet list contains an empty field: ',
        @MalformedCsvFleetLists, N'.');
    THROW 51043, @MalformedCsvFleetListMessage, 1;
END;

DECLARE @NonStringJsonFleetTokens NVARCHAR(1800);
SELECT @NonStringJsonFleetTokens = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side, N'[', invalid.TokenOrdinal, N']')),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, Side, TokenOrdinal
    FROM #ParsedBattleFleetTokens
    WHERE TokenType <> 1
    ORDER BY BattleID, Side, TokenOrdinal
) AS invalid;

IF @NonStringJsonFleetTokens IS NOT NULL
BEGIN
    DECLARE @NonStringJsonFleetTokenMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because JSON fleet arrays may contain strings only: ',
        @NonStringJsonFleetTokens, N'.');
    THROW 51038, @NonStringJsonFleetTokenMessage, 1;
END;

DECLARE @InvalidBattleFleetTokens NVARCHAR(1800);
SELECT @InvalidBattleFleetTokens = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side, N'[', invalid.TokenOrdinal, N']=',
        LEFT(COALESCE(invalid.RawToken, N'<null>'), 80))),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, Side, TokenOrdinal, RawToken
    FROM #ParsedBattleFleetTokens
    WHERE FleetID IS NULL
       OR LEN(LTRIM(RTRIM(COALESCE(RawToken, N'')))) = 0
       OR LEN(LTRIM(RTRIM(COALESCE(RawToken, N'')))) <> 36
       OR LOWER(LTRIM(RTRIM(COALESCE(RawToken, N'')))) <>
          LOWER(CONVERT(NVARCHAR(36), FleetID))
    ORDER BY BattleID, Side, TokenOrdinal
) AS invalid;

IF @InvalidBattleFleetTokens IS NOT NULL
BEGIN
    DECLARE @InvalidBattleFleetTokenMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a fleet token is empty or is not a GUID: ',
        @InvalidBattleFleetTokens, N'.');
    THROW 51039, @InvalidBattleFleetTokenMessage, 1;
END;

DECLARE @MissingParsedBattleSides NVARCHAR(1800);
SELECT @MissingParsedBattleSides = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side)),
    N', ')
FROM
(
    SELECT TOP (12) source.BattleID, source.Side
    FROM #BattleFleetSources AS source
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM #ParsedBattleFleetTokens AS token
        WHERE token.BattleID = source.BattleID
          AND token.Side = source.Side
    )
    ORDER BY source.BattleID, source.Side
) AS invalid;

IF @MissingParsedBattleSides IS NOT NULL
BEGIN
    DECLARE @MissingParsedBattleSideMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a parsed battle side is empty: ',
        @MissingParsedBattleSides, N'.');
    THROW 51040, @MissingParsedBattleSideMessage, 1;
END;

DECLARE @DuplicateBattleFleetTokens NVARCHAR(1800);
SELECT @DuplicateBattleFleetTokens = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', CONVERT(NVARCHAR(36), invalid.FleetID),
        N' x', invalid.Occurrences)),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, FleetID, COUNT_BIG(*) AS Occurrences
    FROM #ParsedBattleFleetTokens
    GROUP BY BattleID, FleetID
    HAVING COUNT_BIG(*) > 1
    ORDER BY BattleID, FleetID
) AS invalid;

IF @DuplicateBattleFleetTokens IS NOT NULL
BEGIN
    DECLARE @DuplicateBattleFleetTokenMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a fleet occurs more than once in one battle or on both sides: ',
        @DuplicateBattleFleetTokens, N'.');
    THROW 51041, @DuplicateBattleFleetTokenMessage, 1;
END;

DECLARE @InvalidBattleFleetScopes NVARCHAR(1800);
SELECT @InvalidBattleFleetScopes = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side, N'=',
        CONVERT(NVARCHAR(36), invalid.FleetID), N'@', CONVERT(NVARCHAR(36), invalid.CycleID))),
    N', ')
FROM
(
    SELECT TOP (12) token.BattleID, token.Side, token.FleetID, token.CycleID
    FROM #ParsedBattleFleetTokens AS token
    LEFT JOIN dbo.Fleets AS fleet
        ON fleet.FleetID = token.FleetID
       AND fleet.CycleID = token.CycleID
    WHERE fleet.FleetID IS NULL
    ORDER BY token.BattleID, token.Side, token.FleetID
) AS invalid;

IF @InvalidBattleFleetScopes IS NOT NULL
BEGIN
    DECLARE @InvalidBattleFleetScopeMessage NVARCHAR(2048) = CONCAT(
        N'Cannot normalise battle fleets because a referenced fleet is missing or belongs to another Cycle: ',
        @InvalidBattleFleetScopes, N'.');
    THROW 51042, @InvalidBattleFleetScopeMessage, 1;
END;

CREATE TABLE #ExpectedBattleFleetParticipants
(
    BattleID UNIQUEIDENTIFIER NOT NULL,
    CycleID UNIQUEIDENTIFIER NOT NULL,
    FleetID UNIQUEIDENTIFIER NOT NULL,
    Side NVARCHAR(16) NOT NULL
);

INSERT INTO #ExpectedBattleFleetParticipants(BattleID, CycleID, FleetID, Side)
SELECT BattleID, CycleID, FleetID, Side
FROM #ParsedBattleFleetTokens;

DECLARE @ConflictingStoredBattleFleets NVARCHAR(1800);
SELECT @ConflictingStoredBattleFleets = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', invalid.Side, N'=',
        CONVERT(NVARCHAR(36), invalid.FleetID), N'@', CONVERT(NVARCHAR(36), invalid.CycleID))),
    N', ')
FROM
(
    SELECT TOP (12) stored.BattleID, stored.CycleID, stored.FleetID, stored.Side
    FROM dbo.BattleFleetParticipants AS stored
    LEFT JOIN #ExpectedBattleFleetParticipants AS expected
        ON expected.BattleID = stored.BattleID
       AND expected.CycleID = stored.CycleID
       AND expected.FleetID = stored.FleetID
       AND expected.Side = stored.Side
    WHERE expected.BattleID IS NULL
    ORDER BY stored.BattleID, stored.Side, stored.FleetID
) AS invalid;

IF @ConflictingStoredBattleFleets IS NOT NULL
BEGIN
    DECLARE @ConflictingStoredBattleFleetMessage NVARCHAR(2048) = CONCAT(
        N'Cannot resume battle-fleet backfill because stored normalised membership conflicts with compatibility columns: ',
        @ConflictingStoredBattleFleets, N'.');
    THROW 51044, @ConflictingStoredBattleFleetMessage, 1;
END;

DECLARE @IncompleteStoredBattleFleets NVARCHAR(1800);
SELECT @IncompleteStoredBattleFleets = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N' missing ',
        CONVERT(NVARCHAR(36), invalid.FleetID), N'/', invalid.Side)),
    N', ')
FROM
(
    SELECT TOP (12) expected.BattleID, expected.FleetID, expected.Side
    FROM #ExpectedBattleFleetParticipants AS expected
    WHERE EXISTS
    (
        SELECT 1
        FROM dbo.BattleFleetParticipants AS anyStored
        WHERE anyStored.BattleID = expected.BattleID
    )
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.BattleFleetParticipants AS stored
          WHERE stored.BattleID = expected.BattleID
            AND stored.CycleID = expected.CycleID
            AND stored.FleetID = expected.FleetID
            AND stored.Side = expected.Side
      )
    ORDER BY expected.BattleID, expected.Side, expected.FleetID
) AS invalid;

IF @IncompleteStoredBattleFleets IS NOT NULL
BEGIN
    DECLARE @IncompleteStoredBattleFleetMessage NVARCHAR(2048) = CONCAT(
        N'Cannot resume battle-fleet backfill because a partially populated battle does not exactly match its compatibility columns: ',
        @IncompleteStoredBattleFleets, N'.');
    THROW 51052, @IncompleteStoredBattleFleetMessage, 1;
END;

DECLARE @DuplicateStoredBattleFleets NVARCHAR(1800);
SELECT @DuplicateStoredBattleFleets = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        CONVERT(NVARCHAR(36), invalid.BattleID), N'/', CONVERT(NVARCHAR(36), invalid.FleetID),
        N' x', invalid.Occurrences)),
    N', ')
FROM
(
    SELECT TOP (12) BattleID, FleetID, COUNT_BIG(*) AS Occurrences
    FROM dbo.BattleFleetParticipants
    GROUP BY BattleID, FleetID
    HAVING COUNT_BIG(*) > 1
    ORDER BY BattleID, FleetID
) AS invalid;

IF @DuplicateStoredBattleFleets IS NOT NULL
BEGIN
    DECLARE @DuplicateStoredBattleFleetMessage NVARCHAR(2048) = CONCAT(
        N'Cannot resume battle-fleet backfill because stored membership contains duplicate battle/fleet pairs: ',
        @DuplicateStoredBattleFleets, N'.');
    THROW 51045, @DuplicateStoredBattleFleetMessage, 1;
END;

-- Rebuild this named constraint even on resume.  Checking its name alone is not
-- sufficient: an interrupted/manual deployment may have left a weaker predicate
-- under the expected name.
IF EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND name = N'CK_BattleFleetParticipants_Side'
)
BEGIN
    ALTER TABLE dbo.BattleFleetParticipants
        DROP CONSTRAINT CK_BattleFleetParticipants_Side;
END;

ALTER TABLE dbo.BattleFleetParticipants WITH CHECK
    ADD CONSTRAINT CK_BattleFleetParticipants_Side
        CHECK (Side IN (N'Attacker', N'Defender'));

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND name = N'UX_BattleFleetParticipants_Battle_Fleet'
)
BEGIN
    DROP INDEX UX_BattleFleetParticipants_Battle_Fleet
        ON dbo.BattleFleetParticipants;
END;

CREATE UNIQUE INDEX UX_BattleFleetParticipants_Battle_Fleet
    ON dbo.BattleFleetParticipants(BattleID, FleetID);

INSERT INTO dbo.BattleFleetParticipants(BattleID, CycleID, FleetID, Side)
SELECT expected.BattleID, expected.CycleID, expected.FleetID, expected.Side
FROM #ExpectedBattleFleetParticipants AS expected
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.BattleFleetParticipants AS stored
    WHERE stored.BattleID = expected.BattleID
);

-- As with the side constraint, rebuild the child-side FK index so its name cannot
-- conceal an incompatible key order or included-column-only definition.
IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND name = N'IX_BattleFleetParticipants_Fleet_Cycle_Battle'
)
BEGIN
    DROP INDEX IX_BattleFleetParticipants_Fleet_Cycle_Battle
        ON dbo.BattleFleetParticipants;
END;

CREATE INDEX IX_BattleFleetParticipants_Fleet_Cycle_Battle
    ON dbo.BattleFleetParticipants(FleetID, CycleID, BattleID);
GO

CREATE TABLE #CycleScopeViolations
(
    RelationshipName NVARCHAR(128) NOT NULL,
    ChildID UNIQUEIDENTIFIER NOT NULL,
    ReferenceID UNIQUEIDENTIFIER NULL,
    ScopeID UNIQUEIDENTIFIER NULL
);

INSERT INTO #CycleScopeViolations
SELECT N'Cycles.ConfigurationInGame', cycle.CycleID, cycle.CycleConfigurationID, cycle.GameID
FROM dbo.Cycles AS cycle
LEFT JOIN dbo.CycleConfigurations AS configuration
    ON configuration.CycleConfigurationID = cycle.CycleConfigurationID
   AND configuration.GameID = cycle.GameID
WHERE cycle.CycleConfigurationID IS NULL
   OR cycle.GameID IS NULL
   OR configuration.CycleConfigurationID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Cycles.PreviousInGame', cycle.CycleID, cycle.PreviousCycleID, cycle.GameID
FROM dbo.Cycles AS cycle
LEFT JOIN dbo.Cycles AS previousCycle
    ON previousCycle.CycleID = cycle.PreviousCycleID
   AND previousCycle.GameID = cycle.GameID
WHERE cycle.PreviousCycleID IS NOT NULL
  AND previousCycle.CycleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Systems.SectorInCycle', system.SystemID, system.SectorID, system.CycleID
FROM dbo.Systems AS system
LEFT JOIN dbo.GalaxySectors AS sector
    ON sector.SectorID = system.SectorID
   AND sector.CycleID = system.CycleID
WHERE system.SectorID IS NOT NULL
  AND sector.SectorID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Empires.HomeSystemInCycle', empire.EmpireID, empire.HomeSystemID, empire.CycleID
FROM dbo.Empires AS empire
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = empire.HomeSystemID
   AND system.CycleID = empire.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Factions.EmpireInCycle', faction.FactionID, faction.EmpireID, faction.CycleID
FROM dbo.Factions AS faction
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = faction.EmpireID
   AND empire.CycleID = faction.CycleID
WHERE faction.EmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'MatchParticipants.EmpireInCycle', participant.MatchParticipantID, participant.EmpireID, participant.CycleID
FROM dbo.MatchParticipants AS participant
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = participant.EmpireID
   AND empire.CycleID = participant.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'MatchParticipants.CycleInGame', participant.MatchParticipantID, participant.CycleID, participant.GameID
FROM dbo.MatchParticipants AS participant
LEFT JOIN dbo.Cycles AS cycle
    ON cycle.CycleID = participant.CycleID
   AND cycle.GameID = participant.GameID
WHERE participant.GameID IS NULL
   OR cycle.CycleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'MatchParticipants.EnrolmentInGame', participant.MatchParticipantID, participant.PlayerID, participant.GameID
FROM dbo.MatchParticipants AS participant
LEFT JOIN dbo.GameEnrolments AS enrolment
    ON enrolment.GameID = participant.GameID
   AND enrolment.PlayerID = participant.PlayerID
WHERE participant.GameID IS NULL
   OR enrolment.GameEnrolmentID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'EmpireMetrics.EmpireInCycle', metric.EmpireMetricID, metric.EmpireID, metric.CycleID
FROM dbo.EmpireMetrics AS metric
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = metric.EmpireID
   AND empire.CycleID = metric.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ShipConstructions.EmpireInCycle', construction.ShipConstructionID, construction.EmpireID, construction.CycleID
FROM dbo.ShipConstructions AS construction
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = construction.EmpireID
   AND empire.CycleID = construction.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'CycleRankings.EmpireInCycle', ranking.CycleRankingID, ranking.EmpireID, ranking.CycleID
FROM dbo.CycleRankings AS ranking
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = ranking.EmpireID
   AND empire.CycleID = ranking.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Admirals.EmpireInCycle', admiral.AdmiralID, admiral.EmpireID, admiral.CycleID
FROM dbo.Admirals AS admiral
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = admiral.EmpireID
   AND empire.CycleID = admiral.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ColonialOutposts.EmpireInCycle', outpost.ColonialOutpostID, outpost.EmpireID, outpost.CycleID
FROM dbo.ColonialOutposts AS outpost
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = outpost.EmpireID
   AND empire.CycleID = outpost.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ColonialOutposts.SystemInCycle', outpost.ColonialOutpostID, outpost.SystemID, outpost.CycleID
FROM dbo.ColonialOutposts AS outpost
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = outpost.SystemID
   AND system.CycleID = outpost.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'EmpireDoctrineUnlocks.EmpireInCycle', unlock.EmpireDoctrineUnlockID, unlock.EmpireID, unlock.CycleID
FROM dbo.EmpireDoctrineUnlocks AS unlock
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = unlock.EmpireID
   AND empire.CycleID = unlock.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'DiplomaticRelationships.FirstEmpireInCycle', relationship.DiplomaticRelationshipID, relationship.FirstEmpireID, relationship.CycleID
FROM dbo.DiplomaticRelationships AS relationship
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = relationship.FirstEmpireID
   AND empire.CycleID = relationship.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'DiplomaticRelationships.SecondEmpireInCycle', relationship.DiplomaticRelationshipID, relationship.SecondEmpireID, relationship.CycleID
FROM dbo.DiplomaticRelationships AS relationship
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = relationship.SecondEmpireID
   AND empire.CycleID = relationship.CycleID
WHERE empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'SystemLinks.SystemAInCycle', link.SystemLinkID, link.SystemAID, link.CycleID
FROM dbo.SystemLinks AS link
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = link.SystemAID
   AND system.CycleID = link.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'SystemLinks.SystemBInCycle', link.SystemLinkID, link.SystemBID, link.CycleID
FROM dbo.SystemLinks AS link
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = link.SystemBID
   AND system.CycleID = link.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Fleets.EmpireInCycle', fleet.FleetID, fleet.EmpireID, fleet.CycleID
FROM dbo.Fleets AS fleet
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = fleet.EmpireID
   AND empire.CycleID = fleet.CycleID
WHERE fleet.EmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Fleets.FactionInCycle', fleet.FleetID, fleet.FactionID, fleet.CycleID
FROM dbo.Fleets AS fleet
LEFT JOIN dbo.Factions AS faction
    ON faction.FactionID = fleet.FactionID
   AND faction.CycleID = fleet.CycleID
WHERE faction.FactionID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Fleets.CurrentSystemInCycle', fleet.FleetID, fleet.CurrentSystemID, fleet.CycleID
FROM dbo.Fleets AS fleet
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = fleet.CurrentSystemID
   AND system.CycleID = fleet.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Fleets.DestinationSystemInCycle', fleet.FleetID, fleet.DestinationSystemID, fleet.CycleID
FROM dbo.Fleets AS fleet
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = fleet.DestinationSystemID
   AND system.CycleID = fleet.CycleID
WHERE fleet.DestinationSystemID IS NOT NULL
  AND system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Fleets.AdmiralInCycle', fleet.FleetID, fleet.AdmiralID, fleet.CycleID
FROM dbo.Fleets AS fleet
LEFT JOIN dbo.Admirals AS admiral
    ON admiral.AdmiralID = fleet.AdmiralID
   AND admiral.CycleID = fleet.CycleID
WHERE fleet.AdmiralID IS NOT NULL
  AND admiral.AdmiralID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'FleetOrders.FleetInCycle', fleetOrder.FleetOrderID, fleetOrder.FleetID, fleetOrder.CycleID
FROM dbo.FleetOrders AS fleetOrder
LEFT JOIN dbo.Fleets AS fleet
    ON fleet.FleetID = fleetOrder.FleetID
   AND fleet.CycleID = fleetOrder.CycleID
WHERE fleet.FleetID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'FleetOrders.TargetSystemInCycle', fleetOrder.FleetOrderID, fleetOrder.TargetSystemID, fleetOrder.CycleID
FROM dbo.FleetOrders AS fleetOrder
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = fleetOrder.TargetSystemID
   AND system.CycleID = fleetOrder.CycleID
WHERE fleetOrder.TargetSystemID IS NOT NULL
  AND system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'FleetOrders.TargetEmpireInCycle', fleetOrder.FleetOrderID, fleetOrder.TargetEmpireID, fleetOrder.CycleID
FROM dbo.FleetOrders AS fleetOrder
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = fleetOrder.TargetEmpireID
   AND empire.CycleID = fleetOrder.CycleID
WHERE fleetOrder.TargetEmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'FleetOrders.TargetFactionInCycle', fleetOrder.FleetOrderID, fleetOrder.TargetFactionID, fleetOrder.CycleID
FROM dbo.FleetOrders AS fleetOrder
LEFT JOIN dbo.Factions AS faction
    ON faction.FactionID = fleetOrder.TargetFactionID
   AND faction.CycleID = fleetOrder.CycleID
WHERE fleetOrder.TargetFactionID IS NOT NULL
  AND faction.FactionID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'FleetOrders.SupersededOrderInCycle', fleetOrder.FleetOrderID, fleetOrder.SupersededByOrderID, fleetOrder.CycleID
FROM dbo.FleetOrders AS fleetOrder
LEFT JOIN dbo.FleetOrders AS supersedingOrder
    ON supersedingOrder.FleetOrderID = fleetOrder.SupersededByOrderID
   AND supersedingOrder.CycleID = fleetOrder.CycleID
WHERE fleetOrder.SupersededByOrderID IS NOT NULL
  AND supersedingOrder.FleetOrderID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Events.SystemInCycle', gameEvent.EventID, gameEvent.SystemID, gameEvent.CycleID
FROM dbo.Events AS gameEvent
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = gameEvent.SystemID
   AND system.CycleID = gameEvent.CycleID
WHERE gameEvent.SystemID IS NOT NULL
  AND system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Events.EmpireInCycle', gameEvent.EventID, gameEvent.EmpireID, gameEvent.CycleID
FROM dbo.Events AS gameEvent
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = gameEvent.EmpireID
   AND empire.CycleID = gameEvent.CycleID
WHERE gameEvent.EmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'Events.FactionInCycle', gameEvent.EventID, gameEvent.FactionID, gameEvent.CycleID
FROM dbo.Events AS gameEvent
LEFT JOIN dbo.Factions AS faction
    ON faction.FactionID = gameEvent.FactionID
   AND faction.CycleID = gameEvent.CycleID
WHERE gameEvent.FactionID IS NOT NULL
  AND faction.FactionID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleRecords.SystemInCycle', battle.BattleID, battle.SystemID, battle.CycleID
FROM dbo.BattleRecords AS battle
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = battle.SystemID
   AND system.CycleID = battle.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleRecords.AttackerEmpireInCycle', battle.BattleID, battle.AttackerEmpireID, battle.CycleID
FROM dbo.BattleRecords AS battle
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = battle.AttackerEmpireID
   AND empire.CycleID = battle.CycleID
WHERE battle.AttackerEmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleRecords.DefenderEmpireInCycle', battle.BattleID, battle.DefenderEmpireID, battle.CycleID
FROM dbo.BattleRecords AS battle
LEFT JOIN dbo.Empires AS empire
    ON empire.EmpireID = battle.DefenderEmpireID
   AND empire.CycleID = battle.CycleID
WHERE battle.DefenderEmpireID IS NOT NULL
  AND empire.EmpireID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleRecords.AttackerFactionInCycle', battle.BattleID, battle.AttackerFactionID, battle.CycleID
FROM dbo.BattleRecords AS battle
LEFT JOIN dbo.Factions AS faction
    ON faction.FactionID = battle.AttackerFactionID
   AND faction.CycleID = battle.CycleID
WHERE faction.FactionID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleRecords.DefenderFactionInCycle', battle.BattleID, battle.DefenderFactionID, battle.CycleID
FROM dbo.BattleRecords AS battle
LEFT JOIN dbo.Factions AS faction
    ON faction.FactionID = battle.DefenderFactionID
   AND faction.CycleID = battle.CycleID
WHERE faction.FactionID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleFleetParticipants.BattleInCycle', participant.BattleID, participant.BattleID, participant.CycleID
FROM dbo.BattleFleetParticipants AS participant
LEFT JOIN dbo.BattleRecords AS battle
    ON battle.BattleID = participant.BattleID
   AND battle.CycleID = participant.CycleID
WHERE battle.BattleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'BattleFleetParticipants.FleetInCycle', participant.BattleID, participant.FleetID, participant.CycleID
FROM dbo.BattleFleetParticipants AS participant
LEFT JOIN dbo.Fleets AS fleet
    ON fleet.FleetID = participant.FleetID
   AND fleet.CycleID = participant.CycleID
WHERE fleet.FleetID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ChronicleEntries.SourceEventInCycle', entry.ChronicleEntryID, entry.SourceEventID, entry.CycleID
FROM dbo.ChronicleEntries AS entry
LEFT JOIN dbo.Events AS gameEvent
    ON gameEvent.EventID = entry.SourceEventID
   AND gameEvent.CycleID = entry.CycleID
WHERE entry.SourceEventID IS NOT NULL
  AND gameEvent.EventID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ChronicleEntries.SourceBattleInCycle', entry.ChronicleEntryID, entry.SourceBattleID, entry.CycleID
FROM dbo.ChronicleEntries AS entry
LEFT JOIN dbo.BattleRecords AS battle
    ON battle.BattleID = entry.SourceBattleID
   AND battle.CycleID = entry.CycleID
WHERE entry.SourceBattleID IS NOT NULL
  AND battle.BattleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'ChronicleEntries.SystemInCycle', entry.ChronicleEntryID, entry.SystemID, entry.CycleID
FROM dbo.ChronicleEntries AS entry
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = entry.SystemID
   AND system.CycleID = entry.CycleID
WHERE entry.SystemID IS NOT NULL
  AND system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'CycleMajorEvents.SourceBattleInCycle', majorEvent.CycleMajorEventID, majorEvent.SourceBattleID, majorEvent.CycleID
FROM dbo.CycleMajorEvents AS majorEvent
LEFT JOIN dbo.BattleRecords AS battle
    ON battle.BattleID = majorEvent.SourceBattleID
   AND battle.CycleID = majorEvent.CycleID
WHERE majorEvent.SourceBattleID IS NOT NULL
  AND battle.BattleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'CycleMajorEvents.SystemInCycle', majorEvent.CycleMajorEventID, majorEvent.SystemID, majorEvent.CycleID
FROM dbo.CycleMajorEvents AS majorEvent
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = majorEvent.SystemID
   AND system.CycleID = majorEvent.CycleID
WHERE majorEvent.SystemID IS NOT NULL
  AND system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'SystemHistoricalSignals.SystemInCycle', signal.SystemHistoricalSignalID, signal.SystemID, signal.CycleID
FROM dbo.SystemHistoricalSignals AS signal
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = signal.SystemID
   AND system.CycleID = signal.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'SystemHistoricalSignals.SourceBattleInCycle', signal.SystemHistoricalSignalID, signal.SourceBattleID, signal.CycleID
FROM dbo.SystemHistoricalSignals AS signal
LEFT JOIN dbo.BattleRecords AS battle
    ON battle.BattleID = signal.SourceBattleID
   AND battle.CycleID = signal.CycleID
WHERE signal.SourceBattleID IS NOT NULL
  AND battle.BattleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'AdmiralBattleHistories.AdmiralInCycle', history.AdmiralBattleHistoryID, history.AdmiralID, history.CycleID
FROM dbo.AdmiralBattleHistories AS history
LEFT JOIN dbo.Admirals AS admiral
    ON admiral.AdmiralID = history.AdmiralID
   AND admiral.CycleID = history.CycleID
WHERE admiral.AdmiralID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'AdmiralBattleHistories.BattleInCycle', history.AdmiralBattleHistoryID, history.BattleID, history.CycleID
FROM dbo.AdmiralBattleHistories AS history
LEFT JOIN dbo.BattleRecords AS battle
    ON battle.BattleID = history.BattleID
   AND battle.CycleID = history.CycleID
WHERE battle.BattleID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'AdmiralBattleHistories.SystemInCycle', history.AdmiralBattleHistoryID, history.SystemID, history.CycleID
FROM dbo.AdmiralBattleHistories AS history
LEFT JOIN dbo.Systems AS system
    ON system.SystemID = history.SystemID
   AND system.CycleID = history.CycleID
WHERE system.SystemID IS NULL;

INSERT INTO #CycleScopeViolations
SELECT N'AdmiralBattleHistories.FleetInCycle', history.AdmiralBattleHistoryID, history.FleetID, history.CycleID
FROM dbo.AdmiralBattleHistories AS history
LEFT JOIN dbo.Fleets AS fleet
    ON fleet.FleetID = history.FleetID
   AND fleet.CycleID = history.CycleID
WHERE fleet.FleetID IS NULL;

DECLARE @CycleScopeViolationDetails NVARCHAR(1800);
SELECT @CycleScopeViolationDetails = STRING_AGG(
    CONVERT(NVARCHAR(MAX), CONCAT(
        invalid.RelationshipName, N':', CONVERT(NVARCHAR(36), invalid.ChildID), N'->',
        COALESCE(CONVERT(NVARCHAR(36), invalid.ReferenceID), N'<null>'), N'@',
        COALESCE(CONVERT(NVARCHAR(36), invalid.ScopeID), N'<null>'))),
    N', ')
FROM
(
    SELECT TOP (16) RelationshipName, ChildID, ReferenceID, ScopeID
    FROM #CycleScopeViolations
    ORDER BY RelationshipName, ChildID, ReferenceID
) AS invalid;

IF @CycleScopeViolationDetails IS NOT NULL
BEGIN
    DECLARE @CycleScopeViolationMessage NVARCHAR(2048) = CONCAT(
        N'Cannot enforce Cycle scope because persisted relationships cross scope or have no parent: ',
        @CycleScopeViolationDetails, N'.');
    THROW 51047, @CycleScopeViolationMessage, 1;
END;
GO

CREATE TABLE #RequiredScopeForeignKeys
(
    ConstraintName SYSNAME NOT NULL,
    ChildTable SYSNAME NOT NULL,
    ChildColumn1 SYSNAME NOT NULL,
    ChildColumn2 SYSNAME NOT NULL,
    ParentTable SYSNAME NOT NULL,
    ParentColumn1 SYSNAME NOT NULL,
    ParentColumn2 SYSNAME NOT NULL
);

INSERT INTO #RequiredScopeForeignKeys
(
    ConstraintName,
    ChildTable,
    ChildColumn1,
    ChildColumn2,
    ParentTable,
    ParentColumn1,
    ParentColumn2
)
VALUES
    (N'FK_Cycles_CycleConfigurationsInGame', N'Cycles', N'CycleConfigurationID', N'GameID', N'CycleConfigurations', N'CycleConfigurationID', N'GameID'),
    (N'FK_Cycles_PreviousCyclesInGame', N'Cycles', N'PreviousCycleID', N'GameID', N'Cycles', N'CycleID', N'GameID'),
    (N'FK_Systems_GalaxySectors', N'Systems', N'CycleID', N'SectorID', N'GalaxySectors', N'CycleID', N'SectorID'),
    (N'FK_Empires_HomeSystemsInCycle', N'Empires', N'HomeSystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_Factions_EmpiresInCycle', N'Factions', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_MatchParticipants_EmpiresInCycle', N'MatchParticipants', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_MatchParticipants_CyclesInGame', N'MatchParticipants', N'CycleID', N'GameID', N'Cycles', N'CycleID', N'GameID'),
    (N'FK_MatchParticipants_GameEnrolments', N'MatchParticipants', N'GameID', N'PlayerID', N'GameEnrolments', N'GameID', N'PlayerID'),
    (N'FK_EmpireMetrics_EmpiresInCycle', N'EmpireMetrics', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_ShipConstructions_EmpiresInCycle', N'ShipConstructions', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_CycleRankings_EmpiresInCycle', N'CycleRankings', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_Admirals_EmpiresInCycle', N'Admirals', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_ColonialOutposts_EmpiresInCycle', N'ColonialOutposts', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_ColonialOutposts_SystemsInCycle', N'ColonialOutposts', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_EmpireDoctrineUnlocks_EmpiresInCycle', N'EmpireDoctrineUnlocks', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_DiplomaticRelationships_FirstEmpiresInCycle', N'DiplomaticRelationships', N'FirstEmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_DiplomaticRelationships_SecondEmpiresInCycle', N'DiplomaticRelationships', N'SecondEmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_SystemLinks_SystemAInCycle', N'SystemLinks', N'SystemAID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_SystemLinks_SystemBInCycle', N'SystemLinks', N'SystemBID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_Fleets_EmpiresInCycle', N'Fleets', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_Fleets_FactionsInCycle', N'Fleets', N'CycleID', N'FactionID', N'Factions', N'CycleID', N'FactionID'),
    (N'FK_Fleets_CurrentSystemsInCycle', N'Fleets', N'CurrentSystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_Fleets_DestinationSystemsInCycle', N'Fleets', N'DestinationSystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_Fleets_AdmiralsInCycle', N'Fleets', N'AdmiralID', N'CycleID', N'Admirals', N'AdmiralID', N'CycleID'),
    (N'FK_FleetOrders_FleetsInCycle', N'FleetOrders', N'FleetID', N'CycleID', N'Fleets', N'FleetID', N'CycleID'),
    (N'FK_FleetOrders_TargetSystemsInCycle', N'FleetOrders', N'TargetSystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_FleetOrders_TargetEmpiresInCycle', N'FleetOrders', N'TargetEmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_FleetOrders_TargetFactionsInCycle', N'FleetOrders', N'CycleID', N'TargetFactionID', N'Factions', N'CycleID', N'FactionID'),
    (N'FK_FleetOrders_SupersededOrdersInCycle', N'FleetOrders', N'SupersededByOrderID', N'CycleID', N'FleetOrders', N'FleetOrderID', N'CycleID'),
    (N'FK_Events_SystemsInCycle', N'Events', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_Events_EmpiresInCycle', N'Events', N'EmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_Events_FactionsInCycle', N'Events', N'CycleID', N'FactionID', N'Factions', N'CycleID', N'FactionID'),
    (N'FK_BattleRecords_SystemsInCycle', N'BattleRecords', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_BattleRecords_AttackerEmpiresInCycle', N'BattleRecords', N'AttackerEmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_BattleRecords_DefenderEmpiresInCycle', N'BattleRecords', N'DefenderEmpireID', N'CycleID', N'Empires', N'EmpireID', N'CycleID'),
    (N'FK_BattleRecords_AttackerFactionsInCycle', N'BattleRecords', N'CycleID', N'AttackerFactionID', N'Factions', N'CycleID', N'FactionID'),
    (N'FK_BattleRecords_DefenderFactionsInCycle', N'BattleRecords', N'CycleID', N'DefenderFactionID', N'Factions', N'CycleID', N'FactionID'),
    (N'FK_BattleFleetParticipants_BattlesInCycle', N'BattleFleetParticipants', N'BattleID', N'CycleID', N'BattleRecords', N'BattleID', N'CycleID'),
    (N'FK_BattleFleetParticipants_FleetsInCycle', N'BattleFleetParticipants', N'FleetID', N'CycleID', N'Fleets', N'FleetID', N'CycleID'),
    (N'FK_ChronicleEntries_EventsInCycle', N'ChronicleEntries', N'SourceEventID', N'CycleID', N'Events', N'EventID', N'CycleID'),
    (N'FK_ChronicleEntries_BattlesInCycle', N'ChronicleEntries', N'SourceBattleID', N'CycleID', N'BattleRecords', N'BattleID', N'CycleID'),
    (N'FK_ChronicleEntries_SystemsInCycle', N'ChronicleEntries', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_CycleMajorEvents_BattlesInCycle', N'CycleMajorEvents', N'SourceBattleID', N'CycleID', N'BattleRecords', N'BattleID', N'CycleID'),
    (N'FK_CycleMajorEvents_SystemsInCycle', N'CycleMajorEvents', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_SystemHistoricalSignals_SystemsInCycle', N'SystemHistoricalSignals', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_SystemHistoricalSignals_BattlesInCycle', N'SystemHistoricalSignals', N'SourceBattleID', N'CycleID', N'BattleRecords', N'BattleID', N'CycleID'),
    (N'FK_AdmiralBattleHistories_AdmiralsInCycle', N'AdmiralBattleHistories', N'AdmiralID', N'CycleID', N'Admirals', N'AdmiralID', N'CycleID'),
    (N'FK_AdmiralBattleHistories_BattlesInCycle', N'AdmiralBattleHistories', N'BattleID', N'CycleID', N'BattleRecords', N'BattleID', N'CycleID'),
    (N'FK_AdmiralBattleHistories_SystemsInCycle', N'AdmiralBattleHistories', N'SystemID', N'CycleID', N'Systems', N'SystemID', N'CycleID'),
    (N'FK_AdmiralBattleHistories_FleetsInCycle', N'AdmiralBattleHistories', N'FleetID', N'CycleID', N'Fleets', N'FleetID', N'CycleID');

DECLARE
    @ConstraintName SYSNAME,
    @ChildTable SYSNAME,
    @ChildColumn1 SYSNAME,
    @ChildColumn2 SYSNAME,
    @ParentTable SYSNAME,
    @ParentColumn1 SYSNAME,
    @ParentColumn2 SYSNAME,
    @ConstraintSql NVARCHAR(MAX);

DECLARE ScopeForeignKeyCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    ConstraintName,
    ChildTable,
    ChildColumn1,
    ChildColumn2,
    ParentTable,
    ParentColumn1,
    ParentColumn2
FROM #RequiredScopeForeignKeys
ORDER BY ConstraintName;

OPEN ScopeForeignKeyCursor;
FETCH NEXT FROM ScopeForeignKeyCursor INTO
    @ConstraintName,
    @ChildTable,
    @ChildColumn1,
    @ChildColumn2,
    @ParentTable,
    @ParentColumn1,
    @ParentColumn2;

WHILE @@FETCH_STATUS = 0
BEGIN
    IF NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys
        WHERE parent_object_id = OBJECT_ID(N'dbo.' + @ChildTable)
          AND name = @ConstraintName
    )
    BEGIN
        SET @ConstraintSql = CONCAT(
            N'ALTER TABLE dbo.', QUOTENAME(@ChildTable), N' WITH CHECK ADD CONSTRAINT ',
            QUOTENAME(@ConstraintName), N' FOREIGN KEY (',
            QUOTENAME(@ChildColumn1), N', ', QUOTENAME(@ChildColumn2), N') REFERENCES dbo.',
            QUOTENAME(@ParentTable), N' (',
            QUOTENAME(@ParentColumn1), N', ', QUOTENAME(@ParentColumn2), N');');
        EXEC sys.sp_executesql @ConstraintSql;
    END;

    SET @ConstraintSql = CONCAT(
        N'ALTER TABLE dbo.', QUOTENAME(@ChildTable), N' WITH CHECK CHECK CONSTRAINT ',
        QUOTENAME(@ConstraintName), N';');
    EXEC sys.sp_executesql @ConstraintSql;

    FETCH NEXT FROM ScopeForeignKeyCursor INTO
        @ConstraintName,
        @ChildTable,
        @ChildColumn1,
        @ChildColumn2,
        @ParentTable,
        @ParentColumn1,
        @ParentColumn2;
END;

CLOSE ScopeForeignKeyCursor;
DEALLOCATE ScopeForeignKeyCursor;

-- SQL Server cannot change nullability while dependent indexes or foreign
-- keys exist. Rebuild the small affected set inside the migration transaction
-- so the contract change remains atomic and keeps the original names.
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MatchParticipants_CyclesInGame')
    ALTER TABLE dbo.MatchParticipants DROP CONSTRAINT FK_MatchParticipants_CyclesInGame;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MatchParticipants_GameEnrolments')
    ALTER TABLE dbo.MatchParticipants DROP CONSTRAINT FK_MatchParticipants_GameEnrolments;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_CycleConfigurationsInGame')
    ALTER TABLE dbo.Cycles DROP CONSTRAINT FK_Cycles_CycleConfigurationsInGame;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_PreviousCyclesInGame')
    ALTER TABLE dbo.Cycles DROP CONSTRAINT FK_Cycles_PreviousCyclesInGame;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_Games')
    ALTER TABLE dbo.Cycles DROP CONSTRAINT FK_Cycles_Games;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Cycles_CycleConfigurations')
    ALTER TABLE dbo.Cycles DROP CONSTRAINT FK_Cycles_CycleConfigurations;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants')
      AND name = N'IX_MatchParticipants_Game_Player_Cycle'
)
    DROP INDEX IX_MatchParticipants_Game_Player_Cycle ON dbo.MatchParticipants;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_CycleID_GameID'
)
    DROP INDEX UX_Cycles_CycleID_GameID ON dbo.Cycles;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_Game_OperationalSlot'
)
    DROP INDEX UX_Cycles_Game_OperationalSlot ON dbo.Cycles;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'IX_Cycles_Game_Status'
)
    DROP INDEX IX_Cycles_Game_Status ON dbo.Cycles;

IF EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'UX_Cycles_CycleConfigurationID'
)
    DROP INDEX UX_Cycles_CycleConfigurationID ON dbo.Cycles;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'GameID'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.Cycles ALTER COLUMN GameID UNIQUEIDENTIFIER NOT NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name = N'CycleConfigurationID'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.Cycles ALTER COLUMN CycleConfigurationID UNIQUEIDENTIFIER NOT NULL;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants')
      AND name = N'GameID'
      AND is_nullable = 1
)
BEGIN
    ALTER TABLE dbo.MatchParticipants ALTER COLUMN GameID UNIQUEIDENTIFIER NOT NULL;
END;

CREATE UNIQUE INDEX UX_Cycles_CycleConfigurationID
    ON dbo.Cycles(CycleConfigurationID)
    WHERE CycleConfigurationID IS NOT NULL;

CREATE UNIQUE INDEX UX_Cycles_Game_OperationalSlot
    ON dbo.Cycles(GameID, OperationalSlot)
    WHERE GameID IS NOT NULL AND Status IN (N'Active', N'RecoveryRequired');

CREATE INDEX IX_Cycles_Game_Status ON dbo.Cycles(GameID, Status, StartAt);

CREATE UNIQUE INDEX UX_Cycles_CycleID_GameID ON dbo.Cycles(CycleID, GameID);

CREATE INDEX IX_MatchParticipants_Game_Player_Cycle
    ON dbo.MatchParticipants(GameID, PlayerID, CycleID);

ALTER TABLE dbo.Cycles WITH CHECK
    ADD CONSTRAINT FK_Cycles_Games FOREIGN KEY (GameID) REFERENCES dbo.Games(GameID);

ALTER TABLE dbo.Cycles WITH CHECK
    ADD CONSTRAINT FK_Cycles_CycleConfigurations
        FOREIGN KEY (CycleConfigurationID) REFERENCES dbo.CycleConfigurations(CycleConfigurationID);

DECLARE RebuildScopeForeignKeyCursor CURSOR LOCAL FAST_FORWARD FOR
SELECT
    ConstraintName,
    ChildTable,
    ChildColumn1,
    ChildColumn2,
    ParentTable,
    ParentColumn1,
    ParentColumn2
FROM #RequiredScopeForeignKeys
WHERE ConstraintName IN
(
    N'FK_Cycles_CycleConfigurationsInGame',
    N'FK_Cycles_PreviousCyclesInGame',
    N'FK_MatchParticipants_CyclesInGame',
    N'FK_MatchParticipants_GameEnrolments'
)
ORDER BY ConstraintName;

OPEN RebuildScopeForeignKeyCursor;
FETCH NEXT FROM RebuildScopeForeignKeyCursor INTO
    @ConstraintName,
    @ChildTable,
    @ChildColumn1,
    @ChildColumn2,
    @ParentTable,
    @ParentColumn1,
    @ParentColumn2;

WHILE @@FETCH_STATUS = 0
BEGIN
    SET @ConstraintSql = CONCAT(
        N'ALTER TABLE dbo.', QUOTENAME(@ChildTable), N' WITH CHECK ADD CONSTRAINT ',
        QUOTENAME(@ConstraintName), N' FOREIGN KEY (',
        QUOTENAME(@ChildColumn1), N', ', QUOTENAME(@ChildColumn2), N') REFERENCES dbo.',
        QUOTENAME(@ParentTable), N' (',
        QUOTENAME(@ParentColumn1), N', ', QUOTENAME(@ParentColumn2), N');');
    EXEC sys.sp_executesql @ConstraintSql;

    FETCH NEXT FROM RebuildScopeForeignKeyCursor INTO
        @ConstraintName,
        @ChildTable,
        @ChildColumn1,
        @ChildColumn2,
        @ParentTable,
        @ParentColumn1,
        @ParentColumn2;
END;

CLOSE RebuildScopeForeignKeyCursor;
DEALLOCATE RebuildScopeForeignKeyCursor;

DECLARE @InvalidScopeForeignKeys NVARCHAR(1800);
SELECT @InvalidScopeForeignKeys = STRING_AGG(
    CONVERT(NVARCHAR(MAX), invalid.ConstraintName),
    N', ')
FROM
(
    SELECT TOP (24) expected.ConstraintName
    FROM #RequiredScopeForeignKeys AS expected
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM sys.foreign_keys AS foreignKey
        INNER JOIN sys.foreign_key_columns AS firstForeignKeyColumn
            ON firstForeignKeyColumn.constraint_object_id = foreignKey.object_id
           AND firstForeignKeyColumn.constraint_column_id = 1
        INNER JOIN sys.columns AS firstChildColumn
            ON firstChildColumn.object_id = firstForeignKeyColumn.parent_object_id
           AND firstChildColumn.column_id = firstForeignKeyColumn.parent_column_id
        INNER JOIN sys.columns AS firstParentColumn
            ON firstParentColumn.object_id = firstForeignKeyColumn.referenced_object_id
           AND firstParentColumn.column_id = firstForeignKeyColumn.referenced_column_id
        INNER JOIN sys.foreign_key_columns AS secondForeignKeyColumn
            ON secondForeignKeyColumn.constraint_object_id = foreignKey.object_id
           AND secondForeignKeyColumn.constraint_column_id = 2
        INNER JOIN sys.columns AS secondChildColumn
            ON secondChildColumn.object_id = secondForeignKeyColumn.parent_object_id
           AND secondChildColumn.column_id = secondForeignKeyColumn.parent_column_id
        INNER JOIN sys.columns AS secondParentColumn
            ON secondParentColumn.object_id = secondForeignKeyColumn.referenced_object_id
           AND secondParentColumn.column_id = secondForeignKeyColumn.referenced_column_id
        WHERE foreignKey.parent_object_id = OBJECT_ID(N'dbo.' + expected.ChildTable)
          AND foreignKey.referenced_object_id = OBJECT_ID(N'dbo.' + expected.ParentTable)
          AND foreignKey.name = expected.ConstraintName
          AND foreignKey.is_disabled = 0
          AND foreignKey.is_not_trusted = 0
          AND foreignKey.delete_referential_action = 0
          AND foreignKey.update_referential_action = 0
          AND firstChildColumn.name = expected.ChildColumn1
          AND secondChildColumn.name = expected.ChildColumn2
          AND firstParentColumn.name = expected.ParentColumn1
          AND secondParentColumn.name = expected.ParentColumn2
          AND 2 =
          (
              SELECT COUNT(*)
              FROM sys.foreign_key_columns AS countedColumn
              WHERE countedColumn.constraint_object_id = foreignKey.object_id
          )
    )
    ORDER BY expected.ConstraintName
) AS invalid;

IF @InvalidScopeForeignKeys IS NOT NULL
BEGIN
    DECLARE @InvalidScopeForeignKeyMessage NVARCHAR(2048) = CONCAT(
        N'Cycle scope migration completed with a missing, untrusted, disabled or unexpectedly defined foreign key: ',
        @InvalidScopeForeignKeys, N'.');
    THROW 51048, @InvalidScopeForeignKeyMessage, 1;
END;

IF EXISTS
(
    SELECT 1
    FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.Cycles')
      AND name IN (N'GameID', N'CycleConfigurationID')
      AND is_nullable = 1
)
   OR EXISTS
   (
       SELECT 1
       FROM sys.columns
       WHERE object_id = OBJECT_ID(N'dbo.MatchParticipants')
         AND name = N'GameID'
         AND is_nullable = 1
   )
BEGIN
    THROW 51049, 'Cycle scope migration did not establish the required non-null Game and configuration contract.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND name = N'CK_BattleFleetParticipants_Side'
      AND is_disabled = 0
      AND is_not_trusted = 0
)
BEGIN
    THROW 51050, 'BattleFleetParticipants.Side is not protected by a trusted side constraint.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes AS indexMetadata
    INNER JOIN sys.index_columns AS firstIndexColumn
        ON firstIndexColumn.object_id = indexMetadata.object_id
       AND firstIndexColumn.index_id = indexMetadata.index_id
       AND firstIndexColumn.key_ordinal = 1
    INNER JOIN sys.columns AS firstColumn
        ON firstColumn.object_id = firstIndexColumn.object_id
       AND firstColumn.column_id = firstIndexColumn.column_id
    INNER JOIN sys.index_columns AS secondIndexColumn
        ON secondIndexColumn.object_id = indexMetadata.object_id
       AND secondIndexColumn.index_id = indexMetadata.index_id
       AND secondIndexColumn.key_ordinal = 2
    INNER JOIN sys.columns AS secondColumn
        ON secondColumn.object_id = secondIndexColumn.object_id
       AND secondColumn.column_id = secondIndexColumn.column_id
    WHERE indexMetadata.object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND indexMetadata.name = N'UX_BattleFleetParticipants_Battle_Fleet'
      AND indexMetadata.is_unique = 1
      AND indexMetadata.is_disabled = 0
      AND firstColumn.name = N'BattleID'
      AND secondColumn.name = N'FleetID'
      AND 2 =
      (
          SELECT COUNT(*)
          FROM sys.index_columns AS keyColumn
          WHERE keyColumn.object_id = indexMetadata.object_id
            AND keyColumn.index_id = indexMetadata.index_id
            AND keyColumn.key_ordinal > 0
      )
)
BEGIN
    THROW 51051, 'BattleFleetParticipants does not have the required unique Battle/Fleet key.', 1;
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes AS indexMetadata
    INNER JOIN sys.index_columns AS firstIndexColumn
        ON firstIndexColumn.object_id = indexMetadata.object_id
       AND firstIndexColumn.index_id = indexMetadata.index_id
       AND firstIndexColumn.key_ordinal = 1
    INNER JOIN sys.columns AS firstColumn
        ON firstColumn.object_id = firstIndexColumn.object_id
       AND firstColumn.column_id = firstIndexColumn.column_id
    INNER JOIN sys.index_columns AS secondIndexColumn
        ON secondIndexColumn.object_id = indexMetadata.object_id
       AND secondIndexColumn.index_id = indexMetadata.index_id
       AND secondIndexColumn.key_ordinal = 2
    INNER JOIN sys.columns AS secondColumn
        ON secondColumn.object_id = secondIndexColumn.object_id
       AND secondColumn.column_id = secondIndexColumn.column_id
    INNER JOIN sys.index_columns AS thirdIndexColumn
        ON thirdIndexColumn.object_id = indexMetadata.object_id
       AND thirdIndexColumn.index_id = indexMetadata.index_id
       AND thirdIndexColumn.key_ordinal = 3
    INNER JOIN sys.columns AS thirdColumn
        ON thirdColumn.object_id = thirdIndexColumn.object_id
       AND thirdColumn.column_id = thirdIndexColumn.column_id
    WHERE indexMetadata.object_id = OBJECT_ID(N'dbo.BattleFleetParticipants')
      AND indexMetadata.name = N'IX_BattleFleetParticipants_Fleet_Cycle_Battle'
      AND indexMetadata.type = 2
      AND indexMetadata.is_unique = 0
      AND indexMetadata.is_disabled = 0
      AND indexMetadata.is_hypothetical = 0
      AND indexMetadata.has_filter = 0
      AND firstColumn.name = N'FleetID'
      AND secondColumn.name = N'CycleID'
      AND thirdColumn.name = N'BattleID'
      AND 3 =
      (
          SELECT COUNT(*)
          FROM sys.index_columns AS anyColumn
          WHERE anyColumn.object_id = indexMetadata.object_id
            AND anyColumn.index_id = indexMetadata.index_id
      )
      AND 3 =
      (
          SELECT COUNT(*)
          FROM sys.index_columns AS keyColumn
          WHERE keyColumn.object_id = indexMetadata.object_id
            AND keyColumn.index_id = indexMetadata.index_id
            AND keyColumn.key_ordinal > 0
      )
)
BEGIN
    THROW 51053, 'BattleFleetParticipants does not have the exact Fleet/Cycle/Battle child index.', 1;
END;

DROP TABLE #RequiredScopeForeignKeys;
DROP TABLE #CycleScopeViolations;
DROP TABLE #ExpectedBattleFleetParticipants;
DROP TABLE #ParsedBattleFleetTokens;
DROP TABLE #BattleFleetSources;
DROP TABLE #RequiredUniqueIndexes;
