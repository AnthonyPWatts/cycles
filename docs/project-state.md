# Project State

Last updated: 2026-07-17

Cycles is a local, runnable pre-alpha development MVP. It proves the server-authoritative loop from galaxy generation through orders, tick resolution, factual history, Cycle completion, and successor generation. It is not yet an alpha release, production game service, or balanced multiplayer game.

## Capability Summary

| Area | Implemented now | Important limit |
| --- | --- | --- |
| Galaxy | A deterministic 8-sector territorial graph with 64 systems, 91 routes, 16 gateway systems, distributed gateway fan-out, home systems, resources, strategic/history fields, and a curated four-empire development opening. | The canonical 64-system scale is browser- and SQL-verified. Larger or differently shaped galaxies remain unproved rather than an implied client contract. |
| Tick execution | CLI tick runner, scheduled Worker, SQL-atomic due execution, accepted authenticated-development-player trigger, duplicate-running-tick guards, configurable persisted-running diagnostics, explicit inspected abandonment, and recovery state. | Production health, shutdown behaviour, multi-Cycle scheduling, and deployment monitoring remain tracked by #132. |
| Influence and economy | Fleet-derived influence, home pressure, resource sharing, 100-point strategic-programme priorities, military ship construction, expansion projection, and one research unlock. | Development and Innovation are locked at zero until their accepted programme models receive bounded implementations; long-run resource sinks are incomplete. |
| Orders | Durable move, hold, attack, colonise, and cancellation lifecycle with submission-time and processing-time validation. | The dashboard does not expose Hold, fleet creation, or fleet splitting. |
| Colonisation | Population-funded outposts that add supported local presence without binary ownership. | No capture, destruction, migration, infrastructure, or cross-Cycle inheritance. |
| Combat | Deterministic first-pass combat, battle facts, losses, events, and admiral outcomes. | Deliberately primitive and not balanced. |
| Diplomacy | Persisted Neutral, War, Non-Aggression Pact, and Alliance states; attacks record aggression and cancel breached treaties. | No player-facing offers or declarations. The accepted first-version Alliance friendly-fire guard and factual-history contract are not implemented; shared visibility remains governed separately by Q025. |
| History | Chronicle scoring and template reports, per-tick metrics, final rankings, major-battle selection, system history signals, and successor-Cycle continuity. | No asynchronous AI narrative or richer historical-system evolution beyond the first continuity pass. |
| Identity and visibility | Development-only username login; non-Development OIDC/cookie authentication; exact issuer/subject invitation mapping; Cycles-owned empire/admin authority and audited bootstrap/grant/revoke; protected dashboard; active-fleet fog-of-war. | A concrete provider registration and deployed proxy/callback configuration are still required; the trusted playground keeps its public landing page separate from an explicitly access-coded Development application surface pending that production identity boundary. |
| Persistence | Mandatory SQL Server API/Worker runtime, ordered migrations, transaction locks, focused SQL tick workspace, targeted tick writes, strict versioned JSON transfer, bounded legacy-file conversion, Azure SQL playground cutover, and proved point-in-time restore. | Generic API/admin SQL mutations still use the whole-state bridge. |
| Client | Public landing page with a provenance-labelled 30-second development film at a stable Cloudflare media URL, plus a protected playable dashboard with an authored empire/Cycle shell; a Command triage hub with a data-driven council agenda, frontier schematic, command stream, strategic watch, programme controls, and turn calendar; focused Galaxy, Fleets, and History workspaces; an authored Galaxy/Sector atlas with live overlays; a resumable Day One guide including visibility and Cycle-history teaching; and responsive browser breakpoints. | Desktop/laptop command use is the accepted priority; narrow screens retain the core loop without equal mobile optimisation. The shell exposes only implemented workspaces; future Strategy or Diplomacy destinations remain decision-gated. The film's concept dramatisations show intended scale and tone, not simulated gameplay output. |

## Implemented Rules

### Cycle And Tick

