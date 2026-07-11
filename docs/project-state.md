# Project State

Last updated: 2026-07-11

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
10. preserve important battles as Chronicle entries;
11. snapshot per-tick empire map-control metrics;
12. complete a Cycle manually and persist final ranked standings;
13. preserve selected major battles as Cycle history.

This is not yet a production game service. It is a working architecture slice.

## Game Screenshots

These screenshots show a temporary local game state after Aurelian Compact and Khepri Mandate fought over Aster Vale. They are intended as a player-facing reference for what the prototype currently feels like, not as an implementation diagram.

### Command Map

The map shows a Cycle at tick 1, contested influence at Aster Vale, last-tick resource gains, military spending, and the current strategic priority split.

![Cycles dashboard command map with Aster Vale contested](images/cycles-dashboard-map.png)

### Issuing Orders

Players can queue fleet movement, attack, or population-funded colonisation orders from the dashboard. Orders remain pending until an authoritative tick processes them.

![Cycles dashboard showing a pending attack order](images/cycles-dashboard-attack-order.png)

### Events And Chronicle

After the tick resolves, ordinary economy and battle facts appear in the event stream. The Battle of Aster Vale crossed the Chronicle threshold and was preserved as a historical entry.

![Cycles dashboard showing tick events and the Chronicle entry for the Battle of Aster Vale](images/cycles-dashboard-chronicle.png)

### Small Screen

The prototype dashboard is still compact, but the command map, Cycle status, and influence readout remain usable on a narrow viewport.

![Cycles dashboard command map on a narrow viewport](images/cycles-dashboard-mobile.png)

## Repository Shape

| Path | Purpose |
| --- | --- |
| `src/Cycles.Core` | Domain model, seeding, order submission, tick processing, influence, combat, Chronicle scoring, and persistence abstraction. |
| `src/Cycles.Cli` | Manual local runner for seeding, ticking, showing state, and submitting fleet orders. |
| `src/Cycles.Api` | ASP.NET Core Minimal API plus a public website and browser dashboard under `wwwroot`. |
| `src/Cycles.Worker` | Scheduled authoritative tick runner using the configured state store. |
| `src/Cycles.Infrastructure.SqlServer` | SQL Server implementation of the prototype state store. |
| `tests/Cycles.Tests` | xUnit tests for core simulation behaviours. |
| `database/sqldockerdeploykit` | SQL Server container bootstrap, schema, and seed scripts based on the SQLDockerDeployKit pattern. |
| `docs` | Working development intent, state, roadmap, backlog, decision records, and original source documents under `docs/source`. |
| `.github/ISSUE_TEMPLATE` | GitHub issue forms for bug reports, implementation tasks, and design decisions. |
| `.github/workflows/ci.yml` | Linux build/test/smoke verification plus migrated SQL Server integration coverage. |

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
- The CLI has `recovery`, `recovery details`, `recovery clear`, and `recovery retry` commands for inspecting failed ticks, clearing repaired Cycles, and retrying the same tick number.
- The CLI has a `diagnostics` command for store identity, Cycle cadence, next-due time, tick-log health, due orders, queued construction, and recovery action guidance.
- `Cycles.Worker` polls the configured store and runs one authoritative tick when the active Cycle is due, using `TickLengthMinutes` and the last completed tick time rather than attempting a backlog of catch-up ticks.
- Development-admin sessions can trigger the same store-level tick operation from the dashboard; ordinary players cannot access that endpoint.

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
- Resources are stockpiles and are clamped non-negative.
- Last-tick generated and spent resource amounts are stored separately from stockpile totals.
- Strategic priority weights must total 100.
- Military priority spending automatically queues ship construction from available industry.
- Expansion priority increases an empire's derived effective presence for resource and system-detail influence calculations.
- Population can fund a colonial outpost in a non-home system where the empire has an active fleet and strictly leading influence.
- Colonial outposts add five local presence while the owning empire maintains an active fleet in that system; they do not create binary ownership or fleetless control.
- Research stockpiles unlock the first doctrine effect: reaching 200 research records a one-time Survey Projection unlock event, which adds a 10% effective-presence bonus for that empire.
- Queued ships cost 25 industry each, complete after 3 ticks, and join the empire's home fleet.
- Generated resource facts are recorded as events.
- Completed ticks record per-empire metric snapshots for `MapControlPercent`, rank, winner flag, total effective presence, and active ship count.
- The CLI can complete a Cycle manually with `cycle end`, ranking active empires by `MapControlPercent` and storing final standings in `CycleRankings`.
- Completing a Cycle increases system historical significance for repeated battles, with an extra signal for systems that hosted one of the largest battles by total losses.
- Completing a Cycle records `SystemHistoricalSignals` for affected systems, including battle count, total losses, largest local battle, and historical-significance increase.
- Completing a Cycle preserves the top 10% of battles by total losses, with a minimum of one battle, in `CycleMajorEvents`.
- The CLI can generate a successor Cycle with `cycle next`, using completed-Cycle rankings to preserve player continuity and selected historical system facts to carry famous system names/significance into the new galaxy.
- The CLI can run a deterministic repeated-tick balance scenario that drives existing orders and reports economy, colonisation, combat, map-control, and retained-history evidence without changing production state.

