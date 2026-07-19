# Backlog

Last updated: 2026-07-19

GitHub issues are authoritative for concrete actionable work: scope, acceptance criteria, ownership, status, dependencies, and completion. This document is the curated roadmap, sequencing summary, decision-gate overview, and issue index. It deliberately does not mirror issue checkboxes or live status.

Completed behaviour and verification belong in [Project State](project-state.md). Accepted product answers and unresolved gates belong in [Product Owner Questions](product-owner-questions.md). Durable rationale belongs in the [Decision Log](decision-log.md). [Issue #130](https://github.com/AnthonyPWatts/cycles/issues/130) records the ownership migration.

## Current Sequence

1. Finish and verify the remaining identity, admin, and dashboard boundaries in [#122-#124](https://github.com/AnthonyPWatts/cycles/issues/122), plus the Day One guidance evidence in [#129](https://github.com/AnthonyPWatts/cycles/issues/129).
2. Continue shared scheduled-execution hardening through [#132](https://github.com/AnthonyPWatts/cycles/issues/132), from the SQL-atomic due-execution boundary into Worker health, shutdown, scheduling policy, and monitoring.
3. Reconcile successor continuity through [#156](https://github.com/AnthonyPWatts/cycles/issues/156): preserve the current factual name, significance, and strategic galaxy echoes while proving that prior players, empires, and winners receive no inherited power. Richer burned-system and historical-naming effects remain later bounded slices rather than part of this reconciliation.
4. Gather guided play and balance evidence through [#131](https://github.com/AnthonyPWatts/cycles/issues/131) before further tuning combat, colonisation, priority, turn-processing, or order-feedback rules. The evidence must test whether players understand the fixed phase order as well as whether its outcomes are balanced. The accepted one-intention-per-fleet replacement, command-closure Colonise reservation, and processing-order contracts define the current baseline.
5. Complete the threat model and security evidence in [#133](https://github.com/AnthonyPWatts/cycles/issues/133) before any untrusted online test.
6. Continue dashboard data and asset-request efficiency through [#141-#145](https://github.com/AnthonyPWatts/cycles/issues/141), starting with the focused SQL read path now that the consolidated bootstrap contract is in place, before validators, topology splitting, or abuse guardrails. Public Cloudflare asset routing in [#144](https://github.com/AnthonyPWatts/cycles/issues/144) can proceed independently once its strict public-only bundle boundary is agreed. Current production pressure is low, so this sequence remains P2 anticipatory hardening unless measurements worsen.

This sequence does not authorise speculative gameplay expansion. The active product-decision queue remains indexed by [issue #119](https://github.com/AnthonyPWatts/cycles/issues/119).

The 18 July command-window and tick-resolution decision is implemented through [#137](https://github.com/AnthonyPWatts/cycles/issues/137): human closure, internal planning, implicit Holds, a durable sealed ledger, shared Cycle locking for command mutations, and explicit deterministic phase boundaries. [Issue #146](https://github.com/AnthonyPWatts/cycles/issues/146) adds Ariadne's first bounded strategic policy: locally visible favourable attacks, threat Holds, affordable outposts, and movement towards valuable reachable systems; neutral factions remain positional Hold obstacles. The processing order is a gameplay contract because it governs income, reinforcement, movement, combat, colonisation, ranking, and progression timing. [Issue #138](https://github.com/AnthonyPWatts/cycles/issues/138) owns showing that contract in Command planning, ongoing Fleets commitments, and turn results. A bounded Recall intention now reverses an outbound journey to its last occupied system before passive arrival. [Issue #131](https://github.com/AnthonyPWatts/cycles/issues/131) owns evidence that players understand processed dispatch, continuing transit, reversal, and return timing; [Q069 / issue #57](https://github.com/AnthonyPWatts/cycles/issues/57) keeps any longer-than-next-tick command activation decision-gated. Route interception, pursuit, arbitrary diversion, coordinated or adaptive game-AI strategy, combat forecasting, and other later command types remain separate decisions.

The 18 July data-overhead investigation measured nine generic whole-state loads and 243 SQL commands for one in-UI refresh, rising to ten loads and 270 commands for a browser reload. The consolidated player bootstrap now reduces both paths to one request, one generic load, and 27 SQL commands while preserving actor scope and selected-fleet context. [#141](https://github.com/AnthonyPWatts/cycles/issues/141) replaces that remaining high-frequency whole-state read, [#142](https://github.com/AnthonyPWatts/cycles/issues/142) adds actor-safe validation and short-lived caching, [#143](https://github.com/AnthonyPWatts/cycles/issues/143) separates stable topology from dynamic state, [#144](https://github.com/AnthonyPWatts/cycles/issues/144) removes unnecessary Worker-first public asset requests, and [#145](https://github.com/AnthonyPWatts/cycles/issues/145) adds cancellation and abuse guardrails last. Private empire responses must not be cached at Cloudflare.

The dashboard shell currently exposes the four implemented Command, Galaxy, Fleets, and History workspaces. Command owns cross-workspace triage and next-turn commitments; specialised work remains in its dedicated view. The navigation can grow when a bounded player-facing Strategy or Diplomacy workspace exists, but decision-gated systems do not receive empty or disabled tabs in advance.

## Actionable Issue Index

| Theme | Authoritative issues | Sequencing note |
| --- | --- | --- |
| Tick diagnosis and inspected abandonment | [#120](https://github.com/AnthonyPWatts/cycles/issues/120), [#121](https://github.com/AnthonyPWatts/cycles/issues/121) | Diagnosis remains read-only; abandonment requires an explicit operator and reason before normal recovery. |
| External identity, local admin authority, and private dashboard | [#122](https://github.com/AnthonyPWatts/cycles/issues/122), [#123](https://github.com/AnthonyPWatts/cycles/issues/123), [#124](https://github.com/AnthonyPWatts/cycles/issues/124) | OIDC proves identity; Cycles owns admission, empire, and admin authority. |
| Player API and Day One teaching contracts | [#127](https://github.com/AnthonyPWatts/cycles/issues/127), [#128](https://github.com/AnthonyPWatts/cycles/issues/128), [#129](https://github.com/AnthonyPWatts/cycles/issues/129) | Keep player responses typed, wire conventions explicit, and guide copy tied to real controls and outcomes. |
| Guided play and balance evidence | [#131](https://github.com/AnthonyPWatts/cycles/issues/131) | Evidence precedes named constant or rule changes. |
| Production Worker operation | [#132](https://github.com/AnthonyPWatts/cycles/issues/132) | Builds on the completed managed-SQL deployment and mandatory runtime-host configuration. |
| Deterministic turn ledger and resolution | [#137](https://github.com/AnthonyPWatts/cycles/issues/137) | Implements command closure, first-pass Hold planners, durable sealing, phase order, and command/tick locking; richer AI and unresolved multi-faction combat remain separate. |
| Deterministic game-AI strategy | [#146](https://github.com/AnthonyPWatts/cycles/issues/146) | Implements Ariadne's first visibility-respecting attack, defence, colonisation, and expansion policy while leaving neutral factions positional. |
| Player-facing turn processing | [#138](https://github.com/AnthonyPWatts/cycles/issues/138) | Teach the authoritative phase order at command time, keep dispatched journeys visible as ongoing Fleets commitments, and explain results without changing the rules established by #137. |
| Same-phase resource contention | [#139](https://github.com/AnthonyPWatts/cycles/issues/139), [#154](https://github.com/AnthonyPWatts/cycles/issues/154) | Implemented for Colonise: reserve from stockpile plus current-turn income at closure, and reject the whole otherwise-eligible set when its complete cost cannot be funded. |
| Allied live visibility | [#155](https://github.com/AnthonyPWatts/cycles/issues/155) | Shares current active-fleet system visibility automatically between allies without pooling any other empire state or settling stale-contact rules. |
| Successor continuity boundary | [#156](https://github.com/AnthonyPWatts/cycles/issues/156) | Reset participant-specific power while retaining deterministic history-driven changes to the successor galaxy. |
| Dashboard refresh and data efficiency | [#141](https://github.com/AnthonyPWatts/cycles/issues/141), [#142](https://github.com/AnthonyPWatts/cycles/issues/142), [#143](https://github.com/AnthonyPWatts/cycles/issues/143), [#145](https://github.com/AnthonyPWatts/cycles/issues/145) | Build the focused SQL path behind the consolidated bootstrap, then add actor-safe validators, topology reuse, and final cancellation/rate-limit guardrails. |
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

## Conditional Technical Risks

The following do not justify standing implementation tickets without evidence:

- add focused regression or SQL integration coverage when a specified behaviour exposes a concrete gap;
- profile retained-history and generic whole-state paths beyond the measured dashboard caller in #141 when another concrete scenario warrants it;
- revisit the generic SQL mutation bridge only if profiling shows a high-frequency or scaling-critical problem;
- revisit dashboard rendering only after an agreed galaxy/player target exceeds the verified 8-sector, 64-system, three-empire development test or requires a materially denser topology; the six-empire domain ceiling is not yet six-player UI evidence;
- introduce `Cycles.Application`, provider-neutral repositories, or broader typed fact schemas only when a selected feature or measured complexity requires them;
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
