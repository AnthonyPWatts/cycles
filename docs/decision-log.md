# Decision Log

Last updated: 2026-07-13

This file records decisions that shape implementation. Entries are chronological and describe the decision at the time it was made; later entries may fulfil, extend, or supersede earlier ones. Add an explicit status when reading an old entry as current guidance would be misleading.

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

Status: superseded on 2026-07-11 by the dedicated Worker and protected tick boundary, including a temporary Development-only player capability. The enduring rule is that player orders do not decide outcomes and production players do not trigger ticks.

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

- Database progress was explicitly prioritised, with SQLDockerDeployKit preferred where feasible.
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

Status: fulfilled as a sequencing gate. Persistence and tick semantics were hardened before first-pass admirals and the diplomacy storage/aggression foundation were added. Deeper diplomacy, doctrine, cloaking, and AI narrative remain decision-gated.

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

## 2026-06-30: Use Per-Cycle SQL Application Locks For Dedicated Tick Execution

Decision: make the dedicated SQL tick runner acquire a transaction-scoped SQL Server application lock named `Cycles.Tick.{CycleID}` before loading or writing tick state.

Reasoning:

- The focused tick path now operates on one Cycle at a time, so a global `Cycles.GameState` lock is broader than the tick runner needs.
- SQL Server application locks already fit the current infrastructure and avoid adding a new lock table before worker ownership is defined.
- Generic whole-state mutations still need the broader lock because they can replace or synchronise rows across the full prototype state.

Consequences:

- Explicit SQL-backed ticks for different Cycles no longer depend on the generic whole-state lock.
- A duplicate worker for the same Cycle is blocked before it reads the tick workspace.
- The project still needs a scheduled worker/admin host before this becomes a production tick service.

## 2026-06-30: Keep Expansion As Derived Influence Projection

Decision: make expansion priority increase an empire's effective presence, rather than storing ownership or introducing outposts.

Status: extended on 2026-07-11. Expansion still modifies derived presence; population-funded outposts now add another supported presence source without introducing binary ownership.

Reasoning:

- The established territorial model treats influence as derived presence, not binary ownership.
- Expansion needed a visible first effect without adding colonisation, discovery, or a new persistence shape.
- Scaling existing fleet and home-system presence keeps the first behaviour transparent and testable.

Consequences:

- Expansion priority can change resource shares and system-detail influence even when fleet counts are unchanged.
- The projection is recalculated from current priorities whenever effective presence is requested.
- Future colonisation or outpost mechanics should build on this model deliberately instead of silently replacing it.

## 2026-06-30: Add First Research Doctrine Unlock

Decision: make 200 stockpiled research unlock a first doctrine, Survey Projection, recorded as a one-time `DoctrineUnlocked` event that grants a 10% effective-presence bonus.

Reasoning:

- Product direction says research should accumulate toward future unlocks, but a full technology tree would be premature.
- An event-backed unlock gives research an observable effect without adding a new persistence table or spending model.
- Effective presence is already the main strategic abstraction for influence, resources, and map control, so a small doctrine modifier fits the current design.

Consequences:

- Research can now change resource shares, system-detail influence, and map-control metrics after the unlock.
- The unlock is durable because the event is persisted; the current implementation does not spend research or support multiple doctrine choices.
- Future doctrine/technology systems can replace the event-backed first slice with a dedicated model when the design needs branching unlocks.

## 2026-06-30: Rank Cycle Winners By Map Control

Decision: define the first Cycle-end winner metric as `MapControlPercent`, calculated from each empire's share of effective presence across all systems.

Reasoning:

- Product-owner direction favours influence across the map rather than a blended score.
- Effective presence already captures active fleets, home-system pressure, and expansion projection without storing ownership.
- A single primary metric keeps the first Cycle-end command explainable.

Consequences:

- Future ranking persistence should store one winner plus ranked standings for all active empires.
- Strategic value, resources, battles, and Chronicle score can become historical categories, but they do not decide the first winner.
- Tie-breaking should be deterministic, with details defined in `simulation-reference.md`.

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
- Next-Cycle generation, selected major-event preservation, and historical-system updates were left for follow-on Stage 5 work.

## 2026-06-30: Apply First Historical System Signals At Cycle End

Decision: during manual Cycle completion, increase `HistoricalSignificance` for systems that hosted battles. Each battle adds one point; systems that hosted one of the Cycle's largest-loss battles receive one additional point.

Reasoning:

- Product-owner direction says systems should retain historical significance across Cycle history.
- The existing `HistoricalSignificance` field gives a small useful signal without introducing a dedicated history schema prematurely.
- Repeated conflict and largest battles are both deterministic facts already recorded in `BattleRecords`.

Consequences:

- Repeated battle systems become more historically important when a Cycle is completed.
- The Cycle-completion event fact JSON includes the applied historical signals.
- Dedicated historical signal tables and next-Cycle continuity were left for follow-on Stage 5 work.

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

## 2026-06-30: Preserve Selected Major Battles At Cycle End

Decision: when a Cycle is completed, preserve the top 10% of that Cycle's battles by total ship losses, with a minimum of one battle when any battles occurred.

Reasoning:

- Product-owner direction says Cycle history should preserve enough to maintain history without making every minor skirmish famous.
- Total ship losses are already deterministic battle facts and provide a simple first magnitude signal.
- Keeping selected major events in a dedicated table gives next-Cycle continuity and narrative work a stable historical input without changing battle resolution or Chronicle visibility.

Consequences:

- `CycleMajorEvents` stores selected battle references, rank, total losses, importance score, summary, and fact JSON at manual Cycle cutoff.
- Final rankings still use `MapControlPercent`; selected battles are historical records, not winner criteria.
- Richer selected-system rules and inter-Cycle continuity summaries remain future Stage 5 work.

