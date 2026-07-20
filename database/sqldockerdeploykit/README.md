# Cycles SQLDockerDeployKit Database

This folder contains a Cycles-specific SQL Server container definition based on the SQLDockerDeployKit pattern: a SQL Server image that creates `CyclesDb`, applies the ordered SQL migrations from `database/migrations`, then executes ordered seed scripts from `SQLScripts`.

Normal local API, Worker, and gameplay/operator CLI runs use SQL Server through `Cycles.Infrastructure.SqlServer`. No executable JSON datastore or file-store fallback remains. Versioned JSON is retained only for explicit operator import/export, validation, offline inspection, fixtures, legacy conversion, and migration evidence.

## Build

From the repository root:

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
```

The image uses SQL Server 2022 and the SQLDockerDeployKit-style readiness path with `sqlcmd`, accepting `MSSQL_SA_PASSWORD` as the primary password variable and `SA_PASSWORD` as a compatibility alias.

## Run

Use a non-default host port so it does not collide with an existing local SQL Server:

```powershell
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

The image also accepts `SA_PASSWORD` for compatibility with SQLDockerDeployKit examples.

## Verify

```powershell
docker exec cycles-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U SA -P "YourStrong!Passw0rd" -Q "SELECT COUNT(*) AS CycleCount FROM CyclesDb.dbo.Cycles"
docker exec cycles-sql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U SA -P "YourStrong!Passw0rd" -Q "SELECT TABLE_NAME FROM CyclesDb.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME"
```

Expected result:

- `CycleCount` is `1`.
- The seed contains one `Legacy Standard Game`, one materialised Cycle configuration, one enrolment per participant, and one `LegacyImported` lifecycle event. Its canonical map/scenario keys are recorded with `LegacyUnverified` provenance; no second Game is created.
- Normal saves append Game lifecycle audit rows and cannot erase them; explicit whole-state replacement is the destructive exception. SQL also freezes every materialised configuration field and rejects malformed hashes, half-specified seat bounds, a second successor for one Cycle, or a second operational Cycle in one Game.
- The table list includes `SchemaMigrations`, `Players`, `AdminRoleAuditRecords`, `Games`, `CycleConfigurations`, `GameEnrolments`, `GameLifecycleEvents`, `Cycles`, `GalaxySectors`, `Systems`, `Empires`, `Factions`, `MatchParticipants`, `EmpireResources`, `EmpireDoctrineUnlocks`, `EmpirePriorities`, `EmpireMetrics`, `CycleRankings`, `CycleMajorEvents`, `SystemHistoricalSignals`, `ColonialOutposts`, `DiplomaticRelationships`, `Admirals`, `AdmiralBattleHistories`, `Fleets`, `FleetOrders`, `ShipConstructions`, `TickLogs`, `Events`, `BattleRecords`, `BattleFleetParticipants`, and `ChronicleEntries`.
- The canonical seed contains 8 sectors, 64 systems, 91 routes, three empire participants, and six neutral fleets; every sector contains 8 systems, exactly two gateway systems, and the active Cycle ends 90 days after container startup.

## Connection String

```text
Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10;
```

## Use From The Application

Run the CLI against SQL Server by prefixing the connection string with `sqlserver:`:

```powershell
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- tick "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
```

Run the API against SQL Server with `ConnectionStrings:Cycles`:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Worker -- --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
```

The SQL Server store uses focused account, selected-Game, Cycle-command, resolution and operator-recovery workspaces for API, Worker and online-safe mutations. Explicit offline CLI/import, seed, continuation and profiling commands retain the generic whole-state bridge under `sp_getapplock`. Migrations are explicit and non-destructive, and the bridge remains a practical compatibility path while measured pressure determines whether further repository extraction is justified.

Use the guarded state-transfer commands for migration, controlled debugging, or reproducible fixtures. A JSON document cannot be placed on disk and used as live game state:

```powershell
dotnet run --project src/Cycles.Cli -- state export "sqlserver:<source-connection-string>" C:\secure\cycles-state-v7.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v7.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v7.json "sqlserver:<target-connection-string>" --confirm-import --confirm-replace
```

The export contains player identity and game state. Store it as a sensitive temporary artefact, verify the imported record count, and remove it through the approved secure-file process after the restore or cutover evidence has been retained.

## Integration Test

The normal test suite does not require Docker. To include SQL Server integration coverage, point `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` at a SQL Server instance the test run may use to create a uniquely named disposable database:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

Without that variable, the repository helper explicitly excludes the SQL integration category. For mandatory SQL evidence, use `.\eng\test.ps1 -RequireSqlIntegration`; this selects the SQL category, writes a TRX result, and fails on missing configuration or a zero-test result. CI uses required mode.

The integration fixture never replaces the configured catalogue. It creates a `CyclesIntegration_*` database, writes a run-specific marker before migration, redirects the suite to that database, and drops it only when both the generated-name and marker checks match. The configured login therefore needs permission to create and drop test databases.

See [Operations](../../docs/operations.md) for Worker paths, diagnostics, recovery, and the destructive `db profile` guard.

## Clean Up

```powershell
docker rm -f cycles-sql
```
