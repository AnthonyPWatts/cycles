# Decision Log

Last updated: 2026-06-23

This file records decisions that shape future implementation. It is intentionally lightweight; add entries when a choice would otherwise be rediscovered or debated repeatedly.

## 2026-06-23: Build A Runnable MVP Before Production Infrastructure

Decision: start with a local .NET MVP using Core, CLI, API, dashboard, and a JSON state store.

Reasoning:

- The first risk was whether the design could become an executable loop.
- External services and database packages would slow initial validation.
- The technical design allowed a console/worker tick processor before full API/UI maturity.

Consequences:

- The current implementation is useful for proving behaviour, not production durability.
- Relational persistence is the next major infrastructure step.
- Documentation must be clear that JSON persistence is temporary.

## 2026-06-23: Keep Tick Execution Out Of The Public API

Decision: the API accepts orders and exposes state; the CLI runs ticks.

Reasoning:

- The design says clients submit intentions and the tick engine resolves outcomes later.
- Running ticks through ordinary public endpoints risks confusing client actions with authoritative simulation.
- A future worker can replace the CLI without changing the player-facing API principle.

Consequences:

- Local development uses CLI plus API/dashboard together.
- The future `Cycles.Worker` should own scheduled tick execution.

## 2026-06-23: Treat Influence As Derived, Not Ownership

Decision: system control is represented by calculated presence/influence rather than stored ownership.

Reasoning:

- The vision depends on coexistence, border ambiguity, blockades, and cold wars.
- Ownership would push the design toward conventional map painting.
- Future systems can modify influence without replacing the territorial model.

Consequences:

- Future mechanics should add influence inputs/modifiers.
- UI should show influence/presence rather than owned/unowned binary state.

## 2026-06-23: Store Facts Before Narrative

Decision: events, battle records, and Chronicle entries store structured facts before any stylised narrative layer exists.

Reasoning:

- The Chronicle must originate from real gameplay.
- AI or generated prose must not decide outcomes.
- Future narratives need source facts for validation.

Consequences:

- `FactJson` exists as a flexible early form but may need typed fact models later.
- Narrative generation should be asynchronous and non-authoritative.
- Chronicle entries should retain source event/battle IDs.

## 2026-06-23: Prefer SQLite As The First Relational Store

Decision: the first relational implementation should likely use SQLite unless new constraints appear.

Status: superseded for the current implementation by the SQLDockerDeployKit/SQL Server path below.

Reasoning:

- It satisfies the need for real tables, transactions, indexes, and constraints.
- It keeps local development simple.
- It avoids choosing hosted infrastructure before the schema stabilises.

Consequences:

- The schema and persistence boundary should avoid SQLite-specific assumptions where practical.
- PostgreSQL or SQL Server can be evaluated after relational tick processing is proven.

## 2026-06-23: Add A SQLDockerDeployKit-Style SQL Server Bootstrap Before Application Persistence

Decision: add a Cycles-specific SQL Server container bootstrap under `database/sqldockerdeploykit` before moving the application off JSON persistence.

Reasoning:

- The user explicitly wanted database progress and preferred using SQLDockerDeployKit if feasible.
- SQLDockerDeployKit builds locally and provides a useful SQL Server container initialisation pattern.
- A schema-first SQL Server image gives the project a concrete relational target while keeping the application persistence migration scoped to a later commit.

Consequences:

- The repository now has a working `CyclesDb` SQL Server bootstrap with schema and seed data.
- Application runtime remains JSON-backed until persistence interfaces and SQL-backed stores are implemented.
- Two SQLDockerDeployKit issues were raised from the adaptation work:
  - https://github.com/AnthonyPWatts/SQLDockerDeployKit/issues/7
  - https://github.com/AnthonyPWatts/SQLDockerDeployKit/issues/8

## 2026-06-23: Add SQL Server As The First Application Persistence Bridge

Decision: add `Cycles.Infrastructure.SqlServer` and let the CLI/API opt into SQL Server while JSON remains the default store.

Reasoning:

- The SQLDockerDeployKit-style container now gives local development a working SQL Server target.
- A small `IGameStateStore` boundary lets API and CLI share the same persistence choice.
- A snapshot-style SQL writer is enough to prove relational read/write, transaction, and locking behaviour before designing incremental repositories.

