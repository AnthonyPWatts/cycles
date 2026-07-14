SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;

IF COL_LENGTH(N'dbo.Players', N'ExternalIssuer') IS NULL
BEGIN
    ALTER TABLE dbo.Players
    ADD ExternalIssuer NVARCHAR(256) NOT NULL
        CONSTRAINT DF_Players_ExternalIssuer DEFAULT N'';
END;

IF COL_LENGTH(N'dbo.Players', N'ExternalSubject') IS NULL
BEGIN
    ALTER TABLE dbo.Players
    ADD ExternalSubject NVARCHAR(256) NOT NULL
        CONSTRAINT DF_Players_ExternalSubject DEFAULT N'';
END;

GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Players') AND name = N'UX_Players_ExternalIdentity')
BEGIN
    CREATE UNIQUE INDEX UX_Players_ExternalIdentity
        ON dbo.Players(ExternalIssuer, ExternalSubject)
        WHERE ExternalIssuer <> N'' AND ExternalSubject <> N'';
END;

IF OBJECT_ID(N'dbo.AdminRoleAuditRecords', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.AdminRoleAuditRecords
    (
        AdminRoleAuditRecordID UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_AdminRoleAuditRecords PRIMARY KEY,
        ActorPlayerID UNIQUEIDENTIFIER NULL CONSTRAINT FK_AdminRoleAuditRecords_ActorPlayer REFERENCES dbo.Players(PlayerID),
        TargetPlayerID UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_AdminRoleAuditRecords_TargetPlayer REFERENCES dbo.Players(PlayerID),
        Action NVARCHAR(32) NOT NULL,
        Reason NVARCHAR(1024) NOT NULL,
        Source NVARCHAR(256) NOT NULL,
        Severity NVARCHAR(32) NOT NULL,
        CreatedAt DATETIMEOFFSET NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM dbo.SchemaMigrations WHERE MigrationID = N'013_add_external_identity_and_admin_audit')
BEGIN
    INSERT INTO dbo.SchemaMigrations(MigrationID, Description, AppliedAt)
    VALUES (N'013_add_external_identity_and_admin_audit', N'Add external player identity and immutable admin role audit records', SYSDATETIMEOFFSET());
END;
