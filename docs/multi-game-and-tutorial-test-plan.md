# Multi-game and tutorial test plan

Status: MG-01 through MG-08 are implemented. MG-03–05 cut online consumers over to scoped routes/stores, antiforgery, scheduling and explicit resolution. MG-06 adds immutable profiles and deterministic roster-aware materialisation. MG-07 adds the bounded, URL-authoritative Games home. MG-08 adds allow-listed, idempotent private Twin Reaches provisioning plus SQL evidence that concurrent requests converge and an ordinary first Move survives authoritative resolution and a fresh-store return. The four-resolution server-derived journey, reset/recovery semantics, complete accessibility states and novice pilot evidence remain MG-09–10 work.

Companion plan: [Multi-game and tutorial programme](multi-game-and-tutorial-plan.md).

## 1. Purpose

This plan turns the programme's product and architecture promises into executable evidence. It covers the account shell, Game and Cycle lifecycle, enrolment, explicit authorisation, tutorial profiles and journey, SQL migration, concurrent Workers, failure recovery, accessibility and live rollout.

The existing xUnit project remains the automated test home. SQL Server integration tests remain locally opt-in through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`. The mandatory `sql-server-integration` CI job sets `CYCLES_REQUIRE_SQL_INTEGRATION=1`, and the fixture fails fast rather than returning or skipping when the connection string or database is unavailable. The same switch applies to predeployment evidence. The repository test helper remains the normal entry point. Static browser contract tests are useful but do not replace live interaction, keyboard or assistive-technology checks.

## 2. Non-negotiable invariants

Every release gate must preserve these properties:

1. One Player may have no Game, one Game or several Games.
2. One Game has one or more Cycles over its lifetime, but at most one operational Cycle.
3. Every gameplay read and mutation names a Game and resolves the Game's current Cycle explicitly.
4. A Game identifier, Cycle identifier or entity identifier is never authority by itself.
5. A Player can only command through their active MatchParticipant and Empire in that Cycle.
6. Tick, order, visibility, tutorial and reset effects cannot cross a Game boundary.
7. A failed Cycle enters recovery without blocking another due Cycle.
8. Login provisions an account, never an implicit seat or empire.
9. Tutorial mechanics use the normal order and tick path; browser acknowledgement cannot fabricate mechanical progress.
10. Tutorial reset preserves history and produces at most one replacement attempt for an idempotency key.
11. Profile key, version, hash and seed reproduce the locked topology and starting state.
12. Cycle completion and the containing Game lifecycle transition commit together.

## 3. Evidence layers

| Layer | Purpose | Expected home | Required cadence |
|---|---|---|---|
| Pure domain | lifecycle, policy, profile and journey predicates | `tests/Cycles.Tests` | every change |
| Deterministic simulation | real orders and resolution on Twin Reaches | `tests/Cycles.Tests` | every tutorial/mechanics change |
| Application contract | context resolution, authorisation and typed errors | `tests/Cycles.Tests` | every endpoint/use-case change |
| Static UI contract | routes, landmarks, copy hooks and client race guards | existing dashboard contract suites | every UI change |
| SQL integration | constraints, migration, scoped persistence and locking | SQL opt-in xUnit suites | every schema/store/Worker change; CI gate before merge |
| Live browser | interaction, responsive layout, map parity and stale responses | deployed or local SQL-backed app | each player-facing increment |
| Accessibility | keyboard, focus, zoom/reflow, semantics and text topology | live browser plus manual screen-reader pass | each tutorial/account-shell increment |
| Operational | metrics, recovery, rollback and feature flags | staging/deployed smoke | before enabling a flag for players |

## 4. Test-state builders

Extend `TestState` with composable builders rather than cloning the full canonical seed for every case:

- `PlayerBuilder` for zero-membership and multi-membership accounts;
- `GameBuilder` with purpose, lifecycle and immutable policy provenance;
- `CycleConfigurationBuilder` with sequence, profiles, seed, schedule and lock state;
- `CycleBuilder.ForGame(...)` with explicit predecessor/configuration;
- `EnrolmentBuilder` with status and audit history;
- `ParticipantBuilder` binding Player, Cycle and Empire;
- `TwinReachesFixture` with named systems, routes and scenario actors;
- `TutorialAttemptBuilder` with definition version, cursor and supersession;
- `DueCycleBuilder` with an injected clock and UTC `NextTickAt`.

Builders must default to valid objects and require explicit opt-in for invalid cross-Game combinations. Tests for constraints should name the exact invariant they violate.

## 5. Domain and lifecycle suites

Suggested suites:

- `GameLifecycleTests.cs`
- `CycleConfigurationTests.cs`
- `GameEnrolmentTests.cs`
- `GameLifecycleCoordinatorTests.cs`
- `GameAccessContextTests.cs`
- `GameCommandContextTests.cs`

Required cases:

| Area | Cases |
|---|---|
| Game lifecycle | all allowed transitions; every illegal transition; timestamps set once; Completed/Cancelled/Terminated terminal |
| Cycle lineage | first Cycle has no predecessor; successor belongs to same Game; sequence is monotonic; one configuration materialises once |
| Operational uniqueness | Active and RecoveryRequired occupy the same operational slot; completed Cycles release it; two Games may each be operational |
| Configuration locking | mutation before lock succeeds; mutation after lock fails; same version with a changed hash fails; retry materialises the same identity |
| Enrolment | one durable row per Game/Player; duplicate join is idempotent; withdrawal/reconfirmation append events; roster lock rejects withdrawal |
| End of Cycle | policy chooses Intermission or Completed; Cycle and Game transition together; a failed persistence transaction changes neither |
| Actor contexts | lobby context works without participant; command context requires participant and empire in named Cycle; admin policy is explicit |

Use table-driven tests for lifecycle matrices so adding a status requires updating the permitted-transition table.

## 6. Profile, map and scenario suites

Suggested suites:

- `ProfileCatalogueTests.cs`
- `TwinReachesProfileTests.cs`
- `CycleFactoryTests.cs`
- extend `CanonicalGalaxyTopologyTests.cs` for profile provenance.

Required cases:

- keys and `(key, version)` pairs are unique;
- serialised content produces the declared hash;
- changed content under an existing version is rejected at startup;
- coordinates, identifiers and names are unique and within bounds;
- topology is connected and all route travel times are legal;
- Twin Reaches contains exactly two sectors, ten systems and thirteen routes;
- every Twin Reaches system has the declared degree and the bridge is the only two-tick route;
- scenario references exist in its map and respect roster bounds;
- one human player produces one MatchParticipant; neutrals do not produce human participants;
- same configuration and seed produce byte-for-byte equivalent topology/provenance;
- renderer contract receives every local and bridge route, including profiles without atlas art.

## 7. Tutorial journey suites

Suggested suites:

- `TutorialJourneyEvaluatorTests.cs`
- `TutorialFoundationsSimulationTests.cs`
- `TutorialAttemptServiceTests.cs`
- `TutorialPresentationContractTests.cs`.

For every lesson, test the entry predicate, accepted evidence, completion predicate, blocked reason, retry route and fresh-attempt threshold from the main plan. Bind evidence to Player, Game, Cycle and durable fact identifiers.

Core golden path:

1. Provision a private Training Game and Twin Reaches Cycle.
2. Issue the ordinary T0 Move through `OrderService`.
3. Resolve through the ordinary authoritative tick boundary.
4. Prove movement outcome and unlock T1 without a browser-written completion flag.
5. Set priorities and colonise; prove both outcomes at T1.
6. Issue attack; accept either deterministic legitimate battle result, not a promised victory.
7. Issue one eligible self-chosen command and prove its processed fact at T3.
8. Mark Core complete once, reload, and prove completion persists.

Negative and recovery paths:

- rejected order exposes the real reason and does not erase prior facts;
- a different Player's fact, a different Game's fact and a presentation acknowledgement cannot satisfy a lesson;
- closing and returning resumes at the server-derived milestone;
- skip and pause remain distinct;
- duplicate acknowledgement is harmless;
- stale milestone cursor causes a bounded rebuild and produces the same result;
- reset racing resolution has the documented winner and never mutates the superseded Game;
- duplicate reset requests return the same replacement attempt;
- replay after completion creates a new attempt while preserving account-level completion;
- both Arrival and Recall complete the optional bridge concept;
- visibility consequences are expressed conditionally when combat outcomes differ.

Run the core golden path against the actual `TickEngine` whenever order or resolution rules change. Do not substitute mocked outcome events.

## 8. API, authorisation and security suites

Suggested suites:

- `GamesApiContractTests.cs`
- `GameScopedApiTests.cs`
- `GameResourceAuthorisationTests.cs`
- `AntiforgeryBoundaryTests.cs`
- extend `ExternalAuthenticationTests.cs` and `ApiOrderBoundaryTests.cs`.

For every selected-game endpoint, generate a matrix with:

| Actor/resource relation | Expected result |
|---|---|
| unauthenticated | authentication challenge |
| authenticated, unknown Game | same unavailable response as hidden Game |
| authenticated, foreign private Game | unavailable without existence disclosure |
| enrolled, lobby only | lobby/bootstrap subset; no command authority |
| participant in a different Cycle | forbidden typed result; no mutation |
| active participant in named Cycle | permitted according to Cycle state |
| defeated participant | read-only view; command denied |
| operator/admin | only the explicitly granted operation; no implicit player identity |

Hostile identifier tests must cross-combine valid IDs from Games A and B for Game, Cycle, enrolment, participant, empire, fleet, system, order, battle and tutorial attempt. Assert response shape as well as no database change.

Security cases:

- all cookie-authenticated POST/PUT/PATCH/DELETE routes reject a missing or invalid antiforgery token;
- a valid token works for JSON requests and DELETE;
- login/logout/token rotation do not leave a reusable stale token;
- idempotency keys are scoped by operation and actor and cannot replay another player's result;
- rate limits partition by authenticated Player and do not let one player exhaust another's allowance;
- lobby summaries and logs omit email, provider claims and hidden membership;
- auth callback creates a Player with zero enrolments and does not load or mutate the active Cycle.

## 9. SQL migration and persistence suites

Suggested suites:

- `SqlServerGameMigrationIntegrationTests.cs`
- `SqlServerGameStoreIntegrationTests.cs`
- `SqlServerGameLifecycleIntegrationTests.cs`
- extend `SqlServerGameStateStoreIntegrationTests.cs` for focused tick writes.

Migration fixtures:

1. empty database;
2. current canonical database with one active Cycle;
3. current database with completed historical Cycles;
4. recovery-required current Cycle;
5. deliberately contradictory two-operational-Cycle state;
6. partially expanded schema before backfill completion;
7. existing data containing a cross-Cycle relationship that must block constraint validation.

The mandatory job starts from the last production migration, applies every new migration, and then exercises two operational Games. It covers concurrent start/tick/reset and one negative case for every row in the composite-relationship matrix; a green job with zero SQL tests executed is itself a failure.

Centralise SQL availability in one `SqlIntegrationGuard` and tag every SQL suite consistently. Extend `eng/test.ps1` with `-RequireSqlIntegration`: it requires the connection string, sets the require switch, selects/reports the SQL category and writes a result file. Local helper runs without SQL explicitly exclude and report the category rather than counting early returns as passes; required mode fails on missing SQL or a zero executed count. The existing `sql-server-integration` CI job uses this switch and retains its CLI/gameplay smoke steps.

Required evidence:

- expand and backfill scripts are restartable;
- all current data maps to one legacy Game without invented predecessor links;
- canonical topology receives the standard profile and unknown topology receives legacy classification;
- each represented `(Game, Player)` produces exactly one enrolment;
- contradictory state aborts before non-null/unique constraints and reports actionable identifiers;
- operational-slot index rejects Active+RecoveryRequired in one Game and permits operational Cycles in different Games;
- configuration-to-Cycle one-to-one and same-Game predecessor constraints hold;
- scoped loads never return rows from another Cycle;
- focused tick persistence changes only its Cycle plus the required containing-Game lifecycle row/events;
- the administrative global save rejects or clearly labels partial state and remains absent from player routes;
- v1-v6 import deterministically applies the required compatibility and scheduling adaptations; a v7 export round-trip preserves Games, enrolments, configurations, battle membership, scheduling mode, next-due time and several operational Games;
- rollback of a pre-contract deployment leaves old columns/data readable.

## 10. Concurrency and hostile-timing suites

Use real concurrent SQL connections and barriers, not sequential calls pretending to race.

| Race | Required invariant |
|---|---|
| two final-seat joins | one seat, one durable enrolment, typed result for loser |
| duplicate Game start | one Cycle and one materialised configuration |
| start versus withdrawal | roster is either withdrawn-before-lock or committed-after-lock; no partial participant |
| two Workers on one due Cycle | exactly one tick and one next-due update |
| Workers on two Games | both may complete independently; no global lock serialization |
| Cycle A failure while B is due | A enters recovery; B resolves |
| reset versus tutorial resolve | one documented ordering wins; old and new Games never merge facts |
| two reset requests | one new attempt for the idempotency key |
| Game completion versus lobby read | reader observes old complete transaction or new complete transaction, never split lifecycle |
| shutdown after Cycle A | committed A remains durable; unstarted B remains due |

Assert lock timeout mapping, transaction rollback and event counts. Include a lock-order test harness that deliberately attempts reverse acquisition and fails fast in development/test rather than deadlocking indefinitely.

## 11. Worker, schedule and recovery suites

Suggested suites:

- `MultiGameWorkerTests.cs`
- `DueCycleSelectionTests.cs`
- `GameRecoveryIntegrationTests.cs`.

Required cases:

- selection orders by UTC `NextTickAt`, then stable Cycle ID;
- query returns a bounded identifier-only batch;
- null `NextTickAt`, Training self-paced policy, completed and recovery-required Cycles are excluded;
- the locked recheck skips a Cycle changed after discovery;
- success atomically writes facts, lifecycle event and next due time;
- failure clears due time and marks only that Cycle for recovery;
- one slow Cycle does not permanently starve later batches;
- batch saturation, oldest due age, duration and failure metrics have Game/Cycle-safe dimensions;
- cancellation is honoured between Cycle transactions, never midway through committed persistence;
- recovery returns a Cycle to a deliberate UTC due time without double-running it.

## 12. Browser, responsive and accessibility evidence

Retain the current source-contract suites for cheap regressions, then run these behaviours in a real browser:

- Games home at 1440x900, 1024x768, 768x1024 and 390x844;
- account nav, grouped selector and exactly four selected-game workspaces;
- canonical attention ordering and reason text;
- empty, loading, stale-cache, partial-error, full-error, forming, active, intermission, recovery, defeated and completed states;
- opening Games A and B in separate tabs, issuing commands and delaying A's response until after switching to B;
- stale or aborted responses never repaint the selected Game or re-enable mutation;
- journey rail at desktop, modal drawer at tablet and modal bottom sheet on mobile;
- focus enters and returns for modal variants; the pinned rail does not trap focus;
- skip, pause and fresh-attempt controls have distinct copy and confirmation;
- keyboard-only operation of Games home, selector, workspaces, map alternatives and journey;
- 200% and 400% zoom/reflow without clipped primary actions;
- visible focus, logical heading order, `aria-current`, live-region restraint and reduced-motion behaviour;
- Systems and routes list exposes topology and shares selection with the visual map;
- every Twin Reaches route is visible, including the two-tick bridge, without canonical artwork.

At least one screen-reader pass must cover: choosing a Game, reading its urgency, issuing the first tutorial Move through the non-visual system list, resolving it, and hearing/reading the outcome. Record tool/browser/version and defects; do not reduce this to DOM assertions.

## 13. Performance and capacity evidence

Establish a pre-change baseline for current dashboard bootstrap and focused tick. Before enabling several Games, measure:

- Games-home query count, p50/p95 duration and payload at 1, 10, 50 and 200 visible Games;
- bootstrap p50/p95 and allocation for canonical and Twin Reaches Cycles;
- due-selector duration with 10, 100, 1,000 and 10,000 historical Cycles;
- Worker throughput, oldest-due age and fairness with several due Cycles;
- journey evaluation with long event histories and snapshot/cursor rebuild;
- SQL plans proving use of player-enrolment, Game/status, due, configuration and tutorial-run indexes.

Initial acceptance targets are regression-based until production baselines exist: no more than 10% p95 regression for the legacy selected-game bootstrap, bounded Games-home payload, due selection that does not scan fact tables, and no full-state load on catalogue/lobby paths. Replace these with measured service-level thresholds before Increment 4 rollout.

## 14. Feature flags, rollout and rollback tests

Proposed flags:

- `GamesAccountShellEnabled`;
- `TrainingGamesEnabled`;
- `ManualGameEnrolmentEnabled`;
- `MultiCycleBatchEnabled` (batch-one explicit selection remains active when off).

Test every flag both off and on. Off must preserve the backfilled legacy Game through the same scoped handler; on must not create a second authorisation path. Flags control exposure and discovery, never database integrity or ownership checks.

For each increment:

1. migrate with flags off;
2. run invariant and SQL gates;
3. enable for operator accounts;
4. run two-account/two-tab live smoke;
5. enable for a small pilot cohort;
6. watch errors, unauthorised attempts, provisioning failures, due age and tutorial abandonment;
7. disable exposure if thresholds fail while preserving committed Games and attempts.

Rollback drills must prove that disabling a flag does not orphan a scheduled Cycle, discard a tutorial attempt or route a player to the wrong legacy Game.

## 15. Increment acceptance gates

### Increment 0

- Twin Reaches validation and deterministic first-Move simulation pass.
- Five novice baselines are recorded with consent and no production personal data in the repository.
- route, context, lifecycle and migration contracts are approved.

### Increment 1

- migration suites pass on all supported fixtures;
- two-tab and cross-Game hostile-ID tests pass;
- first normal Move resolves in Training and persists across leave/return;
- the standard Cycle remains independently resolvable while Training is self-paced: explicit Worker resolution in local/CI and existing manual Development advance in the cost-capped deployed pilot;
- no account login creates an enrolment or empire;
- global active-Cycle selection and whole-state mutation are absent from API, Worker and normal CLI execution before the Training Game is created;
- live keyboard smoke passes for the walking skeleton.

### Increment 2

- four-resolution core golden path passes using real mechanics;
- 4 of 5 non-implementer pilots finish without facilitator intervention;
- median first authoritative Move outcome is at most five minutes and core completion is 10–15 minutes;
- no observed completion is based on false or cross-Game evidence;
- accessibility journey and reset evidence passes.

### Increment 3

- join/withdraw/start races pass on SQL Server;
- operator-created cohort can progress from tutorial completion to a named lobby;
- time-to-seat and abandonment telemetry is visible;
- no AI seat is created.

### Increment 4

- duplicate-Worker, fairness, recovery and shutdown suites pass;
- production-like due load meets the measured threshold;
- cadence, leadership, shutdown and monitoring pass on a separately approved Worker host; the current free playground is not that gate;
- intermission transaction and reconfirmation policy pass end-to-end.

### Increments 5–6

- queue policy receives its own state/race matrix before implementation;
- compatibility route usage is near zero for the agreed window;
- removing adapters does not change scoped-handler contract tests;
- all flags and dual-read paths are removed only after rollback windows expire.

## 16. Commands and reporting

Focused local examples:

```powershell
.\eng\test.ps1 -Filter GameLifecycleTests
.\eng\test.ps1 -Filter TutorialFoundationsSimulationTests
.\eng\test.ps1 -Filter GameScopedApiTests

# Requires a configured CYCLES_SQL_INTEGRATION_CONNECTION_STRING and fails if no SQL tests execute.
.\eng\test.ps1 -RequireSqlIntegration
```

Run the complete repository suite once after the focused suites pass:

```powershell
.\eng\test.ps1
```

Run SQL-backed suites locally with the documented `CYCLES_SQL_INTEGRATION_CONNECTION_STRING` when Docker/SQL Server is available. The dedicated CI/predeployment job supplies it and sets `CYCLES_REQUIRE_SQL_INTEGRATION=1`; absence is a hard failure there. CI and handoff reports must distinguish unit/static-contract, SQL integration, live browser and manual accessibility evidence; “tests passed” without the layer is not sufficient.

Every implementation issue should link the relevant rows in this plan and report:

- focused checks run and result;
- full suite result or reason omitted;
- SQL fixture(s) exercised;
- browser sizes and account/Game states exercised;
- accessibility evidence when applicable;
- unverified risks and rollback state.
