# Cycles

[![CI](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml/badge.svg)](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml)

Cycles is a server-authoritative, tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

The current pre-alpha development build supports a complete gameplay loop locally and through an access-restricted trusted playground:

- start from a curated Day One with movement, colonisation, and combat decisions ready to make;
- explore a canonical 8-sector, 64-system galaxy through authored Galaxy, Sector, and Local charts with live routes, strategic lenses, search, focus controls, and selected-system intelligence;
- submit durable movement, attack, cancellation, and colonisation orders;
- resolve authoritative ticks through the CLI, a scheduled worker, or a temporary development-player control;
- generate resources from influence and spend military industry on queued ships;
- unlock the first research doctrine and establish population-funded outposts;
- resolve deterministic combat and persist factual events, admirals, and Chronicle reports;
- record map-control metrics, complete a Cycle, preserve major history, and generate a successor Cycle;
- learn the first-turn loop through a resumable click-along guide in the dashboard;
- run local and trusted-playground hosts against SQL Server, with versioned JSON retained only for explicit operator transfer, fixtures, inspection, and migration evidence.

This is a working development MVP, not an alpha release or production game service. The hosted playground is a cost-capped Development exception for invited testers; production authentication, persistence, operations, combat balance, and several future systems remain deliberately provisional.

## Start Here

- [Gameplay Guide](docs/alpha-testers-guide.md) explains the curated opening and current gameplay loop.
- [Promo Film Production Notes](src/Cycles.Api/wwwroot/media/PROMO-PRODUCTION.md) identify current-build footage, concept dramatisations, source imagery, and the reproducible render command for the 30-second film.
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
.\eng\alpha-gameplay-smoke.ps1 -ConfirmReplace
```

The test helper writes build output under `%TEMP%\cycles-test-bin\`, avoiding locked assemblies when `Cycles.Api` is already running. The gameplay smoke check uses a dedicated disposable SQL database and exercises login, priority editing, movement, a development-player turn advance, resource generation, and visible events through the real API.

## Run Locally

Normal local API and Worker development uses the SQL Server container described below. After the container is healthy, configure and migrate the database, then deliberately seed the curated opening:

```powershell
$connectionString = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" --confirm-replace
```

With no size or seed arguments, this creates the curated `development-cold-start-v1` opening used by the Day One guide: 8 named sectors, 64 systems, and 91 routes. Every sector contains 8 systems in its own connected 10-route composition and has exactly two gateway systems. The 11 inter-sector bridges form a partial mesh with no central hub and are drawn as live SVG over the route-free authored chart. Supplying explicit values before `--confirm-replace`, for example `30 4 12345`, creates a generic deterministic galaxy instead. SQL seeding always requires the replacement confirmation.

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "$connectionString"
```

Open `http://127.0.0.1:5086/` for the public site or `http://127.0.0.1:5086/app.html` for the dashboard. Log in as the prefilled `player-1` to receive the curated Aurelian opening. The development login creates or finds a local player and issues an HttpOnly cookie; **Sign out** removes that cookie and returns to the login prompt. This flow is suitable only for trusted local testing.

The Galaxy workspace uses three authored ranges rather than a free camera. **Galaxy** shows the eight-sector partial mesh, **Sector** shows one eight-system territorial chart and its outbound bridges, and **Local** subdues systems outside the selected neighbourhood. Search, strategic lenses, Home/Selected/Flashpoint focus controls, direct chart selection, a maximised view, and the adjacent-system inspector recover context without covering the map with a duplicate navigator.

Run one manual tick or inspect the state:

```powershell
dotnet run --project src/Cycles.Cli -- tick "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- show "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- diagnostics "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- galaxy upgrade "sqlserver:$connectionString" --confirm-upgrade
```

Run scheduled ticks against the same database:

```powershell
dotnet run --project src/Cycles.Worker -- --ConnectionStrings:Cycles "$connectionString"
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

The image creates `CyclesDb`, applies ordered migrations, and seeds the same canonical opening with runtime-relative Cycle dates. [The database runbook](database/sqldockerdeploykit/README.md) covers application configuration, verification queries, integration tests, and cleanup.

## Versioned State Transfer

Complete exports are operator artefacts containing identities, audit context, hidden facts, and private state for every empire. Store and transfer them securely, do not attach them to ordinary issue logs, and delete them when the migration/debugging need ends. They are not database backups or player save files.

```powershell
# One-time bridge for a retired raw runtime file; the input is not modified.
dotnet run --project src/Cycles.Cli -- state convert-runtime-file C:\secure\legacy-cycles-state.json C:\secure\cycles-state-v2.json

dotnet run --project src/Cycles.Cli -- state export "sqlserver:$connectionString" C:\secure\cycles-state-v2.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v2.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v2.json "sqlserver:$connectionString" --confirm-import --confirm-replace
```

`convert-runtime-file` is a bounded migration bridge for the old unversioned file-store shape. It requires every persisted collection, normalises inactive priorities, validates the state, and writes a versioned document without changing the source. Export and conversion refuse to overwrite a file unless `--confirm-overwrite` is supplied. Import validates format, complete collection shape, identifiers, references, tick/recovery invariants, and retained JSON before it opens the target. It imports into an empty database with `--confirm-import`; a non-empty target additionally requires `--confirm-replace`, then is reloaded and validated.

## Architectural Position

Clients submit intentions and read filtered state; they do not decide simulation outcomes. The Worker owns scheduled execution. As a temporary development convenience, every authenticated development session can invoke the same store-level tick boundary through **Advance turn** without receiving admin visibility or cross-empire authority. Production players cannot use that capability.

SQL Server is the mandatory gameplay and operator-store path for the API, Worker, and CLI. They fail clearly when a Cycles SQL connection string is absent. The generic API/admin store still loads and synchronises the prototype `GameState`, while SQL-backed ticks use a narrower Cycle-scoped workspace, targeted outcome writes, and a per-Cycle transaction lock. Versioned, validated JSON import/export and the bounded legacy-file conversion remain explicit operator CLI paths; no executable file-backed game store remains.

Events and battle records remain authoritative facts. Chronicle prose is deterministic template output stored separately from those facts, leaving future narrative generation non-authoritative.
