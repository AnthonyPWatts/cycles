# Cycles

Cycles is a tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

This implementation covers the technical MVP from the supplied design documents:

- deterministic galaxy seeding with systems, links, empires, fleets, resources, and priorities;
- server-authoritative tick processing;
- influence-based resource generation and first-pass priority spending;
- automatic ship construction from military industry investment;
- derived expansion priority projection for influence and resource shares;
- durable movement, hold, and attack orders;
- simple structured combat with deterministic randomness;
- factual event logging;
- Chronicle candidate scoring and preserved battle entries;
- per-tick map-control metric snapshots;
- a local JSON state store with file locking for prototype use;
- a CLI tick runner;
- a minimal API and browser dashboard for viewing state and submitting orders.

## Game Surface

The current playable surface is a command dashboard: players read the galaxy map, inspect system influence, adjust strategic priorities, queue fleet orders, and watch events graduate into the Chronicle when a battle becomes historically important.

![Cycles dashboard showing the contested Aster Vale system after a resolved tick](docs/images/cycles-dashboard-map.png)

## Projects

- `src/Cycles.Core`: domain model, seeding, order validation, simulation, combat, Chronicle scoring, and persistence abstraction.
- `src/Cycles.Cli`: manual seeding, ticking, inspection, and order submission.
- `src/Cycles.Api`: Minimal API, public website, and browser dashboard.
- `src/Cycles.Infrastructure.SqlServer`: SQL Server implementation of the prototype state store.
- `tests/Cycles.Tests`: xUnit tests for the core simulation behaviours.
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

Seed and tick a local state file:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json
dotnet run --project src/Cycles.Cli -- tick data/cycles-state.json
dotnet run --project src/Cycles.Cli -- show data/cycles-state.json
dotnet run --project src/Cycles.Cli -- recovery data/cycles-state.json
dotnet run --project src/Cycles.Cli -- recovery details data/cycles-state.json
```

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --Cycles:StatePath ../../data/cycles-state.json
```

Open `http://127.0.0.1:5086/` for the public site and `http://127.0.0.1:5086/app.html` for the dashboard.
The dashboard uses development auth: `/auth/login` creates or finds a local player, issues an HttpOnly development cookie, and derives player empire authority from that session. This is suitable for local/private testing only; production auth remains future work.

## Database

The application uses the JSON state store by default. SQL Server can be used by passing a `sqlserver:` store specifier to the CLI or a connection string to the API.

```powershell
docker build -t cycles-sql -f database/sqldockerdeploykit/Dockerfile .
docker run --name cycles-sql -d -p 14333:1433 -e "MSSQL_SA_PASSWORD=YourStrong!Passw0rd" cycles-sql
```

The container uses SQL Server 2022, creates `CyclesDb`, applies the SQL migrations, and seeds a small smoke-test Cycle. For an existing or empty SQL Server database, apply migrations through the CLI:

```powershell
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet run --project src/Cycles.Cli -- db status "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

Run the CLI against SQL Server:

```powershell
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
dotnet run --project src/Cycles.Cli -- tick "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

Run the API against SQL Server:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

See [database/sqldockerdeploykit](database/sqldockerdeploykit/README.md) for verification queries, persistence notes, and cleanup.

## Notes

The current SQL Server store still uses the prototype `GameState` as its generic read/write unit for API and admin mutations, then synchronises mapped rows with targeted deletes and upserts. SQL-backed tick execution uses a narrower path: it loads only the active Cycle's tick workspace, due work, and running tick guards, then persists the tick outcome rows without the generic missing-row deletion pass. SQL schema changes are tracked in `dbo.SchemaMigrations`, but the generic state writer remains a bridge rather than the final application-service/repository model.

The API exposes state and accepts orders. Player mutations require the development-auth session and derive the acting empire from that context; admin development users can inspect and support other empires. Tick execution remains in the CLI, matching the design principle that public API calls should not decide simulation outcomes.
