IF COL_LENGTH(N'dbo.Fleets', N'DepartureTickNumber') IS NULL
BEGIN
    ALTER TABLE dbo.Fleets ADD DepartureTickNumber INT NULL;
END;
GO

UPDATE fleet
SET DepartureTickNumber = fleet.ArrivalTickNumber - link.TravelTicks + 1
FROM dbo.Fleets AS fleet
INNER JOIN dbo.SystemLinks AS link
    ON link.CycleID = fleet.CycleID
   AND
   (
       (link.SystemAID = fleet.CurrentSystemID AND link.SystemBID = fleet.DestinationSystemID)
       OR (link.SystemBID = fleet.CurrentSystemID AND link.SystemAID = fleet.DestinationSystemID)
   )
WHERE fleet.Status = N'InTransit'
  AND fleet.DepartureTickNumber IS NULL
  AND fleet.ArrivalTickNumber IS NOT NULL;
GO
