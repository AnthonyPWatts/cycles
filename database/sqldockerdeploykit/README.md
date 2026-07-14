# Cycles SQLDockerDeployKit Database

This folder contains a Cycles-specific SQL Server container definition based on the SQLDockerDeployKit pattern: a SQL Server image that creates `CyclesDb`, applies the ordered SQL migrations from `database/migrations`, then executes ordered seed scripts from `SQLScripts`.

Normal local API and Worker runs use this SQL Server database through the `Cycles.Infrastructure.SqlServer` store. Versioned JSON is retained for explicit operator import/export, fixtures, and migration evidence while the hosted cutover remains pending.

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
- The table list includes `SchemaMigrations`, `Players`, `AdminRoleAuditRecords`, `Cycles`, `Systems`, `Empires`, `EmpireResources`, `EmpirePriorities`, `EmpireMetrics`, `CycleRankings`, `CycleMajorEvents`, `SystemHistoricalSignals`, `ColonialOutposts`, `DiplomaticRelationships`, `Admirals`, `AdmiralBattleHistories`, `Fleets`, `FleetOrders`, `ShipConstructions`, `TickLogs`, `Events`, `BattleRecords`, and `ChronicleEntries`.

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
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10" --Cycles:RequireSqlRuntime true
dotnet run --project src/Cycles.Worker -- --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10" --Cycles:RequireSqlRuntime true
```

The SQL Server store currently uses the whole prototype `GameState` for generic API/admin mutations inside one transaction protected by `sp_getapplock`, then synchronises mapped rows with targeted deletes and upserts. SQL-backed tick execution uses a focused tick workspace and targeted outcome writes. Migrations are explicit and non-destructive, but the generic state store is still a practical bridge from JSON persistence to the final application-service/repository model.

Use the guarded state-transfer commands for migration or recovery rather than copying the old JSON store into place:

```powershell
dotnet run --project src/Cycles.Cli -- state export "sqlserver:<source-connection-string>" C:\secure\cycles-state-v1.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v1.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v1.json "sqlserver:<target-connection-string>" --confirm-import --confirm-replace
```

The export contains player identity and game state. Store it as a sensitive temporary artefact, verify the imported record count, and remove it through the approved secure-file process after the restore or cutover evidence has been retained.

## Integration Test

The normal test suite does not require Docker. To include SQL Server integration coverage, point `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` at a disposable Cycles database before using the repository test helper:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

The integration test replaces the configured database contents, so do not run it against data you want to keep.

See [Operations](../../docs/operations.md) for Worker paths, diagnostics, recovery, and the destructive `db profile` guard.

## Clean Up

```powershell
docker rm -f cycles-sql
```
