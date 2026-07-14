# Backlog

Last updated: 2026-07-14

This remains the repository's operative implementation queue until [issue #130](https://github.com/AnthonyPWatts/cycles/issues/130) completes the accepted ownership migration. After that migration, GitHub issues own concrete scope, acceptance criteria, ownership, live status, dependencies, and completion; this document becomes the curated roadmap, sequencing summary, decision-gate overview, and issue index. Completed behaviour belongs in [Project State](project-state.md), while durable rationale belongs in the [Decision Log](decision-log.md).

## Recommended Next Work

These items can proceed without another product-owner decision:

- [ ] Add a mixed-strategy balance scenario in which different policies compete in the same Cycle.
- [ ] Gather guided development-play evidence, then private-alpha evidence, on colonisation, combat, priority clarity, and order feedback before changing named balance constants.
- [ ] Add focused regression or live SQL Server integration coverage whenever an existing specified behaviour exposes a gap.
- [ ] Continue profiling retained-history and generic state paths when a measured caller or scenario justifies it.
- [ ] Improve diagnostics, CI, migration safety, documentation, and clean-checkout reproducibility when concrete friction appears.
- [ ] Fix correctness, data-safety, recovery, migration, and concurrency defects without changing player-visible rules.

Do not treat this list as permission for speculative refactors. `Cycles.Application`, provider-neutral repositories, typed fact contracts, and dashboard scaling remain conditional work: start them only when measured complexity or a selected feature requires them.

## Persistence And Operations

