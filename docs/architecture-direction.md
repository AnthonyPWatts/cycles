# Architecture Direction

Last updated: 2026-06-24

## Architectural Intent

Cycles should remain server-authoritative and simulation-first.

Clients submit orders and read state. The simulation decides outcomes during ticks. Persistence stores authoritative state and factual history. Narrative systems interpret facts only after simulation outcomes exist.

The long-term shape should be:

```text
Browser / other clients
        |
        v
API / Auth / Order Submission
        |
        v
Application Services
        |
        +-------------------+
        |                   |
        v                   v
Persistence          Tick Processor
        |                   |
        +---------+---------+
                  |
                  v
       Events / Battles / Chronicle
                  |
                  v
       Narrative generation later
```

## Current Architecture

Current implementation:

- `Cycles.Core` owns domain models and simulation behaviour.
- `Cycles.Cli` manually seeds state, submits orders, runs ticks, and shows summaries.
- `Cycles.Api` exposes state and accepts orders.
- `IGameStateStore` is the current persistence boundary.
- `FileGameStateStore` persists the whole `GameState` as JSON by default.
- `Cycles.Infrastructure.SqlServer` can persist the same prototype state through SQL Server when configured, using row-level deletes and upserts rather than full table resets.
- The dashboard is static HTML/CSS/JavaScript served by the API.

This is acceptable for the initial MVP but should not become the production architecture by inertia.

## Desired Project Boundaries

### `Cycles.Core`

Owns:

- domain models;
- pure simulation rules;
- influence calculation;
- combat resolution;
- Chronicle scoring;
- order validation rules that are independent of transport;
- result types and domain errors.

Should avoid:

- database package references;
- ASP.NET Core dependencies;
- file-system persistence details;
- authentication provider details;
- AI provider calls.

### `Cycles.Application`

Recommended future project.

Owns:

- use cases such as submit order, update priorities, run tick, seed Cycle, query player dashboard;
- transaction orchestration abstractions;
- clock/random abstractions where needed;
- mapping between domain operations and persistence ports.

This layer may be small at first. Add it when persistence work begins, not as an empty abstraction exercise.

### `Cycles.Infrastructure.SqlServer`

Current SQL Server infrastructure project.

Owns:

- SQL Server state-store implementation;
- SQL Server connection-string parsing helpers;
- transaction-scoped application locking for prototype state updates.

Should grow toward:

- schema/migrations;
- incremental repository/store implementations;
- per-Cycle tick locking implementation;
- optional JSON import/export tooling.

### `Cycles.Api`

Owns:

- HTTP endpoints;
- authentication/authorisation;
- API request/response contracts;
- dashboard hosting while the client remains simple.

Should avoid:

- simulation outcomes;
- direct database mutation that bypasses application/domain services;
- accepting empire IDs as authority when player context can derive them.

### `Cycles.Worker`

Recommended future project or host mode.

Owns:

- scheduled or manually triggered tick execution;
- one-tick-at-a-time enforcement through application/persistence services;
- logging and health checks for tick processing.

The CLI can remain a developer convenience, but a worker should become the production tick runner.

## Persistence Direction

The technical design calls for a relational database. The current JSON store should be treated as a convenient prototype/dev store.

Recommended sequence:

1. Keep the `IGameStateStore` boundary while the prototype shape is still moving.
2. Use the SQLDockerDeployKit-style SQL Server container as the local relational target.
3. Replace full-table SQL reset writes with targeted row-level synchronisation.
4. Add schema versioning and a migration/initialisation command.
5. Move tick execution to focused incremental repository operations.
6. Decide later whether PostgreSQL, SQLite, or hosted SQL Server should be the production target.

## Tick Transaction Model

The intended tick model:

1. Acquire lock for active Cycle.
2. Determine next tick number.
3. Begin transaction.
4. Insert `TickLog` with `Running`.
5. Load state needed for the tick.
6. Load due pending orders.
7. Process arrivals.
8. Generate resources.
9. Process orders.
10. Resolve combat.
11. Append events, battles, and Chronicle entries.
12. Mark orders processed/rejected.
13. Mark `TickLog` completed.
14. Commit transaction.
15. Release lock.

Failure rule:

- Failed ticks must not partially apply outcomes.
- If rollback is not possible, the Cycle must enter `RecoveryRequired`.
- A new tick must not start while recovery is required.
- Admin recovery is CLI-only for now. Clearing recovery requires an operator and reason, writes a `RecoveryCleared` event, and can retry the repaired tick with the same tick number while preserving failed tick logs.

## Domain Modelling Notes

### Influence

Influence should remain derived from presence and modifiers. Avoid storing permanent ownership as the primary territorial model.

Allowed future inputs:

- ships;
- home-system protection;
- admirals;
- doctrines;
- logistics;
- cloaking/detection;
- alliances;
- system terrain;
- historical significance.

### Events

Events are factual records of gameplay. They should be concise, queryable, and tied to Cycle/tick/system/empire where possible.

Event text can be player-visible, but event facts should be the source of truth.

### Battle Records

Battle records should contain structured facts:

- participants;
- fleets;
- ships before battle;
- losses;
- outcome;
- system;
- tick;
- deterministic resolution inputs where useful.

Do not rely on display text as the authoritative battle record.

### Chronicle Entries

Chronicle entries are selected historical records. They should point back to source events or battles.

Keep:

- factual summary;
- narrative text;
- importance score;
- entry type;
- source IDs.

Narrative text may become stylised later, but it must not contradict facts.

## API Direction

The API should expose state and accept orders. It should not run tick logic.

Near-term endpoint direction:

- `POST /auth/login` establishes the development-auth session until real auth exists.
- `GET /auth/session`
- `GET /cycles/current`
- `GET /empire`
- `GET /galaxy`
- `GET /fleets`
- `GET /orders`
- `POST /orders/fleet/move`
- `POST /orders/fleet/attack`
- `POST /orders/fleet/cancel`
- `POST /orders/priorities`
- `GET /events/recent`
- `GET /chronicle`

Order endpoints derive player/empire context from the development-auth user rather than trusting arbitrary IDs. Production auth should preserve that actor boundary with a real authentication provider.

## Client Direction

The dashboard should become a functional game surface, not a marketing page.

Near-term priorities:

- state overview;
- map with system details;
- fleet list and fleet detail;
- move/attack order submission;
- pending order list;
- priority controls;
- recent event feed;
- Chronicle view.

Avoid:

- decorative UI that hides state;
- layout that requires frequent scrolling for primary actions;
- client-side simulation;
- optimistic outcomes that contradict the server.

## Testing Direction

Testing should grow in layers:

1. Domain/unit tests for influence, movement, combat, Chronicle scoring.
2. Tick tests for order lifecycle and transactional behaviour.
3. Persistence integration tests using the local SQL Server container.
4. API tests for auth boundaries and endpoint contracts.
5. Browser smoke tests for the dashboard once UI behaviour becomes more important.

Important regression tests:

- A tick cannot complete twice.
- Due orders process once.
- Invalid orders become rejected events.
- Battle facts and events match.
- Chronicle entries preserve source IDs.
- Narrative generation never changes facts.

## Deployment Direction

No production deployment path exists yet.

When deployment becomes relevant, define:

- database choice;
- migration strategy;
- worker scheduling strategy;
- environment configuration;
- logging and monitoring;
- secret handling;
- backup/restore plan;
- admin recovery process.

Do not add deployment complexity before relational persistence and tick recovery are stable.
