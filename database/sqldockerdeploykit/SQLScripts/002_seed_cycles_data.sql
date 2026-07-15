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

    INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject, Role, CreatedAt, LastLoginAt, Status)
    VALUES
        ('089f8d2b-cb2f-2235-8fae-c0d1a135c865', N'player-1', N'player-1@cycles.local', N'prototype', N'', N'', N'Player', @SeededAt, @SeededAt, N'Active'),
        ('7ff1913a-63f9-122f-cd2b-ac8e709afa03', N'player-2', N'player-2@cycles.local', N'prototype', N'', N'', N'Player', @SeededAt, @SeededAt, N'Active'),
        ('4c2b8d58-af0d-9f22-846e-4c213d53d181', N'player-3', N'player-3@cycles.local', N'prototype', N'', N'', N'Player', @SeededAt, @SeededAt, N'Active'),
        ('5157de0c-e13b-b784-3c78-b5bc77fa9bb0', N'player-4', N'player-4@cycles.local', N'prototype', N'', N'', N'Player', @SeededAt, @SeededAt, N'Active');

    INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
    VALUES
        ('fce1d96a-6a07-4559-cff6-dd6efde758ae', @CycleName, @SeededAt, DATEADD(DAY, 90, @SeededAt), 60, 0, N'Active', @SeededAt);

    INSERT INTO dbo.GalaxySectors(SectorID, CycleID, SectorName, CentreX, CentreY, SortOrder)
    VALUES
        ('8e20bd6d-9b8e-9800-ee6c-4338de928cb1', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Aster Reach', 500, 325, 0),
        ('669f0235-9c47-5c3f-764e-39e80a696594', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Cinder March', 278, 116, 1),
        ('d07e8579-5205-1fa9-ce91-6522e730db13', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Hollow Crown', 722, 112, 2),
        ('fffad29e-8bac-8925-745f-fbe5df74bac4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Lacuna Verge', 866, 329, 3),
        ('307e4cd3-0245-9d11-4ed2-7c1c3862e135', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Orison Fold', 696, 574, 4),
        ('c538bd49-820e-075d-3088-400abf0b89d6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Red Lattice', 337, 568, 5),
        ('552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Umbral Marches', 126, 339, 6),
        ('45426bfc-440f-c4f0-8a7e-7e540275f795', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', N'Warden Line', 493, 100, 7);

    INSERT INTO dbo.Systems(SystemID, CycleID, SectorID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
    VALUES
        ('774da183-9820-66a1-7f3f-80f5eb7fd48d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ashen Gate', 343, 133, 60, 61, 45, 45, 1, @SeededAt),
        ('afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Aster Vale', 469, 272, 78, 32, 28, 27, 0, @SeededAt),
        ('d60c9c3b-48b5-e4d3-9007-49e30edcf5ee', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Brightfall', 476, 376, 20, 74, 40, 26, 0, @SeededAt),
        ('d2b8df88-73a9-5258-296d-f8652ad10430', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Cinder Relay', 309, 169, 35, 62, 12, 21, 0, @SeededAt),
        ('ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Cinderhome', 220, 108, 37, 58, 42, 39, 1, @SeededAt),
        ('5400b54d-c114-8f13-a103-dd92064c7c26', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Crimson Needle', 404, 560, 53, 63, 19, 27, 0, @SeededAt),
        ('fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Crimson Relay', 388, 607, 84, 61, 62, 53, 1, @SeededAt),
        ('1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Crown Meridian', 773, 78, 29, 15, 34, 15, 0, @SeededAt),
        ('33943a72-9906-549a-3c27-d80eb89fdd54', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Dawnward', 444, 347, 35, 63, 41, 27, 0, @SeededAt),
        ('a00244e8-42f4-56fc-ebff-1a1e60153f97', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Deep Vault', 879, 390, 71, 71, 36, 35, 0, @SeededAt),
        ('8ab6466f-0cfa-caef-092e-b7eedfbca20d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ebon Strait', 249, 67, 37, 75, 54, 33, 0, @SeededAt),
        ('f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Ember Watch', 227, 145, 73, 75, 55, 40, 0, @SeededAt),
        ('4182bfd6-48b1-517a-de17-02326198da1b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Far Meridian', 803, 342, 22, 39, 64, 25, 0, @SeededAt),
        ('d3b47ed9-315f-ca64-1ffe-c4835527e96b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Fold Meridian', 665, 623, 38, 21, 58, 23, 0, @SeededAt),
        ('043ae2e1-ceec-5e72-27ed-25b2b2893833', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Glass Meridian', 290, 59, 75, 26, 46, 29, 0, @SeededAt),
        ('4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Glass Refuge', 712, 170, 44, 54, 38, 27, 0, @SeededAt),
        ('289f14d3-141d-ff24-1013-904503e5a2a9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'High Anchorage', 488, 159, 28, 22, 55, 21, 0, @SeededAt),
        ('090ff8af-f3eb-2c34-411e-daabcac34283', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Bastion', 790, 123, 32, 41, 61, 38, 1, @SeededAt),
        ('feb7ab95-935f-b582-fd85-d329d2487b15', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Crown', 656, 115, 59, 67, 47, 51, 2, @SeededAt),
        ('c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Hollow Lantern', 729, 52, 29, 68, 57, 30, 0, @SeededAt),
        ('7dae71c0-5593-bbdc-e86e-c3d198fb7341', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Juniper Rift', 684, 69, 81, 45, 26, 30, 0, @SeededAt),
        ('7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Keystone', 332, 91, 70, 69, 16, 31, 0, @SeededAt),
        ('57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna', 811, 297, 49, 23, 61, 43, 2, @SeededAt),
        ('f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna Beacon', 832, 381, 58, 31, 38, 25, 0, @SeededAt),
        ('a93029b7-0f17-d3c3-6f78-637f01b2c82d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Lacuna Shoal', 897, 280, 69, 42, 64, 35, 0, @SeededAt),
        ('c5120588-0b2e-22fe-c5d7-4697e24aff54', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Marcher Beacon', 62, 343, 33, 74, 17, 24, 0, @SeededAt),
        ('a511a05a-564c-f253-b3d9-d349f3d2215e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Mourn Relay', 920, 364, 65, 48, 38, 42, 1, @SeededAt),
        ('60a870d0-a90b-740c-bc02-8f11358b5a57', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Mournstar', 851, 271, 78, 75, 58, 42, 0, @SeededAt),
        ('ec62360a-6a52-3c7e-62f0-37b657551e11', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Nadir Crossing', 518, 280, 30, 35, 47, 22, 0, @SeededAt),
        ('b611c0c2-5995-70e1-3ffe-417c1d305c14', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Night Span', 83, 382, 45, 49, 63, 31, 0, @SeededAt),
        ('1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Northstar Gate', 442, 136, 62, 61, 23, 29, 0, @SeededAt),
        ('32c77794-0879-14fc-faf4-6d8ac3779318', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison', 633, 553, 32, 23, 56, 39, 2, @SeededAt),
        ('9b18a470-5646-f257-a801-b7994aa2883e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison Anchorage', 754, 605, 25, 61, 51, 39, 1, @SeededAt),
        ('e6118c6d-9fb2-1e7c-e77d-269753b36941', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Orison Lantern', 719, 521, 50, 50, 34, 26, 0, @SeededAt),
        ('30bf6d4c-8c93-57ef-b270-20d6831d4581', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Pale Coil', 757, 556, 23, 20, 49, 18, 0, @SeededAt),
        ('9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Pale Harbour', 555, 304, 77, 61, 10, 29, 0, @SeededAt),
        ('3fd19805-7287-29d4-63b5-48d6ab124adc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fffad29e-8bac-8925-745f-fbe5df74bac4', N'Penumbral Span', 931, 316, 22, 32, 27, 16, 0, @SeededAt),
        ('a0c41129-e9c0-d1e0-8409-da4c22608ad7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Pilgrim''s Wake', 637, 591, 48, 54, 59, 32, 0, @SeededAt),
        ('e90f31ce-2cad-df2f-2e4b-a3eb78828260', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Pseudopolis', 525, 383, 25, 16, 55, 19, 0, @SeededAt),
        ('03bcb0de-acf6-a30c-777c-7757c8661c59', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '669f0235-9c47-5c3f-764e-39e80a696594', N'Pyre Anchorage', 261, 176, 77, 67, 33, 35, 0, @SeededAt),
        ('ecab282f-7cd5-bb87-153c-d4bc1be51de3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Quiet Harbour', 714, 635, 63, 31, 49, 28, 0, @SeededAt),
        ('0f5a5921-4a8d-2752-b90b-2b2e7606876d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '307e4cd3-0245-9d11-4ed2-7c1c3862e135', N'Quietus', 671, 518, 77, 59, 64, 40, 0, @SeededAt),
        ('bf024a45-f9e4-ba98-fb72-43d404c37d4a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Red Haven', 271, 576, 65, 73, 22, 32, 0, @SeededAt),
        ('6de39c30-c8ce-571d-6e07-836f61b66044', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Red Lattice', 280, 539, 58, 15, 49, 41, 2, @SeededAt),
        ('563cfa23-b533-2dfe-7734-9a2151a36731', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Sable Point', 318, 506, 51, 62, 42, 31, 0, @SeededAt),
        ('5da5c091-db1f-6755-2fac-952f7468ef2f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Sable Vault', 345, 627, 83, 45, 59, 37, 0, @SeededAt),
        ('45cdce04-7d01-eead-4a2f-ce299ec25aa3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Sentinel Spur', 530, 147, 71, 20, 49, 28, 0, @SeededAt),
        ('c2a81993-5703-8030-b082-dfe89ce4c7e3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Shadow Cairn', 192, 335, 80, 61, 17, 31, 0, @SeededAt),
        ('9ea672e2-02e9-96a5-6e17-b9c2479b83dd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Silent Array', 670, 147, 54, 57, 11, 24, 0, @SeededAt),
        ('d81b94a7-92b1-0459-383c-c82e86fe57ee', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Ternary', 366, 520, 62, 68, 16, 29, 0, @SeededAt),
        ('f8b85da0-c7e5-24ad-b678-0d3533c78429', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c538bd49-820e-075d-3088-400abf0b89d6', N'Ternary Watch', 299, 616, 69, 60, 36, 33, 0, @SeededAt),
        ('fe739d22-e193-f482-cbcf-3f5977f3ab8b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Treaty Gate', 439, 307, 25, 21, 30, 35, 4, @SeededAt),
        ('181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Bastion', 175, 381, 29, 59, 54, 40, 1, @SeededAt),
        ('780fe021-3614-fceb-71be-fbf1a3cfcf28', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Lantern', 165, 295, 46, 24, 26, 19, 0, @SeededAt),
        ('0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Umbral Way', 74, 301, 75, 33, 38, 46, 2, @SeededAt),
        ('f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Verdant Coil', 119, 279, 28, 29, 17, 14, 0, @SeededAt),
        ('c23b5450-e8d6-e676-023d-6dd23e6ebf7b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd07e8579-5205-1fa9-ce91-6522e730db13', N'Vigil Cairn', 761, 167, 29, 55, 34, 23, 0, @SeededAt),
        ('1b4109ca-8f45-263a-e773-60297272f0aa', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '552c6fc1-0fda-1aaa-b8b5-f7ac3f750b1d', N'Viridian Refuge', 130, 402, 27, 64, 37, 25, 0, @SeededAt),
        ('83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Warden Watch', 541, 63, 34, 56, 42, 26, 0, @SeededAt),
        ('52423a8c-14dd-1699-d94d-f1ce59ec843d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Warden''s Line', 431, 95, 84, 68, 30, 53, 2, @SeededAt),
        ('cfbe7586-f639-add0-c4e6-d41370bc36ae', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Xanthe', 457, 59, 68, 48, 27, 28, 0, @SeededAt),
        ('8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e20bd6d-9b8e-9800-ee6c-4338de928cb1', N'Yanaka''s Reach', 563, 352, 33, 44, 37, 44, 2, @SeededAt),
        ('52134872-ef75-71bd-3102-7375ce47b077', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Yarrow', 496, 45, 68, 24, 21, 22, 0, @SeededAt),
        ('1ed45dec-c261-6f52-6fda-1c2499040654', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45426bfc-440f-c4f0-8a7e-7e540275f795', N'Zenith Yard', 559, 105, 61, 18, 65, 45, 2, @SeededAt);

    INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
    VALUES
        ('1b12558d-ff5e-d372-e4c1-94f63137b642', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '089f8d2b-cb2f-2235-8fae-c0d1a135c865', N'Aurelian Compact', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', @SeededAt, N'Active'),
        ('c0660deb-fa0d-be04-7967-8a1e8691079e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7ff1913a-63f9-122f-cd2b-ac8e709afa03', N'Khepri Mandate', '090ff8af-f3eb-2c34-411e-daabcac34283', @SeededAt, N'Active'),
        ('5b61c575-b567-8f71-e940-4e08f628cad7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '4c2b8d58-af0d-9f22-846e-4c213d53d181', N'Novan League', '9b18a470-5646-f257-a801-b7994aa2883e', @SeededAt, N'Active'),
        ('dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5157de0c-e13b-b784-3c78-b5bc77fa9bb0', N'Vestige Combine', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', @SeededAt, N'Active');

    INSERT INTO dbo.EmpireResources(EmpireResourceID, EmpireID, Industry, Research, Population, LastGeneratedIndustry, LastGeneratedResearch, LastGeneratedPopulation, LastSpentIndustry, LastSpentResearch, LastSpentPopulation, UpdatedAt)
    VALUES
        ('40a59e85-1d96-90c1-201b-70f33711ba04', '1b12558d-ff5e-d372-e4c1-94f63137b642', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt),
        ('98428db6-ba14-1fb9-14c1-86a6d38348b7', 'c0660deb-fa0d-be04-7967-8a1e8691079e', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt),
        ('cac84274-f341-cb4f-1687-b580727b072d', '5b61c575-b567-8f71-e940-4e08f628cad7', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt),
        ('be43d99b-bd01-bfb6-b95b-a04e0680e0db', 'dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', 100, 100, 100, 0, 0, 0, 0, 0, 0, @SeededAt);

    INSERT INTO dbo.EmpirePriorities(EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt)
    VALUES
        ('3510456e-fb83-1403-f88f-2bd6cbc442e1', '1b12558d-ff5e-d372-e4c1-94f63137b642', 0, 0, 67, 33, @SeededAt),
        ('828573b7-ce4b-b890-5065-b80574c14fc0', 'c0660deb-fa0d-be04-7967-8a1e8691079e', 0, 0, 67, 33, @SeededAt),
        ('f60b5f1e-a53f-9d22-3396-4b36a9b25571', '5b61c575-b567-8f71-e940-4e08f628cad7', 0, 0, 67, 33, @SeededAt),
        ('987bba47-af77-9e2c-4760-cc5d60da32dd', 'dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', 0, 0, 67, 33, @SeededAt);

    INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
    VALUES
        ('057dba48-14b5-4f8d-8678-a74b947c3f5a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', '1ed45dec-c261-6f52-6fda-1c2499040654', 234.96, 2),
        ('0d2f2894-0035-e1ea-579e-858e4a84a7b5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5400b54d-c114-8f13-a103-dd92064c7c26', '6de39c30-c8ce-571d-6e07-836f61b66044', 125.77, 1),
        ('14a2ddc6-fcb1-8b3d-5bda-22411a128a25', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', '289f14d3-141d-ff24-1013-904503e5a2a9', 104.69, 1),
        ('15e98a94-7c5e-d98b-7c63-1131ec98f6dc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 'bf024a45-f9e4-ba98-fb72-43d404c37d4a', 48.83, 1),
        ('19941a92-d3c0-0300-7317-da4c0bc7aeb8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd2b8df88-73a9-5258-296d-f8652ad10430', '03bcb0de-acf6-a30c-777c-7757c8661c59', 48.51, 1),
        ('1df8877a-4f9a-6dbf-14b5-28677f35bdec', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '4182bfd6-48b1-517a-de17-02326198da1b', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 119.05, 1),
        ('207049f5-6838-2132-4884-684021b0da9b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', '780fe021-3614-fceb-71be-fbf1a3cfcf28', 48.70, 1),
        ('20b4e188-dad5-7e80-5e2e-5a2fa4cf5f40', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1ed45dec-c261-6f52-6fda-1c2499040654', 'feb7ab95-935f-b582-fd85-d329d2487b15', 97.51, 2),
        ('20dbedc8-c6ee-99d9-204b-f9524d7ac726', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 'd2b8df88-73a9-5258-296d-f8652ad10430', 49.52, 1),
        ('2228070c-39c3-0a12-456d-5ad591928426', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', '32c77794-0879-14fc-faf4-6d8ac3779318', 212.84, 2),
        ('268dcbc7-2ac6-6cee-9621-aeeef2323171', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 412.17, 2),
        ('26ab72c8-5e42-6937-f4d0-86cccfb662e0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', '6de39c30-c8ce-571d-6e07-836f61b66044', 281.26, 2),
        ('27f70438-dec1-a5e5-ae8d-3bb8e2309bcd', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a93029b7-0f17-d3c3-6f78-637f01b2c82d', '3fd19805-7287-29d4-63b5-48d6ab124adc', 49.52, 1),
        ('287b59ba-612b-b695-5bd7-2d7f2ae73b51', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 49.09, 1),
        ('29ce87d9-de05-6eed-3c98-6567a2c80b4a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', '03bcb0de-acf6-a30c-777c-7757c8661c59', 109.66, 1),
        ('2be4b3b7-d284-57dc-4c3c-25079457efb0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '289f14d3-141d-ff24-1013-904503e5a2a9', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', 51.43, 1),
        ('360a5b4b-3c3b-d97b-e213-c1fe17015629', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd60c9c3b-48b5-e4d3-9007-49e30edcf5ee', '33943a72-9906-549a-3c27-d80eb89fdd54', 43.19, 1),
        ('3644203a-916d-56f4-c9ec-76948d4fce1c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'e90f31ce-2cad-df2f-2e4b-a3eb78828260', 49.04, 1),
        ('41b9b523-37cc-706f-c44d-e8a3d2879055', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 254.03, 2),
        ('41f66c1b-3ea6-2845-b1af-2f88fc55712a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'e6118c6d-9fb2-1e7c-e77d-269753b36941', '30bf6d4c-8c93-57ef-b270-20d6831d4581', 51.66, 1),
        ('43d95d1a-26a7-4d6a-f606-d6399d52ed6a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', '289f14d3-141d-ff24-1013-904503e5a2a9', 43.68, 1),
        ('49fc09ba-ef76-baac-4035-e93a29d2558b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '52134872-ef75-71bd-3102-7375ce47b077', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', 48.47, 1),
        ('4b70e309-c395-80e4-9dec-eb8f4159720c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '3fd19805-7287-29d4-63b5-48d6ab124adc', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 121.49, 1),
        ('4bc6321e-5298-ec08-a0fd-ba4f17b9cb7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', 'c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', 48.10, 1),
        ('505c2527-837d-02c5-9e58-f54ddfa9409e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9b18a470-5646-f257-a801-b7994aa2883e', '6de39c30-c8ce-571d-6e07-836f61b66044', 478.57, 2),
        ('519c08fa-f887-dbe5-58b9-8b250f2d92ef', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'bf024a45-f9e4-ba98-fb72-43d404c37d4a', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 121.04, 1),
        ('530df23b-a1ad-0fc7-c9e2-2e207abfd346', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 48.66, 1),
        ('56175e25-430e-7b90-f79f-854d80b3dfd9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 'ec62360a-6a52-3c7e-62f0-37b657551e11', 49.65, 1),
        ('56f2f34d-63db-cd6f-5c6c-16d74ef81c2d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 438.44, 2),
        ('5ed719bd-78b5-22b0-e6b3-009323e9a422', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '60a870d0-a90b-740c-bc02-8f11358b5a57', 'a93029b7-0f17-d3c3-6f78-637f01b2c82d', 46.87, 1),
        ('60c8fa79-23a3-4da5-9ed4-f8f4bc830876', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1beef779-8b9f-7fd7-18e2-5cbf2c347d7f', '1ed45dec-c261-6f52-6fda-1c2499040654', 121.04, 1),
        ('63d6566c-e02f-5e3f-aa71-d3e37f57fd49', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 122.80, 1),
        ('660dab31-1650-2444-ba85-4677eb4e43e2', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '60a870d0-a90b-740c-bc02-8f11358b5a57', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', 111.63, 1),
        ('68fcae3c-81d9-58a9-5f54-37ccc3530ce7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', '9b18a470-5646-f257-a801-b7994aa2883e', 117.83, 1),
        ('6c604f42-c3b4-144c-3b65-0cbca61ae189', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', 48.55, 1),
        ('7355b63e-be26-6351-d625-7de33f40ca9e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c16fd78f-eb52-1e25-c4d3-5bdb13e598ba', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 51.11, 1),
        ('739d2a11-02fc-f24c-fc60-34be847fc1b5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', '043ae2e1-ceec-5e72-27ed-25b2b2893833', 41.77, 1),
        ('749dff83-4923-5cd5-6bfc-b9ecb3e60bc9', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', '1b4109ca-8f45-263a-e773-60297272f0aa', 49.66, 1),
        ('762e84eb-e84f-e519-9627-9ac384fc82f7', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', '9ea672e2-02e9-96a5-6e17-b9c2479b83dd', 47.89, 1),
        ('77547d3b-5948-2f61-3e17-bb588f3c32e0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '6de39c30-c8ce-571d-6e07-836f61b66044', '563cfa23-b533-2dfe-7734-9a2151a36731', 50.33, 1),
        ('7786b88a-07ec-11a6-1503-df8094a5b4c0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ecab282f-7cd5-bb87-153c-d4bc1be51de3', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 50.45, 1),
        ('782c48e7-04d5-9fa5-131f-10b930a13f79', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c5120588-0b2e-22fe-c5d7-4697e24aff54', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 119.22, 1),
        ('79074f2e-213b-f974-ebdc-6fea7bb3cf0b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', 44.41, 1),
        ('79cc22b2-8a13-bf50-b913-1ae9690cf732', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', '4182bfd6-48b1-517a-de17-02326198da1b', 48.60, 1),
        ('79f90174-3f69-ac7b-bef9-7768ead63a39', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '33943a72-9906-549a-3c27-d80eb89fdd54', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 119.10, 1),
        ('7a8a7755-02af-305e-c453-2e0991915e5e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 116.04, 1),
        ('7bd39588-28be-f006-68af-9080ae08dc20', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5da5c091-db1f-6755-2fac-952f7468ef2f', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 47.30, 1),
        ('7dbbc96a-2b34-2fae-053d-7d2200604c7f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '8e2b63e9-5d25-fa85-e962-a1cb0cc683de', 'feb7ab95-935f-b582-fd85-d329d2487b15', 254.59, 2),
        ('7e0a313a-b0b2-9a41-e4a2-fb967d7699e8', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '3fd19805-7287-29d4-63b5-48d6ab124adc', 'a511a05a-564c-f253-b3d9-d349f3d2215e', 49.24, 1),
        ('82809c13-8521-25a4-cae2-5565a2c9f752', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '774da183-9820-66a1-7f3f-80f5eb7fd48d', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 95.85, 2),
        ('82c3f87c-f9cc-2a99-10d7-dde5ae3af848', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '090ff8af-f3eb-2c34-411e-daabcac34283', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', 175.26, 2),
        ('8795b1a2-571f-db37-5164-5282c7706505', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5400b54d-c114-8f13-a103-dd92064c7c26', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', 49.65, 1),
        ('88f3b5ca-1aca-e054-6d28-234a7d5d55a3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', '4e231af0-e98e-2031-2ebc-ff5ddc18abcd', 104.81, 1),
        ('8abe6850-42d2-a938-3b7e-6c03db942767', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '090ff8af-f3eb-2c34-411e-daabcac34283', 'c23b5450-e8d6-e676-023d-6dd23e6ebf7b', 52.70, 1),
        ('8cae82f7-279f-2389-0ce1-ba3657cd83e3', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '03bcb0de-acf6-a30c-777c-7757c8661c59', 'f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', 46.01, 1),
        ('8de1d632-a275-ebb2-2f02-c59ac5dd6430', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '30bf6d4c-8c93-57ef-b270-20d6831d4581', '9b18a470-5646-f257-a801-b7994aa2883e', 49.09, 1),
        ('8eed601c-c38c-a0c5-607d-4be12d668e42', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', 109.11, 1),
        ('8f33472c-e327-d75f-6b6e-72bfeaecfb4c', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'cfbe7586-f639-add0-c4e6-d41370bc36ae', '52134872-ef75-71bd-3102-7375ce47b077', 41.44, 1),
        ('910faa9f-74fb-4eb1-1fe4-bdb40b93a0ba', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0f5a5921-4a8d-2752-b90b-2b2e7606876d', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 105.17, 1),
        ('9e165150-99e4-71f1-1d8b-a670e10b9b7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 43.42, 1),
        ('a308b5a1-6eba-04cb-4c57-877aa53a3c38', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fd571be6-8ad2-fdd3-ca89-4d382c66f3b4', '5da5c091-db1f-6755-2fac-952f7468ef2f', 47.42, 1),
        ('a31f3dde-a947-6b66-0fb7-e5502a3c0d52', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '30bf6d4c-8c93-57ef-b270-20d6831d4581', '32c77794-0879-14fc-faf4-6d8ac3779318', 124.04, 1),
        ('a37cd51e-3380-281a-2280-ce2774070dfe', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '563cfa23-b533-2dfe-7734-9a2151a36731', 'f8b85da0-c7e5-24ad-b678-0d3533c78429', 111.63, 1),
        ('ac070894-329c-5585-39ef-b39cab17d47f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'f8e6533f-bb6b-caf4-30cd-b6c2dd3d33cb', '774da183-9820-66a1-7f3f-80f5eb7fd48d', 116.62, 1),
        ('ad230d2d-44df-fd82-5920-7c8cda11fce4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd81b94a7-92b1-0459-383c-c82e86fe57ee', '5400b54d-c114-8f13-a103-dd92064c7c26', 55.17, 1),
        ('b1bc62e5-08ea-9b5b-52be-ca97166e2e77', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', '52423a8c-14dd-1699-d94d-f1ce59ec843d', 114.56, 1),
        ('bc348867-3ed8-bd1b-6e53-deff0dda7ee4', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9b18a470-5646-f257-a801-b7994aa2883e', 'ecab282f-7cd5-bb87-153c-d4bc1be51de3', 50, 1),
        ('c0cc120d-bde2-4fbf-626a-e1a288d576b0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'feb7ab95-935f-b582-fd85-d329d2487b15', '7dae71c0-5593-bbdc-e86e-c3d198fb7341', 53.85, 1),
        ('c2ab682c-e9db-20e7-41ca-f62f2e67d5db', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '9ea672e2-02e9-96a5-6e17-b9c2479b83dd', '090ff8af-f3eb-2c34-411e-daabcac34283', 122.38, 1),
        ('c4585518-4a16-62e1-1bc6-fb0d2a2ae06f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a511a05a-564c-f253-b3d9-d349f3d2215e', '32c77794-0879-14fc-faf4-6d8ac3779318', 343.64, 2),
        ('c7f77445-e4c4-3efe-a173-b46a3b378cd5', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'd3b47ed9-315f-ca64-1ffe-c4835527e96b', 'a0c41129-e9c0-d1e0-8409-da4c22608ad7', 42.52, 1),
        ('cad0265f-33ce-9c1e-f4fb-120e09cb4145', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '043ae2e1-ceec-5e72-27ed-25b2b2893833', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 52.80, 1),
        ('cce8f3df-d0d1-f9ee-6506-9de421222d12', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0f5a5921-4a8d-2752-b90b-2b2e7606876d', 'e6118c6d-9fb2-1e7c-e77d-269753b36941', 48.09, 1),
        ('cd8dc859-f31e-8ee3-e479-db91ed4e98c0', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ec62360a-6a52-3c7e-62f0-37b657551e11', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', 44.10, 1),
        ('cfbd7763-f392-891c-2f4a-7152efce515d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '0a4ce56e-94fc-f9d7-64f8-9e19977b07bb', 'f34b509b-cd7c-c009-1235-4d3e9a9f2ede', 50.09, 1),
        ('d2a99b73-749f-59f1-b988-d953f4c85c37', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'a00244e8-42f4-56fc-ebff-1a1e60153f97', 'f6eda3ae-c645-0d35-c4e4-e2c4f213e0e4', 47.85, 1),
        ('d3c8d5ad-a78e-14d7-a46b-a1b9cbd13a94', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'e90f31ce-2cad-df2f-2e4b-a3eb78828260', 'd60c9c3b-48b5-e4d3-9007-49e30edcf5ee', 49.50, 1),
        ('d831b323-b49f-e353-a6f2-f6b0c1420718', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '563cfa23-b533-2dfe-7734-9a2151a36731', 'd81b94a7-92b1-0459-383c-c82e86fe57ee', 50, 1),
        ('defc43ee-c9b5-0f1b-c215-5c72b9f31985', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '7ae776ed-3b8f-4ee2-4305-c9527f1648fb', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 113.28, 1),
        ('df26e94e-9fdb-4fc8-b0ed-85363c547f7e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', '8ab6466f-0cfa-caef-092e-b7eedfbca20d', 50.22, 1),
        ('e29eed18-a49a-8397-1e80-5fa2cfa5405d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 49.04, 1),
        ('e4826318-3ead-c55a-8ab6-94f1b11104cc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', 'feb7ab95-935f-b582-fd85-d329d2487b15', 122.71, 1),
        ('e571a40c-837f-9c0d-148d-400074a89a31', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 'd60c9c3b-48b5-e4d3-9007-49e30edcf5ee', 104.24, 1),
        ('e5a29768-c160-239c-8c92-306040553b68', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4d46ab-53b7-689d-25d0-61bf83da6ed9', '090ff8af-f3eb-2c34-411e-daabcac34283', 48.10, 1),
        ('e89313c5-a78c-0c58-8197-ec4383b16fcb', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '780fe021-3614-fceb-71be-fbf1a3cfcf28', 'c2a81993-5703-8030-b082-dfe89ce4c7e3', 48.26, 1),
        ('eab96394-9501-0048-5396-1e0f9f41b701', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b4109ca-8f45-263a-e773-60297272f0aa', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', 51.08, 1),
        ('eb8c71f1-b0b5-d80b-ada9-807f38366076', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'b611c0c2-5995-70e1-3ffe-417c1d305c14', 'c5120588-0b2e-22fe-c5d7-4697e24aff54', 44.29, 1),
        ('ed5ca002-337d-c06a-77ea-d4369a51753f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '83d2d582-4f0c-650b-6a77-0ee5b6a9fa93', '1ed45dec-c261-6f52-6fda-1c2499040654', 45.69, 1),
        ('f52e0b3f-e9ce-d7c4-5287-49dc0f622e14', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1ed45dec-c261-6f52-6fda-1c2499040654', '45cdce04-7d01-eead-4a2f-ce299ec25aa3', 51.04, 1),
        ('f7c71da6-b2e2-2beb-253d-c1cfaba2722d', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', 46.10, 1),
        ('f8e0c393-0949-6d55-ae0f-8b114a735493', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '32c77794-0879-14fc-faf4-6d8ac3779318', '0f5a5921-4a8d-2752-b90b-2b2e7606876d', 51.66, 1),
        ('fbce1e03-0398-6847-0e37-3a6efe9d4e46', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '57b90eaf-8c2e-c782-730c-b91bb50b9ec4', '60a870d0-a90b-740c-bc02-8f11358b5a57', 47.71, 1),
        ('fecb2dce-ceea-4bfd-403a-9ae1a773facc', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', 'ca0cb3c8-68b4-5a61-6d1b-61a09820c3f6', 276.68, 2);

    INSERT INTO dbo.Admirals(AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt)
    VALUES
        ('aff0c7fa-0165-78db-bd0a-a4f770ed422f', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'Elian Voss', 0, N'Active', @SeededAt, @SeededAt),
        ('fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', N'Ilya Sen', 0, N'Active', @SeededAt, @SeededAt),
        ('70f25832-dd43-8d14-72e4-9d1a0fe83d83', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c0660deb-fa0d-be04-7967-8a1e8691079e', N'Mara Sutekh', 0, N'Active', @SeededAt, @SeededAt),
        ('219c6de2-f38e-d076-47c3-d09adf3dee9a', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5b61c575-b567-8f71-e940-4e08f628cad7', N'Tavian Orre', 0, N'Active', @SeededAt, @SeededAt);

    INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt)
    VALUES
        ('04eff6c0-998a-66ef-c882-34b221368b4b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', NULL, N'Aurelian Home Guard', 'afa5fe7c-06d1-228c-e1fc-22f23e6daca0', NULL, NULL, 30, N'Active', @SeededAt),
        ('49b9d4b1-892c-009a-184a-5122d864e12b', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c0660deb-fa0d-be04-7967-8a1e8691079e', '70f25832-dd43-8d14-72e4-9d1a0fe83d83', N'Khepri Gate Raiders', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', NULL, NULL, 20, N'Active', @SeededAt),
        ('59412fdc-6d2f-f5e8-a6e5-88ab480e6372', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'c0660deb-fa0d-be04-7967-8a1e8691079e', NULL, N'Khepri Home Fleet', '090ff8af-f3eb-2c34-411e-daabcac34283', NULL, NULL, 40, N'Active', @SeededAt),
        ('22b80436-db08-9382-3c0c-2e91449be933', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '5b61c575-b567-8f71-e940-4e08f628cad7', '219c6de2-f38e-d076-47c3-d09adf3dee9a', N'Novan League Home Fleet', '9b18a470-5646-f257-a801-b7994aa2883e', NULL, NULL, 60, N'Active', @SeededAt),
        ('e17e8e91-d4fe-aabf-c35d-deaa6c26172e', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', NULL, N'Pale Harbour Survey', '9a6ef3a4-5051-a0ab-8b3e-8c633e44e522', NULL, NULL, 12, N'Active', @SeededAt),
        ('82ba60e7-3827-3d9a-57e4-7511bc9c5fa2', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', '1b12558d-ff5e-d372-e4c1-94f63137b642', 'aff0c7fa-0165-78db-bd0a-a4f770ed422f', N'Treaty Gate Vanguard', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', NULL, NULL, 18, N'Active', @SeededAt),
        ('a8c01cc4-efad-d769-6100-4c8f28606a98', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 'dc99aa36-86c5-ebe6-2ba0-1b557b73ec5b', 'fe2cd889-ba40-cd7b-6cbc-3e8cef6c8037', N'Vestige Combine Home Fleet', '181e55e1-6f33-0ec2-9bb5-b826f6ee5ea7', NULL, NULL, 60, N'Active', @SeededAt);

    INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
    VALUES
        ('029b750a-db56-cf33-628e-4abd4ec3b834', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'CycleSeeded', NULL, NULL, N'Normal', N'{
  "cycleId": "fce1d96a-6a07-4559-cff6-dd6efde758ae",
  "empireCount": 4,
  "systemCount": 64,
  "sectorCount": 8,
  "topologyKey": "territorial-graph-v2",
  "seed": 71421
}', N'The ' + @CycleName + N' began with 4 empires and 64 systems.', @SeededAt),
        ('90507006-2403-9e35-870a-5737678e5137', 'fce1d96a-6a07-4559-cff6-dd6efde758ae', 0, N'OpeningBriefingIssued', 'fe739d22-e193-f482-cbcf-3f5977f3ab8b', '1b12558d-ff5e-d372-e4c1-94f63137b642', N'High', N'{
  "scenarioKey": "development-cold-start-v1",
  "focusSystemId": "fe739d22-e193-f482-cbcf-3f5977f3ab8b",
  "objectives": {
    "move": {
      "fleetId": "04eff6c0-998a-66ef-c882-34b221368b4b",
      "targetSystemId": "ec62360a-6a52-3c7e-62f0-37b657551e11"
    },
    "colonise": {
      "fleetId": "e17e8e91-d4fe-aabf-c35d-deaa6c26172e",
      "systemId": "9a6ef3a4-5051-a0ab-8b3e-8c633e44e522"
    },
    "attack": {
      "fleetId": "82ba60e7-3827-3d9a-57e4-7511bc9c5fa2",
      "systemId": "fe739d22-e193-f482-cbcf-3f5977f3ab8b",
      "targetEmpireId": "c0660deb-fa0d-be04-7967-8a1e8691079e"
    }
  }
}', N'Day 1 briefing: Khepri raiders contest Treaty Gate. Pale Harbour is ready for an outpost, while Nadir Crossing offers immediate expansion.', @SeededAt);

END;
COMMIT TRANSACTION;