### Orders

- Fleet orders are stored as durable records.
- Supported order types are `MoveFleet`, `Hold`, `Attack`, and `Colonise`.
- Orders have submit tick, execute-after tick, processed tick, status, and rejection reason.
- Submission-time validation prevents clearly invalid moves and self-attacks.
- Pending orders can be cancelled before their execution tick by the owning empire.
- Processing-time validation rejects orders that became invalid before execution.
- Colonisation costs 100 population at processing time and is rejected if the fleet leaves, the empire loses leading influence, population is insufficient, or an outpost already exists.

### Movement

- Fleets can move only along linked systems.
- One-tick links move immediately during the processing tick.
- Multi-tick links put fleets in transit with a destination and arrival tick.
- Fleet arrivals generate factual events.

### Combat

- Combat is intentionally simple.
- An attack order engages hostile active fleets in the same system.
- The current resolver uses deterministic pseudo-randomness seeded from Cycle, tick, system, and fleet IDs.
- The deterministic seed contract is documented in `docs/determinism.md`; seeded galaxy generation stabilises layout fields, while combat determinism is based on persisted IDs and tick number.
- Battle records store participants, ships before battle, losses, outcome, and fact JSON.
- Combat events are generated from battle facts.

### Diplomacy Foundation

- Empire pairs can persist Neutral, War, Non-Aggression Pact, or Alliance states; an absent row means Neutral.
- Relationship pairs are canonical and unique per Cycle.
- A resolved attack records a diplomatic aggression event.
- Attacking through a Non-Aggression Pact or Alliance cancels that relationship to Neutral and records a separate treaty-cancellation event.
- Attacks do not automatically create War; the attacked empire's escalation decision remains future work.
- No player-facing diplomatic actions or alliance effects exist because Q013-Q022 have not yet defined their lifecycle and behaviour.

### Admirals And Named Figures

- Seeded empires start with a named admiral assigned to their home fleet.
- Fleets can have one assigned admiral.
- Admirals track reputation score and status: active, retired, killed, missing, or legendary.
- Battle resolution records admiral battle history for assigned commanders, including role, outcome, ships commanded, ships lost, reputation change, status after battle, and whether the battle created a famous system association.
- Destroyed assigned fleets mark their admiral killed.
- High-reputation or famous admiral battle histories increase Chronicle battle importance.
- Admiral battle reports are written as factual events and surfaced in CLI fleet/admiral output and dashboard fleet panels.

### Chronicle

- Important battles receive an importance score.
- Score inputs currently include total losses, system strategic value, historical significance, underdog result, very large loss counts, and notable admiral battle history.
- Battles above the current threshold become Chronicle entries.
- Chronicle entries store factual summaries and narrative text separately from raw battle facts.
- Narrative text is currently deterministic template prose generated from a battle narrative source DTO, not AI-generated prose.
- Generated battle prose is validated for required facts before a Chronicle entry is returned: participants, system, tick, losses, outcome, and importance.
- Chronicle entries store narrative generation status, generation context JSON, generated-at time, and failure reason fields so future queued/AI generation has a persistence shape.

### API And Dashboard

- The API exposes current Cycle, last tick summary, empire summary, galaxy, system detail, fleets, fleet detail, order queue, movement orders, attack orders, colonisation orders, order cancellation, priorities, recent events, and Chronicle entries through explicit response DTOs rather than domain entities.
- The API has development auth: `/auth/login` creates or finds a local player and empire, assigns a `Player` or `Admin` role, and issues an HttpOnly development cookie.
- `/auth/session` restores the current development-auth session for the dashboard.
- Player order and priority mutations derive empire authority from the authenticated player context.
- Admin players can inspect all fleets/orders and can act for an empire for local support/debugging.
- The public website is served from `/`.
- The browser dashboard is served from `/app.html` and uses the development-auth session to render the map, selected-system details, colonial outposts, selected-fleet details, resources, priority editing, fleets, fleet admirals, order queue, events, Chronicle placeholder/content, and order forms.
- Player read endpoints apply first-pass fog-of-war filtering: the full map structure remains visible, exact presence and local fleet details are only returned for systems where the player has an active fleet, and recent events, last-tick summaries, and Chronicle entries are filtered through the same visibility model.
- Admin development users bypass fog-of-war filtering for local support/debugging.
- System summary/detail responses expose historical significance, and the dashboard marks historically significant systems on the map.
- Tick execution is intentionally absent from ordinary player APIs. A development-admin-only endpoint supports private-alpha operation and local testing.