## 2026-06-30: Persist Dedicated System Historical Signals

Decision: add `SystemHistoricalSignals` as the first dedicated per-system history table and state model.

Reasoning:

- `HistoricalSignificance` is useful for display and scoring, but it compresses the underlying reason into one integer.
- Next-Cycle continuity needs durable facts about why a system became historically important.
- Battle count, total losses, largest local battle, and cycle-largest-battle status are already deterministic Cycle-end facts.

Consequences:

- Completing a Cycle now records one `BattleActivity` signal per affected system.
- Each signal stores the source largest local battle, battle count, total losses, largest local losses, whether the system hosted one of the Cycle's largest battles, and the applied historical-significance increase.
- The CLI `show` command displays system history signals for completed Cycles.
- Richer inter-Cycle summaries remain future Stage 5 work.

## 2026-06-30: Generate Successor Cycles From Completed-Cycle History

Decision: add a CLI-owned `cycle next` command that creates a new active Cycle from a completed Cycle's rankings, major battle selections, and system historical signals.

Reasoning:

- Cycle continuity needs to become executable before deeper history or narrative systems can be tested.
- The existing completed-Cycle facts are enough to preserve player continuity and a small set of famous systems without inventing hidden outcomes.
- Keeping this as a CLI/admin operation matches the manual `cycle end` boundary and avoids exposing lifecycle control through player-facing APIs.

Consequences:

- A successor Cycle can only be generated after the source Cycle is completed and no other Cycle is active.
- Players from the ranked source empires are attached to successor empires in final-rank order, while empire advantages do not mechanically carry over.
- A small selected set of historical systems keeps names, historical significance, and a strategic-value echo in the generated galaxy.
- Richer reset policy, inter-Cycle summaries, and AI-written continuity remain follow-on work.

## 2026-06-30: Start Chronicle Narrative With Deterministic Battle Templates

Decision: generate Chronicle battle-report prose from deterministic templates before adding any AI narrative provider.

Reasoning:

- Chronicle prose should become more readable without allowing generated text to decide or alter simulation outcomes.
- Existing battle, system, and empire facts are enough to produce useful reports and prove the fact boundary.
- Deterministic templates can be tested for required facts and revised safely before introducing asynchronous AI generation.

Consequences:

- `ChronicleEntry.FactualSummary` remains the concise authoritative summary.
- `ChronicleEntry.NarrativeText` now contains deterministic battle-report prose that names the participants, system, tick, losses, and importance context.
- The dashboard displays narrative text while keeping the factual summary visible underneath.
- AI-generated narrative, generation status, provider failures, and asynchronous generation remain future Stage 6 work.

## 2026-06-30: Validate Chronicle Battle Narrative Against Source Facts

Decision: route battle Chronicle prose through a `ChronicleBattleNarrativeSource` DTO and validate generated text for required facts before returning the Chronicle entry.

Reasoning:

- Future AI or asynchronous narrative generation needs a stable source contract separate from mutable domain objects.
- Required facts should be enforced by code, not only by tests that happen to inspect the current template text.
- Validation keeps narrative non-authoritative: generated prose may interpret, but it must still name the core battle facts.

Consequences:

- Battle narrative generation now has a DTO containing source event, battle, system, empire, loss, outcome, and importance facts.
- Generated battle reports must include participants, system, tick number, attacker losses, defender losses, total losses, outcome, and importance score.
- Generation status, queueing, context snapshots, and provider boundaries remain follow-on work.

## 2026-06-30: Persist Chronicle Narrative Generation State

Decision: add narrative generation status, context JSON, generated-at, and failure-reason fields to Chronicle entries.

Reasoning:

- Queued or AI-backed generation needs durable state before a worker/provider boundary can be useful.
- Context snapshots preserve the facts a generated report used, even if later domain rows change or future templates/providers are revised.
- Failure fields make provider failures auditable without changing authoritative battle/event facts.

Consequences:

- Current deterministic battle reports are stored as `Generated` with the source DTO serialized into `NarrativeContextJson`.
- SQL Server schema migration `009_add_chronicle_generation_state` upgrades existing entries with generated defaults and adds a status lookup index.
- Actual async queueing, provider selection, and retry/fallback behaviour remain future Stage 6 work.

## 2026-06-30: Add First Admirals As Fleet-Attached Historical Figures

Decision: add admirals as named fleet commanders with battle history, reputation, status, and famous-system associations, but not as a full character-management system.

Reasoning:

- Stage 7 needed narrative anchors that come from real battles rather than generated prose.
- Fleets are the current strategic actor in combat, so attaching one admiral to a fleet is the smallest useful model.
- Reputation should affect Chronicle importance without changing battle outcomes.
- Character transfers, promotions, biographies, succession, and retirement workflows would be premature before diplomacy and richer history systems exist.

Consequences:

- Seeded empires and newly created development-auth empires receive a named admiral assigned to their home fleet.
- Battle resolution writes `AdmiralBattleHistory` rows and `AdmiralBattleReported` events for assigned commanders.
- Destroyed assigned fleets mark their admiral killed; high-reputation survivors can become legendary.
- SQL migration `010_add_admirals` persists admirals, fleet assignments, and battle history.
- Future work can add transfer/retirement/succession flows without changing the current battle-fact contract.

## 2026-07-11: Make Population-Funded Colonisation The Next Gameplay Slice

Decision: use population/colonisation as the next headline system, with the next playable build proving that influence affects strategic choices.

Reasoning:

- The product-owner response selected population/colonisation over diplomacy, deeper visibility, doctrine, narrative AI, or worker-first development.
- Population already accumulates as an unused stockpile, so a bounded colonisation loop adds a real choice without inventing another resource.
- Engineering remains authorised to choose sensible balance constants and the smallest compatible data model.
- The established territorial model derives influence from presence rather than binary ownership, so colonisation should extend that model instead of replacing it.

Consequences:

- Colonisation requires an active fleet in a non-home system, 100 population, and strictly leading local influence at submission and processing time.
- A colonial outpost provides five local presence only while the owning empire maintains an active fleet there; it does not create permanent fleetless control.
- Colonisation is a next-tick `FleetOrder`, persists in JSON and SQL Server, and is exposed through authenticated API and dashboard controls.
- Population cost and projection strength are explicit constants covered by tests and can be tuned after private-alpha play.
- Colony capture, destruction, migration, infrastructure, comeback effects, and cross-Cycle mechanical inheritance remain follow-on decisions.

## 2026-07-11: Record The First Diplomacy State Boundaries Without Starting The Full Feature

Decision: model the first diplomacy vocabulary as Neutral, War, Non-Aggression Pact, and Alliance, with Neutral as the default, but do not implement player-facing treaty actions until their lifecycle is defined.

Reasoning:

- The partial product-owner response accepted the proposed minimum states and explicitly chose Neutral as the default.
- An attack should not automatically declare War; the attacked empire decides whether to escalate.
- An attack through a pact or alliance cancels that treaty, but the response did not settle offers, acceptance, timing, alliance mechanics, or visibility.

Consequences:

- `DiplomaticRelationships` now stores one canonical empire pair per Cycle, while absence still means Neutral.
- Attack processing records aggression, cancels a pact or alliance to Neutral, and does not silently infer War from combat.
- Migration `012_add_diplomatic_relationships` and the focused SQL tick path persist relationship changes and their factual events.
- Q013-Q022 remain the implementation gate for complete diplomacy orders, alliance effects, shared visibility, and Chronicle behaviour.

## 2026-07-11: Run Due Ticks Through A Dedicated Worker And Admin Boundary

Decision: use `Cycles.Worker` for scheduled tick execution and allow development-admin sessions to trigger the same store-level operation manually.

Status: extended later on 2026-07-11 by a temporary capability for every authenticated Development player. Worker ownership and the authoritative store boundary remain current.

Reasoning:

- Private-alpha operation needs repeatable ticks without conflating player intentions with authoritative simulation execution.
- `IGameStateStore.RunTick` provides one shared operation for JSON and SQL Server, while SQL Server retains its transaction-scoped per-Cycle application lock.
- The Cycle already stores `TickLengthMinutes`; scheduling from the last completed tick avoids an immediate catch-up storm after downtime.
- The existing development-admin role is sufficient for local/private-alpha support but is not a production security boundary.

Consequences:

- The worker checks immediately on startup, then polls every 30 seconds by default, and runs at most one due tick per poll.
- The first tick becomes due at the Cycle start; later ticks become due one configured cadence after the last completed tick.
- Recovery-required and non-active Cycles are not scheduled.
- Ordinary player endpoints still cannot execute ticks.
- Production hosting, worker health, leader election, multi-Cycle policy, and admin provisioning remain deployment decisions.

## 2026-07-11: Use A Focused Transactional Working Copy For In-Memory Ticks

Decision: stop deep-cloning every retained entity before each in-memory tick; clone only mutable tick entities, share append-only fact collections during processing, and roll appended facts back if the tick fails.

Reasoning:

- The original full-state clone allocated about 159 KB at 1,086 retained records and 2.17 MB at 15,086 records on each tick.
- Historical events, battles, Chronicle entries, metrics, completed construction, and processed orders grow throughout a Cycle, making repeated full cloning cumulative quadratic work.
- Existing tick transaction semantics still require partial resource, fleet, order, diplomacy, construction, and admiral changes to be discarded on failure.
- Append-only facts can safely share their lists during the synchronous tick when their original counts are captured and additions are removed before recording the failed attempt.

Consequences:

- Mutable Cycle entities use independent copies; read-only reference data and immutable history are not rebuilt on every tick.
- Existing `GameState.DeepClone` behaviour remains available for callers and tests that require a fully independent state.
- Focused and full-clone tick outcomes are compared by regression test, including combat, diplomacy, resources, orders, events, metrics, and Chronicle facts.
- The deterministic balance planner also builds per-tick indexes and one route map instead of rescanning all retained orders, fleets, outposts, and links for each expedition.
- A 2,160-tick sustained-conflict scenario now completes locally with 102,343 retained records, 25,766 processed orders, and 2,298 battles.

## 2026-07-11: Curate And Guide The Development Opening

Decision: make the normal local opening a fixed, eventful Day One; guide the player through its real controls and outcomes; and temporarily let every authenticated Development player advance the whole galaxy by one turn.

Reasoning:

- The generic seed presented a technically functional but strategically empty first turn: fleets began at isolated home systems and there was little immediate evidence of movement, colonisation, combat, or Chronicle history.
- The first session should teach the actual loop by doing, not by presenting a passive tour or a pre-scripted result.
- Development play needs a fast complete loop before Worker scheduling and production roles matter, but widening the player's role would also leak fog-of-war and cross-empire authority.
- A structured briefing fact gives the guide stable entity identifiers without a new endpoint, schema migration, or brittle display-name matching.

Consequences:

