SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS
    (
        SELECT 1
        FROM dbo.Players
        WHERE
            (
                DATALENGTH(ExternalIssuer) > 0
                AND
                (
                    UNICODE(LEFT(ExternalIssuer, 1)) = 32
                    OR UNICODE(RIGHT(ExternalIssuer, 1)) = 32
                )
            )
            OR
            (
                DATALENGTH(ExternalSubject) > 0
                AND
                (
                    UNICODE(LEFT(ExternalSubject, 1)) = 32
                    OR UNICODE(RIGHT(ExternalSubject, 1)) = 32
                )
            )
    )
    BEGIN
        THROW 51054, 'Cannot enforce exact external identities because at least one issuer or subject has leading or trailing U+0020. Clean those rows and retry.', 1;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Players')
          AND name = N'UX_Players_ExternalIdentity'
    )
    BEGIN
        DROP INDEX UX_Players_ExternalIdentity ON dbo.Players;
    END;

    IF EXISTS
    (
        SELECT 1
        FROM sys.check_constraints
        WHERE parent_object_id = OBJECT_ID(N'dbo.Players')
          AND name = N'CK_Players_ExternalIdentity_NoEdgeSpaces'
    )
    BEGIN
        ALTER TABLE dbo.Players
            DROP CONSTRAINT CK_Players_ExternalIdentity_NoEdgeSpaces;
    END;

    ALTER TABLE dbo.Players
        ALTER COLUMN ExternalIssuer NVARCHAR(256)
            COLLATE Latin1_General_100_BIN2 NOT NULL;

    ALTER TABLE dbo.Players
        ALTER COLUMN ExternalSubject NVARCHAR(256)
            COLLATE Latin1_General_100_BIN2 NOT NULL;

    ALTER TABLE dbo.Players WITH CHECK
        ADD CONSTRAINT CK_Players_ExternalIdentity_NoEdgeSpaces CHECK
        (
            (
                DATALENGTH(ExternalIssuer) = 0
                OR
                (
                    UNICODE(LEFT(ExternalIssuer, 1)) <> 32
                    AND UNICODE(RIGHT(ExternalIssuer, 1)) <> 32
                )
            )
            AND
            (
                DATALENGTH(ExternalSubject) = 0
                OR
                (
                    UNICODE(LEFT(ExternalSubject, 1)) <> 32
                    AND UNICODE(RIGHT(ExternalSubject, 1)) <> 32
                )
            )
        );

    CREATE UNIQUE INDEX UX_Players_ExternalIdentity
        ON dbo.Players(ExternalIssuer, ExternalSubject)
        WHERE ExternalIssuer <> N'' AND ExternalSubject <> N'';

    IF NOT EXISTS
    (
        SELECT 1
        FROM dbo.SchemaMigrations
        WHERE MigrationID = N'024_enforce_external_identity_binary_collation'
    )
    BEGIN
        INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
        VALUES
        (
            N'024_enforce_external_identity_binary_collation',
            N'Use exact case-sensitive external identity keys without edge spaces',
            SYSDATETIMEOFFSET()
        );
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;
    THROW;
END CATCH;
