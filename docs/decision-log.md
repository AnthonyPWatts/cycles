# Decision Log

Last updated: 2026-07-18

This file records decisions that shape implementation. Entries are chronological and describe the decision at the time it was made; later entries may fulfil, extend, or supersede earlier ones. Add an explicit status when reading an old entry as current guidance would be misleading.

## 2026-06-23: Build A Runnable MVP Before Production Infrastructure

Decision: start with a local .NET MVP using Core, CLI, API, dashboard, and a JSON state store.

Status: fulfilled for the initial MVP and superseded for persistence on 2026-07-14 by the managed-SQL cutover and removal of the executable file store.

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

Status: fulfilled. The container remains the normal local database path; application runtime is now SQL-only.

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

Status: superseded on 2026-07-14. SQL Server is now mandatory for API, Worker, and gameplay/operator CLI paths, and no executable file store remains.

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

Status: fulfilled and narrowed on 2026-07-14. SQL Server is the sole runtime store; JSON remains only in named transfer, validation, inspection, fixture, legacy-conversion, and migration-evidence paths.

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
- Q014-Q018 now settle mutual consent, unilateral hostile or terminating actions, the absence of a separate warning period, first-version Alliance mechanics, separate empire rankings, and allied influence coexistence, while Q013 and Q019-Q022 remain the implementation gate for complete diplomacy orders, visibility, and Chronicle behaviour.

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
- At that decision point, typed fact schemas and the remaining API/dashboard choices were still governed by Q122-Q130 rather than being inferred from Q120-Q121. Q122-Q130 now settle the fact, API, scale, training, documentation-governance, and state-export boundaries; this API/dashboard decision group is closed.

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

Status: superseded for persistence on 2026-07-14 by the managed Azure SQL cutover. The F1 API, manual-turn, access-gate, and cost-cap shape remains current.

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

Status: fulfilled on 2026-07-14. The cutover and isolated restore proof completed through issue #125, and the temporary restore database was deleted after verification.

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

Status: fulfilled on 2026-07-14. Issue #126 removed runtime file persistence and implicit store selection after the managed-SQL cutover.

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

## 2026-07-14: Put Actionable Ticket Lifecycle In GitHub

Decision: make GitHub issues authoritative for concrete actionable backlog work, including scope, acceptance criteria, owner, live status, dependencies, and completion. Keep `docs/backlog.md` as the curated roadmap, sequencing summary, decision-gate overview, and issue index. Keep accepted answers, implemented state, and durable rationale in their existing canonical repository documents.

Reasoning:

- Concrete work already needs assignment, labels, dependencies, discussion, and closure; duplicating that mutable lifecycle in Markdown creates drift.
- The Markdown backlog remains valuable for explaining priority, sequence, blocked areas, and the relationship between tickets without requiring a large issue dump.
- Product answers and durable technical rationale need reviewable repository history and must not disappear into closed issue comments.
- Filing every conditional risk, parked idea, or unresolved product choice would create noise rather than a useful actionable queue.

Consequences:

- Concrete selected work has one authoritative GitHub issue; Markdown links to it without duplicating live status or complete acceptance criteria.
- `docs/product-owner-questions.md` remains authoritative for accepted product answers, `docs/project-state.md` for implemented behaviour and verification, and `docs/decision-log.md` for durable rationale.
- Unshaped ideas may remain in roadmap or parking-lot prose until selected; unresolved decisions remain in the product-decision queue rather than becoming premature implementation tickets.
- Issue #130 inventories the current Markdown checkboxes, reuses or creates worthwhile tickets, removes duplicated mutable detail, and updates repository guidance.
- Until that migration completes, `docs/backlog.md` remains the operative implementation queue so no concrete work is orphaned during the transition.

## 2026-07-14: Keep Maintained Documentation Aligned With Main

Decision: maintained documentation describes current `main` behaviour. Do not create separate documentation copies for each gameplay Cycle or ad hoc test build. Record the deployed commit or build identifier in test evidence and use Git tags, commits, or release notes when a historical snapshot is genuinely required.

Reasoning:

- A gameplay Cycle is persisted game data and history, not a software documentation version.
- Parallel documentation sets would duplicate manual maintenance and drift as UI, API, persistence, and operations change.
- Git already preserves the documentation that accompanied every commit and can provide deliberate release snapshots when the project reaches that stage.
- Test reproducibility needs the exact deployed code identity more than another copied folder whose relationship to the running build may be unclear.

Consequences:

- README, player guide, project state, runbooks, architecture, backlog summary, and accepted-answer docs stay aligned with current `main`.
- Hosted or organised test evidence records the deployed commit SHA or build identifier and, where useful, the Cycle identifier as separate facts.
- Git tags or releases may preserve named historical documentation snapshots when genuine external compatibility or support needs appear.
- Gameplay Cycle creation does not copy or version the documentation tree, and no implementation issue is required for this accepted existing default.

## 2026-07-14: Keep Complete State Transfer An Operator Tool

Decision: treat complete game-state export/import as an operator/admin support tool for migration, recovery preparation, debugging, and reproducible fixtures. It may also support developer workflows, but it is not a player-facing save/restore feature.

Reasoning:

- A complete export contains private state across every empire, including identities or audit context, hidden facts, and operational state that an ordinary player must not receive.
- The same full-fidelity format is useful for the accepted SQL cutover, controlled recovery preparation, debugging, and deterministic fixtures without creating a second player product.
- Database-native backup and restore remains the authoritative recovery mechanism after the SQL cutover; an application export has different consistency, retention, and operational guarantees.
- Player sharing would require a deliberately redacted scenario contract with its own versioning, visibility, and privacy rules rather than exposing the operator format.

Consequences:

- Issue #126 keeps complete export/import behind explicit operator CLI or admin authorisation and documents secure handling, retention, transfer, and deletion.
- Ordinary players receive no authoritative-state download or upload endpoint, and normal logs must not contain export payloads, credentials, or private state.
- Complete exports remain sensitive support artefacts and do not replace database-native backup and restore.
- Any later player-facing sharing feature requires a separate product decision and a redacted, versioned format.

## 2026-07-14: Require Mutual Consent For Positive Diplomatic Agreements

Decision: require mutual acceptance for Alliance, peace, Non-Aggression Pacts, and any future trade or shared-visibility agreement. War declarations and treaty termination remain unilateral, and an unaccepted offer may be withdrawn unilaterally.

Reasoning:

- Positive bilateral agreements grant obligations or benefits to both empires and must not be imposed by one player.
- Ending War also binds both parties, so peace is a mutually accepted transition to Neutral rather than a unilateral action or a new stored relationship state.
- An empire must remain free to declare hostility, leave an agreement, or retract an offer that the other side has not accepted.
- Applying the same consent rule to future trade and shared visibility avoids inventing inconsistent diplomacy semantics when those systems are designed.

