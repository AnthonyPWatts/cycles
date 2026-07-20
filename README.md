# Cycles

[![CI](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml/badge.svg)](https://github.com/AnthonyPWatts/cycles/actions/workflows/ci.yml)

Cycles is a server-authoritative, tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

## Screenshots

Explore the authored eight-sector galaxy, follow live routes, and inspect the strategic value, output, presence, and local forces of each system.

![Cycles Galaxy workspace showing the eight-sector atlas and selected-system intelligence](docs/images/cycles-dashboard-map.png)

Triage the next authoritative tick through the council agenda, frontier schematic, strategic watch, programme allocation, and resumable Day One guide.

![Cycles Command workspace showing the council agenda, frontier schematic, and Day One guide](docs/images/cycles-dashboard-command-guide.png)

The current pre-alpha development build supports a complete gameplay loop locally and through a public project site with an access-restricted trusted-playground application:

- start from a curated Day One with movement, colonisation, and combat decisions ready to make;
- explore a canonical 8-sector, 64-system galaxy through authored Galaxy, Sector, and Local charts with live routes, strategic lenses, search, focus controls, and selected-system intelligence;
- submit durable movement, in-transit recall, attack, cancellation, and colonisation orders;
- scope dashboard reads and player commands to an explicit Game while compatibility URLs remain pinned to the fixed legacy Game;
- protect cookie-authenticated mutations with an antiforgery token shared by trusted and OIDC sessions;
- resolve authoritative ticks through the CLI, a scheduled worker, or a temporary development-player control;
- inspect the current command-window stage, the complete processing order, projected income and automatic spending, and construction already committed for delivery before closing the turn;
- generate resources from influence and spend military industry on queued ships;
- unlock the first research doctrine and establish population-funded outposts;
- compete against Ariadne's deterministic game-AI policy as it attacks weaker local forces, establishes outposts, and advances towards valuable systems;
- resolve deterministic combat and persist factual events, admirals, and Chronicle reports;
- record map-control metrics, complete a Cycle, preserve major history, and generate a successor Cycle;
- learn the first-turn loop through a resumable click-along guide in the dashboard;
- run local and trusted-playground hosts against SQL Server, with versioned JSON retained only for explicit operator transfer, fixtures, inspection, and migration evidence.

This is a working development MVP, not an alpha release or production game service. The hosted playground is a cost-capped Development exception for invited testers; production authentication, persistence, operations, combat balance, and several future systems remain deliberately provisional.

## Turns Resolve In A Fixed Gameplay Order

Players commit one intention per fleet before the command window closes. The server then seals one complete ledger and resolves it as `income -> due construction -> programme spending and construction starts -> recalls, arrivals, movement, and Holds -> combat -> colonisation -> derived metrics -> next-window progression -> publication`.

That sequence governs strategy. Submission time grants no initiative. A fleet that moves away leaves before combat checks its system; a fleet that arrives can take part in defence later in the turn. Ships whose construction was already due may defend, but they contributed no income and cannot inherit a command sealed before they existed. A colonising fleet must survive combat, and research unlocked during resolution applies when the next command window opens.

The Command workspace presents all nine phases beside the current command-window stage. Its forecast separates values calculated from the present state from ship deliveries whose Industry is already committed. The commitment calendar includes player orders, journeys, projected automatic effects, and queued deliveries. History groups factual Events by authoritative phase by default, so timestamps and display order cannot imply initiative.

Move destinations show route duration plus projected dispatch and arrival before submission. The queued intention repeats that estimate, while the resolver revalidates the direct link and publishes authoritative in-transit timing after dispatch.

[Simulation Reference](docs/simulation-reference.md#authoritative-processing-order) defines the precise processing contract. Changes to the sequence require an explicit gameplay decision, regression coverage, and matching player guidance.

## Start Here

- [Gameplay Guide](docs/alpha-testers-guide.md) explains the curated opening and current gameplay loop.
- [Promo Film Production Notes](src/Cycles.Api/wwwroot/media/PROMO-PRODUCTION.md) identify current-build footage, concept dramatisations, source imagery, and the reproducible render command for the 30-second film.
- [Trusted Playground Deployment](docs/playground-deployment.md) records the hosted test boundary and cost guardrails.
- [Project State](docs/project-state.md) records implemented behaviour, verification, and known limits.
- [Player API Contract](docs/api-contract.md) records JSON conventions, stable error codes, compatibility rules, and the player-facing fact boundary.
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

When `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` is absent, the helper explicitly excludes the SQL integration category. To run that category as mandatory evidence, configure the connection string and use `.\eng\test.ps1 -RequireSqlIntegration`; required mode writes a TRX result and fails if configuration is missing or zero SQL tests execute.

## Run Locally

Normal local API and Worker development uses the SQL Server container described below. After the container is healthy, configure and migrate the database, then deliberately seed the curated opening:

```powershell
$connectionString = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Cli -- db migrate "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" --confirm-replace
```

With no size or seed arguments, this creates the deterministic `development-match-v2` opening used by the Day One guide: 8 named sectors, 64 systems, 91 routes, and three competing empires in distinct sectors. Tony and Will are persistent human development players; Ariadne is a game-AI player whose ordered planner attacks weaker visible forces, establishes affordable outposts, and moves towards high-value expansion systems. Each empire begins with three fleets and 60 ships, while six weaker Free Captain fleets remain positional neutral pressure. Supplying explicit values before `--confirm-replace`, for example `30 4 12345`, creates a generic deterministic galaxy instead. SQL seeding always requires the replacement confirmation.

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "$connectionString"
```

Open `http://127.0.0.1:5086/` for the public site or `http://127.0.0.1:5086/app.html` for the dashboard. Select Tony or Will to command that player's assigned empire. The trusted selector accepts only seeded human participants; it cannot create arbitrary accounts or take over Ariadne. It issues a protected HttpOnly session cookie. The browser obtains a separate antiforgery token before sending mutations, and **Sign out** submits a protected `POST` that removes the session cookie and returns to the selector. A defeated participant may still inspect the match but cannot issue or cancel orders, change priorities, or advance the turn. This flow remains suitable only for trusted Development testing behind the access-code boundary.

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

The worker polls every 30 seconds by default and resolves at most one due Cycle per poll. Due discovery reads the earliest persisted `NextTickAt` for an active Standard Game whose active Cycle uses `Scheduled` mode. A completed tick sets the next deadline from its completion time and `TickLengthMinutes`. A `SelfPaced` Cycle has no deadline and requires the explicit resolution boundary, so the worker does not select it. `Cycles:Worker:Enabled` and `Cycles:Worker:PollIntervalSeconds` are normal .NET configuration values.

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
dotnet run --project src/Cycles.Cli -- state convert-runtime-file C:\secure\legacy-cycles-state.json C:\secure\cycles-state-v7.json

dotnet run --project src/Cycles.Cli -- state export "sqlserver:$connectionString" C:\secure\cycles-state-v7.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v7.json
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v7.json "sqlserver:$connectionString" --confirm-import --confirm-replace
```

`convert-runtime-file` is a bounded migration bridge for the old unversioned file-store shape. It requires every persisted collection, normalises inactive priorities, validates the state, and writes a versioned document without changing the source. Export and conversion refuse to overwrite a file unless `--confirm-overwrite` is supplied. Import validates format, complete collection shape, identifiers, same-scope references, Game lineage and provenance, Cycle scheduling, normalised battle membership, tick/recovery invariants, and retained JSON before it opens the target. The v7 transfer contract can represent several Games and preserves each Cycle's `SchedulingMode` and `NextTickAt`. The current operational importer accepts only the fixed legacy Game identity while the Games home, player selection between Games, Training provisioning, and a second runtime Game remain absent. The identity check does not rederive or freeze mutable metadata from an otherwise valid v7 transfer. An accepted transfer imports into an empty database with `--confirm-import`; a non-empty target additionally requires `--confirm-replace`, then is reloaded and validated.

## Architectural Position

Clients submit intentions and read filtered state; they do not decide simulation outcomes. Canonical gameplay routes place the selected Game at `/games/{gameId}` and resolve its current Cycle, participant, and empire before loading or changing state. The old unscoped URLs remain pinned adapters for the fixed legacy Game and call the same scoped handlers. The browser uses the legacy bootstrap only until it learns the Game ID, then sends selected-Game requests through one client that rejects responses from an earlier selection generation.

The Worker owns scheduled execution. It discovers one persisted due Cycle per poll and revalidates the Game, Cycle, schedule, deadline, and locks before resolution. The explicit resolution boundary handles authorised manual and self-paced resolution without placing `SelfPaced` Cycles in the Worker queue. As a temporary development convenience, every authenticated development session can invoke that boundary for the pinned legacy Game through **Close command window and advance** without receiving admin visibility or cross-empire authority. Production players cannot use that capability.

SQL Server is the mandatory gameplay and operator-store path for the API, Worker, and CLI. They fail clearly when a Cycles SQL connection string is absent. Selected gameplay reads and commands use an explicit Game/Cycle context and focused Cycle stores. SQL-backed resolution uses a Cycle-scoped workspace, targeted outcome writes, and Game plus Cycle transaction locks. Versioned, validated JSON import/export and the bounded legacy-file conversion remain explicit operator CLI paths; no executable file-backed game store remains.

Events and battle records remain authoritative facts. Chronicle prose is deterministic template output stored separately from those facts, leaving future narrative generation non-authoritative.