- A default Cycle lasts 90 days with a 60-minute tick cadence.
- The next simulation step is `CurrentTickNumber + 1`.
- The Worker checks immediately on startup and polls every 30 seconds by default. Each poll asks the store to run at most one due tick; SQL evaluates due state after acquiring the Cycle tick lock so concurrent Workers cannot turn one due observation into two ticks.
- Tick work uses a focused transactional working copy. Mutable entities are isolated; append-only facts are rolled back if processing fails.
- A failed tick records diagnostics, marks the Cycle `RecoveryRequired`, and blocks further ticks until an operator clears or retries it.
- The CLI exposes `diagnostics`, `recovery`, `recovery details`, inspected `recovery abandon`, `recovery clear`, and `recovery retry`.
- Persisted `Running` attempts are suspicious after a configurable threshold that defaults to five minutes. Diagnostics report attempt identity, age, context, and recent finished durations without mutating state.
- A confirmed suspicious attempt can be marked failed only through the explicit operator/reason command; it remains recovery-blocked until normal repair and clear/retry.

### Curated Development Opening

- A normal SQL CLI seed and a missing Development-host SQL store create the fixed `development-cold-start-v1` scenario. Explicit non-canonical dimensions retain generic deterministic generation.
- The canonical map contains 8 named sectors of 8 systems. Each sector has its own connected 10-route composition and exactly two gateway systems. The sector-level graph has 11 bridges, degree 2–4, and no central hub; individual gateways carry one or two bridges. Local routes take one tick and inter-sector bridges take two.
- Highly connected gateways receive additional strategic value and an initial historical signal, making topological hubs tactically desirable and more likely to become notable places.
- The Aurelian player begins with three genuine first-turn opportunities: move the Home Guard from Aster Vale to Nadir Crossing, establish an outpost from the Pale Harbour Survey, and attack the local Khepri force with the Treaty Gate Vanguard.
- Both principal empires retain 60 starting ships. The Treaty Gate outcome is resolved by the normal combat engine and is important enough to enter the Chronicle; it is not a scripted result.
- An empire-scoped `OpeningBriefingIssued` fact carries stable objective identifiers so the dashboard guide does not infer intent from display names.

### Influence, Resources, And Growth

- Active ships create presence; in-transit and destroyed fleets do not.
- A founding empire has minimum home-system presence of 10.
- Each system divides Industry, Research, and Population output in proportion to effective presence.
- Resources are non-negative stockpiles, with last-generated and last-spent values recorded separately.
- Priority weights must total 100 and allocate strategic effort across Development, Innovation, Military, and Expansion rather than mapping one-to-one to the three resource stockpiles. While only two programmes are active, Military and Expansion share the full allocation.
- The persisted Industry and Research weights remain compatibility names for Development and Innovation. Both are locked at zero in domain validation, persisted state, imports, and the dashboard until their programmes are active.
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
- The dashboard uses persistent hash-addressable views inside an authored empire/Cycle shell: **Command** for quick-session triage through a council agenda, real frontier schematic, recent command stream, strategic watch, resource reserves, linked 100-point priority drafting, and a pending-order turn calendar; **Galaxy** for a blue-violet/gold authored atlas with one fixed galaxy composition and eight fixed sector compositions, system-and-sector search, Galaxy/Sector/Local ranges, strategic lenses, live gateway/route emphasis, selected-system inspection, and a maximised cartography mode; **Fleets** for a selected-fleet command workspace plus filterable resolved orders; and **History** for separate, filterable **Chronicle** and **Events** records. The shell's illustrated navigation is designed to expand, but it does not expose empty Strategy or Diplomacy tabs before player-facing behaviour exists. Command agenda, schematic, watch, stream, and calendar content is derived from existing visibility-filtered briefing, fleet, order, event, resource, priority, and galaxy responses rather than new authority or speculative mechanics. The route-free artwork is a stable spatial layer for territory atmosphere and fixed node positions; route topology, names, hit targets, selection, presence, lens metrics, accessibility and command handoff remain data-driven SVG overlays. The Galaxy view keeps compact home, selected-system, and strongest-visible-flashpoint focus controls without covering the authored chart with a duplicate overview navigator or recent-selection history. Its inspector links adjacent systems and hands local player fleets directly into their command context without expanding the empire-scoped API or visibility boundary. The Command view keeps saved priority positions visible while a new allocation is being drafted, locks Development and Innovation at zero, transfers points only between Military and Expansion, and persists changes only through an explicit save.
- Desktop and laptop browsers are the primary command surface. The responsive layout retains readable narrow-screen access to the core loop without promising equal mobile optimisation or a touch-first interaction model.
- Fleet selection is the command context for Move, Attack, and Colonise, so action forms only ask for the target information that action needs. Chronicle entries expose their source tick and label both date and importance before the factual summary and narrative report.
- The Day One guide is scoped per player and seeded Cycle instance, requires the exact live objective orders at gated steps, switches to the relevant dashboard view, survives refreshes, and can be paused, skipped, or restarted from **Guide**.
- The guide teaches resources, priorities, active-fleet visibility, map inspection, movement, colonisation, attack, pending commitments, turn resolution, Events versus Chronicle, and the operator-driven Cycle end/successor boundary through the current UI.
- The public site's 30-second film intercuts labelled current-build Command and Galaxy capture with labelled concept dramatisations of gateway transit, battle, and Cycle continuity. Its source frames, prompts, render command, and audio provenance are recorded beside the master rather than presented as gameplay evidence. External consumers use the stable `/media/cycles-promo.mp4` and `/media/cycles-promo-poster.jpg` contract without duration-based names or manual cache-busting queries.