Consequences:

- Alliance, peace, and Non-Aggression Pact transitions require acceptance by both affected empires.
- Future trade or shared-visibility agreements use the same mutual-consent boundary, but neither feature is authorised by this decision.
- War declarations, treaty termination, and withdrawal of an unaccepted offer require only the acting empire's authority.
- Q013 still determines when diplomatic actions resolve, and Q019-Q022 still govern the remaining lifecycle and effects; no implementation issue is ready yet.

## 2026-07-14: Do Not Add A Separate Diplomatic Warning Period

Decision: allow a player to declare War or terminate a treaty unilaterally, without a separate advance-notice or cooling-off period beyond the normal diplomatic resolution timing selected under Q013.

Reasoning:

- Q014 already establishes that hostile declarations and treaty termination do not require the other empire's consent.
- A second mandatory warning phase would add another state and delay without evidence that it improves the first playable diplomacy loop.
- Attacking through an Alliance or Non-Aggression Pact already cancels the relationship during authoritative attack resolution without advance warning, providing a consistent implemented precedent.
- The affected player still needs a clear factual notification and durable event when the relationship changes; absence of advance notice must not mean a silent state transition.

Consequences:

- War declarations and voluntary treaty termination are unilateral actions with no additional notice timer or cooling-off state.
- Q013 still decides whether the action itself is immediate or resolves as a durable order; Q015 adds no further delay.
- When the transition becomes authoritative, both parties are notified and a high-severity factual event records the actor, prior state, new state, and effective tick.
- The existing attack-driven treaty cancellation remains valid. Explicit declaration and voluntary-termination actions remain unimplemented and blocked with the wider lifecycle by Q013 and Q019-Q022.

## 2026-07-14: Keep First-Version Alliances Mechanically Narrow

Decision: make friendly-fire prevention and factual history the only first-version Alliance mechanics. While an Alliance is active, ordinary direct attacks between its members are not permitted; a player must terminate the Alliance before deliberately attacking.

Reasoning:

- Preventing friendly fire gives Alliance an immediate, understandable mechanical promise without coupling it to every strategic system.
- Pooling influence, resources, rankings, fleets, or attack control would erase empire-level choices and make balance substantially harder before the basic diplomacy loop is proved.
- Movement is not currently blocked by foreign territory, so an Alliance-specific movement permission would add no useful behaviour.
- Shared visibility materially changes fog-of-war and belongs to Q025 rather than being decided indirectly as an Alliance side effect.

Consequences:

- Attack submission and authoritative resolution must reject an ordinary attack against an empire that is still an ally.
- Alliance creation, termination, and any valid betrayal edge case produce factual Events/history.
- Existing Q012 treaty-breach cancellation remains as a defensive boundary for already-pending or exceptional conflicts; normal deliberate betrayal requires terminating the Alliance first.
- Alliance does not pool influence, resources, rankings, fleets, or attack control, and it grants no special movement rule.
- Q025 remains authoritative for allied visibility. Q013 and Q019-Q022 still block the complete player-facing diplomacy lifecycle, so no implementation issue is ready yet.

## 2026-07-14: Keep Alliance Members Separately Ranked

Decision: retain separate per-empire map-control scores and rankings regardless of Alliances. Alliance members do not contribute map control to one another, pool scores, or become joint winners.

Reasoning:

- The first Cycle-end contract ranks each empire by its own `MapControlPercent` and records one winner plus complete standings.
- Pooling allied control would let diplomacy rewrite the victory calculation late in a Cycle and would obscure each empire's strategic performance.
- Q016 already excludes pooled influence, rankings, resources, fleets, and attack control from the first Alliance contract.
- Historical narrative can recognise alliances without changing the authoritative metric or creating coalition ownership.

Consequences:

- `EmpireMetricCalculator`, `CycleEndService`, and `CycleRankings` remain diplomacy-independent and require no implementation change.
- Every active empire retains its own score and rank, and exactly one empire is the Cycle winner under the existing tie-break rules.
- Chronicle or Cycle-end prose may mention the winner's allies, but such acknowledgement is non-mechanical.
- No implementation issue is required. Q013 and Q019-Q022 still gate the remaining player-facing diplomacy lifecycle.

## 2026-07-14: Keep Allied Influence And Rewards Independent

Decision: allow allied empires to maintain influence in the same system while continuing to calculate each empire's effective presence, resource share, and map-control contribution independently.

Reasoning:

- The influence model represents proportional presence rather than binary ownership, so coexistence already supports multiple empires in one system.
- Q016 excludes pooled influence and resources from the first Alliance contract, and Q017 keeps rankings separate.
- Removing competition between allied presence would indirectly pool economic and scoring benefits even if the stored values remained nominally separate.
- Friendly presentation can distinguish allied coexistence from hostile contest without changing the authoritative calculation.

Consequences:

- Alliance prevents hostile action but does not merge presence, resource entitlement, expansion pressure, or map control.
- Existing relationship-independent influence, economy, and ranking calculations remain valid and require no mechanical implementation change.
- Dashboard wording may describe allied empires as coexisting, but displayed shares remain separate and must not imply common ownership.
- No implementation issue is required. Q013 and Q019-Q022 still gate the remaining player-facing diplomacy lifecycle.

## 2026-07-14: Stage The SQL Runtime Cutover Behind An Explicit Activation Gate

Decision: implement strict versioned JSON-to-SQL transfer and a `Cycles:RequireSqlRuntime` startup guard now, switch normal local instructions to SQL Server, but do not remove the hosted file fallback until issue #125 has imported and verified the deployed state and proved database restore.

Status: fulfilled and superseded on 2026-07-14. The activation gate served the cutover; SQL configuration is now unconditional and the flag and file fallback have been removed.

Reasoning:

- Q119 requires import/export before the deployed cutover and requires fallback removal only after the SQL target is configured and verified.
- Removing the fallback in the same undeployed change would redeploy the current JSON-backed playground without an authoritative target.
- A fail-fast activation flag lets local and future environments prove the intended SQL-only startup contract without changing the current hosted storage prematurely.

Consequences:

- The operator CLI exports and imports a complete format-versioned document, validates all persisted collections and references, and requires explicit import/replacement confirmation.
- Complete exports are sensitive cross-empire operator artefacts and are not player endpoints or managed backups.
- Normal local API and Worker instructions set `Cycles:RequireSqlRuntime=true` with SQL Server.
- Issue #125 must use the importer and prove Azure SQL restore before #126 removes the remaining runtime fallback.

## 2026-07-14: Make GitHub The Actionable Backlog Authority

