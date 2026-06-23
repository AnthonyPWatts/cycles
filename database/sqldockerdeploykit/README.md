# Cycles SQLDockerDeployKit Database

This folder contains a Cycles-specific SQL Server container definition based on the SQLDockerDeployKit pattern: a SQL Server image plus ordered `SQLScripts` executed at startup.

It is intentionally separate from the application runtime. At this stage it proves the relational schema and local database bootstrap path; the application still uses JSON persistence until the next persistence slice is implemented.

## Build

From the repository root:

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
```

## Run

Use a non-default host port so it does not collide with an existing local SQL Server:

```powershell
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

The image also accepts `SA_PASSWORD` for compatibility with SQLDockerDeployKit examples.

## Verify

```powershell
docker exec cycles-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U SA -P "YourStrong!Passw0rd" -Q "SELECT COUNT(*) AS CycleCount FROM CyclesDb.dbo.Cycles"
docker exec cycles-sql /opt/mssql-tools/bin/sqlcmd -S localhost -U SA -P "YourStrong!Passw0rd" -Q "SELECT TABLE_NAME FROM CyclesDb.INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME"
```

Expected result:

- `CycleCount` is `1`.
- The table list includes `Players`, `Cycles`, `Systems`, `Empires`, `Fleets`, `FleetOrders`, `TickLogs`, `Events`, `BattleRecords`, and `ChronicleEntries`.

## Connection String

```text
Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;
```

## Clean Up

```powershell
docker rm -f cycles-sql
```
