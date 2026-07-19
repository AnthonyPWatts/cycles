SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'dbo.EmpireDoctrineUnlocks', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.EmpireDoctrineUnlocks
    (
        EmpireDoctrineUnlockID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_EmpireDoctrineUnlocks PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireDoctrineUnlocks_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_EmpireDoctrineUnlocks_Empires REFERENCES dbo.Empires(EmpireID),
        DoctrineKey NVARCHAR(128) NOT NULL,
        UnlockedTickNumber INT NOT NULL,
        UnlockedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT CK_EmpireDoctrineUnlocks_DoctrineKey CHECK (LEN(LTRIM(RTRIM(DoctrineKey))) > 0),
        CONSTRAINT CK_EmpireDoctrineUnlocks_UnlockedTickNumber CHECK (UnlockedTickNumber >= 0),
        CONSTRAINT UX_EmpireDoctrineUnlocks_Cycle_Empire_Doctrine UNIQUE (CycleID, EmpireID, DoctrineKey)
    );
END;
GO

;WITH RankedUnlockEvents AS
(
    SELECT
        events.CycleID,
        events.EmpireID,
        parsed.DoctrineKey,
        events.TickNumber,
        events.CreatedAt,
        ROW_NUMBER() OVER
        (
            PARTITION BY events.CycleID, events.EmpireID, parsed.DoctrineKey
            ORDER BY events.TickNumber, events.CreatedAt, events.EventID
        ) AS UnlockRank
    FROM dbo.Events AS events
    CROSS APPLY
    (
        VALUES
        (
            JSON_VALUE(
                CASE WHEN ISJSON(events.FactJson) = 1 THEN events.FactJson ELSE N'{}' END,
                N'$.doctrine')
        )
    ) AS parsed(DoctrineKey)
    WHERE events.EventType = N'DoctrineUnlocked'
      AND events.EmpireID IS NOT NULL
      AND parsed.DoctrineKey IS NOT NULL
      AND LEN(LTRIM(RTRIM(parsed.DoctrineKey))) > 0
)
INSERT INTO dbo.EmpireDoctrineUnlocks
(
    EmpireDoctrineUnlockID,
    CycleID,
    EmpireID,
    DoctrineKey,
    UnlockedTickNumber,
    UnlockedAt
)
SELECT
    NEWID(),
    source.CycleID,
    source.EmpireID,
    source.DoctrineKey,
    source.TickNumber,
    source.CreatedAt
FROM RankedUnlockEvents AS source
WHERE source.UnlockRank = 1
  AND NOT EXISTS
  (
      SELECT 1
      FROM dbo.EmpireDoctrineUnlocks AS existing
      WHERE existing.CycleID = source.CycleID
        AND existing.EmpireID = source.EmpireID
        AND existing.DoctrineKey = source.DoctrineKey
  );
GO
