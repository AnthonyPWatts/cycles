IF OBJECT_ID(N'dbo.TutorialSkips', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TutorialSkips
    (
        PlayerID UNIQUEIDENTIFIER NOT NULL,
        TutorialKey NVARCHAR(128) NOT NULL,
        DefinitionVersion INT NOT NULL,
        FirstSkippedRunID UNIQUEIDENTIFIER NOT NULL,
        FirstSkippedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT PK_TutorialSkips PRIMARY KEY (PlayerID, TutorialKey, DefinitionVersion),
        CONSTRAINT FK_TutorialSkips_Players FOREIGN KEY (PlayerID)
            REFERENCES dbo.Players(PlayerID),
        CONSTRAINT FK_TutorialSkips_TutorialRuns FOREIGN KEY (FirstSkippedRunID)
            REFERENCES dbo.TutorialRuns(TutorialRunID),
        CONSTRAINT CK_TutorialSkips_Key CHECK (LEN(LTRIM(RTRIM(TutorialKey))) > 0),
        CONSTRAINT CK_TutorialSkips_DefinitionVersion CHECK (DefinitionVersion > 0)
    );
END;