### Persistence

- `IGameStateStore` is shared by the CLI, API, and Worker. Its scheduled path distinguishes a tick that ran from a Cycle that was not due.
- API, Worker, and gameplay/operator CLI commands use SQL Server exclusively. No executable file-backed game store or implicit store-selection factory remains.
- Operator CLI export/import uses a versioned envelope, requires every persisted collection, rejects incompatible or partial input, validates identifiers/references/tick recovery/embedded JSON, protects overwrite/replacement with explicit confirmations, and reloads SQL after import. The bounded `state convert-runtime-file` bridge converts and validates the retired unversioned file-store shape without modifying its source.
- Complete exports contain private state across all empires, identities, audit context, and hidden facts. They are sensitive operator/developer artefacts, not player save files or database backups.
- API and Worker require a Cycles SQL connection unconditionally and fail startup clearly when it is absent. The CLI likewise rejects file paths for gameplay and operator commands. JSON remains bounded to versioned transfer, validation, legacy conversion, inspection, fixtures, and migration evidence.
- SQL Server migrations are plain ordered scripts under `database/migrations` and are tracked in `dbo.SchemaMigrations`.
- Migration `015_add_galaxy_sectors` is the latest schema migration.
- Generic SQL `Replace` and `Update` operations synchronise the mapped prototype state with targeted deletes and upserts under the broad `Cycles.GameState` application lock.
- SQL ticks acquire `Cycles.Tick.{CycleID}`, load a Cycle-scoped workspace, and persist targeted outcome rows without loading or rewriting unrelated history. Scheduled attempts read the latest completed-tick time and evaluate cadence while holding that lock.
- SQL Server-specific locking and persistence details remain contained in `Cycles.Infrastructure.SqlServer`; `Cycles.Core` and `IGameStateStore` remain independent of database packages.

### Trusted Hosted Playground

