using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class ExternalIdentityCollationMigrationTests
{
    [Fact]
    public void Embedded_migration_preflights_edge_spaces_and_rebuilds_the_exact_identity_contract()
    {
        var migration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == "024_enforce_external_identity_binary_collation");

        var preflightOffset = migration.Script.IndexOf("THROW 51054", StringComparison.Ordinal);
        var firstSchemaMutationOffset = migration.Script.IndexOf(
            "DROP INDEX UX_Players_ExternalIdentity",
            StringComparison.Ordinal);
        Assert.InRange(preflightOffset, 0, firstSchemaMutationOffset - 1);
        Assert.Equal(4, CountOccurrences(migration.Script, "UNICODE(LEFT("));
        Assert.Equal(4, CountOccurrences(migration.Script, "UNICODE(RIGHT("));
        Assert.Contains(
            "DROP CONSTRAINT CK_Players_ExternalIdentity_NoEdgeSpaces",
            migration.Script,
            StringComparison.Ordinal);
        Assert.Contains("DROP INDEX UX_Players_ExternalIdentity", migration.Script, StringComparison.Ordinal);
        Assert.Equal(
            2,
            CountOccurrences(migration.Script, "COLLATE Latin1_General_100_BIN2 NOT NULL"));
        Assert.Contains(
            "ADD CONSTRAINT CK_Players_ExternalIdentity_NoEdgeSpaces CHECK",
            migration.Script,
            StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(migration.Script, "DATALENGTH(ExternalIssuer)"));
        Assert.Equal(2, CountOccurrences(migration.Script, "DATALENGTH(ExternalSubject)"));
        Assert.Contains("CREATE UNIQUE INDEX UX_Players_ExternalIdentity", migration.Script, StringComparison.Ordinal);
        Assert.Contains(
            "WHERE ExternalIssuer <> N'' AND ExternalSubject <> N''",
            migration.Script,
            StringComparison.Ordinal);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var offset = 0;
        while ((offset = text.IndexOf(value, offset, StringComparison.Ordinal)) >= 0)
        {
            count++;
            offset += value.Length;
        }

        return count;
    }
}