### Persistence

- `IGameStateStore` is the current persistence boundary used by the CLI and API.
- The default state store remains JSON-file-backed for zero-service local development.
- File locking prevents two local writers from mutating the JSON state file at the same time.
- A SQL Server schema/bootstrap image exists under `database/sqldockerdeploykit`.
- `Cycles.Infrastructure.SqlServer` can read, replace, and update `GameState` through SQL Server.
- SQL Server persists per-tick `EmpireMetrics` snapshots for later Cycle-end history work.
- SQL Server persists final `CycleRankings` for completed Cycles.
- SQL Server persists selected `CycleMajorEvents` for completed Cycle history.
- SQL Server persists `SystemHistoricalSignals` for completed Cycle system-history inputs.
- SQL Server persists Chronicle narrative generation status and context snapshot fields.
- SQL Server persists admirals, fleet admiral assignments, and admiral battle histories.
- SQL Server migration `011_add_colonial_outposts` persists one colonial outpost per empire/system.
- SQL Server migration `012_add_diplomatic_relationships` persists one canonical relationship per Cycle/empire pair.
- SQL Server schema migrations are plain SQL scripts under `database/migrations`.
- The CLI exposes `db init`, `db migrate`, and `db status` for SQL Server schema setup and inspection.
- The guarded CLI `db profile` command measures generic SQL replace/load/update operations against the focused tick path on a disposable database.
- Applied SQL migrations are tracked in `dbo.SchemaMigrations`.
- SQL Server updates run inside a transaction protected by `sp_getapplock`.
- Generic SQL Server updates load the whole prototype state, then synchronise mapped rows with targeted deletes and upserts; this remains a bridge, not the final application-service/repository model.
- SQL-backed CLI tick execution now uses a dedicated tick runner that loads a focused tick workspace for the active Cycle: cycle metadata, systems, links, empires, resources, priorities, admirals, fleets, due pending orders, due queued ship construction, and running tick logs.
- The SQL tick runner persists tick outcome rows for the active Cycle without loading historical events, battle records, Chronicle entries, future orders, completed construction, old tick logs, or running the generic missing-row deletion pass.
- The SQL tick runner acquires a transaction-scoped per-Cycle application lock named `Cycles.Tick.{CycleID}` before loading or writing tick state. Generic whole-state SQL mutations still use the broader `Cycles.GameState` application lock.

## Verified Checks

Latest full verification on 2026-07-11 used the repository test helper with SQL Server integration enabled:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14335;Database=CyclesDb;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

Result: 125 tests passed, including domain, API, worker, operational-diagnostics, full-Cycle simulation, SQL-profile validation, migration, and live SQL Server integration coverage. Migration `012_add_diplomatic_relationships` was applied through the CLI before the run.

The automated suite includes determinism tests for seeded galaxy layout fields and combat resolution with stable persisted IDs.
The balance scenario suite verifies repeatable reports, existing-rule coverage, non-negative resources, invalid inputs, and an explicit retained-record stop for long diagnostic runs.
The tick suite verifies focused-working-copy equivalence with the former full-clone transaction, rollback of facts appended before a late failure, and completion of a sustained-conflict 2,160-tick Cycle retaining more than 100,000 records.
The test helper uses a temporary `BaseOutputPath` so the suite can run even when a local `Cycles.Api` process has the normal build output locked.

Additional smoke checks performed during the MVP build-out:

- CLI seed/tick/show against a temporary state file.
- API health, current Cycle, and galaxy endpoints.
- Browser dashboard desktop and mobile layout checks.
- Browser dashboard move-order submission.
- API development-auth and player/empire authorisation boundary tests.
- API response-contract regression test preventing domain-entity leakage.
- Scheduled worker smoke run against an isolated JSON state file.
- Colonisation dashboard flow and development-admin tick control in the in-app browser before the response-DTO hardening pass.
- CLI `show` and `tick` against SQL Server.
- API `/cycles/current` against SQL Server.
- Opt-in SQL Server integration tests with `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`.
- SQL Server recovery repair/clear/retry persistence, including failed and completed attempts for the same tick.
- SQL Server successor-Cycle round-trip with final rankings and player continuity.
- CLI operational-diagnostics smoke check against a ticked JSON state.
- GitHub Actions Linux build/test, CLI/API smoke, migration, and SQL Server integration jobs.
- Guarded SQL state profiling against a disposable local SQL Server database, including refusal without `--confirm-replace`.

