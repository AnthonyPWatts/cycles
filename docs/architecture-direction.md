# Architecture Direction

Last updated: 2026-07-14

Cycles is server-authoritative and simulation-first. Clients submit intentions and read permitted state. A tick host decides outcomes, persistence stores authoritative facts, and narrative systems interpret those facts only after the simulation has resolved them.

## Invariants

1. Player orders cannot choose simulation outcomes; ordinary production players cannot trigger authoritative ticks.
2. One authoritative tick for a Cycle is processed at a time.
3. Failed ticks do not partially apply state and block further execution until explicitly recovered.
4. Influence is derived from presence and modifiers, not stored as binary system ownership.
5. Events, battles, rankings, and history are grounded in structured facts.
6. Narrative text cannot alter or contradict simulation facts.
7. `Cycles.Core` remains independent of ASP.NET Core and database packages.
8. New systems extend the existing order, influence, event, and history boundaries instead of bypassing them.

## Current Shape

```text
Browser
   |
   v
Cycles.Api ---- OIDC/cookie identity outside Development
   |            Cycles-owned admission, empire, and admin authority
   |                         |
   |                         v
   +---- IGameStateStore.RunTick <---- Cycles.Worker / Cycles.Cli
                  |
                  v
            SQL Server store
                  |
                  v
       focused Cycle tick workspace

Operator CLI <---- versioned validated JSON ----> SQL Server

Cycles.Core owns simulation rules used by every host and store path.
Events, battles, Chronicle entries, metrics, and Cycle history are outputs.
```

The API also uses the generic store for authenticated state queries and player/admin mutations. SQL-backed ticks use a narrower path than those generic mutations.

## Project Boundaries

### `Cycles.Core`

Owns domain models, validation, simulation rules, influence, economy, combat, Chronicle scoring, Cycle completion, continuity, and persistence interfaces.

It must not depend on database providers, HTTP concerns, authentication providers, file-system configuration, or narrative-service clients.

### `Cycles.Api`

Owns HTTP contracts, Development authentication, external OIDC/cookie authentication, local player/admin authorisation, visibility filtering, dashboard hosting, and player intention submission.

Ordinary order endpoints must not run ticks. The protected tick endpoint invokes the same authoritative `IGameStateStore.RunTick` boundary as the Worker and does not implement simulation logic in the API. It is available to admins in every environment and, temporarily, to any authenticated player in Development. That capability does not promote the actor or bypass normal visibility and empire ownership.

Player API contracts use camelCase property names and camelCase string enums; numeric enum values are rejected. Handled failures retain meaningful HTTP status and expose stable machine-readable codes alongside safe human-readable messages, with optional validation detail and trace correlation. Clients must not branch on message wording.

Outside Development, OIDC proves an external issuer/subject identity and a secure application cookie restores the session. Exact configured identities govern admission; Cycles-owned player records govern empire and Admin authority. Provider role/group/email/display claims cannot elevate authority. `/` and `/health` remain public in the normal route contract, while `/app.html` challenges and verifies admission. Every game API independently resolves the local actor.

The dashboard's next-test scale is the curated 16-sector, 280-system, four-empire galaxy. Sector membership, gateway context, bridge routes, semantic Galaxy/Sector/Local ranges, search, and the overview navigator are one client contract rather than cosmetic grouping. The current SVG map and bounded client-side lists do not promise support beyond that canonical scale or for arbitrary dense topologies; a larger target requires fresh payload, filtering, rendering, and interaction evidence.

Desktop and laptop browsers are the primary dashboard command surface. Responsive layouts must preserve a readable narrow-screen core loop for authentication, status and History, priorities, fleet selection, and basic order submission and cancellation without page-level horizontal scrolling. Equal mobile optimisation, a touch-first interaction model, and native mobile clients remain deferred until usage evidence selects mobile as a primary surface.

The resumable Day One guide is the primary in-dashboard training path and uses real controls and authoritative outcomes. It teaches priorities, visibility, order lifecycle, Events versus Chronicle, and the current tick/Cycle boundary using the current Command, Galaxy, Fleets, and History views. Keep contextual hints local and concise rather than creating a parallel help application.

### `Cycles.Worker`

Owns scheduled due-tick execution. It reads Cycle cadence, checks immediately on startup, polls on a configurable interval, and runs at most one due tick per check.

Production use still needs health reporting, leader election or equivalent singleton ownership, multi-Cycle policy, graceful shutdown expectations, and deployment monitoring.

### `Cycles.Cli`

Owns local development and operator workflows: seeding, inspection, manual ticking, migrations, recovery, Cycle completion and continuation, diagnostics, profiling, and balance scenarios.

It is an administrative convenience, not the scheduled production host. Complete state transfer remains here: versioned JSON is validated before SQL import, replacements require explicit confirmation, and private payloads are not exposed through player endpoints.

### `Cycles.Infrastructure.SqlServer`

Owns SQL Server connection handling, migrations, generic state persistence, the focused tick workspace, targeted tick outcome writes, and transaction-scoped application locks.