- `development-cold-start-v1` begins the Aurelian player with a move to Nadir Crossing, an outpost opportunity at Pale Harbour, and opposing fleets at Treaty Gate. Both principal empires retain their original 60 total ships.
- The Treaty Gate battle, admiral effects, events, and Chronicle entry are generated by the normal deterministic simulation. Only the starting position and tutorial objectives are curated.
- Normal local seeding and missing Development stores use this scenario; explicit CLI dimensions or a Production host retain generic generation.
- The dashboard guide is stored per player and seeded Cycle instance, re-queries rendered targets, gates progress on the exact live objective orders, and supports pause, skip, resume, and restart.
- The session response advertises whether the actor can advance a turn. Development players receive that capability without role promotion; ordinary Production players do not.
- This exception is deliberately temporary and must be revisited with production authentication, admin provisioning, and scheduled-host policy before alpha.

## 2026-07-13: Keep Lifecycle Controls Narrow By Environment

Decision: accept the Q110 lifecycle-control default. **Advance turn** remains the only ordinary-player Development exception; shared/private-alpha timing belongs to the Worker, manual lifecycle actions are limited to audited admins, and recovery or Cycle transitions remain operator-only until their audit and confirmation UX is designed.

Reasoning:

- The current Development control makes the complete local gameplay loop practical without promoting players to admin or granting cross-empire visibility.
- Recovery, Cycle transitions, pause, and diagnostics have wider operational or data-safety consequences than advancing one trusted local turn.
- Shared testing needs an auditable operational boundary rather than the permissive convenience used by a local Development host.

Consequences:

- The existing Development-player capability is now an accepted product default, not merely an unapproved engineering assumption.
- Ordinary Production players cannot execute ticks, and shared/private-alpha operation should use scheduled Worker timing with audited admin intervention where needed.
- Recovery clear/retry, Cycle end, successor creation, and diagnostics remain CLI/operator actions. Pause remains unimplemented.
- Production identity, admin provisioning, Worker health and leadership, recovery policy, hosting, monitoring, and backup decisions remain open.

## 2026-07-13: Keep Player-Facing APIs DTO-Only

Decision: accept the Q120-Q121 boundary that every player-facing endpoint uses an explicit response DTO and raw domain entities remain internal rather than being returned to the dashboard.

Reasoning:

- Purpose-built contracts prevent internal domain shape changes from silently becoming public client changes.
- The dashboard needs filtered, actor-appropriate representations rather than unrestricted domain graphs.
- The existing response-contract regression test already enforces this boundary across the current API surface.

Consequences:

- New player-facing endpoints must define explicit response contracts and must not expose `Cycles.Core` entities.
- The existing DTO-only implementation satisfies the accepted decision; no compatibility rewrite is required.
- At that decision point, typed fact schemas and the remaining API/dashboard choices were still governed by Q122-Q130 rather than being inferred from Q120-Q121. Q122-Q127 now settle the fact, API, scale, responsive, and training boundaries; Q128-Q130 remain open.

## 2026-07-13: Organise The Dashboard Around Player Tasks

Decision: replace the permanent map-and-scrolling-rail layout with persistent Command, Galaxy, Fleets, and Chronicle views while keeping the Day One guide as the only overlay.

Reasoning:

- The map is valuable when a player is exploring space, but it consumed most of the viewport while the player was setting priorities, issuing orders, or reading history.
- A single rail mixed current decisions, contextual detail, controls, audit history, and Chronicle prose, making important sections depend on retained-list length.
- Stable views make location and purpose obvious without changing API contracts or gameplay rules.

Consequences:

- **Command** opens as the default task surface for resources, priorities, pending commitments, and a concise status pulse.
- **Galaxy** preserves the large interactive map and places selected-system detail beside it.
- **Fleets** groups selection, detail, order entry, and resolved order history. Resolved history renders 20 records at a time.
- **Chronicle** gives narrative history the main reading area and keeps recent factual events alongside it.
- View state is hash-addressable, persists locally, supports browser navigation and Alt+1 through Alt+4, and the guide switches views before highlighting a target.

## 2026-07-13: Make History And Fleet Command Contextual

Decision: refine the task-focused dashboard so **History** contains separate Chronicle and Events tabs, while the selected fleet becomes the single context for compact Move, Attack, and Colonise controls. Put resolved intentions in a dedicated Fleet tab rather than beside every command.

Reasoning:

- Chronicle prose and the factual event audit serve different reading tasks and need independent search, filters, and ordering.
- A bare Chronicle importance number is ambiguous; players need the source tick and a labelled importance score at the point of entry.
- Repeating a fleet selector inside each action wastes space and permits the command controls to drift away from the fleet being inspected.
- Resolved intentions are useful reference material, but they should not compete with current fleet decisions for permanent screen space.

Consequences:

- Alt+4 and `#history` open the History parent view. The old `#chronicle` hash remains a compatibility alias.
- Chronicle entries label source tick and importance, place the authoritative factual summary first, and retain the longer narrative beneath it.
- Chronicle and Event tabs have independent search, domain-appropriate filters, sorting, and result counts.
- Fleet selection drives all three action forms. The command dock only asks for a destination or target where one is needed, and the full fleet detail uses the remaining workspace below it.
- **Resolved orders** supports selected/all-fleet scope, outcome filtering, chronological sorting, and bounded incremental rendering.

## 2026-07-13: Use A Cost-Capped Trusted Playground

Decision: host the invited Development playground as a single `Cycles.Api` process on Azure App Service F1, persist its curated game as JSON on App Service storage, and use manual Development turns rather than deploying the Worker. Treat this as a bounded play-testing exception, not the production runtime design.

Reasoning:

- Azure subscription budgets notify but do not prevent overspend, so an advisory budget is not an adequate safety boundary.
- App Service F1 enforces compute, bandwidth, and filesystem quotas without automatically moving the application onto paid compute.
- A database or continuously running Worker would add cost exposure that is unnecessary for the trusted, manual-turn play loop.
- GitHub workload identity can deploy the application without a long-lived Azure secret.