SQL checks rerun after the migration and SQLDockerDeployKit alignment work:

- SQLDockerDeployKit-style SQL Server 2022 image build.
- Disposable `CyclesDb` container startup, migration application, healthcheck, and seed verification.
- CLI `db status` and `show` against the disposable SQL Server container.
- Full test suite with SQL integration enabled through `dotnet test --environment CYCLES_SQL_INTEGRATION_CONNECTION_STRING=...`.

## Known Limitations

These are known gaps, not defects in the current MVP claim:

- SQL Server integration coverage is opt-in and currently covers state-store round trip, order/tick persistence, and duplicate running-tick rollback.
- The SQL Server tick runner still uses an in-memory `GameState` workspace for domain rules instead of a separate `Cycles.Application` tick use case or provider-neutral repository abstraction.
- Development auth is intentionally not production authentication or a multiplayer security boundary.
- Fog-of-war is only a first-pass active-fleet visibility model. There are no sensors, partial estimates, delayed discoveries, or nuanced public/private Chronicle redactions yet.
- The scheduled worker is a first operational host only; it has no production deployment, health endpoint, leader election, or multi-Cycle scheduling policy yet.
- No real deployment story.
- Cycle-end ranking persistence, selected major-battle preservation, first historical-significance updates, dedicated historical-signal records, and first next-Cycle continuity generation exist, but there is no richer reset policy, successor diplomacy, or AI-written inter-Cycle history yet.
- Colonisation has no capture, destruction, migration, infrastructure, comeback, or cross-Cycle inheritance rules yet.
- Industry spending only drives the first simple ship construction loop; infrastructure and logistics effects are not implemented.
- Diplomacy has no player-facing offers, acceptance, declarations, alliance mechanics, shared visibility, Chronicle criteria, or cross-Cycle memory yet.
- No broader technology tree, doctrine choices, cloaking, detection, or logistics.
- Admirals are first-pass fleet commanders, not a full character-management system; there are no promotions, transfers, succession rules, biographies, or retirement workflows yet.
- No AI narrative generation or asynchronous narrative worker; Chronicle battle reports currently use validated deterministic templates and store generation metadata synchronously.
- No historical-system evolution across Cycles.
- No multiplayer security boundary.
- Combat is deliberately primitive and not balanced.
- In-memory tick execution uses a focused transactional working copy: mutable Cycle entities are cloned, append-only facts are rolled back on failure, and immutable history is not rebuilt on every tick.
- The dashboard is a prototype, not a full game client.

## Current Development Priority

The partial Q001-Q012 product-owner response selected population/colonisation as the next headline system, strategic choice as the next test goal, and a private alpha as the delivery target. The first population-funded colonisation loop is implemented and verified through Core, JSON, SQL Server, authenticated API endpoints, and the dashboard. Private-alpha operation now also has a scheduled worker and a development-admin manual tick control without exposing simulation execution to ordinary players.

All implementation directly authorised by Q001-Q012 is now present. The next stage should therefore:

1. exercise colonisation balance over repeated ticks before adding capture, destruction, or infrastructure mechanics;
2. keep player-facing diplomacy deferred until Q013-Q022 define treaty lifecycle, declarations, alliance effects, visibility, and Chronicle treatment;
3. keep adding live SQL Server integration verification around every new migration and focused tick write;
4. choose production authentication, admin provisioning, hosting, worker health, and backup boundaries before any untrusted online test;
5. answer the doctrine, combat, narrative, visibility, continuity, and JSON-lifecycle questions before expanding those systems.

## Definition Of The Next Stable State

The repository has reached the locally runnable stable state defined by these checks:

- a fresh checkout can restore, build, test, seed, tick, and run the API from documented commands;
- simulation state can be persisted in a relational store;
- the same due order cannot be processed twice;
- one tick per Cycle is enforced by storage-level locking or an equivalent mechanism;
- a completed Cycle can create a successor Cycle from preserved historical facts;
- population creates an observable strategic choice through a persisted colonisation loop;
- core behaviours are covered by deterministic automated tests;
- the dashboard remains able to view state and submit orders against the new persistence layer.