### Optional `Cycles.Application`

Do not add an empty application project for architectural symmetry. Extract application services when use-case orchestration demonstrably outgrows Core and the current store boundary, or when another persistence provider needs provider-neutral repositories that cannot remain clear within the existing shape.

## Tick Transaction Model

An authoritative tick follows this logical sequence:

1. acquire the Cycle lock;
2. determine the next tick number;
3. begin transactional work and record a running attempt;
4. load the state required for the tick;
5. process arrivals, construction, resource generation, research, spending, and due orders;
6. resolve combat and diplomacy consequences;
7. append events, battles, Chronicle entries, admiral history, and metrics;
8. mark orders and the tick attempt complete;
9. commit and release the lock.

Failure rules:

- do not expose partially applied outcomes;
- record the failed attempt and diagnostics;
- mark the Cycle `RecoveryRequired`;
- reject another tick until an operator repairs and clears or retries the Cycle;
- preserve failed attempts when a repaired tick later succeeds with the same tick number.

The in-memory path uses a focused transactional working copy and rolls back appended facts on failure. The SQL path provides database transactionality and a per-Cycle `sp_getapplock` named `Cycles.Tick.{CycleID}`.

## Persistence Position

SQL Server is the selected runtime path. Q116 required the cost-capped trusted playground to migrate its current state and prove restoration from database-native backup; Q117 selected the existing SQL Server provider on managed Azure SQL for that cutover and first online test. Q119 demotes JSON to explicit, versioned import/export, validation, offline inspection, fixtures, and migration evidence. Q130 defines complete state transfer as sensitive operator/admin support tooling rather than a player-facing save/restore feature; any future sharing format must be separately designed and redacted. Following the completed cutover sequence in issues #126 and #125, API, Worker, and gameplay/operator CLI commands require SQL; no implicit file-store selection remains.

SQL Server-specific features are not categorically forbidden. They may be used inside `Cycles.Infrastructure.SqlServer` and SQL migrations when Azure SQL supports them and they materially improve correctness, consistency, measured performance, or operations. `Cycles.Core` and `IGameStateStore` remain provider-neutral, and material portability implications must be documented. The existing transaction-scoped `sp_getapplock` is the accepted model: a concrete concurrency guarantee kept behind the provider boundary.

Current SQL paths:

- generic `Replace` and `Update` load the prototype `GameState` and synchronise mapped rows under the broad `Cycles.GameState` lock;
- `RunTick` acquires a per-Cycle lock, loads only the active tick workspace, and persists targeted outcomes without loading unrelated retained history;
- plain SQL migrations under `database/migrations` are applied explicitly and recorded in `dbo.SchemaMigrations`.
- external issuer/subject correlation and admin-role audit records are persisted by migration 013.

The generic path is a bridge for low-frequency API/admin mutations. Profile a new high-frequency caller before placing it on that path. Do not start a broad repository rewrite without evidence that the existing orchestration boundary is the problem.

## Facts, Visibility, And Narrative

Events and battle records are factual, queryable records tied to Cycle, tick, system, empire, and source identifiers where possible. `FactJson` remains flexible internal storage while fact shapes evolve. Introduce a typed or validated contract when a payload becomes mechanically consumed, queried, migrated, or publicly exposed; do not launch a broad fact migration merely to type unstable diplomacy or narrative shapes. Ordinary player responses use display text or purpose-built typed detail rather than raw fact storage; the opening briefing is the first typed mechanical consumer.

Chronicle entries select historically important facts. Factual summaries, narrative text, importance scores, source identifiers, generation status, and generation context remain separate. Future AI generation must run outside the tick transaction and must fail without affecting gameplay.

Players see the full galaxy topology but exact local presence, fleets, events, last-tick facts, and Chronicle entries only where active-fleet visibility allows. Admins bypass that filter for trusted support. The temporary Development turn capability changes timing control, not authorisation over player data or simulation outcomes. Non-Development identity uses external OIDC and a secure application cookie while preserving the same local actor/empire boundary.

## Deployment Gate

No production deployment path is complete. The cost-capped trusted playground hosts the Development build behind restricted access, stores authoritative state in managed Azure SQL, and relies on manual Development turns. Its managed-SQL cutover, seven-day point-in-time retention, isolated restore proof, and mandatory SQL host configuration are complete. It remains an invited-test exception rather than the production or private-alpha architecture. Before an untrusted online test, complete the remaining operational gates:

- a configured external provider/proxy path using the implemented OIDC, invited-player, dashboard, and audited-admin boundaries;
- Worker health, leadership, and multi-Cycle behaviour;
- secrets, logging, monitoring, and incident diagnostics;
- deployed recovery administration and a tested incident process around Azure SQL restore.

SQL is no longer a pending deployment gate, but it does not make the hosted Development exception production-ready. Treat both local execution and the trusted playground as pre-alpha Development targets until the remaining identity, Worker, monitoring, security, and recovery gates are closed.
