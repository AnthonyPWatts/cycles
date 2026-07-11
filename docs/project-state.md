# Project State

Last updated: 2026-07-11

Cycles is a local, runnable private-alpha technical MVP. It proves the server-authoritative loop from galaxy generation through orders, tick resolution, factual history, Cycle completion, and successor generation. It is not yet a production game service or a balanced multiplayer game.

## Capability Summary

| Area | Implemented now | Important limit |
| --- | --- | --- |
| Galaxy | Deterministic seeded systems, routes, home systems, resources, and strategic/history fields. | The dashboard assumes a small galaxy. |
| Tick execution | CLI tick runner, scheduled Worker, development-admin trigger, duplicate-running-tick guards, and explicit recovery state. | Production health, leader election, multi-Cycle scheduling, and deployment policy are undefined. |
| Influence and economy | Fleet-derived influence, home pressure, resource sharing, 100-point priorities, military ship construction, expansion projection, and one research unlock. | Industry and research priorities have no separate direct spending effects; long-run resource sinks are incomplete. |
| Orders | Durable move, hold, attack, colonise, and cancellation lifecycle with submission-time and processing-time validation. | The dashboard does not expose Hold, fleet creation, or fleet splitting. |
| Colonisation | Population-funded outposts that add supported local presence without binary ownership. | No capture, destruction, migration, infrastructure, or cross-Cycle inheritance. |
| Combat | Deterministic first-pass combat, battle facts, losses, events, and admiral outcomes. | Deliberately primitive and not balanced. |
| Diplomacy | Persisted Neutral, War, Non-Aggression Pact, and Alliance states; attacks record aggression and cancel breached treaties. | No player-facing offers, declarations, alliance effects, or shared visibility. |
| History | Chronicle scoring and template reports, per-tick metrics, final rankings, major-battle selection, system history signals, and successor-Cycle continuity. | No asynchronous AI narrative or richer historical-system evolution beyond the first continuity pass. |
| Identity and visibility | Development cookie auth, one player per empire, admin exceptions, and active-fleet fog-of-war. | Not a production authentication or multiplayer security boundary. |
| Persistence | JSON development store, SQL Server store, ordered migrations, transaction locks, focused SQL tick workspace, and targeted tick writes. | Generic API/admin SQL mutations still use the whole-state bridge. |
| Client | Public landing page and playable static dashboard for map, state, priorities, fleets, orders, events, and Chronicle. | Prototype interface, not a finished game client. |

## Implemented Rules

### Cycle And Tick

- A default Cycle lasts 90 days with a 60-minute tick cadence.
- The next simulation step is `CurrentTickNumber + 1`.
- The Worker checks immediately on startup, polls every 30 seconds by default, and runs at most one due tick per poll.
- Tick work uses a focused transactional working copy. Mutable entities are isolated; append-only facts are rolled back if processing fails.
- A failed tick records diagnostics, marks the Cycle `RecoveryRequired`, and blocks further ticks until an operator clears or retries it.
- The CLI exposes `diagnostics`, `recovery`, `recovery details`, `recovery clear`, and `recovery retry`.

### Influence, Resources, And Growth

- Active ships create presence; in-transit and destroyed fleets do not.
- A founding empire has minimum home-system presence of 10.
- Each system divides Industry, Research, and Population output in proportion to effective presence.
- Resources are non-negative stockpiles, with last-generated and last-spent values recorded separately.
- Priority weights must total 100.
- Military priority spends available industry on ships costing 25 industry each. Construction takes three ticks and completes into the home fleet.
- Expansion priority increases effective presence by its percentage.
- At 200 stockpiled research, Survey Projection unlocks once and adds a further 10% effective-presence bonus.
- A colonial outpost costs 100 population and adds five local presence while its empire has an active fleet in the system.

### Orders And Combat

- Movement follows linked systems; longer routes put fleets in transit until the recorded arrival tick.
- Pending orders can be cancelled by the owning empire before their execution tick.
- Processing revalidates state, so an order that was valid when submitted can still be rejected later.
- Attack orders engage hostile active fleets in the attacker's current system.
- Combat randomness derives from persisted Cycle, tick, system, and attacking-fleet identifiers.
- Battle records preserve participants, ships before battle, losses, outcome, and fact JSON.
- Seeded fleets have named admirals. Battle history changes reputation and status; destruction can kill an assigned admiral.

### Chronicle And Cycle History

- Chronicle importance currently considers losses, strategic value, historical significance, underdog outcomes, very large losses, and notable admirals.
- Chronicle battle prose is deterministic template output validated against required source facts and stored separately from factual summaries.
- Completed ticks store each empire's map-control percentage, rank, winner flag, effective presence, and active ships.
- `cycle end` ranks active empires by `MapControlPercent`, stores final standings, selects the top 10% of battles by losses with a minimum of one, and records system history signals.
- `cycle next` creates a successor only after the source Cycle completes and no other Cycle is active. It preserves player continuity and selected famous-system names and significance without carrying mechanical empire advantages.

### API And Dashboard

- Ordinary player endpoints expose filtered state and accept intentions through explicit response DTOs.
- Player mutations derive empire authority from the authenticated development session rather than caller-supplied empire IDs.
- Players can see the full map structure, but exact presence, local fleets, events, last-tick facts, and Chronicle entries are limited by active-fleet visibility. Development admins can inspect all state.
- Ordinary players cannot execute ticks. The protected development-admin endpoint invokes the same authoritative store operation used by the Worker and CLI.

### Persistence

- `IGameStateStore` is shared by the CLI, API, and Worker.
- JSON is the default zero-service development store and uses file locking for writers.
- SQL Server migrations are plain ordered scripts under `database/migrations` and are tracked in `dbo.SchemaMigrations`.
- Migration `012_add_diplomatic_relationships` is the latest schema migration.
- Generic SQL `Replace` and `Update` operations synchronise the mapped prototype state with targeted deletes and upserts under the broad `Cycles.GameState` application lock.
- SQL ticks acquire `Cycles.Tick.{CycleID}`, load a Cycle-scoped workspace, and persist targeted outcome rows without loading or rewriting unrelated history.

## Verification

Latest local verification on 2026-07-11:

```powershell
.\eng\test.ps1
```

Result: **126 tests passed, 0 failed**. The latest GitHub Actions run also passed the Linux build/test job and the migrated SQL Server integration job.

The automated coverage includes:

- simulation, influence, economy, orders, movement, combat, admirals, diplomacy, colonisation, Chronicle, Cycle end, continuity, and determinism;
- development auth, authorisation, visibility, API contracts, admin ticks, and Worker scheduling;
- tick rollback, recovery, duplicate-running-tick prevention, focused-working-copy equivalence, and a 2,160-tick retained-history scenario;
- migration discovery/application and opt-in live SQL Server coverage for round trips, focused tick loading and writes, recovery attempts, Cycle locks, rankings, successor continuity, admirals, diplomacy, and colonisation;
- the running-API alpha journey in `eng/alpha-gameplay-smoke.ps1`.

The SQL integration tests remain opt-in locally through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; CI runs them against a migrated SQL Server service container. See the [SQL Server runbook](../database/sqldockerdeploykit/README.md).

## Current Boundaries

The private alpha is suitable for trusted local testing. Before inviting untrusted online players, the project still needs decisions and implementation for production identity, admin provisioning, hosting, Worker health and leadership, secrets, backup/restore, and operational monitoring.

Gameplay expansion is decision-gated in the [Product Owner Questions](product-owner-questions.md). The [Backlog](backlog.md) separates work that engineering can continue now from work blocked on those calls.
