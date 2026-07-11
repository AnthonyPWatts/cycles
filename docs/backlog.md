# Backlog

Last updated: 2026-07-11

This is the repository's implementation queue. It records unfinished work and blockers, not a second project-state history. Completed behaviour belongs in [Project State](project-state.md), while durable rationale belongs in the [Decision Log](decision-log.md).

## Recommended Next Work

These items can proceed without another product-owner decision:

- [ ] Add a mixed-strategy balance scenario in which different policies compete in the same Cycle.
- [ ] Gather private-alpha evidence on colonisation, combat, priority clarity, and order feedback before changing named balance constants.
- [ ] Add focused regression or live SQL Server integration coverage whenever an existing specified behaviour exposes a gap.
- [ ] Continue profiling retained-history and generic state paths when a measured caller or scenario justifies it.
- [ ] Improve diagnostics, CI, migration safety, documentation, and clean-checkout reproducibility when concrete friction appears.
- [ ] Fix correctness, data-safety, recovery, migration, and concurrency defects without changing player-visible rules.

Do not treat this list as permission for speculative refactors. `Cycles.Application`, provider-neutral repositories, typed fact contracts, and dashboard scaling remain conditional work: start them only when measured complexity or a selected feature requires them.

## Persistence And Operations

- [ ] Demote JSON from the default runtime path to explicit import/export support.
  - Direction is settled: JSON is not the intended production store.
  - Q119 still needs to settle timing, compatibility, and whether development runtime support remains during the transition.
- [ ] Add JSON import/export tooling when the lifecycle above is ready.
- [ ] Define production authentication and admin provisioning.
- [ ] Define hosting, database provider, migration ownership, and environment configuration.
- [ ] Add production Worker health, singleton leadership, multi-Cycle scheduling policy, and operational monitoring.
- [ ] Define secret handling plus database backup, restore, and recovery-administration boundaries.

Blocked: production and JSON-lifecycle work must follow the relevant product decisions. Development auth and the local/private Worker are not production substitutes.

## Gameplay And Product Systems

### Diplomacy

- [ ] Add player-facing diplomatic offers, acceptance, declarations, and treaty lifecycle.
- [ ] Define alliance effects on combat, influence, ranking, movement, and shared visibility.
- [ ] Define diplomatic visibility, Chronicle selection, and cross-Cycle memory.

Blocked by Q013-Q022. The stored relationship vocabulary, attack aggression facts, and treaty-breaking cancellation behaviour are already implemented.

### Doctrine, Technology, And Intelligence

- [ ] Define player-selected doctrine or research-project semantics.
- [ ] Define whether research is spent, retained, or both.
- [ ] Add logistics only after its effect on travel, projection, construction, or supply is chosen.
- [ ] Add detection before cloaking and define what information either system can reveal or hide.
- [ ] Define alliance visibility and any stale or estimated intelligence model.

Blocked by Q023-Q046. Survey Projection remains the single automatic threshold unlock, not a complete doctrine system.

### Combat, Comeback, And Colonisation

- [ ] Tune combat or colonisation constants only from repeatable scenario or private-alpha evidence.
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

- [ ] Replace flexible `FactJson` payloads with typed or validated contracts when diplomacy and narrative facts stabilise.
- [ ] Revisit the generic whole-state SQL mutation bridge if profiling shows it on a high-frequency or scaling-critical path.
- [ ] Revisit dashboard rendering when an agreed galaxy/player scale exceeds the current small-map assumption.
- [ ] Add a production security review before any untrusted online test.
- [ ] Balance combat before making competitive balance claims.

## Product Decision Gates

The active queue is indexed by [GitHub issue #119](https://github.com/AnthonyPWatts/cycles/issues/119). Accepted answers must be copied into [Product Owner Questions](product-owner-questions.md) before implementation relies on them.

| Gate | Questions | Blocks |
| --- | --- | --- |
| Diplomacy | Q013-Q022 | Player actions, alliance mechanics, visibility, Chronicle treatment, memory. |
| Visibility and intelligence | Q023-Q034 | Sensors, estimates, alliance sharing, public/private Chronicle detail. |
| Doctrine and technology | Q035-Q046 | Research choices, logistics, detection, cloaking, modifier scope. |
| Population and infrastructure follow-ons | Q047 onward in that area | Outpost evolution, comeback, further industry/population roles. |
| Narrative AI | Q094-Q101 | Provider, queue, fallback, review, and failure contract. |
| JSON lifecycle | Q119 | Timing and compatibility of the import/export-only direction. |
| Production access and operations | Deployment/auth questions in the queue | Untrusted online testing and production hosting. |

## Completed Foundations

The following are established and should not remain as open roadmap stages:

- deterministic simulation, order lifecycle, combat facts, and explicit tick recovery;
- JSON and SQL Server stores, ordered migrations, focused SQL tick loading/writes, and per-Cycle locking;
- strategic priorities, ship construction, expansion projection, Survey Projection, and colonisation;
- development auth, actor-derived empire authority, active-fleet visibility, explicit API DTOs, and order cancellation;
- scheduled Worker ticks, development-admin ticks, diagnostics, and running-API smoke coverage;
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
