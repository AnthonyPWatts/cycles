SET XACT_ABORT ON;

IF COL_LENGTH(N'dbo.Players', N'PlayerKind') IS NULL
BEGIN
    ALTER TABLE dbo.Players
        ADD PlayerKind NVARCHAR(32) NOT NULL
            CONSTRAINT DF_Players_PlayerKind DEFAULT N'Human';
END;

IF COL_LENGTH(N'dbo.Cycles', N'CreatedByPlayerID') IS NULL
BEGIN
    ALTER TABLE dbo.Cycles ADD CreatedByPlayerID UNIQUEIDENTIFIER NULL;
    ALTER TABLE dbo.Cycles WITH CHECK
        ADD CONSTRAINT FK_Cycles_CreatedByPlayers
            FOREIGN KEY (CreatedByPlayerID) REFERENCES dbo.Players(PlayerID);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Empires')
      AND name = N'UX_Empires_EmpireID_CycleID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Empires_EmpireID_CycleID ON dbo.Empires(EmpireID, CycleID);
END;

IF OBJECT_ID(N'dbo.Factions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Factions
    (
        FactionID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Factions PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_Factions_Cycles REFERENCES dbo.Cycles(CycleID),
        EmpireID UNIQUEIDENTIFIER NULL,
        FactionName NVARCHAR(120) NOT NULL,
        Kind NVARCHAR(32) NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL,
        CONSTRAINT FK_Factions_EmpiresInCycle FOREIGN KEY (EmpireID, CycleID) REFERENCES dbo.Empires(EmpireID, CycleID),
        CONSTRAINT CK_Factions_KindEmpire CHECK
        (
            (Kind = N'Empire' AND EmpireID IS NOT NULL)
            OR (Kind = N'Neutral' AND EmpireID IS NULL)
        )
    );
    CREATE UNIQUE INDEX UX_Factions_EmpireID ON dbo.Factions(EmpireID) WHERE EmpireID IS NOT NULL;
END;

INSERT INTO dbo.Factions(FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt)
SELECT empire.EmpireID, empire.CycleID, empire.EmpireID, empire.EmpireName, N'Empire', empire.Status, empire.CreatedAt
FROM dbo.Empires empire
WHERE NOT EXISTS (SELECT 1 FROM dbo.Factions faction WHERE faction.EmpireID = empire.EmpireID);

IF EXISTS
(
    SELECT 1
    FROM dbo.Empires empire
    LEFT JOIN dbo.Players player ON player.PlayerID = empire.PlayerID
    LEFT JOIN dbo.Cycles cycle ON cycle.CycleID = empire.CycleID
    WHERE player.PlayerID IS NULL OR cycle.CycleID IS NULL
)
BEGIN
    THROW 51017, 'Cannot create match participants while Empire ownership contains orphaned Player or Cycle references.', 1;
END;

IF EXISTS
(
    SELECT empire.CycleID, empire.PlayerID
    FROM dbo.Empires empire
    GROUP BY empire.CycleID, empire.PlayerID
    HAVING COUNT(*) > 1
)
BEGIN
    THROW 51018, 'Cannot create match participants because a Player controls more than one Empire in a Cycle.', 1;
END;

IF OBJECT_ID(N'dbo.MatchParticipants', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.MatchParticipants
    (
        MatchParticipantID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_MatchParticipants PRIMARY KEY,
        CycleID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_MatchParticipants_Cycles REFERENCES dbo.Cycles(CycleID),
        PlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_MatchParticipants_Players REFERENCES dbo.Players(PlayerID),
        EmpireID UNIQUEIDENTIFIER NOT NULL,
        Status NVARCHAR(32) NOT NULL,
        JoinedAt DATETIMEOFFSET NOT NULL,
        EndedAt DATETIMEOFFSET NULL,
        CONSTRAINT FK_MatchParticipants_EmpiresInCycle FOREIGN KEY (EmpireID, CycleID) REFERENCES dbo.Empires(EmpireID, CycleID)
    );
    CREATE UNIQUE INDEX UX_MatchParticipants_CycleID_PlayerID ON dbo.MatchParticipants(CycleID, PlayerID);
    CREATE UNIQUE INDEX UX_MatchParticipants_CurrentEmpire ON dbo.MatchParticipants(EmpireID) WHERE EndedAt IS NULL;
END;

INSERT INTO dbo.MatchParticipants(MatchParticipantID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
SELECT
    NEWID(),
    empire.CycleID,
    empire.PlayerID,
    empire.EmpireID,
    CASE
        WHEN empire.Status = N'Defeated' THEN N'Defeated'
        WHEN cycle.Status = N'Completed' THEN N'Completed'
        ELSE N'Active'
    END,
    empire.CreatedAt,
    CASE
        WHEN cycle.Status = N'Completed' THEN cycle.EndAt
        WHEN empire.Status = N'Defeated' THEN SYSDATETIMEOFFSET()
        ELSE NULL
    END
FROM dbo.Empires empire
INNER JOIN dbo.Cycles cycle ON cycle.CycleID = empire.CycleID
WHERE NOT EXISTS
(
    SELECT 1
    FROM dbo.MatchParticipants participant
    WHERE participant.CycleID = empire.CycleID
      AND participant.PlayerID = empire.PlayerID
);

IF COL_LENGTH(N'dbo.Fleets', N'FactionID') IS NULL
    ALTER TABLE dbo.Fleets ADD FactionID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.FleetOrders', N'TargetFactionID') IS NULL
    ALTER TABLE dbo.FleetOrders ADD TargetFactionID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.Events', N'FactionID') IS NULL
    ALTER TABLE dbo.Events ADD FactionID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.BattleRecords', N'AttackerFactionID') IS NULL
    ALTER TABLE dbo.BattleRecords ADD AttackerFactionID UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'dbo.BattleRecords', N'DefenderFactionID') IS NULL
    ALTER TABLE dbo.BattleRecords ADD DefenderFactionID UNIQUEIDENTIFIER NULL;

GO

UPDATE dbo.Fleets SET FactionID = EmpireID WHERE FactionID IS NULL;
UPDATE dbo.FleetOrders SET TargetFactionID = TargetEmpireID WHERE TargetFactionID IS NULL AND TargetEmpireID IS NOT NULL;
UPDATE dbo.Events SET FactionID = EmpireID WHERE FactionID IS NULL AND EmpireID IS NOT NULL;
UPDATE dbo.BattleRecords SET AttackerFactionID = AttackerEmpireID WHERE AttackerFactionID IS NULL;
UPDATE dbo.BattleRecords SET DefenderFactionID = DefenderEmpireID WHERE DefenderFactionID IS NULL;

IF EXISTS (SELECT 1 FROM dbo.Fleets WHERE FactionID IS NULL)
    THROW 51019, 'Cannot make Fleets.FactionID authoritative because an existing Fleet has no owner.', 1;
IF EXISTS (SELECT 1 FROM dbo.BattleRecords WHERE AttackerFactionID IS NULL OR DefenderFactionID IS NULL)
    THROW 51020, 'Cannot make Battle faction ownership authoritative because an existing Battle has no owner.', 1;

ALTER TABLE dbo.Fleets ALTER COLUMN FactionID UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.BattleRecords ALTER COLUMN AttackerFactionID UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.BattleRecords ALTER COLUMN DefenderFactionID UNIQUEIDENTIFIER NOT NULL;
ALTER TABLE dbo.Fleets ALTER COLUMN EmpireID UNIQUEIDENTIFIER NULL;
ALTER TABLE dbo.BattleRecords ALTER COLUMN AttackerEmpireID UNIQUEIDENTIFIER NULL;
ALTER TABLE dbo.BattleRecords ALTER COLUMN DefenderEmpireID UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Fleets_Factions')
    ALTER TABLE dbo.Fleets WITH CHECK ADD CONSTRAINT FK_Fleets_Factions FOREIGN KEY (FactionID) REFERENCES dbo.Factions(FactionID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FleetOrders_TargetFactions')
    ALTER TABLE dbo.FleetOrders WITH CHECK ADD CONSTRAINT FK_FleetOrders_TargetFactions FOREIGN KEY (TargetFactionID) REFERENCES dbo.Factions(FactionID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Events_Factions')
    ALTER TABLE dbo.Events WITH CHECK ADD CONSTRAINT FK_Events_Factions FOREIGN KEY (FactionID) REFERENCES dbo.Factions(FactionID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BattleRecords_AttackerFactions')
    ALTER TABLE dbo.BattleRecords WITH CHECK ADD CONSTRAINT FK_BattleRecords_AttackerFactions FOREIGN KEY (AttackerFactionID) REFERENCES dbo.Factions(FactionID);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BattleRecords_DefenderFactions')
    ALTER TABLE dbo.BattleRecords WITH CHECK ADD CONSTRAINT FK_BattleRecords_DefenderFactions FOREIGN KEY (DefenderFactionID) REFERENCES dbo.Factions(FactionID);
