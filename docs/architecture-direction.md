# Architecture Direction

Last updated: 2026-07-13

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
Cycles.Api ---- development tick capability
   |                         |
   |                         v
   +---- IGameStateStore.RunTick <---- Cycles.Worker / Cycles.Cli
                  |
          +-------+--------+
          |                |
          v                v
     JSON store       SQL Server store
                           |
                           v
              focused Cycle tick workspace

Cycles.Core owns simulation rules used by every host and store path.
Events, battles, Chronicle entries, metrics, and Cycle history are outputs.
```

The API also uses the generic store for authenticated state queries and player/admin mutations. SQL-backed ticks use a narrower path than those generic mutations.

## Project Boundaries

### `Cycles.Core`

Owns domain models, validation, simulation rules, influence, economy, combat, Chronicle scoring, Cycle completion, continuity, and persistence interfaces.

It must not depend on database providers, HTTP concerns, authentication providers, file-system configuration, or narrative-service clients.

### `Cycles.Api`

Owns HTTP contracts, development authentication and authorisation, visibility filtering, dashboard hosting, and player intention submission.

Ordinary order endpoints must not run ticks. The protected tick endpoint invokes the same authoritative `IGameStateStore.RunTick` boundary as the Worker and does not implement simulation logic in the API. It is available to admins in every environment and, temporarily, to any authenticated player in Development. That capability does not promote the actor or bypass normal visibility and empire ownership.

### `Cycles.Worker`

Owns scheduled due-tick execution. It reads Cycle cadence, checks immediately on startup, polls on a configurable interval, and runs at most one due tick per check.

Production use still needs health reporting, leader election or equivalent singleton ownership, multi-Cycle policy, graceful shutdown expectations, and deployment monitoring.

### `Cycles.Cli`

Owns local development and operator workflows: seeding, inspection, manual ticking, migrations, recovery, Cycle completion and continuation, diagnostics, profiling, and balance scenarios.

It is an administrative convenience, not the scheduled production host.

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

SQL Server is the selected runtime path. Q116 requires the cost-capped trusted playground to migrate its current state before further tester invitations and prove restoration from database-native backup; Q117 selects the existing SQL Server provider on managed Azure SQL for that cutover and first online test. Q119 demotes JSON now to explicit, versioned import/export, validation, offline inspection, fixtures, and migration evidence. After the safe cutover sequence in issues #126 and #125, API, Worker, and normal local-development runtime hosts require SQL rather than silently selecting a file store.

SQL Server-specific features are not categorically forbidden. They may be used inside `Cycles.Infrastructure.SqlServer` and SQL migrations when Azure SQL supports them and they materially improve correctness, consistency, measured performance, or operations. `Cycles.Core` and `IGameStateStore` remain provider-neutral, and material portability implications must be documented. The existing transaction-scoped `sp_getapplock` is the accepted model: a concrete concurrency guarantee kept behind the provider boundary.

Current SQL paths:

- generic `Replace` and `Update` load the prototype `GameState` and synchronise mapped rows under the broad `Cycles.GameState` lock;
- `RunTick` acquires a per-Cycle lock, loads only the active tick workspace, and persists targeted outcomes without loading unrelated retained history;
- plain SQL migrations under `database/migrations` are applied explicitly and recorded in `dbo.SchemaMigrations`.

The generic path is a bridge for low-frequency API/admin mutations. Profile a new high-frequency caller before placing it on that path. Do not start a broad repository rewrite without evidence that the existing orchestration boundary is the problem.

## Facts, Visibility, And Narrative

Events and battle records are factual, queryable records tied to Cycle, tick, system, empire, and source identifiers where possible. `FactJson` remains flexible prototype storage; introduce typed fact contracts only when the relevant diplomacy and narrative shapes have stabilised.

Chronicle entries select historically important facts. Factual summaries, narrative text, importance scores, source identifiers, generation status, and generation context remain separate. Future AI generation must run outside the tick transaction and must fail without affecting gameplay.

Development-auth players see the full galaxy topology but exact local presence, fleets, events, last-tick facts, and Chronicle entries only where active-fleet visibility allows. Admins bypass that filter for trusted support. The temporary Development turn capability changes timing control, not authorisation over player data or simulation outcomes. Production identity and security must preserve the actor/empire boundary without treating the current cookie as a deployable solution.

## Deployment Gate

No production deployment path is complete. A cost-capped trusted playground currently hosts the Development build behind restricted access, uses persistent JSON state, and relies on manual Development turns. It is an invited-test exception rather than the production or private-alpha architecture. Before an untrusted online test, implement the accepted boundaries and remaining operational gates:

- external OIDC, invited-player admission, protected dashboard routing, and audited admin provisioning;
- versioned JSON import/export, the managed Azure SQL cutover, migration rollback, database-native backup, and a proved restore;
- Worker health, leadership, and multi-Cycle behaviour;
- secrets, logging, monitoring, and incident diagnostics;
- explicit recovery administration.

Until those implementations exist, treat both local execution and the trusted playground as pre-alpha Development targets and make no production-readiness claim from the hosted deployment.
