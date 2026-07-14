# Project State

Last updated: 2026-07-14

Cycles is a local, runnable pre-alpha development MVP. It proves the server-authoritative loop from galaxy generation through orders, tick resolution, factual history, Cycle completion, and successor generation. It is not yet an alpha release, production game service, or balanced multiplayer game.

## Capability Summary

| Area | Implemented now | Important limit |
| --- | --- | --- |
| Galaxy | Deterministic seeded systems, routes, home systems, resources, strategic/history fields, and a curated 24-system, four-empire development opening. | That scale is accepted for the next player test; 50- and 100-system dashboard behaviour is unverified and deliberately deferred. |
| Tick execution | CLI tick runner, scheduled Worker, accepted authenticated-development-player trigger, duplicate-running-tick guards, and explicit recovery state. | Production health, leader election, multi-Cycle scheduling, and deployment policy are undefined. |
| Influence and economy | Fleet-derived influence, home pressure, resource sharing, 100-point priorities, military ship construction, expansion projection, and one research unlock. | Industry and research priorities have no separate direct spending effects; long-run resource sinks are incomplete. |
| Orders | Durable move, hold, attack, colonise, and cancellation lifecycle with submission-time and processing-time validation. | The dashboard does not expose Hold, fleet creation, or fleet splitting. |
| Colonisation | Population-funded outposts that add supported local presence without binary ownership. | No capture, destruction, migration, infrastructure, or cross-Cycle inheritance. |
| Combat | Deterministic first-pass combat, battle facts, losses, events, and admiral outcomes. | Deliberately primitive and not balanced. |
| Diplomacy | Persisted Neutral, War, Non-Aggression Pact, and Alliance states; attacks record aggression and cancel breached treaties. | No player-facing offers, declarations, alliance effects, or shared visibility. |
| History | Chronicle scoring and template reports, per-tick metrics, final rankings, major-battle selection, system history signals, and successor-Cycle continuity. | No asynchronous AI narrative or richer historical-system evolution beyond the first continuity pass. |
| Identity and visibility | Development cookie login/session/sign-out, one player per empire, admin exceptions, and active-fleet fog-of-war. | External OIDC plus audited local admin provisioning is selected for private-alpha and Production but is not implemented. |
| Persistence | The transitional JSON development store, SQL Server store, ordered migrations, transaction locks, focused SQL tick workspace, and targeted tick writes are implemented. The trusted hosted playground still persists its single-process Development state as JSON on App Service storage. | JSON demotion, versioned import/export, mandatory runtime SQL configuration, and the Azure SQL playground cutover are accepted but not implemented. Generic API/admin SQL mutations still use the whole-state bridge. |
| Client | Public landing page and playable static dashboard with focused Command, Galaxy, Fleets, and History views, a resumable Day One guide, and responsive browser breakpoints. | Desktop/laptop command use is the accepted priority. The guide still needs explicit visibility and Cycle-history teaching through issue #129; the authenticated-dashboard boundary is also not yet implemented. |

## Implemented Rules

### Cycle And Tick

- A default Cycle lasts 90 days with a 60-minute tick cadence.
- The next simulation step is `CurrentTickNumber + 1`.
- The Worker checks immediately on startup, polls every 30 seconds by default, and runs at most one due tick per poll.
- Tick work uses a focused transactional working copy. Mutable entities are isolated; append-only facts are rolled back if processing fails.
- A failed tick records diagnostics, marks the Cycle `RecoveryRequired`, and blocks further ticks until an operator clears or retries it.
- The CLI exposes `diagnostics`, `recovery`, `recovery details`, `recovery clear`, and `recovery retry`.

### Curated Development Opening

