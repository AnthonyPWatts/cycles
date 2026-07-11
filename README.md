# Cycles

[![CI](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml/badge.svg)](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml)

Cycles is a server-authoritative, tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

The current private-alpha build supports a complete local gameplay loop:

- generate a connected galaxy with empires, fleets, resources, and priorities;
- submit durable movement, attack, cancellation, and colonisation orders;
- resolve authoritative ticks through the CLI, a scheduled worker, or a development-admin control;
- generate resources from influence and spend military industry on queued ships;
- unlock the first research doctrine and establish population-funded outposts;
- resolve deterministic combat and persist factual events, admirals, and Chronicle reports;
- record map-control metrics, complete a Cycle, preserve major history, and generate a successor Cycle;
- run against a local JSON store or SQL Server.

This is a working technical MVP, not a production game service. Development authentication, the dashboard, combat balance, deployment, and several future systems remain deliberately provisional.

## Start Here

- [Alpha Tester's Guide](docs/alpha-testers-guide.md) explains the current gameplay loop.
- [Project State](docs/project-state.md) records implemented behaviour, verification, and known limits.
- [Documentation Index](docs/README.md) defines the purpose and ownership of every maintained document.
- [Backlog](docs/backlog.md) is the sole implementation queue.

## Projects

| Path | Purpose |
| --- | --- |
| `src/Cycles.Core` | Domain model, simulation, orders, combat, Chronicle scoring, history, and persistence abstraction. |
| `src/Cycles.Cli` | Local seeding, inspection, ticking, Cycle administration, diagnostics, and balance scenarios. |
| `src/Cycles.Api` | Minimal API, development auth, public website, and browser dashboard. |
| `src/Cycles.Worker` | Scheduled authoritative tick runner. |
| `src/Cycles.Infrastructure.SqlServer` | SQL Server persistence, migrations, and focused tick execution. |
| `tests/Cycles.Tests` | xUnit behaviour, API, worker, migration, and SQL Server integration tests. |
| `database` | SQL migrations and the local SQL Server bootstrap image. |

## Build And Test

```powershell
dotnet restore Cycles.slnx --configfile NuGet.Config
dotnet build Cycles.slnx --no-restore
.\eng\test.ps1
```

Focused and running-application checks:

```powershell
.\eng\test.ps1 -Filter InfluenceTests
.\eng\alpha-gameplay-smoke.ps1
```

The test helper writes build output under `%TEMP%\cycles-test-bin\`, avoiding locked assemblies when `Cycles.Api` is already running. The alpha smoke check uses disposable state and exercises login, priority editing, movement, an admin tick, resource generation, and visible events through the real API.

## Run Locally

Seed a JSON state file:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json
```

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --Cycles:StatePath ../../data/cycles-state.json
```

Open `http://127.0.0.1:5086/` for the public site or `http://127.0.0.1:5086/app.html` for the dashboard. The development login creates or finds a local player and issues an HttpOnly cookie. It is suitable only for trusted local or private testing.

Run one manual tick or inspect the state:

```powershell
dotnet run --project src/Cycles.Cli -- tick data/cycles-state.json
dotnet run --project src/Cycles.Cli -- show data/cycles-state.json
dotnet run --project src/Cycles.Cli -- diagnostics data/cycles-state.json
```

Run scheduled ticks against the same file:

```powershell
dotnet run --project src/Cycles.Worker -- --Cycles:StatePath data/cycles-state.json
```

The worker polls every 30 seconds by default and runs at most one tick when the active Cycle is due. `Cycles:Worker:Enabled` and `Cycles:Worker:PollIntervalSeconds` are normal .NET configuration values; the Cycle's `TickLengthMinutes` defines simulation cadence.

See [Operations](docs/operations.md) for diagnostics, recovery, worker behaviour, and guarded SQL profiling. The CLI also supports `cycle end`, `cycle next`, and deterministic `balance` scenarios; the [Simulation Reference](docs/simulation-reference.md) records their contracts.

## SQL Server

Build and start the disposable local database:

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

Apply migrations and inspect their status:

```powershell
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
```

The image creates `CyclesDb`, applies ordered migrations, and seeds a smoke-test Cycle. [The database runbook](database/sqldockerdeploykit/README.md) covers application configuration, verification queries, integration tests, and cleanup.

## Architectural Position

Ordinary clients submit intentions and read filtered state; they do not decide simulation outcomes. The worker owns scheduled execution, while development admins can invoke the same store-level tick boundary for private-alpha support.

SQL Server is the primary relational proof path. The generic API/admin store still loads and synchronises the prototype `GameState`, while SQL-backed ticks use a narrower Cycle-scoped workspace, targeted outcome writes, and a per-Cycle transaction lock. JSON remains the zero-service development store; the accepted long-term direction is import/export support rather than production runtime persistence.

Events and battle records remain authoritative facts. Chronicle prose is deterministic template output stored separately from those facts, leaving future narrative generation non-authoritative.
