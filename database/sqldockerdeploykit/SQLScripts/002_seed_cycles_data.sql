SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

DECLARE @CycleID UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @PlayerOneID UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222221';
DECLARE @PlayerTwoID UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @SystemOneID UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333331';
DECLARE @SystemTwoID UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333332';
DECLARE @EmpireOneID UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444441';
DECLARE @EmpireTwoID UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444442';
DECLARE @AdmiralOneID UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555551';
DECLARE @AdmiralTwoID UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555552';
DECLARE @Now DATETIMEOFFSET = SYSDATETIMEOFFSET();

IF NOT EXISTS (SELECT 1 FROM dbo.Cycles WHERE Status = N'Active')
BEGIN
    INSERT INTO dbo.Players(PlayerID, Username, Email, PasswordHash, Role, CreatedAt, LastLoginAt, Status)
    VALUES
        (@PlayerOneID, N'player-1', N'player-1@cycles.local', N'prototype', N'Player', @Now, @Now, N'Active'),
        (@PlayerTwoID, N'player-2', N'player-2@cycles.local', N'prototype', N'Player', @Now, @Now, N'Active');

    INSERT INTO dbo.Cycles(CycleID, Name, StartAt, EndAt, TickLengthMinutes, CurrentTickNumber, Status, CreatedAt)
    VALUES (@CycleID, N'Cycle Docker Seed', @Now, DATEADD(day, 90, @Now), 60, 0, N'Active', @Now);

    INSERT INTO dbo.Systems(SystemID, CycleID, SystemName, X, Y, IndustryOutput, ResearchOutput, PopulationOutput, StrategicValue, HistoricalSignificance, CreatedAt)
    VALUES
        (@SystemOneID, @CycleID, N'Pseudopolis', 120, 160, 80, 40, 30, 30, 2, @Now),
        (@SystemTwoID, @CycleID, N'Treaty Gate', 360, 190, 50, 60, 20, 26, 2, @Now);

    INSERT INTO dbo.Empires(EmpireID, CycleID, PlayerID, EmpireName, HomeSystemID, CreatedAt, Status)
    VALUES
        (@EmpireOneID, @CycleID, @PlayerOneID, N'Aurelian Compact', @SystemOneID, @Now, N'Active'),
        (@EmpireTwoID, @CycleID, @PlayerTwoID, N'Khepri Mandate', @SystemTwoID, @Now, N'Active');

    INSERT INTO dbo.EmpireResources(EmpireResourceID, EmpireID, Industry, Research, Population, UpdatedAt)
    VALUES
        (NEWID(), @EmpireOneID, 100, 100, 100, @Now),
        (NEWID(), @EmpireTwoID, 100, 100, 100, @Now);

    INSERT INTO dbo.EmpirePriorities(EmpirePriorityID, EmpireID, IndustryWeight, ResearchWeight, MilitaryWeight, ExpansionWeight, UpdatedAt)
    VALUES
        (NEWID(), @EmpireOneID, 0, 0, 67, 33, @Now),
        (NEWID(), @EmpireTwoID, 0, 0, 67, 33, @Now);

    INSERT INTO dbo.SystemLinks(SystemLinkID, CycleID, SystemAID, SystemBID, Distance, TravelTicks)
    VALUES (NEWID(), @CycleID, @SystemOneID, @SystemTwoID, 241.87, 1);

    INSERT INTO dbo.Admirals(AdmiralID, CycleID, EmpireID, AdmiralName, ReputationScore, Status, CreatedAt, UpdatedAt)
    VALUES
        (@AdmiralOneID, @CycleID, @EmpireOneID, N'Elian Voss', 0, N'Active', @Now, @Now),
        (@AdmiralTwoID, @CycleID, @EmpireTwoID, N'Mara Sutekh', 0, N'Active', @Now, @Now);

    INSERT INTO dbo.Fleets(FleetID, CycleID, EmpireID, AdmiralID, FleetName, CurrentSystemID, DestinationSystemID, ArrivalTickNumber, ShipCount, Status, CreatedAt)
    VALUES
        (NEWID(), @CycleID, @EmpireOneID, @AdmiralOneID, N'Aurelian Compact Home Fleet', @SystemOneID, NULL, NULL, 60, N'Active', @Now),
        (NEWID(), @CycleID, @EmpireTwoID, @AdmiralTwoID, N'Khepri Mandate Home Fleet', @SystemTwoID, NULL, NULL, 60, N'Active', @Now);

    INSERT INTO dbo.Events(EventID, CycleID, TickNumber, EventType, SystemID, EmpireID, Severity, FactJson, DisplayText, CreatedAt)
    VALUES
    (
        NEWID(),
        @CycleID,
        0,
        N'CycleSeeded',
        NULL,
        NULL,
        N'Normal',
        N'{"source":"sqldockerdeploykit"}',
        N'The Docker-seeded Cycle began with 2 empires and 2 systems.',
        @Now
    );
END;