Consequences:

- SQL Server persistence is now usable via `sqlserver:<connectionString>` in the CLI and `ConnectionStrings:Cycles` in the API.
- SQL writes are protected by a transaction-scoped `sp_getapplock`.
- Follow-on persistence work should add migration/versioning support, then move beyond full-state synchronisation toward focused repository operations.

## 2026-06-23: Do Not Add Future Feature Systems Before Hardening The Simulation Spine

Decision: admirals, diplomacy, technologies, cloaking, and AI narrative generation should wait until persistence and tick semantics are stronger.

Reasoning:

- These systems depend on reliable events, battles, influence, and history.
- Adding them before transactionality and tests would amplify churn.
- The MVP already exposes the key extension points.

Consequences:

- Stage 1 and Stage 2 should focus on tests, persistence, locking, and recovery.
- Feature roadmap remains visible in `backlog.md` without driving immediate implementation.

## 2026-06-24: Continue SQL Server As The Relational Target

Decision: keep SQL Server as the primary relational implementation for the next stage, do not add SQLite now, and treat JSON as future import/export or development support rather than the intended runtime store.

Reasoning:

- The SQL Server bootstrap and application store already work locally.
- Adding SQLite would split persistence attention before the SQL-backed tick model is strong.
- A later deployment may move to PostgreSQL or MySQL to avoid SQL Server licensing, so the persistence design should avoid unnecessary provider-specific assumptions.

Consequences:

- Stage 2 should continue through the SQL Server infrastructure project.
- JSON remains useful for local/dev convenience but should not drive the next architecture decisions.
- Future repository abstractions should leave room for another relational provider after the schema and tick flow stabilise.

## 2026-06-24: Use Plain SQL Migrations With A Small Runner

Decision: add idempotent plain SQL migrations, track them in `dbo.SchemaMigrations`, and expose `db init`, `db migrate`, and `db status` through the CLI.

Reasoning:

- The current schema is small and does not yet justify EF Core migrations or another production dependency.
- Plain SQL keeps the Docker bootstrap, CLI tooling, and manual inspection aligned.
- A small runner is enough for local and early shared environments while avoiding destructive startup scripts.

Consequences:

- The SQLDockerDeployKit image now creates `CyclesDb`, applies migrations, then runs idempotent seed scripts.
- `db init` is an alias for `db migrate`; the runner creates the configured database when needed.
- Future schema changes should be added under `database/migrations` and embedded into `Cycles.Infrastructure.SqlServer`.

## 2026-06-24: Replace SQL Full-Table Resets With Row-Level Sync

Decision: keep `IGameStateStore` as the current persistence boundary, but change the SQL Server save path from full table delete/reinsert to targeted row deletion plus update-or-insert writes.

Reasoning:

- The existing store already gives CLI and API one transaction boundary while Stage 2 is still moving.
- Removing full table resets reduces write blast radius without introducing a broad application-layer refactor yet.
- A row-level sync is a useful intermediate step before focused repositories own orders, ticks, fleets, resources, events, battles, and Chronicle records independently.

Consequences:

- `Replace` still removes rows missing from the replacement state, but it does so child-to-parent instead of wiping every mapped table up front.
- `Update` preserves existing row identities and updates only mapped rows, while new events/orders/tick logs are inserted.
- The store still loads the full `GameState`; focused tick repositories remain the next persistence step.

## 2026-06-24: Align Cycles SQL Container With SQLDockerDeployKit SQL Server Provider

Decision: update the Cycles SQLDockerDeployKit-derived container to follow the current SQL Server provider baseline: SQL Server 2022, `mssql-tools18`/legacy `sqlcmd` discovery, `-C` for local certificate trust, configurable readiness polling, and a Docker healthcheck.

Reasoning:

- The upstream SQLDockerDeployKit issues raised during the Cycles adaptation have been addressed in that repository.
- Keeping the local Cycles database bootstrap aligned with that pattern avoids carrying solved startup and tooling issues forward.
- The database bootstrap now exercises the same migration scripts used by the CLI runner.

Consequences:

- The Cycles database image now builds from `mcr.microsoft.com/mssql/server:2022-latest`.
- Startup applies migrations before seed scripts and reports container health through Docker.
- Live SQL integration tests were verified against a disposable SQL Server 2022 container.
