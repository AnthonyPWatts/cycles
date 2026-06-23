# Project State

Last updated: 2026-06-23

## Current Status

Cycles is currently a local, runnable technical MVP prototype. It proves the core loop described in the technical design document:

1. create a Cycle;
2. generate a galaxy graph;
3. create empires and fleets;
4. submit durable orders;
5. run an authoritative tick;
6. generate resources from influence;
7. move fleets;
8. resolve simple combat;
9. write factual events;
10. preserve important battles as Chronicle entries.

This is not yet a production game service. It is a working architecture slice.

## Repository Shape

| Path | Purpose |
| --- | --- |
| `src/Cycles.Core` | Domain model, seeding, order submission, tick processing, influence, combat, Chronicle scoring, and persistence abstraction. |
| `src/Cycles.Cli` | Manual local runner for seeding, ticking, showing state, and submitting fleet orders. |
| `src/Cycles.Api` | ASP.NET Core Minimal API plus a public website and browser dashboard under `wwwroot`. |
| `src/Cycles.Infrastructure.SqlServer` | SQL Server implementation of the prototype state store. |
| `tests/Cycles.Tests` | xUnit tests for core simulation behaviours. |
| `database/sqldockerdeploykit` | SQL Server container bootstrap, schema, and seed scripts based on the SQLDockerDeployKit pattern. |
| `docs` | Working development intent, state, roadmap, backlog, and decision records. |
| Repository root Word documents | Original product and technical design source material. |

## Implemented Behaviour

### Cycle And Tick

- One active Cycle is seeded by default.
- Cycle duration is set to 90 days.
- Tick length is set to 60 minutes.
- The tick number is the canonical simulation step.
- `TickEngine.RunTick` rejects a tick when another `TickLog` for the same Cycle is already `Running`.
- `TickEngine.RunTick` rejects Cycles that are not `Active`, including `RecoveryRequired` Cycles.
- Tick processing works on a cloned state and commits back only after successful processing.
- Failed ticks are recorded and mark the Cycle as `RecoveryRequired`.
- The CLI has a read-only `recovery` command for inspecting failed or unfinished tick logs.

### Galaxy

- Galaxy generation creates named systems with coordinates.
- Systems have industry, research, population, strategic value, and historical significance fields.
- System links form a connected graph using nearest-neighbour linking.
- Link travel time is currently one or two ticks based on distance.
- Home systems are selected far apart from each other.

### Empires, Resources, And Influence

- Players, empires, empire resources, and empire priorities are represented in the domain model.
- Influence is calculated from active ship presence.
- A home-system minimum presence value gives a founding empire recovery/protection pressure.
- Resource output is split by influence share.
- Generated resource facts are recorded as events.

### Orders

- Fleet orders are stored as durable records.
- Supported order types are `MoveFleet`, `Hold`, and `Attack`.
- Orders have submit tick, execute-after tick, processed tick, status, and rejection reason.
- Submission-time validation prevents clearly invalid moves and self-attacks.
- Processing-time validation rejects orders that became invalid before execution.

### Movement

- Fleets can move only along linked systems.
- One-tick links move immediately during the processing tick.
- Multi-tick links put fleets in transit with a destination and arrival tick.
- Fleet arrivals generate factual events.

### Combat

- Combat is intentionally simple.
- An attack order engages hostile active fleets in the same system.
- The current resolver uses deterministic pseudo-randomness seeded from Cycle, tick, system, and fleet IDs.
- Battle records store participants, ships before battle, losses, outcome, and fact JSON.
- Combat events are generated from battle facts.

### Chronicle

- Important battles receive an importance score.
- Score inputs currently include total losses, system strategic value, historical significance, underdog result, and very large loss counts.
- Battles above the current threshold become Chronicle entries.
- Chronicle entries store factual summaries and narrative text separately from raw battle facts.
- Narrative text is currently factual placeholder prose, not AI-generated prose.

### API And Dashboard

- The API exposes current Cycle, empire summary, galaxy, fleets, movement orders, attack orders, priorities, recent events, and Chronicle entries.
- The API has a prototype login endpoint that creates or finds a local player and empire.
- The public website is served from `/`.
- The browser dashboard is served from `/app.html` and renders the map, resources, priority editing, fleets, events, Chronicle placeholder/content, and order forms.
- Tick execution is intentionally not exposed through the API.

### Persistence

- `IGameStateStore` is the current persistence boundary used by the CLI and API.
- The default state store remains JSON-file-backed for zero-service local development.
- File locking prevents two local writers from mutating the JSON state file at the same time.
- A SQL Server schema/bootstrap image exists under `database/sqldockerdeploykit`.
- `Cycles.Infrastructure.SqlServer` can read, replace, and update `GameState` through SQL Server.
- SQL Server updates run inside a transaction protected by `sp_getapplock`.
- The SQL Server implementation writes the whole prototype state snapshot across relational tables; it is a bridge, not yet the final incremental repository model.

## Verified Checks

Last verified on 2026-06-23:

```powershell
dotnet restore Cycles.slnx --configfile NuGet.Config
dotnet build Cycles.slnx --no-restore
dotnet test Cycles.slnx --no-build
```

Additional smoke checks performed:

- CLI seed/tick/show against a temporary state file.
- API health, current Cycle, and galaxy endpoints.
- Browser dashboard desktop and mobile layout checks.
- Browser dashboard move-order submission.
- SQLDockerDeployKit-style SQL Server image build.
- `CyclesDb` container startup, schema creation, and seed verification.
- CLI `show` and `tick` against SQL Server.
- API `/cycles/current` against SQL Server.
- Opt-in SQL Server integration test with `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`.

## Known Limitations

These are known gaps, not defects in the current MVP claim:

- No migration command or schema versioning yet.
- SQL Server integration coverage is opt-in and currently covers state-store round trip, order/tick persistence, and duplicate running-tick rollback.
- No real authentication or authorisation.
- No scheduled worker service.
- No production-grade per-Cycle tick locking.
- No real deployment story.
- No end-of-Cycle rankings, winners, reset, or continuity.
- No build-ships or spending-priority automation beyond storing priority weights.
- No diplomacy, alliances, treaties, or betrayal mechanics.
- No technologies, doctrines, cloaking, detection, or logistics.
- No admirals or persistent named figures.
- No AI narrative generation.
- No historical-system evolution across Cycles.
- No multiplayer security boundary.
- No admin recovery clear/retry workflow for failed ticks.
- Combat is deliberately primitive and not balanced.
- The dashboard is a prototype, not a full game client.

## Current Development Priority

The next stage should harden the simulation spine before adding feature breadth:

1. replace the snapshot-style SQL Server store with incremental persistence operations;
2. make tick execution idempotent and auditable against that persistence layer;
3. add automated SQL Server integration tests around seed/show/tick/order flows;
4. add a minimal build/spending loop so strategic priorities affect gameplay;
5. only then deepen the Chronicle/history systems.

## Definition Of The Next Stable State

The project reaches the next stable state when:

- a fresh checkout can restore, build, test, seed, tick, and run the API from documented commands;
- simulation state can be persisted in a relational store;
- the same due order cannot be processed twice;
- one tick per Cycle is enforced by storage-level locking or an equivalent mechanism;
- core behaviours are covered by deterministic automated tests;
- the dashboard remains able to view state and submit orders against the new persistence layer.