Decision: complete the Q128 ownership transition by keeping concrete actionable scope, acceptance criteria, ownership, dependencies, live status, and completion in GitHub issues; retain `docs/backlog.md` as a curated sequence, decision-gate map, conditional-risk register, and issue index.

Reasoning:

- Duplicated Markdown checkboxes and issue state create two mutable queues that can disagree.
- Product decisions, implemented state, durable rationale, and actionable work already have distinct repository/GitHub owners.
- Conditional risks and parking-lot ideas should not create speculative issue noise.

Consequences:

- Guided play evidence, production Worker operation, and the pre-untrusted-test security review are tracked by issues #131, #132, and #133.
- Decision-gated gameplay and narrative themes stay linked to the product question queue until their rules settle.
- Concrete friction in a conditional risk creates one bounded evidence-backed issue when it occurs; it does not restore a permanent Markdown checklist.
- Contributor and agent guidance must not duplicate live ticket status in repository documentation.

## 2026-07-14: Treat Priorities As Strategic Programmes, Not Resource Mirrors

Decision: keep three resource stockpiles and four 100-point strategic programmes. Present the persisted Industry and Research weights as Development and Innovation compatibility fields, keep them visibly inactive until their programmes exist, retain Military and Expansion as the two active effects, and keep Population as a directly consumed resource without adding a fifth priority.

Status: extended later on 2026-07-14 by locking the inactive Development and Innovation weights at zero and assigning the full active allocation to Military and Expansion.

Reasoning:

- Four priorities and three resources are compatible when priorities represent empire-level strategic effort rather than one control per stockpile.
- The former labels mixed resource names with strategic outcomes and made four equal sliders look equally functional even though only Military and Expansion affect simulation.
- Hiding or disabling the stored Industry and Research weights would conceal or lock points that still participate in the required total of 100.
- Flat Industry, Research, Population, or infrastructure output multipliers would add compounding growth and weaken the established influence-share economy before balance evidence supports that change.
- Location-specific colonisation remains a deliberate order, so Population should not be spent automatically through a global priority.

Consequences:

- The dashboard labels Industry as Development and Research as Innovation, marks both inactive, marks Military and Expansion active, and explains the current limitation before a player edits the allocation.
- Survey Projection remains a universal, non-consuming introductory unlock. Future doctrines use a hybrid model in which the player selects a project and Research is consumed on completion.
- Development's accepted future direction is a bounded civilian-development or construction-capacity programme, not a flat resource multiplier or another ship class.
- Raw system Industry, Research, and Population output remains strictly divided by effective presence for the first version.
- The existing persistence and API field names remain unchanged for compatibility. Any mechanical activation or public-contract rename requires a separate bounded implementation and should begin at a deliberate Cycle or reseed boundary.
- Expansion retains its current effect for compatibility; narrowing or rebalancing its broad presence effect requires guided play and mixed-strategy evidence through issue #131.

## 2026-07-14: Lock Inactive Priority Programmes At Zero

Decision: keep Development and Innovation visible as future strategic programmes but lock both compatibility weights at zero until their mechanics are active. Military and Expansion divide the current 100-point allocation.

Reasoning:

- A visible, editable control with no effect asks players to make a false strategic choice.
- Disabling the inactive controls is coherent once their stored points are moved into the active allocation rather than stranded.
- Preserving each legacy allocation's Military-to-Expansion ratio minimises strategic drift while producing a valid active total. A legacy allocation with neither active weight uses a neutral 50/50 fallback.

Consequences:

- Domain validation rejects new non-zero Development or Innovation weights, and SQL adds an equivalent checked constraint.
- Seeded, newly provisioned, and balance-scenario empires start with zero inactive weight. JSON, SQL, and versioned imports normalise legacy allocations deterministically.
- The dashboard disables the two inactive sliders, labels them **Locked**, and rebalances only Military and Expansion.
- Activating either future programme requires a deliberate validation, persistence, UI, balance, and Cycle-transition change rather than merely enabling a control.

## 2026-07-14: Complete The Managed-SQL Cutover And Retire Runtime Files

Decision: run the trusted playground on Azure SQL through the existing SQL Server provider, prove seven-day point-in-time recovery with an isolated restore, and make SQL configuration unconditional for API and Worker hosts. Retain JSON only in named operator tooling, offline inspection, deterministic fixtures, and migration evidence.

Reasoning:

- The stopped deployed state passed strict conversion and import validation, so the safe activation point defined by Q119 and issue #126 has been reached.
- This subscription could not provision the selected free serverless tier in either UK region. France Central was the nearest eligible region and avoids choosing an always-billable tier merely to keep the database beside the F1 app.
- The Azure SQL free offer can stop at its monthly allowance; `AutoPause` exhaustion is a stronger cost boundary than using the available testing credit as an informal limit.
- A backup configuration claim is insufficient recovery evidence. The post-cutover point-in-time restore had to reproduce the schema and representative state through the authoritative store.
- Keeping `Cycles:RequireSqlRuntime` and a silent file fallback after the proved cutover would preserve an obsolete second host mode and weaken configuration failures.

Consequences:

- `CyclesDb` uses General Purpose Gen5 serverless in France Central with 2 vCores maximum, 0.5 vCores minimum, 32 GB maximum, provider-default 60-minute auto-pause, local backup storage, seven-day point-in-time retention, and free-limit exhaustion behaviour `AutoPause`.
- The cost policy deliberately permits the approved Azure SQL server/database while continuing to deny Container Apps, container registry, Application Insights, and Log Analytics resources.
- The final stopped JSON checkpoint is sensitive migration evidence. Once SQL-backed gameplay resumed, Azure SQL backup/restore became authoritative and the stale file ceased to be a normal rollback path.
- API and Worker require `ConnectionStrings:Cycles`, `Cycles:SqlConnectionString`, or `CYCLES_SQL_CONNECTION_STRING`; they no longer read `Cycles:StatePath` or `CYCLES_STATE_PATH` and cannot select `FileGameStateStore`.
- `state convert-runtime-file` is a bounded bridge from the retired unversioned runtime shape to the validated transfer envelope. It does not make raw file persistence a supported host mode.
- SQL migration discovery queries `master.sys.databases` by name, avoiding Azure SQL's current-database limitation for `DB_ID` and SQL clients that collapse a missing target into a login failure.
- The cutover checkpoint preserved all 23 persisted collection counts and 166 records. The reopened API passed health and authenticated gameplay checks, and an isolated restore reproduced the current 14-migration schema, active tick 3, and zero unresolved recovery.

## 2026-07-14: Remove The Executable File-Backed Game Store

Decision: remove `FileGameStateStore` and implicit path-or-provider selection after the proved managed-SQL cutover. Require SQL Server for every API, Worker, gameplay CLI, and operator CLI state operation while retaining JSON only through named transfer, validation, legacy-conversion, inspection, fixture, and migration-evidence paths.

