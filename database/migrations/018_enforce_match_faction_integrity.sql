SET XACT_ABORT ON;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Empires')
      AND name = N'UX_Empires_CycleID_EmpireID_PlayerID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Empires_CycleID_EmpireID_PlayerID
        ON dbo.Empires(CycleID, EmpireID, PlayerID);
END;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.indexes
    WHERE object_id = OBJECT_ID(N'dbo.Factions')
      AND name = N'UX_Factions_CycleID_FactionID'
)
BEGIN
    CREATE UNIQUE INDEX UX_Factions_CycleID_FactionID
        ON dbo.Factions(CycleID, FactionID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MatchParticipants_EmpireOwnership')
BEGIN
    ALTER TABLE dbo.MatchParticipants WITH CHECK
        ADD CONSTRAINT FK_MatchParticipants_EmpireOwnership
            FOREIGN KEY (CycleID, EmpireID, PlayerID)
            REFERENCES dbo.Empires(CycleID, EmpireID, PlayerID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Fleets_FactionsInCycle')
BEGIN
    ALTER TABLE dbo.Fleets WITH CHECK
        ADD CONSTRAINT FK_Fleets_FactionsInCycle
            FOREIGN KEY (CycleID, FactionID)
            REFERENCES dbo.Factions(CycleID, FactionID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_FleetOrders_TargetFactionsInCycle')
BEGIN
    ALTER TABLE dbo.FleetOrders WITH CHECK
        ADD CONSTRAINT FK_FleetOrders_TargetFactionsInCycle
            FOREIGN KEY (CycleID, TargetFactionID)
            REFERENCES dbo.Factions(CycleID, FactionID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Events_FactionsInCycle')
BEGIN
    ALTER TABLE dbo.Events WITH CHECK
        ADD CONSTRAINT FK_Events_FactionsInCycle
            FOREIGN KEY (CycleID, FactionID)
            REFERENCES dbo.Factions(CycleID, FactionID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BattleRecords_AttackerFactionsInCycle')
BEGIN
    ALTER TABLE dbo.BattleRecords WITH CHECK
        ADD CONSTRAINT FK_BattleRecords_AttackerFactionsInCycle
            FOREIGN KEY (CycleID, AttackerFactionID)
            REFERENCES dbo.Factions(CycleID, FactionID);
END;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_BattleRecords_DefenderFactionsInCycle')
BEGIN
    ALTER TABLE dbo.BattleRecords WITH CHECK
        ADD CONSTRAINT FK_BattleRecords_DefenderFactionsInCycle
            FOREIGN KEY (CycleID, DefenderFactionID)
            REFERENCES dbo.Factions(CycleID, FactionID);
END;
