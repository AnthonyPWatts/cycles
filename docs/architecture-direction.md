# Architecture Direction

Last updated: 2026-07-20

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
   |            Player-only account admission and local admin authority
   |
   +---- account/Game projections
   |
   +---- selected Game context ---- focused Cycle views and commands
   |
   +---- explicit Cycle resolution
                  |
                  v
            SQL Server store <---- Cycles.Worker
                  ^                    |
                  |                    +---- bounded due-Cycle query
                  |                    +---- Game-then-Cycle resolution
                  |
             Cycles.Cli

Operator CLI <---- versioned validated JSON ----> SQL Server

Cycles.Core owns simulation rules used by every host and store path.
Events, battles, Chronicle entries, metrics, and Cycle history are outputs.
```

The API and Worker have no online `IGameStateStore` dependency. Account queries, selected-Game reads, player commands, admin operations, explicit resolution and scheduled resolution use focused application contracts implemented directly by the SQL store. The generic whole-state bridge remains available only to explicit offline CLI/import, seed, continuation, profiling and compatibility workflows outside the online hosts; Cycle completion and failed-tick recovery use focused Game-then-Cycle mutations.

## Approved Multi-Game And Training Direction

The [multi-game and tutorial plan](multi-game-and-tutorial-plan.md) and its [test plan](multi-game-and-tutorial-test-plan.md) define the approved target architecture. MG-01 through MG-07 now implement its persistence, isolation, online-scope, batch-one scheduling, roster-aware profile, and account-shell prerequisites, but the only provisioned player product remains the legacy Game. `docs/project-state.md` remains authoritative for implemented behaviour.

- A player-visible `Game` contains one or more `Cycle` epochs and at most one operational Cycle. Each Cycle locks its own configuration, map, scenario, execution policy, seed, roster, and content provenance.
- `Player` owns account identity, `GameEnrolment` owns the durable Player-to-Game relationship, and `MatchParticipant` owns Cycle-specific empire authority.
- Authentication creates a Player without a Game seat. Enrolment and Cycle start create participation through explicit application operations.
- The browser adds an account-level Games shell above the four existing gameplay workspaces. Every selected-game route, query, mutation, and response carries explicit Game and Cycle context.
- Lobby/archive access uses a Game access context. Gameplay commands add non-null Cycle, participant, and empire authority.
- Account and lobby reads use bounded projections. Online writes use focused Game/Cycle stores; whole-state replacement remains an offline maintenance/import operation.
- Resolution acquires Game then Cycle locks because the same transaction may complete the Cycle and move its Game to Intermission or Completed.
- Twin Reaches is the first Training profile. Its four-resolution Core journey uses ordinary orders, ticks, facts, and recovery. Reset supersedes the old attempt and creates a new Training Game.
- Intermission requires each player to reconfirm before a successor Cycle. The first release uses in-app cross-Game urgency and does not add email or push.

The MG-01–07 technical hard gates have passed their integrated local evidence: scoped routes and stores, explicit Worker selection, resource authorisation, antiforgery, same-Cycle constraints, migration 025, immutable profile hashes/topology, deterministic roster materialisation, the bounded account Games projection, URL-authoritative selection, and mandatory SQL execution are verified. That clears implementation to proceed to private Training provisioning; it does not itself create or expose a second durable Game, and it does not authorise a new paid Worker host for the cost-capped playground.

The executable online source allowance is now empty: neither API nor Worker may reference `IGameStateStore` or its global/unspecified operations. Explicit `/games/{gameId}` gameplay routes resolve the complete account, Game, Cycle, participant and empire context. Existing unscoped player routes are compatibility adapters pinned to the deterministic legacy Game and call the same scoped handlers; they are not a second authorisation path. Legacy global active-Cycle selection still fails closed for offline compatibility callers if more than one Active Cycle exists.

The persistence prerequisites include the additive foundation, its scope contract and explicit scheduling capability. `GameState` and SQL carry `Game`, `CycleConfiguration`, `GameEnrolment`, `GameLifecycleEvent`, explicit participant Game scope, normalised battle-fleet membership, immutable `Scheduled`/`SelfPaced` configuration provenance and authoritative `NextTickAt`. Migration 022 deterministically adapts the existing lineage; migration 023 enforces every currently representable same-Game or same-Cycle relationship; migration 024 hardens external identity matching; and migration 025 backfills scheduling, enforces configuration/Cycle agreement and adds the filtered due index. State-transfer v7 validates these rules and adapts v1-v6 input. Operational import remains pinned to the deterministic legacy Game identity even though the transfer representation can describe several Games.

MG-03, MG-04 and MG-05 are implemented in the code boundary. `Cycles.Application` defines provider-neutral redacted account and bounded per-player Game projections, explicit Game/Cycle scope, a complete Player/Game/enrolment/Cycle/participant/empire command context, coherent one-Cycle views, allow-listed Cycle commands, due-Cycle discovery and explicit resolution. SQL resolves and revalidates the exact authority tuple directly. Read views use one serialisable snapshot, expose no Player credential material, and include only the selected Game and Cycle. Commands revalidate live authority, reject foreign or non-allow-listed changes and persist targeted rows with rollback. API mutations share the ASP.NET Core antiforgery-token policy, and resource identifiers are checked inside the resolved Game/Cycle scope. The Worker selects at most one due scheduled Standard Cycle per poll, then resolution rechecks the work under Game-then-Cycle locks; self-paced Cycles are not selected. A fixed legacy-scope query still provides fail-closed compatibility for the present single-Game dashboard.

The code-owned catalogue and roster-aware factory implement the MG-06 creation boundary, including the authored Twin Reaches topology and scenario. The MG-07 account shell can enumerate existing memberships and safely switch selected-Game workspaces, but it does not create a second Game, provision Training, or implement the tutorial journey. Those remain MG-08 onward. A second Game must still be created only through the scoped factory and account-shell paths, never by bypassing the verified prerequisite boundary.

## Project Boundaries

### `Cycles.Core`

Owns domain models, validation, simulation rules, influence, economy, combat, Chronicle scoring, Cycle completion, continuity, and the legacy simulation-state persistence interface.

It must not depend on database providers, HTTP concerns, authentication providers, file-system configuration, or narrative-service clients.

### `Cycles.Application`

Owns provider-neutral use-case contracts and projections that should not depend on HTTP or SQL. The current boundary covers redacted Player accounts, bounded Player-to-Game catalogue/access reads, explicit Game/Cycle and actor authority, coherent one-Cycle views, one-Cycle command execution, due-Cycle selection and explicit Cycle resolution over existing Core services.

Keep this layer small. It is not a generic repository framework and must not absorb simulation rules from Core, transport policy from API, or provider behaviour from infrastructure.

### `Cycles.Api`

Owns HTTP contracts, Development authentication, external OIDC/cookie authentication, local player/admin authorisation, visibility filtering, dashboard hosting, and player intention submission.

Ordinary order endpoints must not run ticks. The selected `POST /games/{gameId}/admin/tick` route invokes `ICycleResolutionStore.ResolveExplicit` and does not implement simulation logic in the API; `POST /admin/tick` is only its fixed legacy-Game adapter. After acquiring the Game and Cycle locks, the store locks and revalidates the complete live Game/Cycle/player/enrolment/participant/empire authority tuple in a fixed order and holds those rows through resolution commit. The route is available to admins in every environment and, temporarily, to an authenticated commandable player in Development. That capability does not promote the actor or bypass normal Game, Cycle, participant, visibility or empire authority.

Selected gameplay is exposed beneath `/games/{gameId}`. `SelectedGameRequestService` resolves the authenticated account, visible Game, operational Cycle and complete command context before invoking a focused view or command. The older unscoped routes resolve the fixed legacy Game first and delegate to the same handlers. All cookie-authenticated POST, PUT and DELETE routes use the shared antiforgery-token endpoint, header and validation filter.

Player API contracts use camelCase property names and camelCase string enums; numeric enum values are rejected. Handled failures retain meaningful HTTP status and expose stable machine-readable codes alongside safe human-readable messages, with optional validation detail and trace correlation. Clients must not branch on message wording.

Outside Development, OIDC proves an external issuer/subject identity and a secure application cookie restores the session. Exact configured identities govern admission; Cycles-owned player records govern empire and Admin authority. Provider role/group/email/display claims cannot elevate authority. `/` and `/health` remain public in the normal route contract, while `/app.html` challenges and verifies admission. Every game API independently resolves the local actor.

The dashboard's next-test scale is the curated 8-sector, 64-system galaxy with three active empires and neutral factions. The match model permits up to six empire participants, but that is a domain and persistence boundary rather than six-player browser evidence. Each map level has at most eight children, sectors and systems form irregular connected graphs, and each sector exposes exactly two gateway systems whose bridge fan-out may vary. Sector membership, gateway context, bridge routes, route-free atlas artwork, live SVG overlays, curated Galaxy/Sector/Local compositions, search, strategic lenses, Home/Selected/Flashpoint focus controls, and the selected-system inspector form one client contract rather than cosmetic grouping. The fixed compositions deliberately omit a miniature overview navigator, recent-selection history, continuous free zoom, and a recenter action. The current SVG map and bounded client-side lists do not promise support beyond that canonical scale or for arbitrary dense topologies; a larger target requires fresh payload, filtering, rendering, and interaction evidence.

Desktop and laptop browsers are the primary dashboard command surface. Responsive layouts must preserve a readable narrow-screen core loop for authentication, status and History, priorities, fleet selection, and basic order submission and cancellation without page-level horizontal scrolling. Equal mobile optimisation, a touch-first interaction model, and native mobile clients remain deferred until usage evidence selects mobile as a primary surface.

The resumable Day One guide is the primary in-dashboard training path and uses real controls and authoritative outcomes. It teaches priorities, visibility, order lifecycle, Events versus Chronicle, and the current tick/Cycle boundary using the current Command, Galaxy, Fleets, and History views. Keep contextual hints local and concise rather than creating a parallel help application.

### `Cycles.Worker`

Owns scheduled due-tick execution. It checks immediately on startup, polls on a configurable interval, requests at most one due scheduled Standard Cycle, and submits that exact Game/Cycle work item to the focused resolution store. Resolution rechecks the due timestamp and scope under Game-then-Cycle locks before applying a tick. Self-paced Cycles are not Worker work.

Production use still needs health reporting, leader election or equivalent singleton ownership, multi-Cycle policy, graceful shutdown expectations, and deployment monitoring.

### `Cycles.Cli`

Owns local development and operator workflows: seeding, inspection, manual ticking, migrations, recovery, Cycle completion and continuation, diagnostics, profiling, and balance scenarios.

It is an administrative convenience, not the scheduled production host. Complete state transfer remains here: versioned JSON is validated before SQL import, replacements require explicit confirmation, and private payloads are not exposed through player endpoints.

### `Cycles.Infrastructure.SqlServer`

Owns SQL Server connection handling, migrations, offline generic state persistence, focused account/Game projections, one-Cycle view and command workspaces, due-Cycle discovery, explicit Game/Cycle resolution, targeted command/tick writes, and transaction-scoped application locks.

## Tick Transaction Model

An authoritative tick follows this logical sequence:

1. acquire the Game lock and then the Cycle lock;
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

The in-memory path uses a focused transactional working copy and rolls back appended facts on failure. The SQL resolution path provides database transactionality with a Game resolution lock followed by the per-Cycle `sp_getapplock` named `Cycles.Tick.{CycleID}`. Ordinary Cycle commands need only that Cycle lock.

## Persistence Position

SQL Server is the selected runtime path. Q116 required the cost-capped trusted playground to migrate its current state and prove restoration from database-native backup; Q117 selected the existing SQL Server provider on managed Azure SQL for that cutover and first online test. Q119 demotes JSON to explicit, versioned import/export, validation, offline inspection, fixtures, and migration evidence. Q130 defines complete state transfer as sensitive operator/admin support tooling rather than a player-facing save/restore feature; any future sharing format must be separately designed and redacted. Following the completed cutover sequence in issues #126 and #125, API, Worker, and gameplay/operator CLI commands require SQL; no implicit file-store selection remains.

SQL Server-specific features are not categorically forbidden. They may be used inside `Cycles.Infrastructure.SqlServer` and SQL migrations when Azure SQL supports them and they materially improve correctness, consistency, measured performance, or operations. `Cycles.Core` and `IGameStateStore` remain provider-neutral, and material portability implications must be documented. The existing transaction-scoped `sp_getapplock` is the accepted model: a concrete concurrency guarantee kept behind the provider boundary.

Current SQL paths:

- generic `Replace` and `Update` load the complete `GameState` and synchronise mapped rows under the broad `Cycles.GameState` lock for explicit offline CLI/import, seed, continuation, profiling and compatibility work; API and Worker do not depend on this interface, and Cycle completion/recovery use focused mutations instead;
- focused account/Game queries issue bounded direct SQL projections; actor-context and Cycle-view queries revalidate the exact authority tuple and load one coherent, credential-redacted Cycle; `ICycleCommandStore` holds the Cycle lock and serialisable authority reads while persisting only allow-listed target-Cycle changes;
- `IDueCycleQuery` uses the filtered `NextTickAt` index to select one scheduled Standard Cycle whose materialised configuration agrees with the Cycle scheduling mode; `ICycleResolutionStore` acquires Game then Cycle, rechecks scope/capability/due state and any explicit caller authority, loads one focused tick workspace and persists targeted outcomes without loading unrelated retained history;
- plain SQL migrations under `database/migrations` are applied explicitly and recorded in `dbo.SchemaMigrations`;
- migrations 022 and 023 establish the legacy Game foundation and enforce non-null same-scope ownership; migration 024 hardens external identity matching; migration 025 adds immutable scheduling capability, coherent `NextTickAt` state and the due-Cycle index without introducing a second-Game writer.
- external issuer/subject correlation and admin-role audit records are persisted by migration 013.

The generic path is not an online extension point. Complete replacement/import remains a separately guarded offline maintenance concern, and the empty API/Worker source allowance prevents it from being reacquired accidentally.

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