- A normal CLI `seed [statePath]` and a missing Development-host store create the fixed `development-cold-start-v1` scenario. Explicit system, empire, or seed arguments retain generic deterministic generation.
- The Aurelian player begins with three genuine first-turn opportunities: move the Home Guard from Aster Vale to Nadir Crossing, establish an outpost from the Pale Harbour Survey, and attack the local Khepri force with the Treaty Gate Vanguard.
- Both principal empires retain 60 starting ships. The Treaty Gate outcome is resolved by the normal combat engine and is important enough to enter the Chronicle; it is not a scripted result.
- An empire-scoped `OpeningBriefingIssued` fact carries stable objective identifiers so the dashboard guide does not infer intent from display names.

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
- JSON responses use camelCase property names and camelCase string enums. Handled errors still return only `{ "message": "..." }`; [issue #128](https://github.com/AnthonyPWatts/cycles/issues/128) adds stable machine-readable codes, rejects numeric enum input, and locks the accepted compatibility boundary.
- Raw domain entities remain internal and are not returned to the dashboard.
- Event and battle DTOs still carry flexible `FactJson`, and the Day One guide parses the `OpeningBriefingIssued` payload. This now conflicts with the accepted ordinary-player boundary; [issue #127](https://github.com/AnthonyPWatts/cycles/issues/127) removes the raw response fields and supplies a typed opening briefing without changing internal fact storage.
- Player mutations derive empire authority from the authenticated development session rather than caller-supplied empire IDs.
- Players can see the full map structure, but exact presence, local fleets, events, last-tick facts, and Chronicle entries are limited by active-fleet visibility. Development admins can inspect all state.
- In Development, every authenticated player receives an **Advance turn** capability that invokes the same authoritative store operation used by the Worker and CLI. This does not change the player's role, visibility, or empire authority.
- Ordinary production players cannot execute ticks; a trusted admin can still use the protected operational endpoint.
- The dashboard uses persistent hash-addressable views: **Command** for resources, linked 100-point priority drafting, and pending commitments; **Galaxy** for the full map and selected-system inspection; **Fleets** for a selected-fleet command workspace plus filterable resolved orders; and **History** for separate, filterable **Chronicle** and **Events** records. The Command view keeps saved priority positions visible while a new allocation is being drafted and persists changes only through an explicit save.
- Desktop and laptop browsers are the primary command surface. The responsive layout retains readable narrow-screen access to the core loop without promising equal mobile optimisation or a touch-first interaction model.
- Fleet selection is the command context for Move, Attack, and Colonise, so action forms only ask for the target information that action needs. Chronicle entries expose their source tick and label both date and importance before the factual summary and narrative report.
- The Day One guide is scoped per player and seeded Cycle instance, requires the exact live objective orders at gated steps, switches to the relevant dashboard view, survives refreshes, and can be paused, skipped, or restarted from **Guide**.
- The guide currently teaches resources, priorities, map inspection, movement, colonisation, attack, pending commitments, turn resolution, Events, and Chronicle through real actions. It does not yet explicitly teach the active-fleet visibility boundary or the operator-driven Cycle end/successor boundary; [issue #129](https://github.com/AnthonyPWatts/cycles/issues/129) closes those training gaps and audits the copy against the current UI.

### Persistence

- `IGameStateStore` is shared by the CLI, API, and Worker.
- JSON is the default zero-service development store and uses file locking for writers.
- This JSON runtime fallback is transitional. Q119 requires issue #126 to replace it with explicit import/export and mandatory SQL configuration after the safe deployed cutover sequence.
- Q130 limits complete state export/import to operator/admin and developer support. Full exports contain private state across all empires, are sensitive artefacts, and are neither player save files nor database backups; issue #126 owns the missing controls and guidance.
- SQL Server migrations are plain ordered scripts under `database/migrations` and are tracked in `dbo.SchemaMigrations`.
- Migration `012_add_diplomatic_relationships` is the latest schema migration.
- Generic SQL `Replace` and `Update` operations synchronise the mapped prototype state with targeted deletes and upserts under the broad `Cycles.GameState` application lock.
- SQL ticks acquire `Cycles.Tick.{CycleID}`, load a Cycle-scoped workspace, and persist targeted outcome rows without loading or rewriting unrelated history.
- SQL Server-specific locking and persistence details remain contained in `Cycles.Infrastructure.SqlServer`; `Cycles.Core` and `IGameStateStore` remain independent of database packages.

### Trusted Hosted Playground

- `Cycles.Api` targets .NET 10 LTS and is deployed to an Azure App Service F1 Free plan for invited Development play-testing.
- GitHub Actions publishes a successful `main` build through workload identity federation; no long-lived Azure credential is stored in the repository or GitHub environment.
- The hosted process stores state at `/home/data/cycles-state.json`. The App Service persistent filesystem keeps the curated Development game across restarts and deployments.
- No `Cycles.Worker` process or database is deployed. Invited players use the accepted Development-only **Advance turn** capability.
- The hosting scope is protected by a read-only plan lock and an Azure Policy deny list for the known paid resource classes. F1 compute and storage quotas are the enforced spend boundary; budget notifications are not treated as a hard cap.
- `cycles.anthonypwatts.co.uk` is routed through a binding-free Cloudflare Worker on the Free plan. The direct Azure origin and the custom domain share an application-level access-code gate; only `/health` is public.
- The shared code admits Anthony and Will without adding a payment method or enabling usage overages. It is a trusted-playground boundary, not production identity or per-user authorisation.
- The whole-site access-code gate is an explicit deployment override. The accepted private-alpha and Production route contract instead keeps `/` and `/health` public while requiring external authentication and invited-player admission for `/app.html`.
- Before further tester invitations, the playground must migrate its current state through the existing SQL Server provider to managed Azure SQL, enable at least seven days of point-in-time recovery, and prove an isolated restore.

## Verification

Latest local verification on 2026-07-13:

```powershell
.\eng\test.ps1
```

Result: **144 tests passed, 0 failed**. The latest GitHub Actions run also passed the Linux build/test job and the migrated SQL Server integration job.

The automated coverage includes:

- simulation, influence, economy, orders, movement, combat, admirals, diplomacy, colonisation, Chronicle, Cycle end, continuity, and determinism;
- development auth, authorisation, visibility, API contracts, the hosted access-code gate, protected Development turn advancement, and Worker scheduling;
- the curated opening contract and its complete move, colonise, battle, event, and Chronicle outcome;
- tick rollback, recovery, duplicate-running-tick prevention, focused-working-copy equivalence, and a 2,160-tick retained-history scenario;
- migration discovery/application and opt-in live SQL Server coverage for round trips, focused tick loading and writes, recovery attempts, Cycle locks, rankings, successor continuity, admirals, diplomacy, and colonisation;
- the running-API development gameplay journey in `eng/alpha-gameplay-smoke.ps1`.

The SQL integration tests remain opt-in locally through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; CI runs them against a migrated SQL Server service container. See the [SQL Server runbook](../database/sqldockerdeploykit/README.md).

## Current Boundaries

The development build is suitable for trusted local or access-restricted hosted play-testing. The **Advance turn** exception and DTO-only player API boundary are now accepted product contracts. Before further tester invitations, the hosted playground needs its accepted managed-SQL cutover and proved restore. Before calling the build an alpha or inviting untrusted online players, the project also needs implementation of the selected production identity and admin-provisioning boundaries, plus decisions and implementation for hosting, Worker health and leadership, secrets, operational monitoring, and evidence that the opening teaches the intended choices.

Gameplay expansion is decision-gated in the [Product Owner Questions](product-owner-questions.md). The [Backlog](backlog.md) separates work that engineering can continue now from work blocked on those calls.
