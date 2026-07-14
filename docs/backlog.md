# Backlog

Last updated: 2026-07-14

GitHub issues are authoritative for concrete actionable work: scope, acceptance criteria, ownership, status, dependencies, and completion. This document is the curated roadmap, sequencing summary, decision-gate overview, and issue index. It deliberately does not mirror issue checkboxes or live status.

Completed behaviour and verification belong in [Project State](project-state.md). Accepted product answers and unresolved gates belong in [Product Owner Questions](product-owner-questions.md). Durable rationale belongs in the [Decision Log](decision-log.md). [Issue #130](https://github.com/AnthonyPWatts/cycles/issues/130) records the ownership migration.

## Current Sequence

1. Finish and verify the local implementation boundaries in [#120-#124](https://github.com/AnthonyPWatts/cycles/issues/120) and [#127-#129](https://github.com/AnthonyPWatts/cycles/issues/127).
2. Continue shared scheduled-execution hardening through [#132](https://github.com/AnthonyPWatts/cycles/issues/132), from the SQL-atomic due-execution boundary into Worker health, shutdown, scheduling policy, and monitoring.
3. Gather guided play and balance evidence through [#131](https://github.com/AnthonyPWatts/cycles/issues/131) before changing combat, colonisation, priority, or order-feedback rules.
4. Complete the threat model and security evidence in [#133](https://github.com/AnthonyPWatts/cycles/issues/133) before any untrusted online test.

This sequence does not authorise speculative gameplay expansion. The active product-decision queue remains indexed by [issue #119](https://github.com/AnthonyPWatts/cycles/issues/119).

## Actionable Issue Index

| Theme | Authoritative issues | Sequencing note |
| --- | --- | --- |
| Tick diagnosis and inspected abandonment | [#120](https://github.com/AnthonyPWatts/cycles/issues/120), [#121](https://github.com/AnthonyPWatts/cycles/issues/121) | Diagnosis remains read-only; abandonment requires an explicit operator and reason before normal recovery. |
| External identity, local admin authority, and private dashboard | [#122](https://github.com/AnthonyPWatts/cycles/issues/122), [#123](https://github.com/AnthonyPWatts/cycles/issues/123), [#124](https://github.com/AnthonyPWatts/cycles/issues/124) | OIDC proves identity; Cycles owns admission, empire, and admin authority. |
| Player API and Day One teaching contracts | [#127](https://github.com/AnthonyPWatts/cycles/issues/127), [#128](https://github.com/AnthonyPWatts/cycles/issues/128), [#129](https://github.com/AnthonyPWatts/cycles/issues/129) | Keep player responses typed, wire conventions explicit, and guide copy tied to real controls and outcomes. |
| Guided play and balance evidence | [#131](https://github.com/AnthonyPWatts/cycles/issues/131) | Evidence precedes named constant or rule changes. |
| Production Worker operation | [#132](https://github.com/AnthonyPWatts/cycles/issues/132) | Builds on the completed managed-SQL deployment and mandatory runtime-host configuration. |
| Pre-untrusted-test security gate | [#133](https://github.com/AnthonyPWatts/cycles/issues/133) | Review the implemented identity, authorisation, data-transfer, proxy, persistence, and deployment boundaries together. |

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
- profile retained-history and generic whole-state paths when a measured caller or scenario warrants it;
- revisit the generic SQL mutation bridge only if profiling shows a high-frequency or scaling-critical problem;
- revisit dashboard rendering only after an agreed galaxy/player target exceeds the current 24-system, four-empire test;
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
