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
- The table list includes `SchemaMigrations`, `Players`, `AdminRoleAuditRecords`, `Cycles`, `GalaxySectors`, `Systems`, `Empires`, `Factions`, `MatchParticipants`, `EmpireResources`, `EmpirePriorities`, `EmpireMetrics`, `CycleRankings`, `CycleMajorEvents`, `SystemHistoricalSignals`, `ColonialOutposts`, `DiplomaticRelationships`, `Admirals`, `AdmiralBattleHistories`, `Fleets`, `FleetOrders`, `ShipConstructions`, `TickLogs`, `Events`, `BattleRecords`, and `ChronicleEntries`.
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

The SQL Server store currently uses the whole prototype `GameState` for generic API/admin mutations inside one transaction protected by `sp_getapplock`, then synchronises mapped rows with targeted deletes and upserts. Tick execution uses a focused SQL workspace and targeted outcome writes. Migrations are explicit and non-destructive, but the generic state store remains a practical bridge from the prototype aggregate boundary to a future application-service/repository model if measured pressure justifies that extraction.

Use the guarded state-transfer commands for migration, controlled debugging, or reproducible fixtures. A JSON document cannot be placed on disk and used as live game state:

```powershell
dotnet run --project src/Cycles.Cli -- state export "sqlserver:<source-connection-string>" C:\secure\cycles-state-v2.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v2.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v2.json "sqlserver:<target-connection-string>" --confirm-import --confirm-replace
```

The export contains player identity and game state. Store it as a sensitive temporary artefact, verify the imported record count, and remove it through the approved secure-file process after the restore or cutover evidence has been retained.

## Integration Test

The normal test suite does not require Docker. To include SQL Server integration coverage, point `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` at a SQL Server instance the test run may use to create a uniquely named disposable database:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

The integration fixture never replaces the configured catalogue. It creates a `CyclesIntegration_*` database, writes a run-specific marker before migration, redirects the suite to that database, and drops it only when both the generated-name and marker checks match. The configured login therefore needs permission to create and drop test databases.

See [Operations](../../docs/operations.md) for Worker paths, diagnostics, recovery, and the destructive `db profile` guard.

## Clean Up

```powershell
docker rm -f cycles-sql
```