Reasoning:

- A second executable datastore no longer provides rollback: once SQL accepted new gameplay, a file state became a stale divergent timeline.
- Keeping file-backed CLI seeding, ticking, orders, Cycle administration, and recovery would continue to exercise and document a runtime mode that the deployed game cannot use.
- Versioned import/export and the bounded legacy converter preserve the useful JSON capabilities without retaining locking, save, implicit seeding, or provider-selection code.
- Balance scenarios remain deterministic in-memory diagnostics and do not require a persistent file store.

Consequences:

- Gameplay and operator CLI commands require `sqlserver:<connectionString>` (or a raw SQL connection string) and reject file paths with an actionable error.
- The file store, fallback factory, and their redundant persistence test are removed.
- CI runs CLI seed, tick, and show checks against its disposable SQL Server service.
- JSON state is never mutated in place as live game state; explicit transfer output remains versioned, validated, sensitive operator data.

## 2026-07-14: Evaluate Scheduled Due State Inside The Cycle Lock

Decision: scheduled Workers ask the game-state store to run a tick only if it is due. The SQL implementation acquires the per-Cycle application lock before reading the latest completed-tick time and evaluating cadence.

Reasoning:

- A lock around tick execution serialises writers, but it does not by itself stop two Workers from both observing the same due state before either obtains the lock.
- Re-evaluating due state after lock acquisition lets a waiting Worker observe the first Worker's completed tick and return without advancing again.
- The same `TickSchedule` policy remains authoritative for in-memory tests and SQL-backed scheduling; database code only supplies the locked latest-completion value.

Consequences:

- Multiple Workers may poll the same active Cycle without turning one due observation into consecutive ticks.
- Explicit operator or Development tick execution remains an intentional unconditional operation; only the scheduled Worker path applies cadence.
- A singleton leader is not required solely for duplicate-due prevention. Health, shutdown, supported Cycle topology, operational signals, and deployment monitoring remain in issue #132.

## 2026-07-14: Treat The Galaxy Map As A Strategic Inspection Workspace

Decision: keep the Galaxy view anchored beside a scrollable selected-system inspector and derive interactive strategic lenses, route emphasis, search, zoom, pan, and local-fleet handoff from the existing empire-scoped dashboard data.

Reasoning:

- The existing map exposed the topology but made the player visually decode every system at one fixed scale.
- Presence, strategic value, output, history, adjacency, and local fleet context already exist in the filtered dashboard responses, so useful comparison and navigation do not require a broader API or new gameplay rules.
- The map and inspector serve different tasks: the chart preserves spatial context while the inspector carries the detailed, scrollable intelligence.

Consequences:

- The desktop map stage remains pinned to the available Galaxy workspace height while the inspector scrolls independently; narrow layouts stack the same information.
- Overview, Presence, Strategy, Output, and History lenses change visual emphasis only. They do not reveal data hidden by active-fleet visibility.
- Selecting or searching for a system highlights its immediate routes. Inspector links can focus adjacent systems or transfer a local player fleet into the existing Fleets command view.
- The Galaxy surface is intentionally allowed to lead the rest of the dashboard's visual ambition. A maximised viewport keeps the chart and inspector together, while named Galaxy, Sector, and Local ranges provide predictable spatial scale.
- Recovery is a first-class map capability rather than a reset afterthought: players can return to their home system, current target, strongest visible flashpoint, recent system locks, or any position on the overview navigator.
- Camera telemetry, the overview viewport frame, deterministic star field, target reticle, and animated selected routes are presentation derived entirely from already-visible data; none changes authoritative state or visibility.
- The original interaction was proved against the 24-system opening. That product boundary was explicitly superseded by the canonical sector-crown decision below.

## 2026-07-15: Adopt A Canonical Sector-Crown Galaxy

Decision: replace the 24-system next-test boundary with a canonical four-empire galaxy containing 16 named sectors, 280 systems, and 296 routes. Give each sector 12–24 systems in a local ring and exactly two distinct gateway systems. Connect the sectors as a crown so every sector has two parent-level neighbours; local routes take one tick and inter-sector bridges take two. Treat sector persistence, upgrade, API semantics, semantic map ranges, and the Galaxy-specific blue-violet/gold palette as one feature.

Reasoning:

- A technically connected scatter of 24 systems did not create a convincing strategic geography or exercise the Galaxy workspace's navigation model.
- Local rings make movement legible without turning every system into a high-degree hub. Two distinct gateways per sector create chokepoints and alternate strategic directions while preserving full connectivity.
- A ring at both the system-within-sector and sector-within-galaxy levels is deterministic, easy to validate, and robust enough for the current simulation without pretending to model astrophysics.
- Rendering all 280 systems at one visual weight would recreate the old clutter at a larger scale. Sector envelopes, bridge routes, gateways, and semantic Galaxy/Sector/Local ranges make scale meaningful rather than merely smaller.
- The rest of the dashboard already uses green as a dominant identity colour. A scoped navy, blue, violet, and gold cartographic palette gives the Galaxy view its own spatial mode without changing global UI semantics.

Consequences:

- Q125's 2026-07-14 24-system answer is explicitly superseded for the next player test.
- The curated Core seed, generated Docker fixture, default Development database, SQL profiler default, and 280-system successor Cycles use the sector crown. Existing 24-system curated states can be upgraded in place while retaining their original system identities; disposable Development databases may instead be reseeded.
- `GalaxySectors` and nullable legacy system membership are introduced by migration `015_add_galaxy_sectors`. Transfer format version 2 persists sectors while continuing to read pre-sector version 1 and legacy runtime documents.
- The operator upgrade takes both the broad game-state and active-Cycle tick locks. Hosted deployment stops the API, applies migrations, upgrades the map, deploys the new binary, and always attempts to restart the app.
- The `/galaxy` response includes sectors, gateway membership, and sector adjacency. The client exposes sector/system search, clickable sector envelopes, bridge-route distinction, keyboard-safe focus restoration, crown navigation, and scale-dependent detail without expanding visibility.
- The canonical scale is verified at 16 sectors and 280 systems. Larger or materially denser maps remain a conditional scaling decision, not an implied capability.

## 2026-07-15: Replace The Crown With A Compact Territorial Graph

Decision: supersede the 16-sector crown with a canonical four-empire galaxy containing 8 sectors, 64 systems, and 93 routes. Limit both hierarchy levels to 8 children. Give every sector an irregular connected 8-system, 10-route graph and exactly two gateway systems. Connect sectors through 13 bridges, allowing selected gateway systems to serve several lanes. Present the topology through three curated Galaxy, Sector, and Local compositions rather than continuous camera zoom.