Consequences:

- The playground targets .NET 10 LTS and uses `/home/data/cycles-state.json`, one API process, no `Cycles.Worker` process, and no database. SQL Server remains the intended relational runtime direction outside this exception.
- The App Service plan is locked against changes, and the playground resource group denies the known Azure database, Container Apps, registry, private telemetry, and log-workspace resource types that could introduce paid runtime spend.
- A successful `main` CI run deploys the same revision and verifies `/health`.
- A binding-free Cloudflare Worker on the Free plan fronts `cycles.anthonypwatts.co.uk`; the application itself protects both that route and the direct Azure origin with a shared access code for Anthony and Will.
- Cloudflare Zero Trust is not used because its checkout path required a payment card and offered usage-overage authorisation. Per-email sign-in does not justify weakening the hard-spend boundary for this temporary environment.
- The shared code and seven-day secure cookie are trusted-playground exceptions, not production identity or authorisation.
- Production identity, hosting, Worker scheduling, persistence lifecycle, monitoring, recovery administration, and backup decisions remain open.

## 2026-07-13: Create The Worker Before Further Gameplay Expansion

Decision: accept the Q107 sequencing default and create `Cycles.Worker` before adding the next gameplay system.

Reasoning:

- Repeated testing needs an authoritative scheduled host rather than continued dependence on manual CLI ticks.
- Keeping scheduled execution outside the API preserves the server-authoritative boundary and gives operational concerns a clear owner.
- The Worker implementation and scheduling tests already provide concrete evidence for the default.

Consequences:

- The existing `Cycles.Worker` project is the accepted scheduled tick host and does not need replacement merely to revisit the sequencing question.
- Production Worker health, leadership, deployment topology, authentication, backup, and recovery policy remain separate open decisions.

## 2026-07-14: Use Each Cycle's Configured Tick Cadence

Decision: accept the Q108 scheduling default. The active Cycle's `TickLengthMinutes` controls scheduled cadence rather than using a fixed hourly schedule or a separate scheduled flag.

Reasoning:

- Cadence is already part of the authoritative Cycle state and can vary without changing Worker configuration.
- Scheduling from the last completed tick avoids an immediate catch-up storm after downtime.
- Active status and recovery state already express whether a Cycle may advance safely.

Consequences:

- The first tick is due at Cycle start; later ticks are due one configured cadence after the last completed tick.
- The Worker runs at most one due tick per check and does not process a backlog of missed ticks.
- Recovery-required and non-active Cycles are not scheduled.

## 2026-07-14: Restrict Manual Tick Authority By Environment

Decision: accept the Q109 manual tick boundary. Any authenticated player may use **Advance turn** in Development; shared private-alpha and Production environments use scheduled Worker timing normally and restrict manual tick execution to audited admins.

Reasoning:

- Trusted Development play needs a quick way to complete the gameplay loop without waiting for scheduled time.
- Manual execution already uses the same authoritative store operation as the Worker and CLI.
- Shared and Production environments need explicit identity and audit boundaries around an operation that advances the whole galaxy.

Consequences:

- The trusted playground may continue using the Development exception without promoting players or broadening their visibility or empire authority.
- Ordinary Production players cannot execute ticks.
- Production identity, admin provisioning, and audit implementation remain required before the admin capability is suitable for shared use.

## 2026-07-14: Flag Persisted Running Ticks After Five Minutes

Decision: accept the Q111 diagnostic default. A persisted `Running` tick becomes suspicious after a configurable threshold that defaults to five minutes.

Reasoning:

- Normal tick execution is expected to complete far faster than five minutes, while the threshold leaves ample room for constrained infrastructure and retained state.
- A Cycle's scheduling cadence is unrelated to execution duration, so waiting a full tick length would delay fault detection unnecessarily.
- A warning must remain separate from recovery because elapsed time alone does not prove that a tick is abandoned.

Consequences:

- The threshold classifies and reports suspicious state only; it does not fail, retry, repair, or cancel a tick.
- At the time of this decision, the atomic JSON store did not persist its intermediate `Running` log, so the hosted path called for end-to-end duration and retained-state evidence. Q119 later retired JSON as a runtime path.
- GitHub issue #120 now tracks persisted SQL attempt diagnostics only; representative JSON import/export validation belongs to issue #126. Q112 remains responsible for the operator response to an abandoned running tick.

## 2026-07-14: Require Inspection Before Abandoning A Running Tick

Decision: accept the Q112 conservative recovery default. A persisted `Running` tick continues to block until an admin inspects it and explicitly marks a confirmed abandoned attempt failed with an operator and reason.

Reasoning:

- Age indicates suspicion but cannot prove that a process or transaction is no longer active.
- Automatically failing or retrying an active tick risks concurrent or duplicate simulation outcomes.
- An explicit operator identity and reason preserve the recovery audit trail and force diagnosis before simulation resumes.

Consequences:

- Existing duplicate-tick and recovery-clear guards remain in force.
- No elapsed-time threshold may automatically change a tick attempt's state.
- A bounded operator command must provide the missing audited `Running`-to-`Failed` transition, leave the Cycle in `RecoveryRequired`, and defer resumption to normal repair and recovery clear/retry. Implementation is tracked by GitHub issue #121.

## 2026-07-14: Use External OIDC With Cycles-Owned Authorisation

Decision: use ASP.NET Core cookie authentication backed by an external OpenID Connect provider for private-alpha and Production identity. Map the provider's stable issuer and subject to a local player, while keeping empire ownership, admin role, and operational permissions in Cycles-owned data.

