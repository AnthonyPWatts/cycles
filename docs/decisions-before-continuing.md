# Current Decision Gates

Last updated: 2026-07-11

This file is a navigation aid for deciding what may be implemented next. It no longer repeats the original 2026-06 questionnaire: those early choices have either been implemented or moved into the active product-owner queue.

Use these sources of truth:

- `product-owner-questions.md` for accepted product answers and the GitHub decision queue;
- `decision-log.md` for durable engineering and product decisions;
- `backlog.md` for implementation status and explicit blockers;
- `project-state.md` for evidence of what currently works.

## Settled Foundations

The following questions no longer block implementation:

- SQL Server is the primary relational proof path. Plain SQL migrations, `dbo.SchemaMigrations`, CLI migration commands, application locks, and live integration tests are implemented.
- SQL tick execution uses a focused per-Cycle workspace and dedicated outcome writes. Generic non-tick updates still use the whole-state bridge.
- Failed ticks require explicit operator repair, clear, or retry with an audited reason. Silent retry is not permitted.
- Scheduled ticks belong to `Cycles.Worker`; development admins may trigger the same authoritative boundary manually.
- Development authentication, one-player/one-empire authorisation, admin exceptions, and active-fleet visibility are implemented for private testing.
- Industry, research, population, priorities, ship construction, the first research unlock, and population-funded colonisation have working first-pass semantics.
- Public API endpoints use explicit response DTOs. The public website and playable dashboard are separate.
- Cycle-end rankings, selected major battles, historical system signals, and successor-Cycle player continuity are implemented.
- Chronicle battle prose uses deterministic templates with required-fact validation and persisted generation status.
- Admirals and the stored diplomacy/aggression foundation are implemented without inventing the missing player-facing lifecycles.

## Active Product Gates

Do not expand these areas until the corresponding product-owner issues are answered and copied into `product-owner-questions.md`:

1. Player-facing diplomacy: offer and acceptance timing, declarations, alliance effects, visibility, Chronicle treatment, and cross-Cycle memory.
2. Doctrine and research choices: unlock selection, modifier scope, logistics, cloaking, detection, and related visibility rules.
3. Combat and comeback design beyond evidence-led tuning of existing named constants.
4. AI narrative generation: provider boundary, queue ownership, review/fallback behaviour, and player-visible failures.
5. Production authentication and operations: identity provider, admin provisioning, hosting, health, leader election, secrets, backups, and online-test boundaries.
6. JSON persistence lifecycle: retain it for development, demote it to import/export, or remove it.
7. Richer Cycle continuity, colonisation capture/destruction, admiral management, and historical-system evolution.

The pinned GitHub PO decision index linked from `product-owner-questions.md` owns prioritisation and assignment for these questions.

## Engineering Work That Does Not Need A Product Call

Engineering may continue to:

- add regression and live SQL Server integration coverage for behaviour already specified;
- improve deterministic scenario evidence and profile the current tick/state path;
- improve build, CI, documentation, diagnostics, and clean-checkout reproducibility;
- fix correctness, data-safety, recovery, migration, and concurrency defects without changing player-visible rules;
- tune existing named balance constants only when repeated scenario or private-alpha evidence supports a specific change;
- extract `Cycles.Application` only when measured orchestration complexity justifies it.

## Current Recommended Gate

Until the PO queue advances, prefer evidence and scalability work around the existing playable loop. The first repeated-tick baseline is documented in `balance-scenarios.md`; it does not justify an isolated balance-number change, but it does show retained-history growth preventing a complete high-activity 2,160-tick diagnostic run. That engineering limit is ready for investigation without further product input.