- `Cycles.Api` targets .NET 10 LTS and is deployed to an Azure App Service F1 Free plan for invited Development play-testing.
- GitHub Actions publishes a successful `main` build through workload identity federation; no long-lived Azure credential is stored in the repository or GitHub environment.
- The hosted process reads and writes Azure SQL database `CyclesDb` in France Central through the sole App Service connection string, `Cycles`. No state-path or SQL-activation setting remains. The final stopped JSON file is retained only as sensitive cutover evidence and is not a rollback or recovery path.
- The 15 July 2026 sector-schema deployment applied migration 015 and upgraded the preserved tick-3 opening in place. Manual deployment run `29446689039` later used the explicit destructive `reseed` input to supersede that intermediate crown with the compact territorial graph. Independent protected API and browser checks confirmed 8 sectors, 64 systems, 93 routes, 13 bridges, 16 gateways, sector size exactly 8, gateway fan-out up to three, the three-range toolbar, and a clean console. The deployment retries the initial migration connection while serverless Azure SQL wakes from auto-pause and always attempts to restart the app after maintenance.
- The database uses the Azure SQL free serverless offer with a 2-vCore maximum, 0.5-vCore minimum, 32 GB maximum, provider-default 60-minute auto-pause, local backup storage, seven-day point-in-time retention, and automatic pause rather than billing when the free allowance is exhausted.
- No `Cycles.Worker` process is deployed. Invited players use the accepted Development-only **Advance turn** capability.
- The hosting scope is protected by a read-only App Service plan lock and an Azure Policy deny list for unapproved platform resources. The policy deliberately permits the approved Azure SQL server/database; F1 quotas and SQL free-limit exhaustion behaviour are the enforced spend boundaries, while budget notifications are not treated as a hard cap.
- `cycles.anthonypwatts.co.uk` is routed through a Cloudflare Worker on the Free plan. Its static-assets binding serves the public landing shell and all image/video artwork without Azure egress; dashboard HTML/JavaScript/CSS, authentication, game APIs, and `/health` retain the Azure proxy path. The API publish package excludes `wwwroot/assets` and `wwwroot/media`, so the Azure website upload contains no video assets, and direct-origin media requests redirect to the custom domain.
- The public 1080p/30 fps film is an 11.54 MiB CRF 22 web derivative that satisfies Cloudflare's 25 MiB static-asset ceiling. Its canonical URL is duration-independent, while the former duration-based URL redirects permanently for existing links. The reproducible 34.26 MiB CRF 16 master is retained outside `wwwroot` under `tools/promo` and is not deployed to Azure or Cloudflare.
- The shared code admits Anthony and Will without adding a payment method or enabling usage overages. It is a trusted-playground boundary, not production identity or per-user authorisation.
- The whole-site access-code gate remains available as an explicit deployment override but is not active on the trusted playground. The accepted private-alpha and Production route contract keeps `/` and `/health` public while requiring external authentication and invited-player admission for `/app.html`.
- The managed-SQL cutover preserved all 23 persisted collection counts and 166 records, and the reopened health plus authenticated gameplay smoke passed. Azure SQL retains seven days of point-in-time recovery; an isolated restore at the post-cutover checkpoint reproduced the current schema, all collection counts, active tick 3, and zero unresolved recovery, then the temporary paid restore database was deleted.

## Verification

Latest local verification on 2026-07-17 used the normal repository test helper:

```powershell
.\eng\test.ps1
```

Result: **250 tests passed, 0 failed** in the normal suite. The Cloudflare Worker has a further **5 passing Node routing tests**; Wrangler's deployment dry run accepted the complete static-asset set; and a Release publish produced no files under `wwwroot/assets` or `wwwroot/media`. A further **22 SQL Server state-store integration tests passed** against a disposable database, and all **6 generated Docker seed checks passed**. The end-to-end Development gameplay smoke passed login, priorities, pending movement, turn advancement, processed movement, resources, events, and the 8-sector/64-system topology assertions.

The retained film master and public web derivative both decode all 900 frames at 1920×1080 and 30 fps with 48 kHz stereo audio, and their final titles remain visible. The master keeps its documented −43.2 dB final-tail exception; the delivery-specific fade brings the web derivative inside the strict −45 dB final-250-ms gate.

The canonical territorial contract is covered at 8 sectors, 64 assigned systems, 80 locally varied routes, 11 inter-sector bridges, exactly two gateway systems per sector, distributed gateway fan-out, connected local/sector graphs, and an active Cycle ending 90 days after startup. The route-free authored atlas contract additionally verifies one galaxy image and eight sector images at native 992-pixel height and 1585–1586-pixel width. Live browser verification exercises Galaxy, Sector, and Local at 1918x1041 and 1280x800: Galaxy renders 8 contour-aware sector overlays and 11 SVG route paths; each sector renders exactly 8 system overlays and 10 SVG route paths; selected routes use the `map-route-flow` animation; no horizontal overflow, console warnings, or console errors are present.