Reasoning:

- External identity avoids making Cycles responsible for storing passwords, password resets, and credential hardening.
- The existing player and empire model already owns game authority, so authentication can change without moving authorisation into provider-specific claims.
- Invitations and allowlists are useful private-alpha admission controls, but they do not prove who is signing in.
- A provider-configurable OIDC boundary keeps the product contract independent of the first deployment's vendor.

Consequences:

- The application will use the confidential authorisation-code flow with PKCE and a secure application cookie.
- Provider issuer and subject, not mutable email or display name, will correlate an identity with a local player.
- The first private alpha may restrict admission to invited identities before local player provisioning.
- Development username login remains an explicit Development-only convenience. The trusted playground's shared access code remains a temporary perimeter and is not promoted into production authentication.
- Full ASP.NET Core Identity password management is not required unless a later product need justifies owning local credentials.
- OIDC implementation is tracked by GitHub issue #122; the accepted audited admin-provisioning implementation is tracked separately by issue #123.

## 2026-07-14: Provision Admins Explicitly And Audit Role Changes

Decision: keep admin authority in Cycles-owned player data. Bootstrap the first named admins through explicit operator configuration tied to stable external identities, then require an authenticated admin, target player, and non-empty reason for every routine grant or revocation. Keep emergency operator access separate from player-admin accounts.

Reasoning:

- Authentication proves an external identity but should not silently grant game-wide authority through provider groups, email addresses, display names, invitation state, or login input.
- The first admin needs an explicit bootstrap path because no in-application administrator exists yet.
- Admins can inspect and act for every empire, so grants and revocations need a durable accountability record.
- Separate break-glass operation avoids leaving a hidden permanent superuser in the ordinary player model.

Consequences:

- The Development `isAdmin` login switch remains local scaffolding and must be refused outside Development.
- Bootstrap application records the target identity, configuration source or deployment revision, reason, and timestamp.
- Routine grants and revocations record immutable actor, target, action, reason, and timestamp data.
- Routine operations cannot remove the final active admin; a documented operator recovery path handles exceptional lockout or compromise.
- A general account-management UI and granular administrator permissions remain out of scope.
- Implementation is tracked by GitHub issue #123 and composes with the OIDC identity work in issue #122.

## 2026-07-14: Keep The Landing Page Public And Protect The Dashboard

Decision: keep `/` public in the normal private-alpha and Production route contract. Require external authentication and invited-player admission before serving `/app.html`, keep `/health` public, and continue authenticating and authorising every game API independently. Allow an explicit whole-site perimeter as a deployment override.

Reasoning:

- The landing page can explain the project and provide a deliberate sign-in entry without exposing game state.
- Challenging at the dashboard entry gives invited testers a clear authentication boundary before the playable shell loads.
- Protecting a document is not API security, so every state and command endpoint must retain its own identity and authority checks.
- Some pre-release deployments should remain undiscoverable; an environment-wide perimeter supports that need without redefining the permanent application routes.

Consequences:

- Anonymous requests may reach `/` and a non-sensitive `/health` endpoint in the normal private-alpha and Production configuration.
- `/app.html` challenges anonymous users and refuses authenticated identities that have not passed invited-player admission.
- Authentication callbacks, sign-out callbacks, and safe error routes remain reachable as required by the selected provider.
- The trusted playground may continue protecting both the landing page and dashboard with its shared access code before normal route handling. This remains a temporary perimeter rather than identity proof.
- Implementation is tracked by GitHub issue #124 and depends on the external OIDC and invited-player work in issue #122.

## 2026-07-14: Move The Deployed Playground To Managed SQL Before Further Invitations

Decision: before further tester invitations, migrate the deployed playground's current state to managed SQL, enable at least seven days of database-native point-in-time recovery, document restoration, and prove one isolated restore. Do not build a separate production-style backup system around the hosted JSON file.

Reasoning:

- Tester decisions, orders, events, and history are no longer credibly disposable once repeated play sessions matter.
- The repository already has a SQL Server store, ordered migrations, transaction locking, recovery state, and focused tick persistence; backup work should reinforce that intended runtime rather than extend the JSON exception.
- A database service's backup claim is incomplete operational evidence until the project has restored and validated representative state.
- Moving the deployed test instance now exercises the same persistence boundary required by later private-alpha operation.

Consequences:

- The final JSON state is captured consistently while the playground is stopped or quiescent, retained off-host as migration evidence, imported through the authoritative SQL store, and validated before access reopens.
- The application does not dual-write. Before reopening, rollback may return to the frozen JSON snapshot; after new SQL-backed gameplay, recovery uses SQL backup/restore instead of stale JSON.
- The selected managed service must provide at least seven days of point-in-time recovery and remain inside an explicitly reviewed cost boundary.
- One backup is restored to an isolated target and checked for Cycle/tick, player, empire, fleet, order, event, Chronicle, and recovery-state continuity.
- Q117 selects the existing SQL Server provider on managed Azure SQL. Q119 subsequently confirms JSON's remaining import/export-only lifecycle and mandatory SQL runtime direction.
- Implementation is tracked by GitHub issue #125.

## 2026-07-14: Use The Existing SQL Server Provider For The First Online Test

Decision: use the existing SQL Server provider on managed Azure SQL for the deployed playground and first online test. Do not delay that deployment to build PostgreSQL or MySQL portability.

Reasoning:

- The repository already has ordered SQL Server migrations, a working store, transaction and application locks, recovery support, focused tick persistence, and live integration coverage.
- Exercising the implemented relational path now provides more useful operational evidence than adding an abstraction for an unselected second provider.
- Managed Azure SQL aligns with the current Azure-hosted API and supplies the database-native backup boundary required by Q116.
- Possible future licensing or hosting cost matters, but no measured constraint currently justifies paying the delivery and testing cost of provider portability first.

