# Backlog

Last updated: 2026-07-20

GitHub issues are authoritative for concrete actionable work: scope, acceptance criteria, ownership, status, dependencies, and completion. This document is the curated roadmap, sequencing summary, decision-gate overview, and issue index. It deliberately does not mirror issue checkboxes or live status.

Completed behaviour and verification belong in [Project State](project-state.md). Accepted product answers and unresolved gates belong in [Product Owner Questions](product-owner-questions.md). Durable rationale belongs in the [Decision Log](decision-log.md). [Issue #130](https://github.com/AnthonyPWatts/cycles/issues/130) records the ownership migration.

## Current Sequence

1. Finish and verify the remaining identity, admin, and dashboard boundaries in [#122-#124](https://github.com/AnthonyPWatts/cycles/issues/122), plus the Training-only guidance and accessibility evidence in [#129](https://github.com/AnthonyPWatts/cycles/issues/129).
2. Continue shared scheduled-execution hardening through [#132](https://github.com/AnthonyPWatts/cycles/issues/132). Explicit batch-one due selection and Game-then-Cycle resolution are now in place; health, leadership, graceful shutdown, monitoring, and any later bounded several-Cycle policy remain.
3. Reconcile successor continuity through [#156](https://github.com/AnthonyPWatts/cycles/issues/156): preserve the current factual name, significance, and strategic galaxy echoes while proving that prior players, empires, and winners receive no inherited power. Richer burned-system and historical-naming effects remain later bounded slices rather than part of this reconciliation.
4. Gather guided play and balance evidence through [#131](https://github.com/AnthonyPWatts/cycles/issues/131) before further tuning combat, colonisation, priority, turn-processing, or order-feedback rules. The evidence must test whether players understand the fixed phase order as well as whether its outcomes are balanced. The accepted one-intention-per-fleet replacement, command-closure Colonise reservation, and processing-order contracts define the current baseline.
5. Complete the threat model and security evidence in [#133](https://github.com/AnthonyPWatts/cycles/issues/133) before any untrusted online test.
6. Continue dashboard data and asset-request efficiency through [#141-#145](https://github.com/AnthonyPWatts/cycles/issues/141). The consolidated bootstrap now uses the focused one-Cycle SQL view; actor-safe validators, topology splitting and abuse guardrails remain. Public Cloudflare asset routing in [#144](https://github.com/AnthonyPWatts/cycles/issues/144) can proceed independently once its strict public-only bundle boundary is agreed. Current production pressure is low, so this sequence remains P2 anticipatory hardening unless measurements worsen.

This sequence does not authorise speculative gameplay expansion. The active product-decision queue remains indexed by [issue #119](https://github.com/AnthonyPWatts/cycles/issues/119).

The 18 July command-window and tick-resolution decision is implemented through [#137](https://github.com/AnthonyPWatts/cycles/issues/137): human closure, internal planning, implicit Holds, a durable sealed ledger, shared Cycle locking for command mutations, and explicit deterministic phase boundaries. [Issue #146](https://github.com/AnthonyPWatts/cycles/issues/146) adds Ariadne's first bounded strategic policy: locally visible favourable attacks, threat Holds, affordable outposts, and movement towards valuable reachable systems; neutral factions remain positional Hold obstacles. The processing order is a gameplay contract because it governs income, reinforcement, movement, combat, colonisation, ranking, and progression timing. [Issue #138](https://github.com/AnthonyPWatts/cycles/issues/138) presents that contract in Command, separates projected automatic effects from committed deliveries, confirms game-wide Development closure with private aggregate counts, keeps ongoing journeys visible, and groups Events by phase. The Standard Council Agenda retains its genuine opening-objective hand-offs and authoritative outcomes without acting as a tutorial or progress gate. [Issue #152](https://github.com/AnthonyPWatts/cycles/issues/152) makes direct-route duration and projected dispatch/arrival visible before Move submission and repeats the current projection on the queued intention; resolution still revalidates the route. A bounded Recall intention reverses an outbound journey to its last occupied system before passive arrival. [Issue #131](https://github.com/AnthonyPWatts/cycles/issues/131) owns evidence that players understand processed dispatch, continuing transit, reversal, return timing, and the player-facing phase explanation; [Q069 / issue #57](https://github.com/AnthonyPWatts/cycles/issues/57) keeps any longer-than-next-tick command activation decision-gated. Route interception, pursuit, arbitrary diversion, coordinated or adaptive game-AI strategy, combat forecasting, and other later command types remain separate decisions.

The 18 July data-overhead investigation measured nine generic whole-state loads and 243 SQL commands for one in-UI refresh, rising to ten loads and 270 commands for a browser reload. Consolidation first reduced both paths to one request, one generic load and 27 SQL commands; MG-03 and MG-04 now replace that final API whole-state read with one scoped Cycle view while preserving actor scope and selected-fleet context. [#142](https://github.com/AnthonyPWatts/cycles/issues/142) adds actor-safe validation and short-lived caching, [#143](https://github.com/AnthonyPWatts/cycles/issues/143) separates stable topology from dynamic state, [#144](https://github.com/AnthonyPWatts/cycles/issues/144) removes unnecessary Worker-first public asset requests, and [#145](https://github.com/AnthonyPWatts/cycles/issues/145) adds cancellation and abuse guardrails last. Private empire responses must not be cached at Cloudflare.

The dashboard shell currently exposes the four implemented Command, Galaxy, Fleets, and History workspaces. Command owns cross-workspace triage and next-turn commitments; specialised work remains in its dedicated view. The navigation can grow when a bounded player-facing Strategy or Diplomacy workspace exists, but decision-gated systems do not receive empty or disabled tabs in advance.

## Actionable Issue Index

| Theme | Authoritative issues | Sequencing note |
| --- | --- | --- |
| Tick diagnosis and inspected abandonment | [#120](https://github.com/AnthonyPWatts/cycles/issues/120), [#121](https://github.com/AnthonyPWatts/cycles/issues/121) | Diagnosis remains read-only; abandonment requires an explicit operator and reason before normal recovery. |
| External identity, local admin authority, and private dashboard | [#122](https://github.com/AnthonyPWatts/cycles/issues/122), [#123](https://github.com/AnthonyPWatts/cycles/issues/123), [#124](https://github.com/AnthonyPWatts/cycles/issues/124) | OIDC proves identity; Cycles owns admission, empire, and admin authority. |
| Player API and Training teaching contracts | [#127](https://github.com/AnthonyPWatts/cycles/issues/127), [#128](https://github.com/AnthonyPWatts/cycles/issues/128), [#129](https://github.com/AnthonyPWatts/cycles/issues/129) | Keep player responses typed, wire conventions explicit, and Training guidance tied to real controls and outcomes. Standard opening objectives remain ordinary gameplay rather than tutorial state. |
| Guided play and balance evidence | [#131](https://github.com/AnthonyPWatts/cycles/issues/131) | Evidence precedes named constant or rule changes. |
| Production Worker operation | [#132](https://github.com/AnthonyPWatts/cycles/issues/132) | Builds on the completed managed-SQL deployment and mandatory runtime-host configuration. |
| Deterministic turn ledger and resolution | [#137](https://github.com/AnthonyPWatts/cycles/issues/137) | Implements command closure, first-pass Hold planners, durable sealing, phase order, and command/tick locking; richer AI and unresolved multi-faction combat remain separate. |
| Deterministic game-AI strategy | [#146](https://github.com/AnthonyPWatts/cycles/issues/146) | Implements Ariadne's first visibility-respecting attack, defence, colonisation, and expansion policy while leaving neutral factions positional. |
| Player-facing turn processing | [#138](https://github.com/AnthonyPWatts/cycles/issues/138) | Presents stage, phase order, forecasts, commitments, game-wide closure context, continuing journeys, and phase-ordered results without changing the rules established by #137. |
| Move journey projection | [#152](https://github.com/AnthonyPWatts/cycles/issues/152) | Shows one-hop duration and projected next-tick dispatch/arrival before and after submission while preserving authoritative route revalidation. |
| Same-phase resource contention | [#139](https://github.com/AnthonyPWatts/cycles/issues/139), [#154](https://github.com/AnthonyPWatts/cycles/issues/154) | Implemented for Colonise: reserve from stockpile plus current-turn income at closure, and reject the whole otherwise-eligible set when its complete cost cannot be funded. |
| Allied live visibility | [#155](https://github.com/AnthonyPWatts/cycles/issues/155) | Shares current active-fleet system visibility automatically between allies without pooling any other empire state or settling stale-contact rules. |
| Successor continuity boundary | [#156](https://github.com/AnthonyPWatts/cycles/issues/156) | Reset participant-specific power while retaining deterministic history-driven changes to the successor galaxy. |
| Dashboard refresh and data efficiency | [#141](https://github.com/AnthonyPWatts/cycles/issues/141), [#142](https://github.com/AnthonyPWatts/cycles/issues/142), [#143](https://github.com/AnthonyPWatts/cycles/issues/143), [#145](https://github.com/AnthonyPWatts/cycles/issues/145) | The focused SQL bootstrap path is implemented; actor-safe validators, topology reuse, and final cancellation/rate-limit guardrails remain. |
| Public asset request efficiency | [#144](https://github.com/AnthonyPWatts/cycles/issues/144) | Move approved public assets to asset-first Cloudflare routing without exposing protected dashboard or source files. |
| Pre-untrusted-test security gate | [#133](https://github.com/AnthonyPWatts/cycles/issues/133) | Review the implemented identity, authorisation, data-transfer, proxy, persistence, and deployment boundaries together. |
| Public media consumer contract | [#134](https://github.com/AnthonyPWatts/cycles/issues/134) | Cycles owns the stable Cloudflare film and poster URLs; each consuming site owns its presentation, fallback, and responsive behaviour. |

## Decision-Gated Roadmap

These are roadmap themes, not implementation tickets. Create bounded implementation issues only after the linked product questions settle enough behaviour to build.

### Diplomacy

Q013-Q018 now establish next-tick durable actions, mutual consent for positive agreements, unilateral hostile/terminating actions, first-version Alliance limits, independent rankings, and independent allied influence. Q019-Q022 still gate disclosure, Chronicle treatment, NPC participation, and cross-Cycle memory, so the complete player-facing lifecycle is not yet ready. Q025's independently bounded automatic allied live visibility is implemented through [#155](https://github.com/AnthonyPWatts/cycles/issues/155).

### Visibility, Doctrine, Technology, And Intelligence

Q023-Q025 retain the fully visible topology and implement live detailed visibility from the player's own active fleets plus active fleets belonging to current allies. Q026-Q034 still gate stale-contact precision and persistence, Chronicle disclosure, sensor semantics, historical visibility, and ranking exposure. Q038-Q046 continue to gate selectable doctrine categories and branches, logistics, detection, cloaking, and modifier scope. Q035-Q037 retain Survey Projection as the non-consuming universal introduction and select a future player-chosen, Research-consuming project model.

### Combat, Comeback, And Colonisation

Q050-Q052 and Q054-Q056 still gate comeback mechanics, home-system protection, ship classes, rally points, and capacity limits. Q047-Q049 confirm the current Population-funded, fleet-supported outpost model; Q053 selects bounded Development/construction capacity as Industry's next role; Q057 keeps raw output influence-share based; and Q058 permits factual resource, population, infrastructure, and battle history to create successor-system echoes without carrying player stockpiles, ownership, or infrastructure benefits. Current combat, colonisation, and priorities may be exercised through [#131](https://github.com/AnthonyPWatts/cycles/issues/131), but rule or constant changes require repeatable evidence and any applicable remaining product answer.

### Chronicle And Narrative

Q097 establishes a provider-neutral interface with replaceable live-provider connectors and a deterministic development/test connector. Q094-Q096 and Q098-Q106 still gate queued narrative ownership, runtime failure/fallback, tone, inference, required facts, review/safety, threshold configuration, interaction, privacy, versioning, and player-visible failure behaviour. Deterministic templates, required-fact validation, generation status, and retained context remain the current safe runtime boundary.

### Cycle Continuity And Named Figures

Q058, Q081-Q084, and Q092 establish the boundary: successor Cycles reset participant-specific power and confer no winner-specific benefit, while accumulated factual history may change the galaxy's physical state, strategic character, and names. [#156](https://github.com/AnthonyPWatts/cycles/issues/156) owns reconciliation with the current continuity implementation. Q071 keeps admirals narrative-first before adding power, while Q081 allows notable figures to affect both their historical record and later galaxy naming. Q091 still gates decay, caps, compounding, and multi-Cycle thresholds; [#157](https://github.com/AnthonyPWatts/cycles/issues/157) owns the unresolved fiction behind the roughly century-scale break and restart. The other remaining continuity and admiral questions gate disclosure, summaries, actual bonuses, transfer, promotion, retirement, succession, biography, and player management.

### Tutorial And Match Enrolment

The approved [multi-game and tutorial programme](multi-game-and-tutorial-plan.md) defines a player-visible Game as a lineage of one or more Cycles. A persistent Player may enrol in several Games, while each Cycle retains separate participant and empire authority. Players reconfirm during Intermission before a successor Cycle. The first release uses in-app cross-Game urgency and defers email or push.

[Twin Reaches](multi-game-and-tutorial-plan.md#82-recommended-tutorial-foundations-v1-twin-reaches) is the first Training profile. Its four-resolution Core journey uses ordinary commands and authoritative outcomes. The current canonical galaxy becomes the first standard profile, and operators create the first standard Games without AI seat fill. The [companion test plan](multi-game-and-tutorial-test-plan.md) owns the executable safety and pilot evidence.

Standard games no longer expose the former Day One guide, its toolbar button, client-local tutorial progress, or tutorial-specific advance gate. Their authored Council Agenda and `OpeningBriefingIssued` objectives remain genuine ordinary-gameplay prompts whose orders and outcomes use the normal simulation. MG-01 through MG-09 provide the Game/configuration/enrolment/lifecycle foundation; exhaustive same-scope SQL ownership; Player-only identity and focused stores; explicit selected-Game routes and authority; antiforgery; persisted scheduled/self-paced execution; immutable Standard/Twin Reaches profiles; the Games home; allow-listed private Twin Reaches provisioning; and the server-derived four-resolution Core journey. A Training attempt is an ordinary second Game, uses normal commands and authoritative resolution, persists journey status, presentation acknowledgements, account-level completion/skip evidence, request identity and supersession rather than mechanical progress, and remains excluded from Worker due selection. The first MG-10 slices now expose truthful self-paced turn control, profile-selected Twin Reaches overview/sector artwork, a keyboard-operable Systems and routes representation driven entirely from selected-Game topology, and a responsive guide rail/sheet proven through the first two lessons at desktop, intermediate and mobile widths. Remaining MG-10 work is the complete account/journey state matrix plus end-to-end keyboard, 200% zoom and screen-reader evidence; the five-novice Increment 2 pilot applies to Training rather than gating removal of obsolete Standard tutorial UI.

Training provisioning is enabled only through the typed `TrainingGames` pilot audience; disabling exposure prevents new prompts and attempts without removing committed Games. The trusted playground deployment allow-lists Tony for the first pilot. An active journey owns its guided resolve command; after explicit skip or completion the participant may resolve the same self-paced Cycle directly, with an expected-turn token preventing a two-tab double advance. Paused journeys remain frozen, the Development Standard control rejects Training, and the Worker continues to ignore it. Worker health, leadership, graceful shutdown, monitoring and any later several-Cycle batch policy remain under [#132](https://github.com/AnthonyPWatts/cycles/issues/132). MG-00's baseline and Increment 2 novice evidence remain open.

The dependency-labelled MG identifiers are planning labels, not live issue numbers. MG-01 through MG-09 have been implemented as bounded slices; creating the wider programme epic or its remaining GitHub issues still requires separate authority. Public discovery, queue offers, no-shows, AI fill, player-created standard Games, automatic private-Game successor carry-forward, and external notification transport remain deferred.

## Conditional Technical Risks

The following do not justify standing implementation tickets without evidence:

- add focused regression or SQL integration coverage when a specified behaviour exposes a concrete gap;
- profile retained-history and generic whole-state paths beyond the measured dashboard caller in #141 when another concrete scenario warrants it;
- change or retire the offline generic SQL mutation/import bridge only when an explicit maintenance or compatibility requirement justifies it; it is no longer an API/Worker path;
- revisit dashboard rendering only after an agreed galaxy/player target exceeds the verified 8-sector, 64-system, three-empire development test or requires a materially denser topology; the six-empire domain ceiling is not yet six-player UI evidence;
- extend the new `Cycles.Application` boundary only for selected use cases that need provider-neutral orchestration; do not turn the focused MG-03 contracts into a speculative repository framework;
- continue small correctness, migration-safety, diagnostics, CI, and clean-checkout repairs when concrete friction is observed.

When one of these risks becomes concrete, create one bounded GitHub issue with evidence and acceptance criteria rather than restoring a permanent Markdown checkbox.

## Migration Audit

The former Markdown checklist was classified as follows:

- the mixed-strategy scenario is implemented and belongs in Project State;
- guided play/balance evidence moved to #131;
- Worker health/leadership moved to #132;
- the security review moved to #133;
- hosting, migration, identity, admin, dashboard, recovery, API, guide, and state-transfer work already had #120-#129;
- remaining gameplay/narrative items are product-decision gates;
- scaling, profiling, architecture extraction, and opportunistic regression work are conditional risks;
- measured dashboard refresh, retained-history, caching, and asset-routing work moved to #140-#145;
- long-horizon ideas remain in the parking lot.

No concrete actionable work remains owned only by this Markdown file.

## Parking Lot

- native mobile clients;
- large-scale multiplayer deployment;
- real-time combat;
- individual planet or building management;
- public leaderboards and monetisation;
- modding support;
- AI-generated character biographies beyond an agreed narrative boundary.
