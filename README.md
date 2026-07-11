# Cycles

[![CI](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml/badge.svg)](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml)

Cycles is a tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

This implementation covers the technical MVP from the supplied design documents:

- deterministic galaxy seeding with systems, links, empires, fleets, resources, and priorities;
- server-authoritative tick processing;
- influence-based resource generation and first-pass priority spending;
- automatic ship construction from military industry investment;
- population-funded colonial outposts that extend local influence while supported by an active fleet;
- derived expansion priority projection for influence and resource shares;
- durable movement, hold, and attack orders;
- simple structured combat with deterministic randomness;
- persisted Neutral, War, Non-Aggression Pact, and Alliance relationship states with treaty-breaking aggression facts;
- named admirals assigned to fleets, with battle reputation and status history;
- factual event logging;
- Chronicle candidate scoring and preserved battle entries;
- deterministic template-based Chronicle battle reports;
- per-tick map-control metric snapshots;
- Cycle-end ranking and selected major-battle preservation;
- a local JSON state store with file locking for prototype use;
- a CLI tick runner;
- a scheduled worker and development-admin manual tick control;
- a minimal API and browser dashboard for viewing state and submitting orders.

## Game Surface

The current playable surface is a command dashboard: players read the galaxy map, inspect system influence, adjust strategic priorities, queue fleet orders, and watch events graduate into the Chronicle when a battle becomes historically important.

Invited players should start with the [Alpha Tester's Guide to Gameplay](docs/alpha-testers-guide.md).

![Cycles dashboard showing the contested Aster Vale system after a resolved tick](docs/images/cycles-dashboard-map.png)

## Projects

- `src/Cycles.Core`: domain model, seeding, order validation, simulation, combat, Chronicle scoring, and persistence abstraction.
- `src/Cycles.Cli`: manual seeding, ticking, inspection, and order submission.
- `src/Cycles.Api`: Minimal API, public website, and browser dashboard.
- `src/Cycles.Worker`: scheduled authoritative tick runner.
- `src/Cycles.Infrastructure.SqlServer`: SQL Server implementation of the prototype state store.
- `tests/Cycles.Tests`: xUnit tests for simulation, API contracts, worker scheduling, and SQL Server persistence.
- `database/sqldockerdeploykit`: SQL Server container bootstrap based on the SQLDockerDeployKit pattern.
- `docs`: current state, roadmap, architecture direction, backlog, and decision log.

## Planning Docs

Start here before adding the next substantial feature:

- [Project state](docs/project-state.md)
- [Development roadmap](docs/development-roadmap.md)
- [Architecture direction](docs/architecture-direction.md)
- [Backlog](docs/backlog.md)
- [Decision log](docs/decision-log.md)
- [Decisions before continuing](docs/decisions-before-continuing.md)
- [Tick recovery](docs/recovery.md)
- [Determinism contract](docs/determinism.md)
- [Ranking metrics](docs/ranking-metrics.md)
- [Balance scenarios](docs/balance-scenarios.md)
- [SQL state path profile](docs/sql-state-profile.md)

## Run

Restore and build:

```powershell
dotnet restore Cycles.slnx --configfile NuGet.Config
dotnet build Cycles.slnx --no-restore
```

Run the tests:

```powershell
.\eng\test.ps1
.\eng\test.ps1 -Filter InfluenceTests
```

The test script builds into `%TEMP%\cycles-test-bin\` so tests can run while a local `Cycles.Api` process is serving from the normal `bin\Debug` output directory.
GitHub Actions runs the same suite on Linux, performs CLI and API smoke checks, and runs the full suite again against a migrated SQL Server service container.

Seed and tick a local state file:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json
dotnet run --project src/Cycles.Cli -- tick data/cycles-state.json
dotnet run --project src/Cycles.Cli -- show data/cycles-state.json
dotnet run --project src/Cycles.Cli -- diagnostics data/cycles-state.json
dotnet run --project src/Cycles.Cli -- cycle end data/cycles-state.json
dotnet run --project src/Cycles.Cli -- cycle next data/cycles-state.json
dotnet run --project src/Cycles.Cli -- balance 48 24 4 71421
dotnet run --project src/Cycles.Cli -- recovery data/cycles-state.json
dotnet run --project src/Cycles.Cli -- recovery details data/cycles-state.json
```

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --Cycles:StatePath ../../data/cycles-state.json
```

Open `http://127.0.0.1:5086/` for the public site and `http://127.0.0.1:5086/app.html` for the dashboard.
The dashboard uses development auth: `/auth/login` creates or finds a local player, issues an HttpOnly development cookie, and derives player empire authority from that session. Player read endpoints apply first-pass fog-of-war filtering: the full map structure remains visible, but exact local fleet/presence facts, events, and Chronicle entries are scoped to systems where the player has an active fleet. This is suitable for local/private testing only; production auth remains future work.

Run scheduled ticks against the same state store as the API:

```powershell
dotnet run --project src/Cycles.Worker -- --Cycles:StatePath ../../data/cycles-state.json
```

The worker polls every 30 seconds by default and runs one tick when the active Cycle is due. Configure `Cycles:Worker:Enabled` or `Cycles:Worker:PollIntervalSeconds` through normal .NET configuration. The Cycle's `TickLengthMinutes` controls the simulation cadence. Development-admin dashboard sessions can also trigger one authoritative tick through the protected admin boundary; ordinary players cannot.

## Database

The application uses the JSON state store by default. SQL Server can be used by passing a `sqlserver:` store specifier to the CLI or a connection string to the API.

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

The container uses SQL Server 2022, creates `CyclesDb`, applies the SQL migrations, and seeds a small smoke-test Cycle. For an existing or empty SQL Server database, apply migrations through the CLI:

```powershell
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
```

For repeatable engineering measurements against a disposable database, `db profile` compares whole-state replacement/load/update with the focused tick path. It refuses to run without `--confirm-replace`; see [SQL state path profile](docs/sql-state-profile.md).

Run the CLI against SQL Server:

```powershell
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
dotnet run --project src/Cycles.Cli -- tick "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
```

Run the API against SQL Server:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False"
```

See [database/sqldockerdeploykit](database/sqldockerdeploykit/README.md) for verification queries, persistence notes, and cleanup.

## Notes

The current SQL Server store still uses the prototype `GameState` as its generic read/write unit for API and admin mutations, then synchronises mapped rows with targeted deletes and upserts. SQL-backed tick execution uses a narrower path: it loads only the active Cycle's tick workspace, due work, colonial outposts, diplomatic relationships, and running tick guards, then persists the tick outcome rows without the generic missing-row deletion pass. SQL schema changes are tracked in `dbo.SchemaMigrations`, but the generic state writer remains a bridge rather than the final application-service/repository model.

The API exposes state and accepts movement, attack, cancellation, priority, and colonisation intentions. Player mutations require the development-auth session and derive the acting empire from that context; admin development users can inspect and support other empires and manually trigger a tick. Diplomacy currently persists the accepted relationship vocabulary and treaty-breaking attack facts, but has no player-facing offers, declarations, or alliance effects. Chronicle entries keep factual summaries separate from deterministic template prose, leaving future AI narrative generation non-authoritative. Scheduled tick execution belongs to `Cycles.Worker`; the CLI remains a developer convenience and ordinary player API calls cannot decide simulation outcomes.