The deployed custom-domain check repeated the route-boundary and media-delivery verification against the reseeded Azure SQL state. The public root returned `200`; unauthenticated `/app.html` returned the trusted-playground access form with `401`; the promo video returned `200` with a Cloudflare cache hit; and the direct Azure video URL returned `307` to the matching custom-domain URL. The protected app, login, API counts, and deployed Galaxy render had passed in the preceding authenticated smoke.

The 17 July UI-evidence refresh replaced the maintained 1600×900 Command and Galaxy screenshots, removed their redundant public-media copies, and regenerated the film from those canonical captures. Both 900-frame outputs passed full decode, timing, luminance, and audio-tail verification; the Cloudflare derivative is 11.54 MiB. A Release publish contained 66 files but zero video or `assets`/`media` files. Cloudflare revision `e0a90e57-4996-4f5b-97d1-c4e3d003311c` deployed the refreshed landing shell and edge media; live playback reached ready state 4, the hero used one matching preloaded image URL without console errors, and removed screenshot URLs returned `404`.

The duration-independent promo contract is deployed through Cloudflare revision `a190e52e-979f-4ee5-a976-3c62edad5aaf`. Real unauthenticated GETs return `200`, `video/mp4` and `image/jpeg` for the canonical film and poster, with Cloudflare cache hits, revalidation headers and content-derived ETags. GET and HEAD requests for the former duration-based film path return `308` to the canonical film, while a direct Azure request returns `307` to the same Cloudflare URL without serving media bytes. The landing page uses both canonical paths without query strings; browser playback reached ready state 4 and advanced without media or console errors. The consuming-site dependency is therefore cleared. A future real derivative replacement, rather than a sacrificial media deployment, should retain the URL and record the validator change as longitudinal evidence.

The automated coverage includes:

- simulation, influence, economy, orders, movement, combat, admirals, diplomacy, colonisation, Chronicle, Cycle end, continuity, and determinism;
- development and external identity mapping, authorisation, audited admin authority, dashboard admission, visibility, stable API contracts, the hosted access-code gate, protected Development turn advancement, and Worker scheduling;
- the curated opening contract and its complete move, colonise, battle, event, and Chronicle outcome;
- tick rollback, guarded abandonment, recovery, duplicate-running-tick prevention, focused-working-copy equivalence, and a 2,160-tick retained-history scenario;
- strict versioned state-transfer validation across every persisted collection and retained history, including the bounded legacy-runtime conversion path;
- migration discovery/application and opt-in live SQL Server coverage for round trips, external identity/admin audit persistence and immutability, focused tick loading and writes, recovery attempts, Cycle locks, rankings, successor continuity, admirals, diplomacy, and colonisation;
- the SQL-backed running-API development gameplay journey in `eng/alpha-gameplay-smoke.ps1`;
- explicit API and Worker startup failures when SQL configuration is absent;
- live Azure SQL migration/import/export round-trip evidence, deployed authenticated smoke checks, seven-day retention, and an isolated point-in-time restore.

The SQL integration tests remain opt-in locally through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; CI runs them against a migrated SQL Server service container. See the [SQL Server runbook](../database/sqldockerdeploykit/README.md).

## Current Boundaries

The development build is suitable for trusted local or access-restricted hosted play-testing. The **Advance turn** exception, DTO-only player API, external identity boundary, audited local admin authority, protected dashboard, explicit state-transfer tooling, mandatory SQL hosts, managed-SQL playground cutover, isolated restore proof, and duplicate-safe scheduled due execution are implemented contracts. Before calling the build an alpha or inviting untrusted online players, it still needs the remaining Worker health, shutdown, scheduling-policy, and monitoring work in #132, guided play evidence in #131, and the security gate in #133.

Gameplay expansion is decision-gated in the [Product Owner Questions](product-owner-questions.md). GitHub issues own actionable work; the [Backlog](backlog.md) curates sequence, decision gates, conditional risks, and links.
