namespace Cycles.Infrastructure.SqlServer;

public sealed record SqlServerMigration(
    string MigrationId,
    string Description,
    string Script);

public sealed record SqlServerMigrationStatus(
    bool DatabaseExists,
    IReadOnlyList<string> AppliedMigrationIds,
    IReadOnlyList<SqlServerMigration> AvailableMigrations,
    IReadOnlyList<SqlServerMigration> PendingMigrations)
{
    public bool IsCurrent => DatabaseExists && PendingMigrations.Count == 0;
}

public sealed record SqlServerMigrationResult(
    string MigrationId,
    string Description);
