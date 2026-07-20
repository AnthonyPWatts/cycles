using Cycles.Infrastructure.SqlServer;

namespace Cycles.Tests;

public sealed class TutorialRunsMigrationTests
{
    [Fact]
    public void Migrations_add_scoped_runs_acknowledgements_completion_skip_and_one_current_slot()
    {
        var migration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == "026_add_tutorial_runs");

        Assert.Contains("CREATE TABLE dbo.TutorialRuns", migration.Script);
        Assert.Contains("FOREIGN KEY (GameID, PlayerID)", migration.Script);
        Assert.Contains("FOREIGN KEY (CycleID, GameID)", migration.Script);
        Assert.Contains("CREATE TABLE dbo.TutorialAcknowledgements", migration.Script);
        Assert.Contains("CREATE TABLE dbo.TutorialCompletions", migration.Script);
        Assert.Contains("UX_TutorialRuns_Current", migration.Script);
        Assert.Contains("ELSE CONVERT(NVARCHAR(36), TutorialRunID)", migration.Script);
        Assert.Contains("ROW_NUMBER() OVER", migration.Script);

        var skipMigration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == "027_add_tutorial_skips");
        Assert.Contains("CREATE TABLE dbo.TutorialSkips", skipMigration.Script);
        Assert.Contains("FOREIGN KEY (FirstSkippedRunID)", skipMigration.Script);

        var scopeMigration = Assert.Single(
            SqlServerMigrator.LoadEmbeddedMigrations(),
            item => item.MigrationId == "023_enforce_cycle_scope_integrity");
        Assert.Contains("DROP CONSTRAINT FK_TutorialRuns_Cycles", scopeMigration.Script);
        Assert.Contains("ADD CONSTRAINT FK_TutorialRuns_Cycles", scopeMigration.Script);
    }
}