Status: the 8×8 territorial scale and 10-route sector rule remain current. The exact 93-route/13-bridge graph is superseded by the authored-atlas decision below, which makes the visible 91-route/11-bridge chart authoritative.

Reasoning:

- Degree-two graphs can only form paths and cycles, so the former route limit mathematically forced the necklace appearance at both levels.
- Risk-style territorial maps depend on partial meshes: alternate routes, dead ends, chokepoints, and occasional hubs make position strategically legible.
- The 12–24 child counts made label and route clutter unavoidable. An eight-child ceiling lets each map range be composed rather than merely filtered.
- Two exit systems still give every sector a readable boundary. Allowing either exit to fan out creates rare, valuable hubs without turning every local system into an inter-sector gateway.
- Connectivity should create game value. Multi-bridge and high-degree gateways therefore receive additional strategic value and an initial historical signal.

Consequences:

- `territorial-graph-v2` is the canonical seed for the Core, generated Docker fixture, normal Development seed, successor Cycles, and trusted playground.
- The sector graph is connected with degree 2–4. Each sector remains internally connected with local system degree 2–4; gateway bridge degree is 1–2.
- The deployed Development database is deliberately reseeded rather than migrated from the intermediate 280-system crown. The deployment workflow requires an explicit manual `reseed` input for this destructive path and retains guarded upgrades for normal deployments.
- The `/galaxy` contract and sector schema do not change shape. Gateway and adjacency data remain derived from links, including gateway fan-out.
- Galaxy range shows regional strategy, Sector range shows eight local systems and their outbound bridge lanes, and Local range shows the selected neighbourhood. Continuous free zoom and raw bottom-of-map totals are removed.
- Scale beyond 8 sectors or 64 systems remains an explicit product and rendering decision, not an implied capability.

## 2026-07-15: Adopt A Fixed Authored Galaxy Atlas

Decision: make the canonical 8-sector and 8-system-per-sector distribution a designed spatial contract. Use one full-resolution galaxy chart and eight full-resolution sector charts as stable map layers, with position-aware SVG overlays for live labels, hit targets, selection, strategic lenses, route emphasis, visibility-safe intelligence, keyboard access, and command handoff.

Reasoning:

- The canonical map is deliberately finite. Procedurally redrawing the same fixed geography added rendering complexity without adding player value.
- Authored compositions can carry visual hierarchy, depth, atmosphere, and recognisable shapes more effectively than a generic layout algorithm while the overlays preserve real interaction and current data.
- A central hub made the galaxy strategically and visually monotonous. The 11 visible bridges form a connected partial mesh with sector degree 2–4, and the eight sector graphs use different 10-route compositions.
- Internal construction references such as `Y00` were useful during design but are not player-facing names.

Consequences:

- Galaxy, Sector, and Local are discrete authored compositions rather than positions on a freely pannable camera. Local retains the selected sector chart for orientation and subdues systems outside the selected neighbourhood.
- The image files contain no gameplay text. All names, status, ownership, metrics, focus states and accessible controls remain runtime overlays sourced from the existing empire-scoped response. Every route has an authored SVG trace so live colour and animation follow the painted curve instead of drawing a straight endpoint chord.
- No client rendering dependency is added. The existing SVG interaction layer hosts the atlas images and overlays.
- Canonical system and sector identifiers remain stable under `territorial-graph-v2`. The guarded topology upgrade can repair the previous canonical coordinates and route network in place when there are no fleets in transit and no pending orders.
- The generated SQL development fixture and normal seed use the visible 80-local-route plus 11-bridge graph. Hosted state is not changed merely by merging the client and seed update; deployment remains a separate guarded operation.

## 2026-07-16: Keep Route Topology Out Of Atlas Artwork

Decision: remove inter-sector corridors and internal system routes from the nine master atlas images. Keep the fixed territory silhouettes and node positions in the artwork, but make the SVG overlay the sole visual and interactive source of route topology.

Reasoning:

- Baked routes forced live state to trace image-generation curves exactly. Small deviations produced doubled lines and made an otherwise polished chart look imprecise.
- Route topology is gameplay data and may change during development. Separating it from the fixed geography allows routes to be recoloured, animated, rebalanced, or reconfigured without regenerating the atlas.
- Territory atmosphere and system placement benefit from authored imagery; route clarity benefits from deterministic vector geometry.

Consequences:

- The galaxy background retains exactly eight territory contours, and every sector background retains exactly eight fixed system nodes, but no connecting corridor pixels.
- All 11 inter-sector bridges and 80 internal routes are rendered from the canonical link graph. Selected routes use a narrow animated signal treatment; dormant routes remain calm gold vectors.
- Hover and selected-sector states use authored irregular contour paths rather than generic ovals.
- The existing topology upgrade, identities, API contract, pathfinding and movement rules are unchanged.

## 2026-07-16: Remove The Galaxy Overview Navigator

Decision: remove the floating overview navigator, recenter action, and recent-system lock history from the Galaxy map. Retain the compact Home, Selected, and Flashpoint focus controls, system-and-sector search, named map ranges, direct chart selection, and maximised map mode.

Reasoning:

- The fixed authored atlas already provides the orientation context that the miniature duplicated at lower fidelity.
- The 226×237-pixel floating card covered artwork that was not composed around it and added visual awkwardness at every desktop map range.
- Recent locks repeated selections that remain directly available through the chart, search, inspector links, and the three useful focus shortcuts.

Consequences:

- The authored chart owns the complete map stage without a permanent overlay in its lower-left corner.
- No recent-map breadcrumb state, navigator rendering, navigator event handling, or navigator-only CSS remains in the client.
- Home, Selected, and Flashpoint continue to recover useful context without changing authoritative state, visibility, routes, or map data.

## 2026-07-16: Label Promo Imagery By Provenance

Decision: make the public-site film a deliberate mix of current-build capture and generated concept dramatisation. Label both forms on screen, retain the source frames and prompts, and keep the distinction in the landing-page caption and production notes.

Reasoning:

- Dashboard footage proves the current command and map experience, but it cannot carry the full dramatic scale of gateway transit, fleet combat, or a galaxy surviving into its successor Cycle.
- Generated cinematic frames can express those core concepts without pretending that they are simulation output or an implemented battle renderer.
- Reproducible prompts and explicit labels preserve provenance while allowing the film to operate as a trailer rather than a screen recording.

Consequences:

- The 30-second master identifies Command, Galaxy, and authored-sector imagery as **Current build**.
- Gateway transit, Treaty Gate combat, and Cycle continuity are identified as **Concept dramatisation**.
- The film uses an original generated score and sound design, with no third-party audio in the current master.
- Current film assets, prompt text, timing, render inputs, and verification command are maintained in `src/Cycles.Api/wwwroot/media/PROMO-PRODUCTION.md`.