Consequences:

- Issue #125 targets managed Azure SQL and must run a compatibility smoke test before cutover.
- SQL Server-specific implementation remains contained in `Cycles.Infrastructure.SqlServer` and its migration layer rather than leaking into `Cycles.Core`.
- PostgreSQL or MySQL support remains a future option, triggered by measured cost, licensing, hosting, or operational evidence.
- This decision does not grant permission for gratuitous new SQL Server-specific dependencies or features; Q118 separately defines that narrower policy.

## 2026-07-14: Allow Justified Provider Features Behind The SQL Server Boundary

Decision: do not ban SQL Server-specific features. Permit features supported by Azure SQL inside `Cycles.Infrastructure.SqlServer` and SQL migrations when they materially improve correctness, consistency, measured performance, or operations. Keep `Cycles.Core` and the store contract provider-neutral.

Reasoning:

- A lowest-common-denominator rule would discard useful database guarantees before another provider has been selected or implemented.
- Provider-specific behaviour is cheaper to replace later when it remains behind an explicit adapter and migration boundary.
- Every provider feature still creates migration cost, so it needs a concrete benefit rather than speculative convenience.
- The existing transaction-scoped `sp_getapplock` prevents concurrent authoritative ticks and is already an appropriate example of this trade-off.

Consequences:

- `sp_getapplock` remains part of the accepted SQL Server tick and broad-update concurrency boundary.
- New provider-specific behaviour must remain outside `Cycles.Core`, be compatible with the selected Azure SQL target, and carry focused integration coverage.
- Material portability implications are documented when introduced; a hypothetical future provider does not require an unused abstraction today.
- Convenience-only provider coupling and leakage into domain or public application contracts remain out of bounds.
- This confirms the implemented architectural default and creates no separate implementation issue.

## 2026-07-14: Demote JSON To Explicit Import And Export Now

Decision: demote JSON now to explicit import/export, validation, offline inspection, fixtures, and migration evidence. After the deployed cutover is safely configured and verified, API and Worker runtime hosts require explicit SQL configuration and normal local development uses the documented SQL Server container.

Reasoning:

- Q116 and Q117 already require the deployed playground and first online test to use the existing SQL Server provider on Azure SQL.
- Keeping a silent file fallback after that cutover would preserve two runtime behaviours, weaken configuration failure signals, and continue exercising the path the project has decided to retire.
- A deliberate, versioned transfer format remains useful for migration, support, reproducible fixtures, and offline inspection without acting as the authoritative live store.
- The deployed game needs the importer before the runtime fallback can disappear, so sequencing is part of the decision rather than an implementation detail to improvise later.

Consequences:

- Issue #126 adds versioned, validated SQL-to-JSON export and JSON-to-SQL import before changing runtime defaults.
- Issue #125 consumes the verified importer, migrates and validates the deployed game, and switches App Service to Azure SQL.
- Only after the deployed SQL configuration is live and verified do API and Worker remove `CYCLES_STATE_PATH` and implicit `FileGameStateStore` selection.
- Local development uses the SQL Server container and migrations. JSON may remain in bounded tooling and test-fixture paths, but not as a supported runtime host store.
- The application does not dual-write JSON and SQL, and JSON exports do not replace database-native backup or restore.
- Issue #120 no longer includes transient hosted-JSON file-size or whole-file runtime diagnostics; it remains focused on persisted SQL attempts.

## 2026-07-14: Keep Fact Storage Flexible Until A Consumer Needs A Contract

Decision: keep `FactJson` as flexible internal storage for another stage. Introduce a typed or validated contract when a fact becomes mechanically consumed, queried, migrated, or publicly exposed, rather than typing every payload merely because it can be displayed.

Reasoning:

- Event, battle, Chronicle, diplomacy, and doctrine fact shapes are still evolving, so a broad schema migration would create churn without improving a current product boundary.
- Flexible internal storage remains useful for factual details that are written and rendered but do not yet drive mechanics or integration contracts.
- Mechanical consumers need stable field meaning, validation, and compatibility handling; allowing clients to understand storage JSON directly moves that responsibility to the wrong boundary.
- The Day One guide already reads stable objective identifiers from `OpeningBriefingIssued`, making that payload the current candidate for a purpose-built contract.

Consequences:

- Existing fact rows remain stored as JSON strings; this decision does not create a generic typed-fact framework or broad migration issue.
- New code that mechanically consumes, queries, migrates, or publicly exposes fact fields must introduce a named typed or validated reader, model, or response contract at that boundary.
- Display-only facts may remain flexible while their shapes stabilise, provided normal UI does not expose raw storage JSON.
- Q123 subsequently keeps raw fact storage out of ordinary player responses and requires the purpose-built replacement for the dashboard's direct opening-briefing parsing. Implementation is tracked by issue #127.

## 2026-07-14: Present Fact Detail Through Purpose-Built Player Contracts

Decision: keep raw `FactJson` out of the normal dashboard and ordinary player API. Use display text as the default event presentation and add purpose-built typed detail only when it provides player value. Raw fact inspection belongs, if needed, behind an explicit authorised operator diagnostic.

Reasoning:

- Storage JSON is an internal persistence shape, not a stable or approachable player interface.
- Display text already gives the History view useful factual context without coupling the client to each evolving event shape.
- Parsed detail is valuable when the player needs structured information or an interaction depends on stable identifiers, but a generic panel would merely expose implementation detail with nicer formatting.
- The Day One guide currently parses `OpeningBriefingIssued` in JavaScript, so its stable objective identifiers need a server-owned contract and compatibility boundary.

