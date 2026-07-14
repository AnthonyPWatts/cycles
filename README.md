# Cycles

[![CI](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml/badge.svg)](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml)

Cycles is a server-authoritative, tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

The current pre-alpha development build supports a complete gameplay loop locally and through an access-restricted trusted playground:

- start from a curated Day One with movement, colonisation, and combat decisions ready to make;
- generate a connected galaxy with empires, fleets, resources, and priorities;
- submit durable movement, attack, cancellation, and colonisation orders;
- resolve authoritative ticks through the CLI, a scheduled worker, or a temporary development-player control;
- generate resources from influence and spend military industry on queued ships;
- unlock the first research doctrine and establish population-funded outposts;
- resolve deterministic combat and persist factual events, admirals, and Chronicle reports;
- record map-control metrics, complete a Cycle, preserve major history, and generate a successor Cycle;
- learn the first-turn loop through a resumable click-along guide in the dashboard;
- run normal local development against SQL Server, with versioned JSON retained for explicit operator transfer, fixtures, and migration evidence while the deployed cutover is still pending.

This is a working development MVP, not an alpha release or production game service. The hosted playground is a cost-capped Development exception for invited testers; production authentication, persistence, operations, combat balance, and several future systems remain deliberately provisional.

## Start Here

- [Gameplay Guide](docs/alpha-testers-guide.md) explains the curated opening and current gameplay loop.
- [Trusted Playground Deployment](docs/playground-deployment.md) records the hosted test boundary and cost guardrails.
- [Project State](docs/project-state.md) records implemented behaviour, verification, and known limits.
- [Documentation Index](docs/README.md) defines the purpose and ownership of every maintained document.
- [Backlog](docs/backlog.md) curates priorities, sequencing, decision gates, conditional risks, and links; GitHub issues own concrete actionable work and live status.

## Projects

| Path | Purpose |
| --- | --- |
| `src/Cycles.Core` | Domain model, simulation, orders, combat, Chronicle scoring, history, and persistence abstraction. |
| `src/Cycles.Cli` | Local seeding, inspection, ticking, Cycle administration, diagnostics, and balance scenarios. |
| `src/Cycles.Api` | Minimal API, Development login, external OIDC boundary, public website, and protected browser dashboard. |
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

The test helper writes build output under `%TEMP%\cycles-test-bin\`, avoiding locked assemblies when `Cycles.Api` is already running. The gameplay smoke check uses disposable state and exercises login, priority editing, movement, a development-player turn advance, resource generation, and visible events through the real API.

## Run Locally

Normal local API and Worker development uses the SQL Server container described below. After the container is healthy, configure and migrate the database, then deliberately seed the curated opening:

```powershell
$connectionString = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" --confirm-replace
```

With no size or seed arguments, this creates the curated `development-cold-start-v1` opening used by the Day One guide. Supplying explicit values before `--confirm-replace`, for example `24 4 71421`, creates the generic deterministic galaxy instead. SQL seeding always requires the replacement confirmation.

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "$connectionString" --Cycles:RequireSqlRuntime true
```

Open `http://127.0.0.1:5086/` for the public site or `http://127.0.0.1:5086/app.html` for the dashboard. Log in as the prefilled `player-1` to receive the curated Aurelian opening. The development login creates or finds a local player and issues an HttpOnly cookie; **Sign out** removes that cookie and returns to the login prompt. This flow is suitable only for trusted local testing.

Run one manual tick or inspect the state:

```powershell
dotnet run --project src/Cycles.Cli -- tick "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- show "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- diagnostics "sqlserver:$connectionString"
```

Run scheduled ticks against the same file:

```powershell
dotnet run --project src/Cycles.Worker -- --ConnectionStrings:Cycles "$connectionString" --Cycles:RequireSqlRuntime true
```

The worker polls every 30 seconds by default and runs at most one tick when the active Cycle is due. `Cycles:Worker:Enabled` and `Cycles:Worker:PollIntervalSeconds` are normal .NET configuration values; the Cycle's `TickLengthMinutes` defines simulation cadence.

See [Operations](docs/operations.md) for diagnostics, recovery, worker behaviour, and guarded SQL profiling. The CLI also supports `cycle end`, `cycle next`, and deterministic `balance` scenarios; the [Simulation Reference](docs/simulation-reference.md) records their contracts.

## SQL Server Bootstrap

Build and start the disposable local database:

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

Apply migrations and inspect their status if they were not already applied in the local run sequence:

```powershell
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
```

The image creates `CyclesDb`, applies ordered migrations, and seeds a smoke-test Cycle. [The database runbook](database/sqldockerdeploykit/README.md) covers application configuration, verification queries, integration tests, and cleanup.

## Versioned State Transfer

Complete exports are operator artefacts containing identities, audit context, hidden facts, and private state for every empire. Store and transfer them securely, do not attach them to ordinary issue logs, and delete them when the migration/debugging need ends. They are not database backups or player save files.

```powershell
dotnet run --project src/Cycles.Cli -- state export "sqlserver:$connectionString" C:\secure\cycles-state-v1.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v1.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v1.json "sqlserver:$connectionString" --confirm-import --confirm-replace
```

Export refuses to overwrite a file unless `--confirm-overwrite` is supplied. Import validates format, complete collection shape, identifiers, references, tick/recovery invariants, and retained JSON before it opens the target. It imports into an empty database with `--confirm-import`; a non-empty target additionally requires `--confirm-replace`, then is reloaded and validated.

## Architectural Position

Clients submit intentions and read filtered state; they do not decide simulation outcomes. The Worker owns scheduled execution. As a temporary development convenience, every authenticated development session can invoke the same store-level tick boundary through **Advance turn** without receiving admin visibility or cross-empire authority. Production players cannot use that capability.

SQL Server is the selected runtime path. The generic API/admin store still loads and synchronises the prototype `GameState`, while SQL-backed ticks use a narrower Cycle-scoped workspace, targeted outcome writes, and a per-Cycle transaction lock. Versioned, validated JSON import/export is available through the operator CLI. `Cycles:RequireSqlRuntime=true` makes API/Worker startup fail clearly without SQL; it must be activated for the hosted environment only after [#125](https://github.com/AnthonyPWatts/cycles/issues/125) has imported and verified the deployed state. Removing the transitional host fallback remains the final ordered step in [#126](https://github.com/AnthonyPWatts/cycles/issues/126).

Events and battle records remain authoritative facts. Chronicle prose is deterministic template output stored separately from those facts, leaving future narrative generation non-authoritative.