## 2026-07-16: Move Static Media Delivery To The Existing Cloudflare Worker

Decision: serve the public landing shell plus image and video artwork through the existing Cloudflare Worker's static-assets binding. Keep dashboard HTML, JavaScript and CSS, authentication, APIs and health checks on the Azure proxy path. Retain the high-quality film master outside `wwwroot`, deploy a sub-25-MiB web derivative, and exclude `wwwroot/assets` and `wwwroot/media` from the App Service package.

Reasoning:

- The live F1 plan exhausted its daily Data Out quota while using negligible CPU. Its 32.50 MiB film alone could consume the observed 225.5 MiB allowance in roughly seven uncached origin transfers.
- Cloudflare already fronts the custom domain, and Workers static assets on the Free plan add neither storage nor bandwidth charges. The binding therefore removes the dominant Azure egress without introducing R2 checkout, overage billing, another hosting account, or a second public hostname.
- A CRF 22/128 kbps derivative preserves the film's 1080p, 30 fps and 48 kHz stereo delivery contract at 10.93 MiB, 66% below the retained CRF 16/256 kbps master and safely under Cloudflare's 25 MiB per-file limit.
- The repository is public and the artwork contains no player, credential, or authoritative game-state data. Keeping executable dashboard assets and every dynamic route on the existing access-controlled origin preserves the meaningful playground boundary.

Consequences:

- `deploy/cloudflare` owns the explicit edge allowlist and static-assets binding. Unknown files and routes continue to Azure by default.
- Cloudflare must be deployed before an Azure revision that removes or changes edge assets. The API publish package deliberately provides no media fallback.
- Direct Azure image/video requests redirect to `cycles.anthonypwatts.co.uk`; a request already forwarded from that host fails rather than forming a redirect loop.
- Repeated media requests should not increase App Service Data Out. Azure bandwidth remains relevant only for the small application shell, authentication, APIs, health and any unexpectedly proxied traffic.
- The earlier binding-free Worker description is superseded for the trusted playground; the Worker still uses no paid feature, R2, KV, or paid observability.

## 2026-07-16: Make Command The Cross-Workspace Triage Hub

Decision: treat Command as the place to understand what needs attention and what is already committed for the next turn, not as a container for every specialised action. Place the four implemented workspaces inside an authored empire/Cycle shell with illustrated navigation, and keep future Strategy or Diplomacy destinations out of the navigation until they have bounded player-facing behaviour.

Reasoning:

- Galaxy already owns strategic spatial inspection, Fleets owns fleet detail and order planning, and History owns the factual and Chronicle record. Repeating those full tools in Command would create a second, weaker version of each workspace.
- A council agenda, command stream, strategic watch, frontier schematic, and order queue paired with a turn calendar let a player dip into the game, identify unfinished decisions, and commit the next turn without pretending Command is another specialist surface.
- The former flat application bar and generic navigation hierarchy communicated framework structure more strongly than game identity. Empire identity, Cycle state, authored destination imagery, and restrained archival instruments make the parent shell part of the game world without turning it into a decorative HUD.
- Strategy and Diplomacy are plausible future workspaces, but their remaining product gates do not justify dead tabs or invented controls now.

Consequences:

- The parent shell presents empire, home system, current Cycle, next-turn cadence, guide, refresh, and permitted turn advancement as consequential game state rather than account metadata.
- Command derives its agenda, frontier, stream, watch, resource, programme, and calendar content from the existing visibility-filtered opening briefing, fleet, order, event, empire, and galaxy contracts. It introduces no new gameplay authority or API shape.
- Agenda and frontier actions hand off to the existing Fleets, Galaxy, and History workspaces. Priority saving, pending-order cancellation, and development turn advancement retain their existing server-authoritative paths.
- The responsive shell preserves the four real destinations and can expand later without changing the current navigation contract.

## 2026-07-16: Keep The Trusted Playground Landing Page Public

Decision: remove the trusted playground's whole-site access-code override. Keep `/`, `index.html`, the landing stylesheet, static image/video artwork, and `/health` public. Require the shared playground code before serving `/app.html`, dashboard JavaScript/CSS, authentication routes, or game APIs.

Reasoning:

- The public landing page is the deliberate explanation and entry surface already selected under Q115; hiding it behind the playground code defeats that boundary.
- The Development application still requires a perimeter because username login is not suitable for untrusted access. Protecting the dashboard alone would leave its login and game APIs reachable directly.
- A small public allowlist keeps unknown routes protected by default and avoids duplicating every current and future application endpoint in the middleware.

Consequences:

- Anonymous visitors can view the landing page, film, atlas, and interface artwork on the custom domain. Direct-origin image/video requests redirect to that canonical edge route.
- **Enter the Build** reaches the playground access form. A successful code exchange redirects to `/app.html`.
- Dashboard HTML, scripts, styles, authentication routes, and game APIs remain behind the shared code before their normal route-specific authentication and authorisation checks.
- The earlier permission to use a whole-site perimeter remains available for a distinct deployment that must not be publicly discoverable, but it is no longer active on the trusted playground.

## 2026-07-17: Keep Maintained Screenshots Outside The Website Asset Set

Decision: use `docs/images` as the single maintained source for current Command and Galaxy screenshots. Use those captures in the root README, player guide, and reproducible promo-film inputs. Do not retain duplicate gameplay captures under `wwwroot/media` when the landing page has no runtime use for them.

Reasoning:

- The command-centre and expanded-atlas changes made the previous captures visibly stale.
- Duplicate screenshots under the public media tree could drift independently and were uploaded to Cloudflare despite having no landing-page consumer.
- One 1600×900 documentation set keeps project orientation, player guidance, and future film renders aligned with the same inspected UI state.

Consequences:

- `docs/images/cycles-dashboard-command-guide.png` and `docs/images/cycles-dashboard-map.png` are the maintained current screenshots.
- The README and gameplay guide reference that set directly.
- Promo production uses the same files as render inputs; generated video outputs retain their separate master and Cloudflare-delivery boundaries.
- The Azure website package continues to exclude every video and media file. The deployed film remains a Cloudflare static asset, while the trusted-playground access scope begins at `/app.html` and the landing page remains public.

## 2026-07-17: Give Public Promo Consumers A Stable Media Contract

Decision: publish the current web derivative and poster at duration-independent Cloudflare URLs. Consumers reference `/media/cycles-promo.mp4` and `/media/cycles-promo-poster.jpg` without manual version queries. Preserve the former duration-based film URL as a permanent redirect.

Reasoning:

- Duration and production labels describe a particular edit, not the durable identity of the public film.
- Cloudflare's static-asset responses already require revalidation and provide content-derived ETags, so the content can change behind a stable URL without pinning consumers to a stale filename.
- The main website and future consumers should not copy an 11.54 MiB derivative into their own deployments or coordinate markup changes for every verified edit.

Consequences:

- Cycles owns media generation, verification, publication, compatibility, and the canonical URL contract.
- Consuming repositories own presentation, playback lifecycle, provenance copy, responsive behaviour, and local fallback evidence.
- Publish and verify new Cycles edge assets before deploying a consumer that depends on them. The Azure package remains deliberately unable to serve media bytes.

## 2026-07-17: Treat Fleet Orders As Replaceable Next-Tick Intentions

Decision: allow at most one pending order per fleet and execution tick. Repeating the same intention is idempotent. Submitting a different intention must explicitly identify the current pending order being replaced; the previous order remains in history as `Superseded` and links to its replacement.

Reasoning:

- A fleet can perform only one mutually exclusive action at a tick, so accepting an unbounded stack of move, hold, attack, and colonise commands creates misleading commitments and avoidable processing failures.
- Automatic replacement matches command intent, while requiring the current order ID gives the dashboard a concrete confirmation boundary and prevents a stale client from silently overwriting a newer decision.
- Retaining superseded orders preserves the player's decision history and keeps cancellation, rejection, processing, and replacement as distinct outcomes.

Consequences:

- The SQL schema enforces one pending row for each Cycle, fleet, and execution tick. Migration deterministically retains the latest legacy pending intention and supersedes older conflicts.
- API requests return the existing order for an identical resubmission and return a state conflict for a different or stale unconfirmed replacement.
- The dashboard names the existing and proposed intentions before confirmation, then shows superseded records in order history.
- Tick-time validation can still reject a validly submitted intention when authoritative state changes before execution.
- Relative processing semantics between different fleets are unchanged. Simultaneous resolution or initiative remains a separate design question.

## 2026-07-17: Let The Starting Admiral Present The Opening Guide

Decision: use the player's existing fleet-attached starting admiral as the speaker throughout the Day One guide. Open with three general frames: welcome the player, introduce Command, then introduce the Map before moving into the current click-along objectives.

Reasoning:

- One named figure can establish the game's character voice while remaining a real participant in fleets, combat, reputation, and history rather than a separate tutorial mascot.
- The current fleet response already exposes the player's admiral name and stable ID, so the first presentation pass needs no new gameplay authority or duplicate character record.
- The guide is likely to be reworked. A small presenter layer and conventional opening sequence are useful now without treating the current tutorial copy or ordering as permanent.

Consequences:

- The guide panel shows the admiral's name and a portrait selected deterministically from the human portrait concepts using the admiral ID. The same persisted admiral therefore keeps the same presentation within the current model.
- Portrait choice remains a client presentation convention until species, portrait ownership, background meaning, or player selection receives an explicit model.
- The detailed resource, priority, map, fleet, order, turn, and history teaching remains otherwise provisional.

## 2026-07-17: Separate Persistent Players From Match Control

Decision: represent a persistent `Player` as either a human account or a game-AI identity, and assign that player to exactly one empire in a Cycle through a `MatchParticipant`. A match may contain up to six empire participants, never shares control of an empire, and may record the player who created it without treating that player as an owner. General `Faction` ownership covers both empire and neutral fleets.

Use a deterministic three-empire Development match for current play-testing. Tony and Will are the two selectable human players; Ariadne is game-AI controlled. Place their home systems in different sectors, give every empire three fleets totalling 60 ships and one starting admiral, and add a neutral Free Captains faction with six weaker fleets. Give each empire a real move, colonise, and neutral-attack opening briefing.

Reasoning:

- Persistent identity and per-match control have different lifecycles. Keeping them separate allows a human or AI player to participate in many future matches without making an empire a permanent account property.
- One participant per empire preserves unambiguous order authority while allowing defeated or completed participation to be recorded explicitly.
- Neutral fleets need combat, influence, history, and presentation identity but do not need an empire economy, diplomacy, resources, or a controlling participant.
- Distinct starting sectors, equal fleet totals, comparable home outputs, and deterministic randomisation make repeated Development matches varied enough to exercise the galaxy while remaining reproducible.
- A fixed Tony/Will selector is sufficient behind the existing access-code perimeter. Email, registration, invitations, matchmaking, and production identity remain separate decisions.

Consequences:

- Migration `017_add_match_participants_and_factions` adds player kind, optional Cycle creator, factions, match participants, and faction-based ownership while retaining nullable legacy empire references during transition.
- Influence and attacks are faction-based. Neutral fleets dilute local resource shares but receive no resources, and attacking them creates battle facts without inventing diplomatic relationships.
- The normal curated seed is `development-match-v2`; it creates three empire factions, one neutral faction, three active participants, nine empire fleets, six neutral fleets, and one opening briefing per empire.
- `Cycles:TrustedPlayerSelection:Enabled` must be deliberately enabled outside local Development, where it also requires the shared playground access code at startup. The trusted endpoints list active human accounts participating in the match, reject arbitrary or game-AI identities, and use a protected session cookie. Ended participants retain read-only access to the historical match but cannot mutate state.
- The six-empire ceiling is a model limit, not evidence that the current dashboard or balance is proved for six concurrent human players.

## 2026-07-18: Seal A Turn Ledger And Resolve Ticks In Explicit Batches

Decision: close the human command window when the configured tick deadline expires, then generate game-AI and neutral intentions from the same pre-resolution world state before sealing one complete turn ledger. Resolve that sealed ledger through explicit, deterministic phases; submission order does not participate. The sequence is resources; mandatory economy and scheduled construction; new programme spending and construction starts; arrivals and movement; combat; colonisation; derived control, visibility, and defeat state; progression for the next command window; then factual events and Chronicle selection.

Treat the sequence as part of the gameplay rules and compatibility surface. It controls economic liquidity, reinforcement timing, escape and defence, combat participation, colonisation survival, ranking inputs, and progression timing. A later feature or refactor may add work inside a phase while preserving those boundaries. Reordering phases requires an explicit product decision, focused regression coverage, updated forecasts and result presentation, and a review of saved-state or API meaning.

Use two distinct closure boundaries. The deadline first changes the Cycle from command-open to closing and rejects further human submissions. Internal AI and neutral planners may then append only their own commands. Validation, implicit Holds, and durable recording produce the final sealed ledger; no command may enter after that seal. **Advance turn** remains a Development or operator shortcut that invokes this same global closure pipeline early, not a player readiness mechanic.

Treat `CommandOpen -> Closing -> Sealed -> Resolving -> Publishing -> CommandOpen` as the intended authoritative lifecycle. A failure during resolution or publication enters the existing recovery-required boundary rather than partially opening the next command window.

