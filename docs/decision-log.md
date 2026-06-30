# Decision Log

Last updated: 2026-06-30

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

## 2026-06-24: Make Tick Recovery Explicit And Audited

Decision: keep recovery administration in the CLI for now, require an operator and reason to clear a `RecoveryRequired` Cycle, and record recovery clears as high-severity events.

Reasoning:

- Failed ticks should block further simulation until a deliberate admin action confirms the underlying issue has been repaired.
- The CLI is enough for the current local/private prototype and avoids exposing dangerous recovery actions through the player-facing API.
- Failed tick attempts are useful history and should not be deleted just to allow retry.

Consequences:

- `recovery details` prints full diagnostics for failed or running tick logs.
- `recovery clear` marks a repaired Cycle active and writes a `RecoveryCleared` audit event.
- `recovery retry` clears recovery and immediately reruns the same tick number.
- SQL tick log uniqueness now applies to running and completed logs separately, allowing failed retry history to coexist with a later successful tick log.

## 2026-06-24: Define Determinism As A Stable-Facts Contract

Decision: seeded galaxy generation is deterministic for stable layout fields, while combat determinism is based on persisted authoritative IDs plus tick number.

Reasoning:

- Reproducible layout and combat outcomes are useful for tests and investigation, but IDs and wall-clock fields are deliberately generated at state creation time.
- Combat needs to replay from persisted state after recovery, so the combat seed uses Cycle, tick, system, and attacking fleet identifiers.
- Long-term cross-runtime replay would need a versioned PRNG rather than relying on the current `System.Random` implementation.

Consequences:

- A fresh seed with the same integer seed should produce the same system layout and home assignments, but not the same IDs.
- The same persisted state and tick number should resolve combat the same way.
- Changes to seeding or combat algorithms should be treated as simulation behaviour changes and covered by tests.

## 2026-06-24: Keep Source Documents Under Docs Source

Decision: move the original Word source documents into `docs/source` and add GitHub issue forms for bug reports, implementation tasks, and design decisions.

Reasoning:

- The repository root should stay focused on runnable project entry points.
- The Word documents remain useful source artefacts, but the Markdown docs are now the working development layer.
- Issue forms keep future GitHub tracking aligned with the current backlog categories without adding process-heavy templates.

Consequences:

- Original design documents now live under `docs/source`.
- Future documentation updates should keep Markdown current and treat the Word documents as source/reference material.
- New GitHub issues can start from structured forms while still allowing blank issues when needed.

## 2026-06-30: Add The First Strategic Economy Spending Loop

Decision: treat resources as non-negative stockpiles, require strategic priority weights to total 100, and make the first automatic spend effect convert military industry allocation into queued ship construction.

Reasoning:

- Product-owner feedback confirmed stockpiled resources, automatic per-tick spending, no negative resources, and 100-point priorities.
- Industry's long-term role is a mix, but ship construction is the clearest first gameplay effect.
- Research should accumulate toward future unlocks and population should support future colonisation, so this slice records them as stockpiles without adding speculative effects.
- A single early ship type keeps the first implementation testable while leaving room for more powerful ships and longer build times later.

Consequences:

- Each tick records last generated and last spent resource amounts separately from stockpile totals.
- Military priority spends up to its percentage of the current industry stockpile; unspent or insufficient industry remains reserved.
- Ships cost 25 industry each, take 3 ticks to complete, and join the empire's home fleet by default.
- Material ship construction queue and completion facts are written as events.
- The SQL tick runner now loads priorities and persists ship construction rows.

## 2026-06-30: Narrow SQL Tick Loading To A Focused Workspace

Decision: keep `TickEngine` as the authoritative domain behaviour for now, but change SQL-backed tick execution so it no longer loads historical or future rows that the next tick cannot affect.

Reasoning:

- The generic SQL state store remains useful for API/admin mutations while the prototype shape is still moving.
- Tick execution has a clearer access pattern than generic state mutation: current Cycle metadata, map data, empire resources/priorities, fleets, due orders, due construction, and running tick guards.
- Loading historical events, battle records, Chronicle entries, old tick logs, completed construction, and future orders makes the SQL tick path scale like whole-state persistence even though those rows are append-only or not due yet.
- Keeping the current domain engine avoids duplicating simulation rules during the persistence hardening pass.

Consequences:

- SQL-backed CLI ticks now assemble a focused in-memory tick workspace and persist only active-Cycle outcome rows.
- Future pending orders, completed or future construction, historical events, battle records, and Chronicle entries stay in SQL Server without being part of the next tick's read model.
- The generic `IGameStateStore.Update` path still loads and synchronises the full prototype state.
- A future `Cycles.Application` layer can replace the in-memory `GameState` workspace with provider-neutral tick repositories when the use-case boundary is worth extracting.

## 2026-06-30: Keep Expansion As Derived Influence Projection

Decision: make expansion priority increase an empire's effective presence, rather than storing ownership or introducing outposts.

Reasoning:

- The established territorial model treats influence as derived presence, not binary ownership.
- Expansion needed a visible first effect without adding colonisation, discovery, or a new persistence shape.
- Scaling existing fleet and home-system presence keeps the first behaviour transparent and testable.

Consequences:

- Expansion priority can change resource shares and system-detail influence even when fleet counts are unchanged.
- The projection is recalculated from current priorities whenever effective presence is requested.
- Future colonisation or outpost mechanics should build on this model deliberately instead of silently replacing it.

## 2026-06-30: Rank Cycle Winners By Map Control

Decision: define the first Cycle-end winner metric as `MapControlPercent`, calculated from each empire's share of effective presence across all systems.

Reasoning:

- Product-owner direction favours influence across the map rather than a blended score.
- Effective presence already captures active fleets, home-system pressure, and expansion projection without storing ownership.
- A single primary metric keeps the first Cycle-end command explainable.

Consequences:

- Future ranking persistence should store one winner plus ranked standings for all active empires.
- Strategic value, resources, battles, and Chronicle score can become historical categories, but they do not decide the first winner.
- Tie-breaking should be deterministic, with details defined in `ranking-metrics.md`.

## 2026-06-30: Add Manual Cycle-End Ranking Persistence

Decision: add a CLI-owned `cycle end` command that completes an active Cycle, calculates final standings from the current state snapshot, records one winner, and persists standings in `CycleRankings`.

Reasoning:

- Product-owner direction says Cycle ends are manual and effectively freeze the database at cutoff.
- The ranking metric was already defined and per-tick `EmpireMetrics` already prove the calculation shape.
- A CLI command keeps Cycle completion out of ordinary player-facing API actions, matching the existing tick-execution boundary.

Consequences:

- Completed Cycles no longer appear as active Cycles.
- Final standings store rank, winner flag, map-control percentage, total effective presence, active ship count, cutoff tick, and cutoff time.
- Pending orders remain pending at cutoff; only already-processed state affects the final ranking.
- Next-Cycle generation, selected major-event preservation, and historical-system updates remain future Stage 5 work.

## 2026-06-30: Use Development Auth Before Production Auth

Decision: replace the prototype `playerId`/`empireId` dashboard flow with a deliberate development auth boundary: `/auth/login` establishes an HttpOnly development cookie, players have explicit `Player` or `Admin` roles, and player mutations derive the empire from the authenticated context.

Reasoning:

- The next playable test needs player/empire boundaries before any online deployment work.
- Product-owner direction accepts simple development auth for now and requires admins to be distinct from ordinary players.
- Adding ASP.NET Core Identity, OAuth, or OpenID Connect now would be premature while deployment and provider choices are still open.

Consequences:

- Normal player order and priority endpoints no longer trust caller-supplied empire IDs.
- Admin players can inspect all fleets/orders and can act for an empire for local support/debugging paths.
- This is not production security; before deployment, the same actor/role boundary should be backed by a real authentication provider.
- Fog-of-war and event/Chronicle visibility filtering can build on the development actor boundary.

## 2026-06-30: Use Active Fleets For First Fog-Of-War Visibility

Decision: make the first fog-of-war model depend on active fleets. Players can see the full map structure, but exact local presence, local fleet details, events, last-tick facts, and Chronicle entries are filtered to systems where their empire has an active fleet. Admin development users can inspect everything.

Reasoning:

- Product-owner direction explicitly chose active fleets as the first meaning of "resources there".
- This preserves the server-authoritative model while avoiding a speculative sensor, scouting, or intelligence system.
- Filtering read models now is useful before online testing because the development-auth actor boundary already exists.
- The full galaxy remains navigable without revealing exact hidden fleet counts or remote event details.

Consequences:

- Normal players no longer receive global event or Chronicle feeds.
- System details outside active-fleet visibility return map/static system facts without exact influence or local fleet lists.
- Player-owned empire/audit events remain visible even when they have no system location.
- Destroyed fleets, sensors, public summaries, delayed discoveries, and Chronicle redaction remain future design work.
