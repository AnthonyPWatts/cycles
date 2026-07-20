using Cycles.Infrastructure.SqlServer;
using Microsoft.Data.SqlClient;

namespace Cycles.Tests;

[Collection(SqlServerIntegrationCollection.CollectionName)]
[Trait(SqlIntegrationGuard.CategoryName, SqlIntegrationGuard.CategoryValue)]
public sealed class SqlServerExternalIdentityCollationMigrationIntegrationTests
{
    private const string MigrationId = "024_enforce_external_identity_binary_collation";

    [Fact]
    public void Migration_uses_binary_identity_columns_and_allows_case_distinct_exact_keys()
    {
        var serverConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(
            serverConnectionString,
            "023_enforce_cycle_scope_integrity");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        var preservedPlayerId = InsertPlayer(
            connection,
            "https://Existing.Identity.example",
            "ExistingSubject");
        var migrator = new SqlServerMigrator(database.ConnectionString);

        var migration = Assert.Single(migrator.MigrateThrough(MigrationId));

        Assert.Equal(MigrationId, migration.MigrationId);
        Assert.Equal(
            "Latin1_General_100_BIN2",
            ReadColumnCollation(connection, "ExternalIssuer"));
        Assert.Equal(
            "Latin1_General_100_BIN2",
            ReadColumnCollation(connection, "ExternalSubject"));
        Assert.Equal(
            1,
            Scalar<int>(
                connection,
                """
                SELECT COUNT(*)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.Players')
                  AND name = N'UX_Players_ExternalIdentity'
                  AND is_unique = 1
                  AND has_filter = 1;
                """));
        Assert.Equal(1, CountTrustedEdgeSpaceConstraints(connection));
        Assert.Equal(
            "https://Existing.Identity.example|ExistingSubject",
            Scalar<string>(
                connection,
                """
                SELECT ExternalIssuer + N'|' + ExternalSubject
                FROM dbo.Players
                WHERE PlayerID = @PlayerID;
                """,
                ("@PlayerID", preservedPlayerId)));

        InsertPlayer(connection, "https://Identity.example", "Subject");
        InsertPlayer(connection, "https://identity.example", "Subject");
        InsertPlayer(connection, "https://Identity.example", "subject");

        Assert.Equal(
            3,
            Scalar<int>(
                connection,
                """
                SELECT COUNT(*)
                FROM dbo.Players
                WHERE ExternalIssuer IN (N'https://Identity.example', N'https://identity.example')
                  AND ExternalSubject IN (N'Subject', N'subject');
                """));
        var duplicate = Assert.Throws<SqlException>(() =>
            InsertPlayer(connection, "https://Identity.example", "Subject"));
        Assert.Contains(duplicate.Number, new[] { 2601, 2627 });

        foreach (var (issuer, subject) in new[]
                 {
                     (" https://edge.example", "subject"),
                     ("https://edge.example ", "subject"),
                     ("https://edge.example", " subject"),
                     ("https://edge.example", "subject ")
                 })
        {
            var edgeSpace = Assert.Throws<SqlException>(() =>
                InsertPlayer(connection, issuer, subject));
            Assert.Equal(547, edgeSpace.Number);
        }

        InsertPlayer(connection, "", "");
        InsertPlayer(connection, "", "");
        Assert.Equal(
            1,
            Scalar<int>(
                connection,
                "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
                ("@MigrationID", MigrationId)));

        Assert.Empty(migrator.MigrateThrough(MigrationId));
        var embeddedMigration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == MigrationId);
        Execute(connection, embeddedMigration.Script);

