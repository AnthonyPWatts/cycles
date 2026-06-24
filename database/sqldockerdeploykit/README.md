# Cycles SQLDockerDeployKit Database

This folder contains a Cycles-specific SQL Server container definition based on the SQLDockerDeployKit pattern: a SQL Server image that creates `CyclesDb`, applies the ordered SQL migrations from `database/migrations`, then executes ordered seed scripts from `SQLScripts`.

The application still uses JSON persistence by default, but the CLI and API can opt into this SQL Server database through the `Cycles.Infrastructure.SqlServer` store.

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
- The table list includes `SchemaMigrations`, `Players`, `Cycles`, `Systems`, `Empires`, `Fleets`, `FleetOrders`, `TickLogs`, `Events`, `BattleRecords`, and `ChronicleEntries`.

## Connection String

```text
Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
```

## Use From The Application

Run the CLI against SQL Server by prefixing the connection string with `sqlserver:`:

```powershell
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet run --project src/Cycles.Cli -- tick "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

Run the API against SQL Server with `ConnectionStrings:Cycles`:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

The SQL Server store currently reads the whole prototype `GameState` inside one transaction protected by `sp_getapplock`, then synchronises mapped rows with targeted deletes and upserts. Migrations are explicit and non-destructive, but the state store is still a practical bridge from JSON persistence to the final focused repository model.

## Integration Test

The normal test suite does not require Docker. To include the SQL Server integration test, point `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` at a disposable Cycles database before running `dotnet test`:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet test Cycles.slnx --no-build
```

Or pass the variable directly to the test host:

```powershell
dotnet test Cycles.slnx --no-build --environment CYCLES_SQL_INTEGRATION_CONNECTION_STRING="Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

The integration test replaces the configured database contents, so do not run it against data you want to keep.

## Clean Up

```powershell
docker rm -f cycles-sql
```