- [ ] [Demote JSON persistence to explicit import and export](https://github.com/AnthonyPWatts/cycles/issues/126): add operator-only, versioned, validated state transfer with sensitive-data handling; use it for the deployed cutover; then require SQL for API, Worker, and normal local-development runtime hosts.
- [ ] [Add external OIDC authentication and invited-player mapping](https://github.com/AnthonyPWatts/cycles/issues/122), preserving Cycles-owned empire and admin authorisation while keeping Development username login Development-only.
- [ ] [Add audited admin provisioning and revocation](https://github.com/AnthonyPWatts/cycles/issues/123), including explicit initial bootstrap, immutable role-change records, final-admin protection, and a separate break-glass operator procedure.
- [ ] [Protect the private dashboard while keeping landing and health public](https://github.com/AnthonyPWatts/cycles/issues/124), while retaining the trusted playground's whole-site access-code gate as an explicit deployment override.
- [ ] [Migrate the deployed playground to managed SQL and prove restore](https://github.com/AnthonyPWatts/cycles/issues/125), including JSON-state import, cutover validation, at least seven days of point-in-time recovery, one isolated restore, and deliberate cost-policy changes.
- [ ] Define the remaining hosting, migration ownership, and environment configuration after the accepted Azure SQL playground choice.
- [ ] Add production Worker health, singleton leadership, multi-Cycle scheduling policy, and operational monitoring.
- [ ] [Add persisted-running tick diagnostics](https://github.com/AnthonyPWatts/cycles/issues/120), including the accepted configurable five-minute suspicion default, SQL attempt context, end-to-end duration, and the strict no-automatic-recovery boundary.
- [ ] [Add audited abandoned-tick resolution](https://github.com/AnthonyPWatts/cycles/issues/121) so an inspected persisted `Running` attempt can be explicitly marked failed with an operator and reason before normal repair and recovery.
- [ ] Define secret handling and the remaining recovery-administration boundaries.

Production persistence and operations may proceed only within the accepted Q107-Q119 boundaries and their linked implementation issues. The import/export work in #126 must precede the deployed cutover in #125; Development auth and the local/private Worker are not production substitutes.

The cost-capped hosted playground is also not a production substitute. Its F1 App Service, persistent JSON file, manual Development turns, and restricted edge access exist only to gather trusted play evidence without opening a paid runtime path.

## Gameplay And Product Systems

### Diplomacy

- [ ] Add player-facing diplomatic offers, acceptance, declarations, and treaty lifecycle.
- [ ] Define alliance effects on combat, influence, ranking, movement, and shared visibility.
- [ ] Define diplomatic visibility, Chronicle selection, and cross-Cycle memory.

Blocked by Q013 and Q016-Q022. Q014 requires mutual acceptance for positive bilateral agreements. Q015 keeps war declarations, treaty termination, and pre-acceptance offer withdrawal unilateral without a separate warning period. The stored relationship vocabulary, attack aggression facts, and treaty-breaking cancellation behaviour are already implemented.

### Doctrine, Technology, And Intelligence

- [ ] Define player-selected doctrine or research-project semantics.
- [ ] Define whether research is spent, retained, or both.
- [ ] Add logistics only after its effect on travel, projection, construction, or supply is chosen.
- [ ] Add detection before cloaking and define what information either system can reveal or hide.
- [ ] Define alliance visibility and any stale or estimated intelligence model.

Blocked by Q023-Q046. Survey Projection remains the single automatic threshold unlock, not a complete doctrine system.

### Combat, Comeback, And Colonisation

- [ ] Tune combat or colonisation constants only from repeatable scenarios, guided development play, or later private-alpha evidence.
- [ ] Define comeback mechanics beyond minimum home-system presence.
- [ ] Define outpost capture, destruction, infrastructure, migration, and cross-Cycle treatment.
- [ ] Define whether home systems are merely protected by pressure or mechanically unconquerable.

Blocked where rules would change player-visible behaviour. Evidence gathering and diagnostic scenarios are ready now.

### Chronicle And Narrative

- [ ] Queue narrative work outside the tick transaction.
- [ ] Select an AI/provider boundary and durable job ownership.
- [ ] Define retry, fallback, review, safety, and player-visible failure behaviour.
- [ ] Decide which Chronicle thresholds become Cycle configuration.

Blocked by Q094-Q101 and related visibility questions. Deterministic templates, required-fact validation, generation status, and context snapshots already exist.

### Cycle Continuity And Named Figures

- [ ] Define richer successor-faction flavour and historical summaries.
- [ ] Define historical-system evolution beyond preserved names, significance, and strategic echoes.
- [ ] Define admiral transfer, promotion, retirement, succession, biography, and player-management rules.

Blocked until the corresponding continuity and character questions are selected. Current Cycle-end history and fleet-attached admiral behaviour are complete first slices.

## Technical Debt And Risks

- [ ] Complete [issue #127](https://github.com/AnthonyPWatts/cycles/issues/127): remove raw `FactJson` from ordinary player responses and replace the guide's direct parsing with a typed opening-briefing contract. Keep internal storage flexible and do not launch a broad fact migration before diplomacy and narrative shapes stabilise.
- [ ] Complete [issue #128](https://github.com/AnthonyPWatts/cycles/issues/128): lock camelCase properties and string enums, reject numeric enum input, and replace the message-only API error with stable codes plus safe messages and optional diagnostic detail.
- [ ] Complete [issue #129](https://github.com/AnthonyPWatts/cycles/issues/129): add explicit visibility and Cycle-history teaching to the resumable Day One guide, audit every instruction against the current UI, and keep the player guide aligned without introducing a separate help system.
- [ ] Revisit the generic whole-state SQL mutation bridge if profiling shows it on a high-frequency or scaling-critical path.
- [ ] Revisit dashboard rendering when an agreed galaxy/player scale exceeds the current small-map assumption.
- [ ] Add a production security review before any untrusted online test.
- [ ] Balance combat before making competitive balance claims.

## Product Decision Gates

The active queue is indexed by [GitHub issue #119](https://github.com/AnthonyPWatts/cycles/issues/119). Accepted answers must be copied into [Product Owner Questions](product-owner-questions.md) before implementation relies on them.

| Gate | Questions | Blocks |
| --- | --- | --- |
| Diplomacy | Q013 and Q016-Q022 | Player-action timing, alliance mechanics, visibility, Chronicle treatment, and memory. Q014-Q015 settle consent and unilateral hostile or terminating actions. |
| Visibility and intelligence | Q023-Q034 | Sensors, estimates, alliance sharing, public/private Chronicle detail. |
| Doctrine and technology | Q035-Q046 | Research choices, logistics, detection, cloaking, modifier scope. |
| Population and infrastructure follow-ons | Q047 onward in that area | Outpost evolution, comeback, further industry/population roles. |
| Narrative AI | Q094-Q101 | Provider, queue, fallback, review, and failure contract. |

Q107-Q110 and Q120-Q121 confirm behaviour already implemented and covered by tests: the scheduled Worker was created before further gameplay expansion, uses each Cycle's configured cadence without catch-up storms, manual player turn control remains a narrow Development-only exception, broader lifecycle controls remain restricted, player responses are DTO-only, and domain entities remain internal. They do not by themselves authorise the still-gated production operations or API/dashboard follow-on work above.

Q014 requires mutual acceptance for Alliance, peace, Non-Aggression Pacts, and any future trade or shared-visibility agreement. Q015 confirms that War declarations and treaty termination are unilateral, with no separate advance-notice or cooling-off period; pending offers may also be withdrawn before acceptance. Q013 and Q016-Q022 still block implementation, so no player-facing diplomacy ticket is ready yet.

Q111 selects a new diagnostic default rather than confirming existing behaviour. Issue [#120](https://github.com/AnthonyPWatts/cycles/issues/120) tracks the bounded implementation without authorising automatic recovery.

Q112 confirms the existing conservative block but requires a missing audited abandonment operation. Issue [#121](https://github.com/AnthonyPWatts/cycles/issues/121) tracks that operation without authorising automatic failure or recovery.

Q113 selects external OIDC authentication with local player correlation and Cycles-owned authorisation. Issue [#122](https://github.com/AnthonyPWatts/cycles/issues/122) tracks implementation.

Q114 selects explicit local admin bootstrap plus audited grants and revocations. Issue [#123](https://github.com/AnthonyPWatts/cycles/issues/123) tracks implementation without promoting the Development `isAdmin` switch into shared environments.

Q115 selects a public landing page and health endpoint with an authenticated, admitted dashboard. Issue [#124](https://github.com/AnthonyPWatts/cycles/issues/124) tracks the route boundary while preserving the current whole-site access-code gate as a trusted-playground override.

Q116 requires a managed-SQL cutover for the deployed playground plus database-native backup retention and a proved restore. Issue [#125](https://github.com/AnthonyPWatts/cycles/issues/125) tracks the migration and recovery evidence and consumes the importer now specified by Q119 and issue #126.

Q117 selects the existing SQL Server provider on managed Azure SQL for that cutover and first online test. Provider portability does not block issue #125 and remains conditional on measured cost, licensing, or hosting evidence.

Q118 confirms the existing provider-isolation default: justified Azure-SQL-compatible features may remain inside `Cycles.Infrastructure.SqlServer` and SQL migrations, while `Cycles.Core` and the store contract stay provider-neutral. No separate implementation issue is required.

Q119 demotes JSON to explicit import/export, inspection, fixtures, and migration evidence. Issue [#126](https://github.com/AnthonyPWatts/cycles/issues/126) owns the safe tooling/runtime-default sequence and must supply the verified importer consumed by issue #125.

Q130 defines complete state export/import as sensitive operator/admin support tooling for migration, recovery preparation, debugging, and reproducible fixtures. It is not a player-facing save/restore feature or a replacement for database backups. Issue #126 owns the authorisation and handling controls; any future player-sharing format requires a separate redacted design.

Q122 accepts flexible internal `FactJson` for another stage and establishes the trigger for a typed or validated contract: mechanical consumption, querying, migration, or public exposure. It does not authorise a broad fact-schema migration. Q123 subsequently settles the public-exposure and opening-briefing API boundary.

Q123 keeps raw `FactJson` out of the normal dashboard and ordinary player API. Display text remains the default presentation, with purpose-built typed detail only where it adds player value. Issue [#127](https://github.com/AnthonyPWatts/cycles/issues/127) removes the current raw response fields and replaces the guide's direct JSON parsing without changing internal fact storage.

Q124 locks the existing camelCase property and camelCase string-enum defaults before external clients exist, but does not freeze the current message-only error shape. Issue [#128](https://github.com/AnthonyPWatts/cycles/issues/128) adds stable machine-readable codes, safe messages, optional validation detail and trace correlation, with HTTP status remaining authoritative.

Q125 accepts the current 24-system, four-empire curated galaxy as the dashboard target for the next player test. No 50- or 100-system optimisation is required now; a larger target needs gameplay evidence and a fresh assessment of navigation, clustering, filtering, payload size, and rendering rather than denser use of the current layout.

Q126 makes desktop and laptop browsers the primary command surface while retaining a functional narrow-screen core loop. Equal mobile polish and touch-first redesign remain deferred unless tester usage makes mobile a primary play surface; the existing responsive implementation requires no separate issue now.

Q127 accepts the existing resumable Day One guide as the primary in-dashboard training path. Issue [#129](https://github.com/AnthonyPWatts/cycles/issues/129) adds the missing explicit visibility and Cycle-history teaching, audits instructions and targets against the current four-view UI, and keeps `docs/alpha-testers-guide.md` aligned without creating a separate help centre.

Q128 selects GitHub issues as the future authority for concrete actionable backlog work while keeping this document as a curated roadmap and sequencing summary. Issue [#130](https://github.com/AnthonyPWatts/cycles/issues/130) inventories every checkbox, creates or reuses only worthwhile concrete tickets, removes duplicate mutable status, and updates ownership guidance. This Markdown queue remains operative until that migration is complete.

Q129 confirms that maintained documentation describes current `main` rather than being copied per gameplay Cycle or ad hoc test build. Historical test evidence records the deployed commit/build and relies on Git tags, commits, or release notes when an old documentation snapshot is required; no implementation issue is needed.

## Completed Foundations

The following are established and should not remain as open roadmap stages:

- deterministic simulation, order lifecycle, combat facts, and explicit tick recovery;
- JSON and SQL Server stores, ordered migrations, focused SQL tick loading/writes, and per-Cycle locking;
- strategic priorities, ship construction, expansion projection, Survey Projection, and colonisation;
- development auth, actor-derived empire authority, active-fleet visibility, the accepted explicit-DTO/internal-domain API boundary, and order cancellation;
- scheduled Worker ticks, the accepted narrow Development-player turn exception, diagnostics, and running-API smoke coverage;
- the curated Day One opening, structured objective briefing, and resumable click-along dashboard guide;
- the F1 Free trusted playground deployment path, GitHub Actions workload identity, and Azure-side paid-resource guardrails;
- the task-focused Command, Galaxy, Fleets, and History dashboard views, contextual fleet actions, filterable Chronicle/Event records, and bounded resolved-order rendering;
- Cycle-end rankings, major-battle preservation, historical signals, and successor-Cycle generation;
- deterministic Chronicle reports, admirals, and the diplomacy storage/aggression foundation.

Git history and [Project State](project-state.md) hold the evidence; completed checklists are intentionally not duplicated here.

## Parking Lot

- native mobile clients;
- large-scale multiplayer deployment;
- real-time combat;
- individual planet or building management;
- public leaderboards and monetisation;
- modding support;
- AI-generated character biographies beyond an agreed narrative boundary.
