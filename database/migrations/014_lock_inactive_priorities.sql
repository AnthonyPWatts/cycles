SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF EXISTS
(
    SELECT 1
    FROM dbo.EmpirePriorities
    WHERE IndustryWeight < 0
       OR ResearchWeight < 0
       OR MilitaryWeight < 0
       OR ExpansionWeight < 0
       OR CAST(IndustryWeight AS BIGINT) + ResearchWeight + MilitaryWeight + ExpansionWeight <> 100
)
BEGIN
    THROW 50001, 'Empire priorities must be non-negative and total 100 before inactive programmes can be locked.', 1;
END;

;WITH ActiveWeights AS
(
    SELECT EmpirePriorityID,
           CASE WHEN MilitaryWeight > 0 THEN MilitaryWeight ELSE 0 END AS MilitaryWeight,
           CASE WHEN ExpansionWeight > 0 THEN ExpansionWeight ELSE 0 END AS ExpansionWeight
    FROM dbo.EmpirePriorities
),
NormalisedWeights AS
(
    SELECT EmpirePriorityID,
           CASE
               WHEN CAST(MilitaryWeight AS BIGINT) + ExpansionWeight = 0 THEN 50
               ELSE CAST(ROUND(CAST(MilitaryWeight AS DECIMAL(18, 6)) * 100 / (CAST(MilitaryWeight AS BIGINT) + ExpansionWeight), 0) AS INT)
           END AS MilitaryWeight
    FROM ActiveWeights
)
UPDATE priorities
SET IndustryWeight = 0,
    ResearchWeight = 0,
    MilitaryWeight = normalised.MilitaryWeight,
    ExpansionWeight = 100 - normalised.MilitaryWeight
FROM dbo.EmpirePriorities priorities
INNER JOIN NormalisedWeights normalised ON normalised.EmpirePriorityID = priorities.EmpirePriorityID;

IF NOT EXISTS
(
    SELECT 1
    FROM sys.check_constraints
    WHERE parent_object_id = OBJECT_ID(N'dbo.EmpirePriorities')
      AND name = N'CK_EmpirePriorities_ActiveProgrammes'
)
BEGIN
    ALTER TABLE dbo.EmpirePriorities WITH CHECK
    ADD CONSTRAINT CK_EmpirePriorities_ActiveProgrammes CHECK
    (
        IndustryWeight = 0
        AND ResearchWeight = 0
        AND MilitaryWeight BETWEEN 0 AND 100
        AND ExpansionWeight BETWEEN 0 AND 100
        AND CAST(MilitaryWeight AS BIGINT) + ExpansionWeight = 100
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'014_lock_inactive_priorities')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'014_lock_inactive_priorities', N'Lock inactive strategic priorities at zero', SYSDATETIMEOFFSET());
END;