Consequences:

- Ordinary event, battle, and tick-result responses will stop carrying raw `FactJson`; internal storage remains unchanged.
- The dashboard continues to show display text by default and gains typed detail only for deliberate player-facing use cases.
- The opening briefing becomes a purpose-built, visibility-checked response consumed by the guide rather than raw event JSON.
- Any future raw fact viewer must be an explicit authorised operator diagnostic, not a field retained in ordinary responses.
- Implementation is tracked by GitHub issue #127; no general typed-fact framework or storage migration is authorised.

## 2026-07-14: Lock API Serialization Before External Clients

Decision: lock camelCase JSON property names and camelCase string enum values before external clients exist. Do not freeze the current message-only error response unchanged; add a stable machine-readable error code while retaining a safe human-readable message, meaningful HTTP status, and optional structured validation detail and trace correlation.

Reasoning:

- Property casing and enum wire values are already consistent across the API and dashboard, so changing them later would create avoidable client churn.
- Numeric enum values weaken the public vocabulary by allowing unnamed or version-sensitive values and are not needed by the current client.
- A human-readable exception message is useful to display but is not a durable identifier for client behaviour, tests, telemetry, or support.
- The contract can be stabilised before external clients exist without adding URL versioning or a general error-handling framework.

Consequences:

- Player API fields remain camelCase and enums remain camelCase strings; numeric enum input is rejected at the public JSON boundary.
- Handled errors carry a stable `code` and safe `message`; optional `details` supports structured validation and optional `traceId` supports correlation without exposing stack traces or secrets.
- HTTP status remains authoritative for the broad failure class, while clients may branch on documented codes and must not parse message wording.
- Additive optional fields and new error codes are compatible. Renaming or removing fields, changing existing enum wire values or code meanings, or making optional fields required needs an explicit compatibility decision.
- Implementation and contract tests are tracked by GitHub issue #128. API URL versioning and message localisation remain out of scope.

## 2026-07-14: Keep The Next Dashboard Test At 24 Systems

Decision: target the existing curated 24-system, four-empire galaxy for the next player test. Do not optimise the dashboard for 50, 100, or more systems until gameplay evidence shows that a larger galaxy improves the intended test.

Reasoning:

- The curated opening, guide, strategic choices, map, and bounded lists are designed and exercised together at 24 systems and four empires.
- The next test needs evidence about player decisions and comprehension, not speculative maximum-map capacity.
- Increasing node count without reconsidering navigation and information density would create clutter rather than credible scale support.
- A concrete larger target should drive measurement and design work; an abstract promise of 50 or 100 systems would not identify which interactions must remain usable.

Consequences:

- The current small-galaxy dashboard default is an accepted next-test product boundary rather than an unapproved implementation assumption.
- No 50- or 100-system dashboard implementation issue is created now.
- Larger test scenarios remain valid for simulation or persistence evidence, but do not establish dashboard usability at that scale.
- If a later test selects a larger galaxy, reassess navigation, clustering, filtering, API payloads, rendering cost, and system selection together.

## 2026-07-14: Prioritise Desktop Commands While Preserving A Narrow-Screen Core

Decision: prioritise desktop and laptop command usability. Narrow browser layouts must remain readable and support the core game loop, but equal mobile optimisation and a touch-first redesign are not requirements for the next player test.

Reasoning:

- The dashboard combines a galaxy map, dense command context, resource allocation, order history, and Chronicle detail; these interactions benefit from a desktop-sized workspace.
- Existing responsive breakpoints already make the browser interface usable at narrower widths without defining a separate mobile product.
- Requiring equal optimisation now would split design and verification effort before tester behaviour shows that mobile is an important play surface.
- A functional floor prevents responsive regressions while keeping the primary interaction model coherent.

Consequences:

- Desktop and laptop browsers remain the reference surface for command efficiency and visual hierarchy.
- Narrow layouts retain sign-in, status and History reading, priorities, fleet selection, and basic order submission and cancellation without page-level horizontal scrolling.
- Mobile parity, touch-first interaction, and native clients remain deferred until usage evidence changes the target.
- The existing desktop-first responsive implementation satisfies this decision, so no separate implementation issue is created.

## 2026-07-14: Use The Live Day One Guide As Primary Training

Decision: keep the resumable Day One guide as the primary in-dashboard training path. It must explicitly teach priorities, visibility and fog-of-war, the order lifecycle, factual Events versus the selective Chronicle, and basic tick/Cycle history through the real controls. Retain concise contextual hints, but do not create a separate help centre now.

Reasoning:

- A click-along guide tied to successful server actions teaches the actual command loop more reliably than a detached slideshow or manual.
- The existing guide already covers resources, priorities, map and fleet selection, movement, colonisation, attack, pending orders, turn resolution, Events, and Chronicle.
- The live sequence does not yet explain why remote facts are absent or how tick results, Chronicle selection, Cycle end, ranking, and successor history relate.
- Training copy drifts quickly when view names, tabs, and controls change, so the live guide and player-facing written guide need one reviewed source of product truth rather than another help surface.

Consequences:

- The guide remains resumable per player and seeded Cycle instance, with pause, skip, resume, and restart controls.
- Add concise non-blocking visibility and Cycle-history teaching and audit every instruction and target against the current Command, Galaxy, Fleets, and History UI.
- Bump the guide content version when the step sequence changes so stored progress cannot misalign with the new steps.
- Keep `docs/alpha-testers-guide.md` aligned with the live wording and current controls.
- Implementation and desktop/narrow smoke verification are tracked by GitHub issue #129; no Playwright dependency or separate help centre is authorised.
