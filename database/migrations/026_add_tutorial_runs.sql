SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.Games', N'U') IS NULL
   OR OBJECT_ID(N'dbo.GameEnrolments', N'U') IS NULL
   OR OBJECT_ID(N'dbo.Cycles', N'U') IS NULL
BEGIN
    THROW 51056, 'Cannot add tutorial runs before the Game foundation exists.', 1;
END;

IF OBJECT_ID(N'dbo.TutorialRuns', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TutorialRuns
    (
        TutorialRunID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_TutorialRuns PRIMARY KEY,
        GameID UNIQUEIDENTIFIER NOT NULL,
        CycleID UNIQUEIDENTIFIER NOT NULL,
        PlayerID UNIQUEIDENTIFIER NOT NULL,
        TutorialKey NVARCHAR(128) NOT NULL,
        DefinitionVersion INT NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CurrentSlot AS
        (
            CASE
                WHEN Status IN (N'Active', N'Paused') THEN CONVERT(NVARCHAR(36), N'current')
                ELSE CONVERT(NVARCHAR(36), TutorialRunID)
            END
        ) PERSISTED,
        OriginatingRequestID UNIQUEIDENTIFIER NULL,
        SupersededByTutorialRunID UNIQUEIDENTIFIER NULL,
        StartedAt DATETIMEOFFSET NOT NULL,
        StatusChangedAt DATETIMEOFFSET NOT NULL,
        EndedAt DATETIMEOFFSET NULL,
        RowVersion ROWVERSION NOT NULL,
        CONSTRAINT FK_TutorialRuns_GameEnrolments FOREIGN KEY (GameID, PlayerID)
            REFERENCES dbo.GameEnrolments(GameID, PlayerID),
        CONSTRAINT FK_TutorialRuns_Cycles FOREIGN KEY (CycleID, GameID)
            REFERENCES dbo.Cycles(CycleID, GameID),
        CONSTRAINT FK_TutorialRuns_SupersededBy FOREIGN KEY (SupersededByTutorialRunID)
            REFERENCES dbo.TutorialRuns(TutorialRunID),
        CONSTRAINT CK_TutorialRuns_Key CHECK (LEN(LTRIM(RTRIM(TutorialKey))) > 0),
        CONSTRAINT CK_TutorialRuns_DefinitionVersion CHECK (DefinitionVersion > 0),
        CONSTRAINT CK_TutorialRuns_Status CHECK
        (
            Status IN (N'Active', N'Paused', N'Completed', N'Skipped', N'Superseded')
        ),
        CONSTRAINT CK_TutorialRuns_EndState CHECK
        (
            (Status IN (N'Completed', N'Skipped', N'Superseded') AND EndedAt IS NOT NULL)
            OR (Status IN (N'Active', N'Paused') AND EndedAt IS NULL)
        ),
        CONSTRAINT CK_TutorialRuns_Supersession CHECK
        (
            (Status = N'Superseded' AND SupersededByTutorialRunID IS NOT NULL)
            OR (Status <> N'Superseded' AND SupersededByTutorialRunID IS NULL)
        ),
        CONSTRAINT CK_TutorialRuns_NoSelfSupersession CHECK
        (
            SupersededByTutorialRunID IS NULL OR SupersededByTutorialRunID <> TutorialRunID
        )
    );
END;

IF OBJECT_ID(N'dbo.TutorialAcknowledgements', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TutorialAcknowledgements
    (
        TutorialRunID UNIQUEIDENTIFIER NOT NULL,
        AcknowledgementKey NVARCHAR(128) NOT NULL,
        AcknowledgedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT PK_TutorialAcknowledgements PRIMARY KEY (TutorialRunID, AcknowledgementKey),
        CONSTRAINT FK_TutorialAcknowledgements_TutorialRuns FOREIGN KEY (TutorialRunID)
            REFERENCES dbo.TutorialRuns(TutorialRunID),
        CONSTRAINT CK_TutorialAcknowledgements_Key CHECK
        (
            LEN(LTRIM(RTRIM(AcknowledgementKey))) > 0
        )
    );
END;

IF OBJECT_ID(N'dbo.TutorialCompletions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TutorialCompletions
    (
        PlayerID UNIQUEIDENTIFIER NOT NULL,
        TutorialKey NVARCHAR(128) NOT NULL,
        DefinitionVersion INT NOT NULL,
        FirstCompletedRunID UNIQUEIDENTIFIER NOT NULL,
        FirstCompletedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT PK_TutorialCompletions PRIMARY KEY (PlayerID, TutorialKey, DefinitionVersion),
        CONSTRAINT FK_TutorialCompletions_Players FOREIGN KEY (PlayerID)
            REFERENCES dbo.Players(PlayerID),
        CONSTRAINT FK_TutorialCompletions_TutorialRuns FOREIGN KEY (FirstCompletedRunID)
            REFERENCES dbo.TutorialRuns(TutorialRunID),
        CONSTRAINT CK_TutorialCompletions_Key CHECK (LEN(LTRIM(RTRIM(TutorialKey))) > 0),
        CONSTRAINT CK_TutorialCompletions_DefinitionVersion CHECK (DefinitionVersion > 0)
    );
END;

;WITH TrainingCandidates AS
(
    SELECT
        game.GameID,
        cycle.CycleID,
        enrolment.PlayerID,
        game.CreatedAt,
        TRY_CONVERT(UNIQUEIDENTIFIER, enrolment.OriginatingRequestID) AS OriginatingRequestID,
        CONVERT(BIT, CASE WHEN game.Status IN (N'Active', N'Intermission')
                              AND enrolment.Status = N'Enrolled'
                              AND cycle.Status IN (N'Active', N'RecoveryRequired')
                         THEN 1 ELSE 0 END) AS IsOperational,
        ROW_NUMBER() OVER
        (
            PARTITION BY enrolment.PlayerID
            ORDER BY
                CASE WHEN game.Status IN (N'Active', N'Intermission')
                           AND enrolment.Status = N'Enrolled'
                           AND cycle.Status IN (N'Active', N'RecoveryRequired')
                     THEN 0 ELSE 1 END,
                game.CreatedAt DESC,
                game.GameID
        ) AS AttemptRank
    FROM dbo.Games AS game
    INNER JOIN dbo.GameEnrolments AS enrolment ON enrolment.GameID = game.GameID
    INNER JOIN dbo.CycleConfigurations AS configuration
        ON configuration.GameID = game.GameID
       AND configuration.SequenceNumber = 1
    INNER JOIN dbo.Cycles AS cycle
        ON cycle.GameID = game.GameID
       AND cycle.CycleConfigurationID = configuration.CycleConfigurationID
    WHERE game.Purpose = N'Training'
      AND game.CreationSource = N'TrainingProvisioning'
      AND configuration.ScenarioProfileKey = N'tutorial-foundations-v1'
      AND configuration.ScenarioProfileVersion = 1
), MissingRuns AS
(
    SELECT candidate.*
    FROM TrainingCandidates AS candidate
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM dbo.TutorialRuns AS run
        WHERE run.GameID = candidate.GameID
          AND run.PlayerID = candidate.PlayerID
          AND run.TutorialKey = N'tutorial-foundations-v1'
          AND run.DefinitionVersion = 1
    )
)
INSERT dbo.TutorialRuns
(
    TutorialRunID,
    GameID,
    CycleID,
    PlayerID,
    TutorialKey,
    DefinitionVersion,
    Status,
    OriginatingRequestID,
    SupersededByTutorialRunID,
    StartedAt,
    StatusChangedAt,
    EndedAt
)
SELECT
    NEWID(),
    missing.GameID,
    missing.CycleID,
    missing.PlayerID,
    N'tutorial-foundations-v1',
    1,
    CASE WHEN missing.AttemptRank = 1 AND missing.IsOperational = 1 THEN N'Active' ELSE N'Skipped' END,
    missing.OriginatingRequestID,
    NULL,
    missing.CreatedAt,
    missing.CreatedAt,
    CASE WHEN missing.AttemptRank = 1 AND missing.IsOperational = 1 THEN NULL ELSE missing.CreatedAt END
FROM MissingRuns AS missing;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TutorialRuns')
      AND name = N'UX_TutorialRuns_Game_Player_Definition'
)
BEGIN
    CREATE UNIQUE INDEX UX_TutorialRuns_Game_Player_Definition
        ON dbo.TutorialRuns(GameID, PlayerID, TutorialKey, DefinitionVersion);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TutorialRuns')
      AND name = N'UX_TutorialRuns_Current'
)
BEGIN
    CREATE UNIQUE INDEX UX_TutorialRuns_Current
        ON dbo.TutorialRuns(PlayerID, TutorialKey, DefinitionVersion, CurrentSlot);
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TutorialRuns')
      AND name = N'UX_TutorialRuns_Request'
)
BEGIN
    CREATE UNIQUE INDEX UX_TutorialRuns_Request
        ON dbo.TutorialRuns(PlayerID, TutorialKey, DefinitionVersion, OriginatingRequestID)
        WHERE OriginatingRequestID IS NOT NULL;
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.TutorialRuns')
      AND name = N'IX_TutorialRuns_Player_End'
)
BEGIN
    CREATE INDEX IX_TutorialRuns_Player_End
        ON dbo.TutorialRuns(PlayerID, TutorialKey, DefinitionVersion, EndedAt DESC)
        INCLUDE(GameID, CycleID, Status);
END;
