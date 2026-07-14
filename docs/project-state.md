# Project State

Last updated: 2026-07-14

Cycles is a local, runnable pre-alpha development MVP. It proves the server-authoritative loop from galaxy generation through orders, tick resolution, factual history, Cycle completion, and successor generation. It is not yet an alpha release, production game service, or balanced multiplayer game.

## Capability Summary

| Area | Implemented now | Important limit |
| --- | --- | --- |
| Galaxy | Deterministic seeded systems, routes, home systems, resources, strategic/history fields, and a curated 24-system, four-empire development opening. | That scale is accepted for the next player test; 50- and 100-system dashboard behaviour is unverified and deliberately deferred. |
| Tick execution | CLI tick runner, scheduled Worker, accepted authenticated-development-player trigger, duplicate-running-tick guards, configurable persisted-running diagnostics, explicit inspected abandonment, and recovery state. | Production health, singleton leadership, multi-Cycle scheduling, and deployment monitoring remain tracked by #132. |
| Influence and economy | Fleet-derived influence, home pressure, resource sharing, 100-point priorities, military ship construction, expansion projection, and one research unlock. | Industry and research priorities have no separate direct spending effects; long-run resource sinks are incomplete. |
| Orders | Durable move, hold, attack, colonise, and cancellation lifecycle with submission-time and processing-time validation. | The dashboard does not expose Hold, fleet creation, or fleet splitting. |
| Colonisation | Population-funded outposts that add supported local presence without binary ownership. | No capture, destruction, migration, infrastructure, or cross-Cycle inheritance. |
| Combat | Deterministic first-pass combat, battle facts, losses, events, and admiral outcomes. | Deliberately primitive and not balanced. |
| Diplomacy | Persisted Neutral, War, Non-Aggression Pact, and Alliance states; attacks record aggression and cancel breached treaties. | No player-facing offers or declarations. The accepted first-version Alliance friendly-fire guard and factual-history contract are not implemented; shared visibility remains governed separately by Q025. |
| History | Chronicle scoring and template reports, per-tick metrics, final rankings, major-battle selection, system history signals, and successor-Cycle continuity. | No asynchronous AI narrative or richer historical-system evolution beyond the first continuity pass. |
| Identity and visibility | Development-only username login; non-Development OIDC/cookie authentication; exact issuer/subject invitation mapping; Cycles-owned empire/admin authority and audited bootstrap/grant/revoke; protected dashboard; active-fleet fog-of-war. | A concrete provider registration and deployed proxy/callback configuration are still required; the trusted playground remains on its explicit whole-site Development override until cutover. |
| Persistence | SQL Server store, ordered migrations, transaction locks, focused SQL tick workspace, targeted tick writes, strict versioned JSON transfer, and a fail-fast SQL-runtime activation flag. | The trusted hosted playground still persists JSON until #125 imports/verifies it in Azure SQL and proves restore; only then may #126 remove the fallback. Generic API/admin SQL mutations still use the whole-state bridge. |
| Client | Public landing page and protected playable dashboard with focused Command, Galaxy, Fleets, and History views, a resumable Day One guide including visibility and Cycle-history teaching, and responsive browser breakpoints. | Desktop/laptop command use is the accepted priority; narrow screens retain the core loop without equal mobile optimisation. |

## Implemented Rules

### Cycle And Tick

- A default Cycle lasts 90 days with a 60-minute tick cadence.
- The next simulation step is `CurrentTickNumber + 1`.
- The Worker checks immediately on startup, polls every 30 seconds by default, and runs at most one due tick per poll.
- Tick work uses a focused transactional working copy. Mutable entities are isolated; append-only facts are rolled back if processing fails.
- A failed tick records diagnostics, marks the Cycle `RecoveryRequired`, and blocks further ticks until an operator clears or retries it.
- The CLI exposes `diagnostics`, `recovery`, `recovery details`, inspected `recovery abandon`, `recovery clear`, and `recovery retry`.
- Persisted `Running` attempts are suspicious after a configurable threshold that defaults to five minutes. Diagnostics report attempt identity, age, context, and recent finished durations without mutating state.
- A confirmed suspicious attempt can be marked failed only through the explicit operator/reason command; it remains recovery-blocked until normal repair and clear/retry.

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
- Rankings remain per empire regardless of diplomatic relationships: allies do not pool map control or become joint winners.
- Influence and resource shares remain per empire regardless of diplomatic relationships: allied fleets may coexist in a system, but their effective presence is not pooled.
- `cycle next` creates a successor only after the source Cycle completes and no other Cycle is active. It preserves player continuity and selected famous-system names and significance without carrying mechanical empire advantages.

### API And Dashboard

