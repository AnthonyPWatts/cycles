# Backlog

Last updated: 2026-07-18

GitHub issues are authoritative for concrete actionable work: scope, acceptance criteria, ownership, status, dependencies, and completion. This document is the curated roadmap, sequencing summary, decision-gate overview, and issue index. It deliberately does not mirror issue checkboxes or live status.

Completed behaviour and verification belong in [Project State](project-state.md). Accepted product answers and unresolved gates belong in [Product Owner Questions](product-owner-questions.md). Durable rationale belongs in the [Decision Log](decision-log.md). [Issue #130](https://github.com/AnthonyPWatts/cycles/issues/130) records the ownership migration.

## Current Sequence

1. Finish and verify the remaining identity, admin, and dashboard boundaries in [#122-#124](https://github.com/AnthonyPWatts/cycles/issues/122), plus the Day One guidance evidence in [#129](https://github.com/AnthonyPWatts/cycles/issues/129).
2. Continue shared scheduled-execution hardening through [#132](https://github.com/AnthonyPWatts/cycles/issues/132), from the SQL-atomic due-execution boundary into Worker health, shutdown, scheduling policy, and monitoring.
3. Gather guided play and balance evidence through [#131](https://github.com/AnthonyPWatts/cycles/issues/131) before further tuning combat, colonisation, priority, turn-processing, or order-feedback rules. The evidence must test whether players understand the fixed phase order as well as whether its outcomes are balanced. The accepted one-intention-per-fleet replacement and processing-order contracts define the current baseline. [Issue #139](https://github.com/AnthonyPWatts/cycles/issues/139) remains an explicit exception for same-phase resource contention.
4. Complete the threat model and security evidence in [#133](https://github.com/AnthonyPWatts/cycles/issues/133) before any untrusted online test.
5. Reduce dashboard refresh and asset-request amplification through the #140-#145 work indexed below, starting with the consolidated bootstrap contract and focused SQL read path before validators, topology splitting, or abuse guardrails. Public Cloudflare asset routing in [#144](https://github.com/AnthonyPWatts/cycles/issues/144) can proceed independently once its strict public-only bundle boundary is agreed. Current production pressure is low, so this sequence remains P2 anticipatory hardening unless measurements worsen.

This sequence does not authorise speculative gameplay expansion. The active product-decision queue remains indexed by [issue #119](https://github.com/AnthonyPWatts/cycles/issues/119).

The 18 July command-window and tick-resolution decision is implemented through [#137](https://github.com/AnthonyPWatts/cycles/issues/137): human closure, deterministic game-AI/neutral Hold planning, implicit Holds, a durable sealed ledger, shared Cycle locking for command mutations, and explicit deterministic phase boundaries. The processing order is a gameplay contract because it governs income, reinforcement, movement, combat, colonisation, ranking, and progression timing. [Issue #138](https://github.com/AnthonyPWatts/cycles/issues/138) owns showing that contract in Command planning and turn results. A bounded Recall intention now reverses an outbound journey to its last occupied system before passive arrival; route interception, pursuit, arbitrary diversion, richer game-AI strategy, combat forecasting, and other later command types remain separate decisions.

The 18 July data-overhead investigation measured nine generic whole-state loads and 243 SQL commands for one in-UI refresh, rising to ten loads and 270 commands for a browser reload. Response compression is implemented; [#140](https://github.com/AnthonyPWatts/cycles/issues/140) consolidates dashboard loading, [#141](https://github.com/AnthonyPWatts/cycles/issues/141) replaces the measured high-frequency whole-state read, [#142](https://github.com/AnthonyPWatts/cycles/issues/142) adds actor-safe validation and short-lived caching, [#143](https://github.com/AnthonyPWatts/cycles/issues/143) separates stable topology from dynamic state, [#144](https://github.com/AnthonyPWatts/cycles/issues/144) removes unnecessary Worker-first public asset requests, and [#145](https://github.com/AnthonyPWatts/cycles/issues/145) adds cancellation and abuse guardrails last. Private empire responses must not be cached at Cloudflare.

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
| Player-facing turn processing | [#138](https://github.com/AnthonyPWatts/cycles/issues/138) | Teach the authoritative phase order at command time and in results without changing the rules established by #137. |
| Same-phase resource contention | [#139](https://github.com/AnthonyPWatts/cycles/issues/139) | Replace opaque stable-ID priority when several Colonise intentions compete for insufficient Population, then generalise only if later commands need shared budgets. |
| Dashboard refresh and data efficiency | [#140](https://github.com/AnthonyPWatts/cycles/issues/140), [#141](https://github.com/AnthonyPWatts/cycles/issues/141), [#142](https://github.com/AnthonyPWatts/cycles/issues/142), [#143](https://github.com/AnthonyPWatts/cycles/issues/143), [#145](https://github.com/AnthonyPWatts/cycles/issues/145) | Consolidate refresh first, then add the focused SQL path, actor-safe validators, topology reuse, and final cancellation/rate-limit guardrails. |
| Public asset request efficiency | [#144](https://github.com/AnthonyPWatts/cycles/issues/144) | Move approved public assets to asset-first Cloudflare routing without exposing protected dashboard or source files. |
| Pre-untrusted-test security gate | [#133](https://github.com/AnthonyPWatts/cycles/issues/133) | Review the implemented identity, authorisation, data-transfer, proxy, persistence, and deployment boundaries together. |
| Public media consumer contract | [#134](https://github.com/AnthonyPWatts/cycles/issues/134) | Cycles owns the stable Cloudflare film and poster URLs; each consuming site owns its presentation, fallback, and responsive behaviour. |

## Decision-Gated Roadmap

These are roadmap themes, not implementation tickets. Create bounded implementation issues only after the linked product questions settle enough behaviour to build.

### Diplomacy

Q013 and Q019-Q022 still gate player-action timing, visibility, Chronicle treatment, NPC participation, and cross-Cycle memory. Q014-Q018 already establish mutual consent for positive agreements, unilateral hostile/terminating actions, first-version Alliance limits, independent rankings, and independent allied influence. The stored relationship vocabulary and aggression foundation remain valid but do not authorise a player-facing lifecycle yet.

### Visibility, Doctrine, Technology, And Intelligence

Q023-Q034 and Q038-Q046 gate discovery, sensor and estimate semantics, alliance visibility, selectable doctrine categories and branches, logistics, detection, cloaking, and modifier scope. Q035-Q037 retain Survey Projection as the non-consuming universal introduction and select a future player-chosen, Research-consuming project model.

### Combat, Comeback, And Colonisation

Q050-Q052, Q054-Q056, and Q058 still gate comeback mechanics, home-system protection, ship classes, rally points, capacity limits, and cross-Cycle economic echoes. Q047-Q049 confirm the current Population-funded, fleet-supported outpost model; Q053 selects bounded Development/construction capacity as Industry's next role, and Q057 keeps raw output influence-share based. Current combat, colonisation, and priorities may be exercised through [#131](https://github.com/AnthonyPWatts/cycles/issues/131), but rule or constant changes require repeatable evidence and any applicable remaining product answer.

### Chronicle And Narrative

Q094-Q106 gate queued narrative ownership, provider selection, retry/fallback/review/safety, threshold configuration, and player-visible failure behaviour. Deterministic templates, required-fact validation, generation status, and retained context remain the current safe boundary.

### Cycle Continuity And Named Figures

The remaining continuity and admiral questions gate richer successor flavour, historical-system evolution, transfer, promotion, retirement, succession, biography, and player management. Current final rankings, major-event preservation, system signals, and fleet-attached admirals are complete first slices.

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