Reasoning:

- A human command accepted while AI planning is in progress would create a timing race and could let the AI act against a different state from the one the player saw.
- AI must make decisions from information its empire could legitimately observe. It must not inspect hidden human intentions, and its generated commands must be persisted so a completed tick can be audited and reproduced.
- Submission time must not create initiative. Each phase calculates simultaneous outcomes from a common phase-start snapshot where the rules describe simultaneous behaviour; stable identifiers may order deterministic implementation work without granting gameplay priority.
- Explicit phases make economic timing, multi-tick journeys, combat participation, colonisation eligibility, progression effects, and event publication explainable to players and testable in isolation.
- Players need the phase order at command time because it changes the value and risk of Move, Attack, Colonise, priority, and construction decisions. Hiding it in implementation documentation would make correct outcomes feel arbitrary.
- Events describe committed outcomes. They must not act as substitutes for idempotent state transitions such as a one-time doctrine unlock.

Consequences:

- Missing human, AI, or neutral fleet commands normalise to Hold for audit and resolution without requiring a player-level Ready state.
- Current-tick income may fund current-tick programme spending. The command surface therefore needs to forecast expected income, reserved costs, automatic spending, and scheduled delivery clearly.
- Construction already due may complete before movement and may defend during that tick, but newly completed ships cannot receive commands that were sealed before they existed. Construction commissioned in the current tick does not also progress in that tick.
- Existing journeys advance and arrive within the movement phase; new movement dispatches are applied as a batch. Combat then forms from the post-movement system state. The first version has no route interception or pursuit; those require explicit later rules rather than hidden exceptions.
- Battles resolve independently by system from a shared post-movement snapshot. A fleet cannot gain priority or fight twice because its battle happened to be processed first.
- Colonisation resolves after combat. Its population cost is reserved during resolution and consumed only when an eligible surviving fleet successfully establishes the outpost.
- Research and other progression achieved during the tick become effective in the next command window unless a future rule explicitly says otherwise.
- Derived control, visibility, fleet availability, defeat conditions, facts, events, Chronicle entries, and the next command window are published only after authoritative phase outcomes are complete.
- The tick remains one authoritative atomic operation with the existing recovery boundary.
- Player-facing forecasts and results must describe causality through these phases. Submission timestamp and event display order must not imply initiative.

Implementation note: issue #137 persists the Cycle turn stage and each order's command source, sealed tick, and sealed time. Player command mutations share the Cycle tick lock. The first internal game-AI and neutral planners emit Hold by design, missing human intentions become deterministic implicit Holds, due ships form home reinforcements when an existing home fleet has another sealed command, and same-faction attacks against one opposing faction in a system form one battle. This establishes the lifecycle and audit boundary without inventing richer AI, interception, pursuit, or unresolved multi-faction alliance sides. Issue #138 owns clear phase-order presentation in the Command and result experience; issue #131 must gather evidence about player understanding as well as balance.

Known contention gap: if several eligible Colonise intentions from one empire compete for insufficient Population, stable fleet/order traversal in the current build decides which consumes the available stockpile. That behaviour is reproducible but grants an opaque identifier gameplay priority. Issue #139 asks for an explicit reservation or player-priority rule before this pattern spreads to other shared phase budgets.

## 2026-07-18: Recall Outbound Fleets Before Passive Arrival

Decision: treat post-dispatch reversal as a new `RecallFleet` intention rather than cancelling or rewriting the processed Move. An empire may recall an owned outbound in-transit fleet to its last occupied system. The sealed Recall resolves before passive arrivals, including when the original destination arrival is due in the same tick. Return duration equals the number of ticks already travelled outward.

A still-pending Recall may be cancelled through the normal order-cancellation boundary, which leaves the outward journey unchanged. A processed Recall records its own factual event and keeps the original Move in immutable history. Returning fleets cannot be recalled again.

Reasoning:

- Cancelling a processed Move would falsify the durable command history: the fleet did depart and spent time in transit.
- Resolving Recall after passive arrivals would make it useless for the current two-tick inter-sector journey, because the fleet would arrive before the new intention acted.
- Using elapsed outward travel makes reversal timing symmetric and explainable without introducing continuous position inside a route.
- The Fleets workspace is the natural control surface because it can show route, reversal tick, projected return, cancellation, and temporary command restrictions together.

Consequences:

- Fleet transit state records its departure tick as well as destination and arrival. Migration `020_add_fleet_departure_tick` backfills existing SQL journeys from link travel time; versioned and legacy JSON imports infer the same value when absent.
- The movement phase is now ordered Recall, passive arrivals, new Move, then Hold before combat. This is a deliberate gameplay-contract refinement rather than incidental implementation ordering.
- The commitment calendar suppresses the original destination forecast after Recall is queued and shows the projected return instead.
- Recall returns only to the last occupied system. Arbitrary diversion, stopping between systems, multi-hop return-home routing, interception, pursuit, and richer retreat rules remain separate product decisions.

## 2026-07-18: Deliver Dashboard Artwork As WebP Derivatives

Decision: retain the authored PNG files as artwork masters, but serve quality-90 WebP derivatives for the dashboard's navigation, resource, galaxy-overview, and sector artwork. Preserve the source dimensions and composition; change only the delivery encoding. Runtime CSS and JavaScript reference the WebP files, with a new version identifier for the JavaScript-owned atlas URLs and dashboard shell assets.

Reasoning:

- A measured cold or hard dashboard load transferred about 15.47 MiB of images, while in-UI refresh already transferred no image bodies and warm reloads reused cached bodies. The image encoding was therefore the dominant remaining UI-transfer cost.
- Representative q90 derivatives preserved fine star fields, route lines, labels, and background detail in full-size inspection while reducing individual files by 86–94%.
- Keeping the PNG masters avoids making a lossy delivery derivative the new source for later art, promo, or responsive-image work.
- WebP has broad support across the modern browser set expected for the trusted playground. A PNG fallback would cause extra CSS and testing complexity without helping the supported target.

Consequences:

- The 17 runtime derivatives total 4.08 MiB instead of 39.26 MiB for their PNG masters, an 89.6% reduction with unchanged dimensions.
- The nine authored images requested by the measured initial dashboard state fall from 15.38 MiB to 1.42 MiB before protocol overhead, a 90.8% reduction without changing the 23-request page shape.
- Sector charts remain lazy and load one at a time. The measured Umbral Marches request is 313 KiB rather than the 2.88 MiB PNG master.
- Retained masters still occupy the current Cloudflare upload set even though browsers no longer request them. The public-only bundle boundary in issue #144 may exclude source masters later; responsive derivatives and longer-lived cache policy remain separate backlog work.