- Ordinary player endpoints expose filtered state and accept intentions through explicit response DTOs.
- JSON responses use explicit camelCase property names and camelCase string enums. Numeric enum input is rejected. Handled errors return the correct HTTP status with stable `code`, safe `message`, optional structured `details`, and optional `traceId`.
- Raw domain entities remain internal and are not returned to the dashboard.
- Event, battle, and tick DTOs omit internal `FactJson`. The dashboard receives a purpose-built empire-scoped opening-briefing contract; internal flexible fact storage remains unchanged.
- Player mutations derive empire authority from the authenticated development session rather than caller-supplied empire IDs.
- Players can see the full map structure, but exact presence, local fleets, events, last-tick facts, and Chronicle entries are limited by active-fleet visibility. Development admins can inspect all state.
- In Development, every authenticated player receives an **Advance turn** capability that invokes the same authoritative store operation used by the Worker and CLI. This does not change the player's role, visibility, or empire authority.
- Ordinary production players cannot execute ticks; a trusted admin can still use the protected operational endpoint.
- Every game-state endpoint derives an authenticated local actor. Outside Development, anonymous `/app.html` requests start OIDC, authenticated identities must be explicitly admitted, and `/` plus `/health` remain public unless the trusted-playground perimeter override is configured.
- External identities map by exact issuer and subject. Provider email, display name, groups, roles, and invitation state do not grant local admin authority. Explicit configured bootstrap and authenticated grant/revoke operations append high-severity audit records; routine revocation cannot remove the final active admin.
- The dashboard uses persistent hash-addressable views: **Command** for resources, linked 100-point priority drafting, and pending commitments; **Galaxy** for the full map and selected-system inspection; **Fleets** for a selected-fleet command workspace plus filterable resolved orders; and **History** for separate, filterable **Chronicle** and **Events** records. The Command view keeps saved priority positions visible while a new allocation is being drafted and persists changes only through an explicit save.
- Desktop and laptop browsers are the primary command surface. The responsive layout retains readable narrow-screen access to the core loop without promising equal mobile optimisation or a touch-first interaction model.
- Fleet selection is the command context for Move, Attack, and Colonise, so action forms only ask for the target information that action needs. Chronicle entries expose their source tick and label both date and importance before the factual summary and narrative report.
- The Day One guide is scoped per player and seeded Cycle instance, requires the exact live objective orders at gated steps, switches to the relevant dashboard view, survives refreshes, and can be paused, skipped, or restarted from **Guide**.
- The guide teaches resources, priorities, active-fleet visibility, map inspection, movement, colonisation, attack, pending commitments, turn resolution, Events versus Chronicle, and the operator-driven Cycle end/successor boundary through the current UI.

### Persistence

- `IGameStateStore` is shared by the CLI, API, and Worker.
- Normal local API and Worker instructions use SQL Server. The file store remains bounded to deterministic fixtures, offline inspection, and the ordered deployed migration.
- Operator CLI export/import uses a versioned envelope, requires every persisted collection, rejects incompatible or partial input, validates identifiers/references/tick recovery/embedded JSON, protects overwrite/replacement with explicit confirmations, and reloads SQL after import.
- Complete exports contain private state across all empires, identities, audit context, and hidden facts. They are sensitive operator/developer artefacts, not player save files or database backups.
- `Cycles:RequireSqlRuntime=true` makes API/Worker startup fail clearly without SQL. It is implemented for deliberate activation after #125's deployed import and restore proof; the fallback is not removed early because doing so would break the current hosted playground.
- SQL Server migrations are plain ordered scripts under `database/migrations` and are tracked in `dbo.SchemaMigrations`.
- Migration `013_add_external_identity_and_admin_audit` is the latest schema migration.
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

Latest local verification on 2026-07-14 used the migrated local SQL Server container:

```powershell
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

Result: **180 tests passed, 0 failed**. The repository CI runs the Linux build/test job and the migrated SQL Server integration job; these working-tree changes have not been pushed and therefore have not run in GitHub Actions.

The automated coverage includes:

- simulation, influence, economy, orders, movement, combat, admirals, diplomacy, colonisation, Chronicle, Cycle end, continuity, and determinism;
- development and external identity mapping, authorisation, audited admin authority, dashboard admission, visibility, stable API contracts, the hosted access-code gate, protected Development turn advancement, and Worker scheduling;
- the curated opening contract and its complete move, colonise, battle, event, and Chronicle outcome;
- tick rollback, guarded abandonment, recovery, duplicate-running-tick prevention, focused-working-copy equivalence, and a 2,160-tick retained-history scenario;
- strict versioned state-transfer validation across every persisted collection and retained history;
- migration discovery/application and opt-in live SQL Server coverage for round trips, external identity/admin audit persistence and immutability, focused tick loading and writes, recovery attempts, Cycle locks, rankings, successor continuity, admirals, diplomacy, and colonisation;
- the running-API development gameplay journey in `eng/alpha-gameplay-smoke.ps1`.

The SQL integration tests remain opt-in locally through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; CI runs them against a migrated SQL Server service container. See the [SQL Server runbook](../database/sqldockerdeploykit/README.md).

## Current Boundaries

The development build is suitable for trusted local or access-restricted hosted play-testing. The **Advance turn** exception, DTO-only player API, external identity boundary, audited local admin authority, protected dashboard, and explicit state-transfer tooling are implemented contracts. Before further tester invitations, the hosted playground still needs the managed-SQL import/cutover and proved restore in #125, followed by mandatory SQL activation/fallback removal in #126. Before calling the build an alpha or inviting untrusted online players, it also needs production Worker leadership/health in #132, guided play evidence in #131, the security gate in #133, and the remaining deployment/monitoring work those issues expose.

Gameplay expansion is decision-gated in the [Product Owner Questions](product-owner-questions.md). GitHub issues own actionable work; the [Backlog](backlog.md) curates sequence, decision gates, conditional risks, and links.