        Assert.Equal(1, CountTrustedEdgeSpaceConstraints(connection));
        Assert.Equal(
            1,
            Scalar<int>(
                connection,
                "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
                ("@MigrationID", MigrationId)));
        Assert.Equal(
            "https://Existing.Identity.example|ExistingSubject",
            Scalar<string>(
                connection,
                """
                SELECT ExternalIssuer + N'|' + ExternalSubject
                FROM dbo.Players
                WHERE PlayerID = @PlayerID;
                """,
                ("@PlayerID", preservedPlayerId)));
    }

    [Fact]
    public void Migration_rejects_dirty_edge_spaces_without_schema_change_and_retries_after_cleanup()
    {
        var serverConnectionString = SqlIntegrationGuard.GetConnectionString();
        if (string.IsNullOrWhiteSpace(serverConnectionString))
        {
            return;
        }

        using var database = new SqlServerIntegrationDatabase(
            serverConnectionString,
            "023_enforce_cycle_scope_integrity");
        using var connection = new SqlConnection(database.ConnectionString);
        connection.Open();
        var originalIssuerCollation = ReadColumnCollation(connection, "ExternalIssuer");
        var originalSubjectCollation = ReadColumnCollation(connection, "ExternalSubject");
        var dirtyPlayerId = InsertPlayer(
            connection,
            " https://dirty.example",
            "dirty-subject ");
        var migrator = new SqlServerMigrator(database.ConnectionString);

        var rejected = Assert.Throws<SqlException>(() => migrator.MigrateThrough(MigrationId));

        Assert.Equal(51054, rejected.Number);
        Assert.Equal(originalIssuerCollation, ReadColumnCollation(connection, "ExternalIssuer"));
        Assert.Equal(originalSubjectCollation, ReadColumnCollation(connection, "ExternalSubject"));
        Assert.Equal(
            1,
            Scalar<int>(
                connection,
                """
                SELECT COUNT(*)
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.Players')
                  AND name = N'UX_Players_ExternalIdentity';
                """));
        Assert.Equal(0, CountTrustedEdgeSpaceConstraints(connection));
        Assert.Equal(
            0,
            Scalar<int>(
                connection,
                "SELECT COUNT(*) FROM dbo.SchemaMigrations WHERE MigrationID = @MigrationID;",
                ("@MigrationID", MigrationId)));

        Execute(
            connection,
            """
            UPDATE dbo.Players
            SET ExternalIssuer = @Issuer,
                ExternalSubject = @Subject
            WHERE PlayerID = @PlayerID;
            """,
            ("@Issuer", "https://dirty.example"),
            ("@Subject", "dirty-subject"),
            ("@PlayerID", dirtyPlayerId));

        var applied = Assert.Single(migrator.MigrateThrough(MigrationId));

        Assert.Equal(MigrationId, applied.MigrationId);
        Assert.Equal("Latin1_General_100_BIN2", ReadColumnCollation(connection, "ExternalIssuer"));
        Assert.Equal("Latin1_General_100_BIN2", ReadColumnCollation(connection, "ExternalSubject"));
        Assert.Equal(1, CountTrustedEdgeSpaceConstraints(connection));
        Assert.Equal(
            "https://dirty.example|dirty-subject",
            Scalar<string>(
                connection,
                """
                SELECT ExternalIssuer + N'|' + ExternalSubject
                FROM dbo.Players
                WHERE PlayerID = @PlayerID;
                """,
                ("@PlayerID", dirtyPlayerId)));
    }

    private static Guid InsertPlayer(
        SqlConnection connection,
        string issuer,
        string subject)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO dbo.Players
                (PlayerID, Username, Email, PasswordHash, ExternalIssuer, ExternalSubject,
                 PlayerKind, Role, CreatedAt, LastLoginAt, Status)
            VALUES
                (@PlayerID, @Username, N'', N'', @Issuer, @Subject,
                 N'Human', N'Player', '2026-07-20T00:00:00+00:00', NULL, N'Active');
            """;
        var playerId = Guid.NewGuid();
        command.Parameters.AddWithValue("@PlayerID", playerId);
        command.Parameters.AddWithValue("@Username", $"migration-{playerId:N}");
        command.Parameters.AddWithValue("@Issuer", issuer);
        command.Parameters.AddWithValue("@Subject", subject);
        command.ExecuteNonQuery();
        return playerId;
    }

    private static string ReadColumnCollation(SqlConnection connection, string columnName) =>
        Scalar<string>(
            connection,
            """
            SELECT collation_name
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'dbo.Players')
              AND name = @ColumnName;
            """,
            ("@ColumnName", columnName));

    private static int CountTrustedEdgeSpaceConstraints(SqlConnection connection) =>
        Scalar<int>(
            connection,
            """
            SELECT COUNT(*)
            FROM sys.check_constraints
            WHERE parent_object_id = OBJECT_ID(N'dbo.Players')
              AND name = N'CK_Players_ExternalIdentity_NoEdgeSpaces'
              AND is_disabled = 0
              AND is_not_trusted = 0;
            """);

    private static void Execute(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        command.ExecuteNonQuery();
    }

    private static T Scalar<T>(
        SqlConnection connection,
        string sql,
        params (string Name, object Value)[] parameters)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return (T)Convert.ChangeType(command.ExecuteScalar()!, typeof(T));
    }
}
