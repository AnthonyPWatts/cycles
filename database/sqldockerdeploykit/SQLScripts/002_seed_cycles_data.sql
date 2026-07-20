-- Generated from GameSeeder.CreateCuratedColdStart. Do not hand-edit.
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
SET XACT_ABORT ON;

BEGIN TRANSACTION;
IF NOT EXISTS (SELECT 1 FROM dbo.Cycles)
BEGIN
    DECLARE @SeededAt DATETIMEOFFSET = SYSDATETIMEOFFSET();
    DECLARE @CycleName NVARCHAR(120) = CONCAT(N'Cycle ', DATEPART(YEAR, @SeededAt), N'.', RIGHT(N'0' + CONVERT(NVARCHAR(2), DATEPART(MONTH, @SeededAt)), 2));

    INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, PlayerKind, Role, CreatedAt, LastLoginAt, Status)
    VALUES
        ('2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', N'Tony', N'', N'', N'', N'', N'Human', N'Player', @SeededAt, NULL, N'Active'),
        ('8bf77462-f7ce-4c67-8c20-29e4e1e5bb02', N'Will', N'', N'', N'', N'', N'Human', N'Player', @SeededAt, NULL, N'Active'),
        ('3ecfbf78-b6b3-42cd-a811-85efb916cc03', N'Ariadne', N'', N'', N'', N'', N'AI', N'Player', @SeededAt, NULL, N'Active');

    INSERT INTO dbo.Games(GameID, Name, Purpose, Status, Visibility, CreationSource, GamePolicyKey, GamePolicyVersion, GamePolicyContentHash, PolicyProvenanceStatus, CreatedByPlayerID, CreatedAt, FirstStartedAt, CompletedAt, CancelledAt, TerminatedAt)
    VALUES
        ('01fcdded-9718-4436-b585-d97d504b1d57', N'Legacy Standard Game', N'Standard', N'Active', N'Private', N'LegacyImport', N'legacy-single-lineage-v1', 1, NULL, N'LegacyUnverified', NULL, @SeededAt, @SeededAt, NULL, NULL, NULL);

    INSERT INTO dbo.CycleConfigurations(CycleConfigurationID, GameID, SequenceNumber, Status, ProvenanceStatus, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, SchedulingMode, MinimumHumanSeats, MaximumHumanSeats, ScheduledStartAt, ScheduledEndAt, TickLengthMinutes, CreatedAt, LockedAt, MaterializedAt, CancelledAt)
    VALUES
        ('fce1d96a-6a07-4559-cff6-dd6efde758ae', '01fcdded-9718-4436-b585-d97d504b1d57', 1, N'Materialized', N'LegacyUnverified', N'territorial-graph-v2', NULL, NULL, 71421, N'development-match-v2', NULL, NULL, 20260717, N'legacy-cycle-policy-v1', 1, NULL, N'Scheduled', NULL, NULL, @SeededAt, DATEADD(DAY, 90, @SeededAt), 60, @SeededAt, @SeededAt, @SeededAt, NULL);

    INSERT INTO dbo.Cycles(CycleID, GameID, CycleConfigurationID, PreviousCycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, TurnStage, MapProfileKey, MapProfileVersion, MapProfileContentHash, MapSeed, ScenarioProfileKey, ScenarioProfileVersion, ScenarioProfileContentHash, ScenarioSeed, CyclePolicyKey, CyclePolicyVersion, CyclePolicyContentHash, SchedulingMode, NextTickAt, ProfileProvenanceStatus, CreatedByPlayerID, CreatedAt)
    VALUES
        ('fce1d96a-6a07-4559-cff6-dd6efde758ae', '01fcdded-9718-4436-b585-d97d504b1d57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, @CycleName, @SeededAt, DATEADD(DAY, 90, @SeededAt), 60, 0, N'Active', N'CommandOpen', N'territorial-graph-v2', NULL, NULL, 71421, N'development-match-v2', NULL, NULL, 20260717, N'legacy-cycle-policy-v1', 1, NULL, N'Scheduled', @SeededAt, N'LegacyUnverified', '2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', @SeededAt);

    INSERT INTO dbo.GameEnrolments(GameEnrolmentID, GameID, PlayerID, Status, Origin, OriginatingRequestID, EnrolledAt, StatusChangedAt, EndedAt)
    VALUES
        ('2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', '01fcdded-9718-4436-b585-d97d504b1d57', '2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', N'Enrolled', N'LegacyImport', NULL, @SeededAt, @SeededAt, NULL),
        ('3ecfbf78-b6b3-42cd-a811-85efb916cc03', '01fcdded-9718-4436-b585-d97d504b1d57', '3ecfbf78-b6b3-42cd-a811-85efb916cc03', N'Enrolled', N'LegacyImport', NULL, @SeededAt, @SeededAt, NULL),
        ('8bf77462-f7ce-4c67-8c20-29e4e1e5bb02', '01fcdded-9718-4436-b585-d97d504b1d57', '8bf77462-f7ce-4c67-8c20-29e4e1e5bb02', N'Enrolled', N'LegacyImport', NULL, @SeededAt, @SeededAt, NULL);

    INSERT INTO dbo.GameLifecycleEvents(GameLifecycleEventID, GameID, EventType, SubjectPlayerID, ActorPlayerID, FromStatus, ToStatus, Reason, CorrelationID, FactJson, CreatedAt)
    VALUES
        ('b283628d-2899-475c-9c6e-5dd8e20c2e91', '01fcdded-9718-4436-b585-d97d504b1d57', N'LegacyImported', NULL, NULL, NULL, N'Active', NULL, NULL, N'{"source":"legacy-single-lineage","schemaVersion":1}', @SeededAt);

    INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
    VALUES
        ('8e20bd6d-9b8e-9800-ee6c-4338de928cb1', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Aster Reach', 700, 340, 0),
        ('669f0235-9c47-5c3f-764e-39e80a696594', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Cinder March', 160, 340, 1),
        ('d07e8579-5205-1fa9-ce91-6522e730db13', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Hollow Crown', 500, 120, 2),
        ('fffad29e-8bac-8925-745f-fbe5df74bac4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Lacuna Verge', 830, 155, 3),
        ('307e4cd3-0245-9d11-4ed2-7c1c3862e135', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Orison Fold', 800, 555, 4),
        ('c538bd49-820e-075d-3088-400abf0b89d6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Red Lattice', 490, 555, 5),
        ('552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Umbral Marches', 180, 555, 6),
        ('45426bfc-440f-c4f0-8a7e-7e540275f795', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Warden Line', 170, 125, 7);

    INSERT INTO dbo.Systems(SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
    VALUES
        ('774da183-9820-66a1-7f3f-80f5eb7fd48d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ashen Gate', 210, 290, 60, 61, 45, 45, 1, @SeededAt),
        ('afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Aster Vale', 698, 293, 78, 32, 28, 27, 0, @SeededAt),
        ('d60c9c3b-48b5-e4d3-9007-49e30edcf5ee', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Brightfall', 635, 376, 20, 74, 40, 26, 0, @SeededAt),
        ('d2b8df88-73a9-5258-296d-f8652ad10430', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Cinder Relay', 180, 345, 35, 62, 12, 21, 0, @SeededAt),
        ('ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Cinderhome', 95, 330, 37, 58, 42, 44, 2, @SeededAt),
        ('5400b54d-c114-8f13-a103-dd92064c7c26', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Crimson Needle', 545, 555, 53, 63, 19, 27, 0, @SeededAt),
        ('fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Crimson Relay', 545, 603, 84, 61, 62, 58, 2, @SeededAt),
        ('1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Crown Meridian', 555, 110, 29, 15, 34, 15, 0, @SeededAt),
        ('33943a72-9906-549a-3c27-d80eb89fdd54', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Dawnward', 653, 344, 35, 63, 41, 27, 0, @SeededAt),
        ('a00244e8-42f4-56fc-ebff-1a1e60153f97', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Deep Vault', 865, 200, 71, 71, 36, 35, 0, @SeededAt),
        ('8ab6466f-0cfa-caef-092e-b7eedfbca20d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ebon Strait', 118, 292, 37, 75, 54, 33, 0, @SeededAt),
        ('f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ember Watch', 200, 392, 73, 75, 55, 40, 0, @SeededAt),
        ('4182bfd6-48b1-517a-de17-02326198da1b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Far Meridian', 785, 193, 22, 39, 64, 25, 0, @SeededAt),
        ('d3b47ed9-315f-ca64-1ffe-c4835527e96b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Fold Meridian', 760, 612, 38, 21, 58, 35, 1, @SeededAt),
        ('043ae2e1-ceec-5e72-27ed-25b2b2893833', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Glass Meridian', 145, 318, 75, 26, 46, 29, 0, @SeededAt),
        ('4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Glass Refuge', 505, 178, 44, 54, 38, 27, 0, @SeededAt),
        ('289f14d3-141d-ff24-1013-904503e5a2a9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'High Anchorage', 180, 175, 28, 22, 55, 21, 0, @SeededAt),
        ('090ff8af-f3eb-2c34-411e-daabcac34283', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Bastion', 550, 152, 32, 41, 61, 43, 2, @SeededAt),
        ('feb7ab95-935f-b582-fd85-d329d2487b15', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Crown', 442, 85, 59, 67, 47, 46, 1, @SeededAt),
        ('c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Lantern', 520, 85, 29, 68, 57, 30, 0, @SeededAt),
        ('7dae71c0-5593-bbdc-e86e-c3d198fb7341', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Juniper Rift', 480, 95, 81, 45, 26, 30, 0, @SeededAt),
        ('7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Keystone', 112, 355, 70, 69, 16, 31, 0, @SeededAt),
        ('57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna', 770, 155, 49, 23, 61, 38, 1, @SeededAt),
        ('f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna Beacon', 825, 215, 58, 31, 38, 25, 0, @SeededAt),
        ('a93029b7-0f17-d3c3-6f78-637f01b2c82d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna Shoal', 835, 100, 69, 42, 64, 35, 0, @SeededAt),
        ('c5120588-0b2e-22fe-c5d7-4697e24aff54', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Marcher Beacon', 242, 540, 33, 74, 17, 24, 0, @SeededAt),
        ('a511a05a-564c-f253-b3d9-d349f3d2215e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Mourn Relay', 890, 160, 65, 48, 38, 42, 1, @SeededAt),
        ('60a870d0-a90b-740c-bc02-8f11358b5a57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Mournstar', 795, 115, 78, 75, 58, 42, 0, @SeededAt),
        ('ec62360a-6a52-3c7e-62f0-37b657551e11', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Nadir Crossing', 753, 318, 30, 35, 47, 22, 0, @SeededAt),
        ('b611c0c2-5995-70e1-3ffe-417c1d305c14', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Night Span', 210, 507, 45, 49, 63, 31, 0, @SeededAt),
        ('1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Northstar Gate', 225, 175, 62, 61, 23, 41, 1, @SeededAt),
        ('32c77794-0879-14fc-faf4-6d8ac3779318', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison', 750, 510, 32, 23, 56, 22, 0, @SeededAt),
        ('9b18a470-5646-f257-a801-b7994aa2883e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison Anchorage', 840, 590, 25, 61, 51, 27, 0, @SeededAt),
        ('e6118c6d-9fb2-1e7c-e77d-269753b36941', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison Lantern', 840, 513, 50, 50, 34, 38, 1, @SeededAt),
        ('30bf6d4c-8c93-57ef-b270-20d6831d4581', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Pale Coil', 847, 555, 23, 20, 49, 18, 0, @SeededAt),
        ('9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Pale Harbour', 715, 337, 77, 61, 10, 29, 0, @SeededAt),
        ('3fd19805-7287-29d4-63b5-48d6ab124adc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Penumbral Span', 875, 120, 22, 32, 27, 16, 0, @SeededAt),
        ('a0c41129-e9c0-d1e0-8409-da4c22608ad7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Pilgrim''s Wake', 800, 555, 48, 54, 59, 32, 0, @SeededAt),
        ('e90f31ce-2cad-df2f-2e4b-a3eb78828260', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Pseudopolis', 678, 373, 25, 16, 55, 19, 0, @SeededAt),
        ('03bcb0de-acf6-a30c-777c-7757c8661c59', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Pyre Anchorage', 215, 360, 77, 67, 33, 35, 0, @SeededAt),
        ('ecab282f-7cd5-bb87-153c-d4bc1be51de3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Quiet Harbour', 825, 610, 63, 31, 49, 28, 0, @SeededAt),
        ('0f5a5921-4a8d-2752-b90b-2b2e7606876d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Quietus', 792, 515, 77, 59, 64, 40, 0, @SeededAt),
        ('bf024a45-f9e4-ba98-fb72-43d404c37d4a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Red Haven', 503, 557, 65, 73, 22, 32, 0, @SeededAt),
        ('6de39c30-c8ce-571d-6e07-836f61b66044', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Red Lattice', 445, 510, 58, 15, 49, 41, 2, @SeededAt),
        ('563cfa23-b533-2dfe-7734-9a2151a36731', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Sable Point', 467, 539, 51, 62, 42, 31, 0, @SeededAt),
        ('5da5c091-db1f-6755-2fac-952f7468ef2f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Sable Vault', 476, 590, 83, 45, 59, 37, 0, @SeededAt),
        ('45cdce04-7d01-eead-4a2f-ce299ec25aa3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Sentinel Spur', 145, 157, 71, 20, 49, 28, 0, @SeededAt),
        ('c2a81993-5703-8030-b082-dfe89ce4c7e3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Shadow Cairn', 200, 590, 80, 61, 17, 31, 0, @SeededAt),
        ('9ea672e2-02e9-96a5-6e17-b9c2479b83dd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Silent Array', 452, 165, 54, 57, 11, 24, 0, @SeededAt),
        ('d81b94a7-92b1-0459-383c-c82e86fe57ee', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Ternary', 522, 510, 62, 68, 16, 29, 0, @SeededAt),
        ('f8b85da0-c7e5-24ad-b678-0d3533c78429', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Ternary Watch', 445, 572, 69, 60, 36, 33, 0, @SeededAt),
        ('fe739d22-e193-f482-cbcf-3f5977f3ab8b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Treaty Gate', 636, 310, 25, 21, 30, 35, 2, @SeededAt),
        ('181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Bastion', 235, 510, 29, 59, 54, 40, 1, @SeededAt),
        ('780fe021-3614-fceb-71be-fbf1a3cfcf28', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Lantern', 175, 545, 46, 24, 26, 19, 0, @SeededAt),
        ('0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Way', 120, 603, 75, 33, 38, 41, 1, @SeededAt),
        ('f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Verdant Coil', 150, 590, 28, 29, 17, 14, 0, @SeededAt),
        ('c23b5450-e8d6-e676-023d-6dd23e6ebf7b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Vigil Cairn', 510, 155, 29, 55, 34, 23, 0, @SeededAt),
        ('1b4109ca-8f45-263a-e773-60297272f0aa', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Viridian Refuge', 220, 565, 27, 64, 37, 25, 0, @SeededAt),
        ('83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Warden Watch', 125, 123, 34, 56, 42, 26, 0, @SeededAt),
        ('52423a8c-14dd-1699-d94d-f1ce59ec843d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Warden''s Line', 115, 80, 84, 68, 30, 48, 1, @SeededAt),
        ('cfbe7586-f639-add0-c4e6-d41370bc36ae', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Xanthe', 156, 90, 68, 48, 27, 28, 0, @SeededAt),
        ('8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Yanaka''s Reach', 754, 377, 33, 44, 37, 39, 2, @SeededAt),
        ('52134872-ef75-71bd-3102-7375ce47b077', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Yarrow', 204, 105, 68, 24, 21, 22, 0, @SeededAt),
        ('1ed45dec-c261-6f52-6fda-1c2499040654', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Zenith Yard', 214, 141, 61, 18, 65, 28, 0, @SeededAt);

    INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
    VALUES
        ('1b12558d-ff5e-d372-e4c1-94f63137b642', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', N'Aurelian Compact', '1b4109ca-8f45-263a-e773-60297272f0aa', @SeededAt, N'Active'),
        ('98428db6-ba14-1fb9-14c1-86a6d38348b7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8bf77462-f7ce-4c67-8c20-29e4e1e5bb02', N'Khepri Mandate', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', @SeededAt, N'Active'),
        ('f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '3ecfbf78-b6b3-42cd-a811-85efb916cc03', N'Novan League', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', @SeededAt, N'Active');

    INSERT INTO dbo.Factions(FactionID, CycleID, EmpireID, FactionName, Kind, Status, CreatedAt)
    VALUES
        ('1b12558d-ff5e-d372-e4c1-94f63137b642', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'Aurelian Compact', N'Empire', N'Active', @SeededAt),
        ('fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, N'Free Captains', N'Neutral', N'Active', @SeededAt),
        ('98428db6-ba14-1fb9-14c1-86a6d38348b7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '98428db6-ba14-1fb9-14c1-86a6d38348b7', N'Khepri Mandate', N'Empire', N'Active', @SeededAt),
        ('f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', N'Novan League', N'Empire', N'Active', @SeededAt);

    INSERT INTO dbo.MatchParticipants(MatchParticipantID, GameID, CycleID, PlayerID, EmpireID, Status, JoinedAt, EndedAt)
    VALUES
        ('40a59e85-1d96-90c1-201b-70f33711ba04', '01fcdded-9718-4436-b585-d97d504b1d57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '2bbf6b63-b50f-4fe3-bc11-913c2b74aa01', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'Active', @SeededAt, NULL),
        ('219c6de2-f38e-d076-47c3-d09adf3dee9a', '01fcdded-9718-4436-b585-d97d504b1d57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '3ecfbf78-b6b3-42cd-a811-85efb916cc03', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', N'Active', @SeededAt, NULL),
        ('828573b7-ce4b-b890-5065-b80574c14fc0', '01fcdded-9718-4436-b585-d97d504b1d57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8bf77462-f7ce-4c67-8c20-29e4e1e5bb02', '98428db6-ba14-1fb9-14c1-86a6d38348b7', N'Active', @SeededAt, NULL);

    INSERT INTO dbo.EmpireResources(EmpireResourceID, EmpireID, Industry, Research, Population, LastGeneratedIndustry, LastGeneratedResearch, LastGeneratedPopulation, LastSpentIndustry, LastSpentResearch, LastSpentPopulation, UpdatedAt)
    VALUES
        ('3510456e-fb83-1403-f88f-2bd6cbc442e1', '1b12558d-ff5e-d372-e4c1-94f63137b642', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt),
        ('70f25832-dd43-8d14-72e4-9d1a0fe83d83', '98428db6-ba14-1fb9-14c1-86a6d38348b7', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt),
        ('22b80436-db08-9382-3c0c-2e91449be933', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt);

    INSERT INTO dbo.EmpirePriorities(EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt)
    VALUES
        ('aff0c7fa-0165-78db-bd0a-a4f770ed422f', '1b12558d-ff5e-d372-e4c1-94f63137b642', 0, 0, 67, 33, @SeededAt),
        ('49b9d4b1-892c-009a-184a-5122d864e12b', '98428db6-ba14-1fb9-14c1-86a6d38348b7', 0, 0, 67, 33, @SeededAt),
        ('5157de0c-e13b-b784-3c78-b5bc77fa9bb0', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 0, 0, 67, 33, @SeededAt);

    INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
    VALUES
        ('0888c05a-9ff2-d6f5-f5f4-f3a4a9c0f7e6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', 39.45, 1),
        ('0d4abdd2-abb0-e63e-5ca1-d510f3c02fc4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', 45.28, 1),
        ('15e98a94-7c5e-d98b-7c63-1131ec98f6dc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 'bf024a45-f9e4-ba98-fb72-43d404c37d4a', 59.91, 1),
        ('19941a92-d3c0-0300-7317-da4c0bc7aeb8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd2b8df88-73a9-5258-296d-f8652ad10430', '03bcb0de-acf6-a30c-777c-7757c8661c59', 38.08, 1),
        ('207049f5-6838-2132-4884-684021b0da9b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', '780fe021-3614-fceb-71be-fbf1a3cfcf28', 51.48, 1),
        ('20dbedc8-c6ee-99d9-204b-f9524d7ac726', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 'd2b8df88-73a9-5258-296d-f8652ad10430', 62.65, 1),
        ('27f70438-dec1-a5e5-ae8d-3bb8e2309bcd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a93029b7-0f17-d3c3-6f78-637f01b2c82d', '3fd19805-7287-29d4-63b5-48d6ab124adc', 44.72, 1),
        ('287b59ba-612b-b695-5bd7-2d7f2ae73b51', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 23.54, 1),
        ('29ce87d9-de05-6eed-3c98-6567a2c80b4a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', '03bcb0de-acf6-a30c-777c-7757c8661c59', 118.46, 1),
        ('2be4b3b7-d284-57dc-4c3c-25079457efb0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', '289f14d3-141d-ff24-1013-904503e5a2a9', 45, 1),
        ('360a5b4b-3c3b-d97b-e213-c1fe17015629', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd60c9c3b-48b5-e4d3-9007-49e30edcf5ee', '33943a72-9906-549a-3c27-d80eb89fdd54', 36.72, 1),
        ('3644203a-916d-56f4-c9ec-76948d4fce1c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'e90f31ce-2cad-df2f-2e4b-a3eb78828260', 76.11, 1),
        ('41c7baf9-2864-2b4b-e245-d68dda16a5f4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 202.30, 2),
        ('41f66c1b-3ea6-2845-b1af-2f88fc55712a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'e6118c6d-9fb2-1e7c-e77d-269753b36941', '30bf6d4c-8c93-57ef-b270-20d6831d4581', 42.58, 1),
        ('43d95d1a-26a7-4d6a-f606-d6399d52ed6a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '289f14d3-141d-ff24-1013-904503e5a2a9', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', 39.36, 1),
        ('4661fb05-8ec7-dc09-54bc-083b409f73f8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '33943a72-9906-549a-3c27-d80eb89fdd54', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 38.01, 1),
        ('47cf666a-df57-67e9-7c8f-d13c20951202', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '4182bfd6-48b1-517a-de17-02326198da1b', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 40.85, 1),
        ('4bc6321e-5298-ec08-a0fd-ba4f17b9cb7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', 'c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', 41.23, 1),
        ('4cff35f9-a7f8-9f9e-c7f1-eafaa51ab9a6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd81b94a7-92b1-0459-383c-c82e86fe57ee', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 98.86, 1),
        ('52a25608-01f4-1d64-457c-c46894b38db0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c5120588-0b2e-22fe-c5d7-4697e24aff54', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', 65.30, 1),
        ('530df23b-a1ad-0fc7-c9e2-2e207abfd346', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 55.87, 1),
        ('56175e25-430e-7b90-f79f-854d80b3dfd9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 'ec62360a-6a52-3c7e-62f0-37b657551e11', 60.42, 1),
        ('5d1f29c2-048f-953f-e8cf-a0e5990097ff', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 47.17, 1),
        ('5ed719bd-78b5-22b0-e6b3-009323e9a422', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '60a870d0-a90b-740c-bc02-8f11358b5a57', 'a93029b7-0f17-d3c3-6f78-637f01b2c82d', 42.72, 1),
        ('5fb1ef1d-e4c0-af2f-ea98-b481000bf131', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', '6de39c30-c8ce-571d-6e07-836f61b66044', 210, 2),
        ('60c8fa79-23a3-4da5-9ed4-f8f4bc830876', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1ed45dec-c261-6f52-6fda-1c2499040654', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', 35.74, 1),
        ('658f9ef2-564e-f152-8b3b-1c922b9b00ac', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 294.98, 2),
        ('677dba36-9962-df67-762e-12d03813f855', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 'c5120588-0b2e-22fe-c5d7-4697e24aff54', 104.71, 1),
        ('6b830363-03e4-6784-6dd2-128ae45c87dd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', 63.64, 1),
        ('6c604f42-c3b4-144c-3b65-0cbca61ae189', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', 47.17, 1),
        ('7355b63e-be26-6351-d625-7de33f40ca9e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 43.01, 1),
        ('739d2a11-02fc-f24c-fc60-34be847fc1b5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', '043ae2e1-ceec-5e72-27ed-25b2b2893833', 37.48, 1),
        ('749dff83-4923-5cd5-6bfc-b9ecb3e60bc9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', '1b4109ca-8f45-263a-e773-60297272f0aa', 57.01, 1),
        ('762e84eb-e84f-e519-9627-9ac384fc82f7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', '9ea672e2-02e9-96a5-6e17-b9c2479b83dd', 54.57, 1),
        ('77547d3b-5948-2f61-3e17-bb588f3c32e0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '6de39c30-c8ce-571d-6e07-836f61b66044', '563cfa23-b533-2dfe-7734-9a2151a36731', 36.40, 1),
        ('7786b88a-07ec-11a6-1503-df8094a5b4c0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ecab282f-7cd5-bb87-153c-d4bc1be51de3', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 65.03, 1),
        ('79074f2e-213b-f974-ebdc-6fea7bb3cf0b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', 42.20, 1),
        ('79cc22b2-8a13-bf50-b913-1ae9690cf732', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', '4182bfd6-48b1-517a-de17-02326198da1b', 45.65, 1),
        ('7bd39588-28be-f006-68af-9080ae08dc20', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5da5c091-db1f-6755-2fac-952f7468ef2f', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 35.85, 1),
        ('7e0a313a-b0b2-9a41-e4a2-fb967d7699e8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '3fd19805-7287-29d4-63b5-48d6ab124adc', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 42.72, 1),
        ('82c3f87c-f9cc-2a99-10d7-dde5ae3af848', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '090ff8af-f3eb-2c34-411e-daabcac34283', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 220.02, 2),
        ('83b2d6d8-d8a7-5174-12a4-d40ba5e546c5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 215.19, 2),
        ('8795b1a2-571f-db37-5164-5282c7706505', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5400b54d-c114-8f13-a103-dd92064c7c26', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 48, 1),
        ('88f3b5ca-1aca-e054-6d28-234a7d5d55a3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 86.68, 1),
        ('8abe6850-42d2-a938-3b7e-6c03db942767', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '090ff8af-f3eb-2c34-411e-daabcac34283', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', 40.11, 1),
        ('8cae82f7-279f-2389-0ce1-ba3657cd83e3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '03bcb0de-acf6-a30c-777c-7757c8661c59', 'f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', 35.34, 1),
        ('8de1d632-a275-ebb2-2f02-c59ac5dd6430', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '30bf6d4c-8c93-57ef-b270-20d6831d4581', '9b18a470-5646-f257-a801-b7994aa2883e', 35.69, 1),
        ('8e452235-708d-7d15-da5d-03d1830e7d7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '30bf6d4c-8c93-57ef-b270-20d6831d4581', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', 47, 1),
        ('8f33472c-e327-d75f-6b6e-72bfeaecfb4c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', '52134872-ef75-71bd-3102-7375ce47b077', 50.29, 1),
        ('9a1676a3-2b98-16cb-0ef2-a9de14a2ea66', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 'feb7ab95-935f-b582-fd85-d329d2487b15', 327.04, 2),
        ('9a7578f1-c43a-656b-8367-cd3f81a94ba4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '090ff8af-f3eb-2c34-411e-daabcac34283', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 179.89, 2),
        ('9af1891c-eee1-0eb2-95f4-41be33a0148c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ecab282f-7cd5-bb87-153c-d4bc1be51de3', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', 60.42, 1),
        ('9dd5c2ed-d5c8-daae-9e36-556cda794521', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '780fe021-3614-fceb-71be-fbf1a3cfcf28', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 79.93, 1),
        ('9e165150-99e4-71f1-1d8b-a670e10b9b7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 117.60, 1),
        ('a308b5a1-6eba-04cb-4c57-877aa53a3c38', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', '5da5c091-db1f-6755-2fac-952f7468ef2f', 70.21, 1),
        ('a3d7bb14-0d5c-7db9-6c18-09912c441824', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', '4182bfd6-48b1-517a-de17-02326198da1b', 80.31, 1),
        ('a6ea2994-77b3-d027-10d1-97476039bd86', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 'e90f31ce-2cad-df2f-2e4b-a3eb78828260', 51.62, 1),
        ('ab857b3c-e26a-7b49-c346-cae96fc76cb1', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 307.83, 2),
        ('ac070894-329c-5585-39ef-b39cab17d47f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 102.49, 1),
        ('ad230d2d-44df-fd82-5920-7c8cda11fce4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd81b94a7-92b1-0459-383c-c82e86fe57ee', '5400b54d-c114-8f13-a103-dd92064c7c26', 50.54, 1),
        ('b1bc62e5-08ea-9b5b-52be-ca97166e2e77', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 44.15, 1),
        ('bc348867-3ed8-bd1b-6e53-deff0dda7ee4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9b18a470-5646-f257-a801-b7994aa2883e', 'ecab282f-7cd5-bb87-153c-d4bc1be51de3', 25, 1),
        ('c0cc120d-bde2-4fbf-626a-e1a288d576b0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'feb7ab95-935f-b582-fd85-d329d2487b15', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', 39.29, 1),
        ('c588a931-23ab-7b10-39cb-d8db4cfdb5af', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '52134872-ef75-71bd-3102-7375ce47b077', '1ed45dec-c261-6f52-6fda-1c2499040654', 37.36, 1),
        ('c6b75443-1eed-46fa-17b3-9a02fe20df01', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '60a870d0-a90b-740c-bc02-8f11358b5a57', '3fd19805-7287-29d4-63b5-48d6ab124adc', 80.16, 1),
        ('c7f77445-e4c4-3efe-a173-b46a3b378cd5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', 69.63, 1),
        ('c8050c6f-8736-93ef-8a56-7dae07eed8aa', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '563cfa23-b533-2dfe-7734-9a2151a36731', '5da5c091-db1f-6755-2fac-952f7468ef2f', 51.79, 1),
        ('c99651fa-0e8e-9161-511a-a14c8c9058c3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', '6de39c30-c8ce-571d-6e07-836f61b66044', 393.57, 2),
        ('cad0265f-33ce-9c1e-f4fb-120e09cb4145', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '043ae2e1-ceec-5e72-27ed-25b2b2893833', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 49.58, 1),
        ('cce8f3df-d0d1-f9ee-6506-9de421222d12', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0f5a5921-4a8d-2752-b90b-2b2e7606876d', 'e6118c6d-9fb2-1e7c-e77d-269753b36941', 48.04, 1),
        ('cd8dc859-f31e-8ee3-e479-db91ed4e98c0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ec62360a-6a52-3c7e-62f0-37b657551e11', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 42.49, 1),
        ('cfbd7763-f392-891c-2f4a-7152efce515d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 32.70, 1),
        ('d2a99b73-749f-59f1-b988-d953f4c85c37', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', 42.72, 1),
        ('d3c8d5ad-a78e-14d7-a46b-a1b9cbd13a94', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'e90f31ce-2cad-df2f-2e4b-a3eb78828260', 'd60c9c3b-48b5-e4d3-9007-49e30edcf5ee', 43.10, 1),
        ('d831b323-b49f-e353-a6f2-f6b0c1420718', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '563cfa23-b533-2dfe-7734-9a2151a36731', 'd81b94a7-92b1-0459-383c-c82e86fe57ee', 62.18, 1),
        ('de5c360a-66b2-8c7b-887a-b6d738b749b0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'bf024a45-f9e4-ba98-fb72-43d404c37d4a', '6de39c30-c8ce-571d-6e07-836f61b66044', 74.65, 1),
        ('defc43ee-c9b5-0f1b-c215-5c72b9f31985', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 30.23, 1),
        ('df26e94e-9fdb-4fc8-b0ed-85363c547f7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', 44.42, 1),
        ('e29eed18-a49a-8397-1e80-5fa2cfa5405d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 87.32, 1),
        ('e5a29768-c160-239c-8c92-306040553b68', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', '090ff8af-f3eb-2c34-411e-daabcac34283', 42.30, 1),
        ('e89313c5-a78c-0c58-8197-ec4383b16fcb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '780fe021-3614-fceb-71be-fbf1a3cfcf28', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', 51.48, 1),
        ('eab96394-9501-0048-5396-1e0f9f41b701', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4109ca-8f45-263a-e773-60297272f0aa', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', 58.86, 1),
        ('eb8c71f1-b0b5-d80b-ada9-807f38366076', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', 'c5120588-0b2e-22fe-c5d7-4697e24aff54', 45.97, 1),
        ('ed5ca002-337d-c06a-77ea-d4369a51753f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', '1ed45dec-c261-6f52-6fda-1c2499040654', 90.80, 1),
        ('ef82ad4d-8382-3d1c-1123-c01d3eef9b8b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '774da183-9820-66a1-7f3f-80f5eb7fd48d', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 325.68, 2),
        ('f26f1597-55fd-5f5d-9882-8964fce0d0cf', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9ea672e2-02e9-96a5-6e17-b9c2479b83dd', 'feb7ab95-935f-b582-fd85-d329d2487b15', 80.62, 1),
        ('f3ef9f53-b18e-75c5-7ce1-fce8a47fa876', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'e6118c6d-9fb2-1e7c-e77d-269753b36941', 160.91, 2),
        ('f7c71da6-b2e2-2beb-253d-c1cfaba2722d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 64.29, 1),
        ('f8e0c393-0949-6d55-ae0f-8b114a735493', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '32c77794-0879-14fc-faf4-6d8ac3779318', '0f5a5921-4a8d-2752-b90b-2b2e7606876d', 42.30, 1),
        ('fbce1e03-0398-6847-0e37-3a6efe9d4e46', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', '60a870d0-a90b-740c-bc02-8f11358b5a57', 47.17, 1),
        ('fff611bc-5207-2746-5dc5-4051f8efd3c0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', '32c77794-0879-14fc-faf4-6d8ac3779318', 67.27, 1);

    INSERT INTO dbo.Admirals(AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt)
    VALUES
        ('82ba60e7-3827-3d9a-57e4-7511bc9c5fa2', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'Elian Voss', 0, N'Active', @SeededAt, @SeededAt),
        ('4c2b8d58-af0d-9f22-846e-4c213d53d181', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '98428db6-ba14-1fb9-14c1-86a6d38348b7', N'Mara Sutekh', 0, N'Active', @SeededAt, @SeededAt),
        ('dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', N'Tavian Orre', 0, N'Active', @SeededAt, @SeededAt);

    INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, FactionID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, DepartureTickNumber, ArrivalTickNumber, ShipCount, Status, CreatedAt)
    VALUES
        ('a8c01cc4-efad-d769-6100-4c8f28606a98', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', '1b12558d-ff5e-d372-e4c1-94f63137b642', '82ba60e7-3827-3d9a-57e4-7511bc9c5fa2', N'Aurelian Compact Home Guard', '1b4109ca-8f45-263a-e773-60297272f0aa', NULL, NULL, NULL, 30, N'Active', @SeededAt),
        ('6b3cdbd7-27ff-28b4-ac0b-4e982c7ec1d6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Deep Vault Free Captains', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', NULL, NULL, NULL, 8, N'Active', @SeededAt),
        ('095612c2-4f59-0f52-284c-5118d428c24f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '98428db6-ba14-1fb9-14c1-86a6d38348b7', '98428db6-ba14-1fb9-14c1-86a6d38348b7', NULL, N'Deep Vault Vanguard', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', NULL, NULL, NULL, 18, N'Active', @SeededAt),
        ('6e539737-1902-9ae8-b3f7-4c3767c677c3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Free Captain Patrol 1', 'a93029b7-0f17-d3c3-6f78-637f01b2c82d', NULL, NULL, NULL, 14, N'Active', @SeededAt),
        ('5f1651d7-bb68-7068-ad68-f2103e041364', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Free Captain Patrol 2', '9ea672e2-02e9-96a5-6e17-b9c2479b83dd', NULL, NULL, NULL, 13, N'Active', @SeededAt),
        ('6108056b-cbd8-615c-4dd1-925378c8f783', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Free Captain Patrol 3', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', NULL, NULL, NULL, 12, N'Active', @SeededAt),
        ('90507006-2403-9e35-870a-5737678e5137', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '98428db6-ba14-1fb9-14c1-86a6d38348b7', '98428db6-ba14-1fb9-14c1-86a6d38348b7', '4c2b8d58-af0d-9f22-846e-4c213d53d181', N'Khepri Mandate Home Guard', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', NULL, NULL, NULL, 30, N'Active', @SeededAt),
        ('e17e8e91-d4fe-aabf-c35d-deaa6c26172e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Night Span Free Captains', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', NULL, NULL, NULL, 8, N'Active', @SeededAt),
        ('029b750a-db56-cf33-628e-4abd4ec3b834', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', '1b12558d-ff5e-d372-e4c1-94f63137b642', NULL, N'Night Span Vanguard', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', NULL, NULL, NULL, 18, N'Active', @SeededAt),
        ('b46b714a-061e-0c83-1619-15f278031670', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', NULL, N'Northstar Gate Survey', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', NULL, NULL, NULL, 12, N'Active', @SeededAt),
        ('93547ff6-2b99-abde-bb56-8e878963df45', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', N'Novan League Home Guard', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', NULL, NULL, NULL, 30, N'Active', @SeededAt),
        ('6aae2bb7-53a1-2efe-9165-6e7492ac34e8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '98428db6-ba14-1fb9-14c1-86a6d38348b7', '98428db6-ba14-1fb9-14c1-86a6d38348b7', NULL, N'Penumbral Span Survey', '3fd19805-7287-29d4-63b5-48d6ab124adc', NULL, NULL, NULL, 12, N'Active', @SeededAt),
        ('8c66cf9b-5fc8-b567-b80d-6af68ba079ae', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', NULL, 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', NULL, N'Sentinel Spur Free Captains', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', NULL, NULL, NULL, 8, N'Active', @SeededAt),
        ('7e6494db-66fc-5539-e43a-20493061ba8c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', NULL, N'Sentinel Spur Vanguard', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', NULL, NULL, NULL, 18, N'Active', @SeededAt),
        ('04eff6c0-998a-66ef-c882-34b221368b4b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', '1b12558d-ff5e-d372-e4c1-94f63137b642', NULL, N'Umbral Way Survey', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', NULL, NULL, NULL, 12, N'Active', @SeededAt);

    INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, FactionID, Severity, FactJson, DisplayText, CreatedAt)
    VALUES
        ('56c7fae3-29e6-a57e-e402-4cb71936ff8f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'OpeningBriefingIssued', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', 'f60b5f1e-a53f-9d22-3396-4b36a9b25571', N'High', N'{
  "scenarioKey": "development-match-v2",
  "scenarioSeed": 20260717,
  "mapVersion": "territorial-graph-v2",
  "setupAlgorithmVersion": 1,
  "focusSystemId": "45cdce04-7d01-eead-4a2f-ce299ec25aa3",
  "objectives": {
    "move": {
      "fleetId": "93547ff6-2b99-abde-bb56-8e878963df45",
      "targetSystemId": "1ed45dec-c261-6f52-6fda-1c2499040654"
    },
    "colonise": {
      "fleetId": "b46b714a-061e-0c83-1619-15f278031670",
      "systemId": "1beef779-8b9f-7fd7-18e2-5cbf2c347d7f"
    },
    "attack": {
      "fleetId": "7e6494db-66fc-5539-e43a-20493061ba8c",
      "systemId": "45cdce04-7d01-eead-4a2f-ce299ec25aa3",
      "targetFactionId": "fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037"
    }
  }
}', N'Day 1 briefing: Free Captains contest Sentinel Spur. Northstar Gate is ready for an outpost, while Zenith Yard offers immediate expansion.', @SeededAt),
        ('59412fdc-6d2f-f5e8-a6e5-88ab480e6372', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'OpeningBriefingIssued', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', '1b12558d-ff5e-d372-e4c1-94f63137b642', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'High', N'{
  "scenarioKey": "development-match-v2",
  "scenarioSeed": 20260717,
  "mapVersion": "territorial-graph-v2",
  "setupAlgorithmVersion": 1,
  "focusSystemId": "b611c0c2-5995-70e1-3ffe-417c1d305c14",
  "objectives": {
    "move": {
      "fleetId": "a8c01cc4-efad-d769-6100-4c8f28606a98",
      "targetSystemId": "181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7"
    },
    "colonise": {
      "fleetId": "04eff6c0-998a-66ef-c882-34b221368b4b",
      "systemId": "0a4ce56e-94fc-f9d7-64f8-9e19977b07bb"
    },
    "attack": {
      "fleetId": "029b750a-db56-cf33-628e-4abd4ec3b834",
      "systemId": "b611c0c2-5995-70e1-3ffe-417c1d305c14",
      "targetFactionId": "fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037"
    }
  }
}', N'Day 1 briefing: Free Captains contest Night Span. Umbral Way is ready for an outpost, while Umbral Bastion offers immediate expansion.', @SeededAt),
        ('987bba47-af77-9e2c-4760-cc5d60da32dd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'CycleSeeded', NULL, NULL, NULL, N'Normal', N'{
  "cycleId": "fce1d96a-6a07-4559-cff6-dd6efde758ae",
  "empireCount": 3,
  "systemCount": 64,
  "sectorCount": 8,
  "topologyKey": "territorial-graph-v2",
  "seed": 71421
}', N'The ' + @CycleName + N' began with 3 empires and 64 systems.', @SeededAt),
        ('fda733e5-d001-80f3-2f94-f1626ad0fd3a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'OpeningBriefingIssued', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', '98428db6-ba14-1fb9-14c1-86a6d38348b7', '98428db6-ba14-1fb9-14c1-86a6d38348b7', N'High', N'{
  "scenarioKey": "development-match-v2",
  "scenarioSeed": 20260717,
  "mapVersion": "territorial-graph-v2",
  "setupAlgorithmVersion": 1,
  "focusSystemId": "a00244e8-42f4-56fc-ebff-1a1e60153f97",
  "objectives": {
    "move": {
      "fleetId": "90507006-2403-9e35-870a-5737678e5137",
      "targetSystemId": "4182bfd6-48b1-517a-de17-02326198da1b"
    },
    "colonise": {
      "fleetId": "6aae2bb7-53a1-2efe-9165-6e7492ac34e8",
      "systemId": "3fd19805-7287-29d4-63b5-48d6ab124adc"
    },
    "attack": {
      "fleetId": "095612c2-4f59-0f52-284c-5118d428c24f",
      "systemId": "a00244e8-42f4-56fc-ebff-1a1e60153f97",
      "targetFactionId": "fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037"
    }
  }
}', N'Day 1 briefing: Free Captains contest Deep Vault. Penumbral Span is ready for an outpost, while Far Meridian offers immediate expansion.', @SeededAt);

END;
COMMIT TRANSACTION;
