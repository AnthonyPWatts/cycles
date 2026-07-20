# Cycles Documentation

The maintained documentation has one owner for each kind of information. Update the owning document when behaviour changes instead of copying the same status into several files.

## Core Documents

| Document | Owns |
| --- | --- |
| [Gameplay Guide](alpha-testers-guide.md) | Player-facing Twin Reaches Training, Standard gameplay, and development-build expectations. |
| [Project State](project-state.md) | What is implemented, verified, and still limited. |
| [Architecture Direction](architecture-direction.md) | System boundaries, invariants, and intended technical shape. |
| [Simulation Reference](simulation-reference.md) | Authoritative turn processing order, determinism, final ranking, and repeatable balance-scenario contracts. |
| [Player API Contract](api-contract.md) | JSON conventions, stable error codes, compatibility rules, and player-facing fact presentation. |
| [Backlog](backlog.md) | Curated priorities, sequencing, decision gates, conditional risks, and links to authoritative GitHub issues. |
| [Product Owner Questions](product-owner-questions.md) | Accepted product answers and active product-decision gates. |
| [Decision Log](decision-log.md) | Durable chronological decisions and their consequences. |

The root [README](../README.md) owns project orientation and copy-and-run setup commands. It should link to deeper material rather than duplicate current-state or backlog detail.

## Supporting References

| Document | Owns |
| --- | --- |
| [Operations](operations.md) | Worker operation, diagnostics, failed-tick recovery, and guarded profiling. |
| [Trusted Playground Deployment](playground-deployment.md) | Free-tier hosted play-testing, CI deployment, and enforced cost guardrails. |
| [Multi-game and Tutorial Plan](multi-game-and-tutorial-plan.md) | Approved product, UI, Training, architecture, migration, rollout, and implementation sequence. |
| [Multi-game and Tutorial Test Plan](multi-game-and-tutorial-test-plan.md) | Required domain, SQL, concurrency, browser, accessibility, capacity, and rollout evidence for that programme. |
| [Promo Film Production Notes](../src/Cycles.Api/wwwroot/media/PROMO-PRODUCTION.md) | The 30-second master and edge-delivery derivative, shot provenance, concept-image prompts, render command, and verification boundary. |
| [SQL Server Runbook](../database/sqldockerdeploykit/README.md) | Local database image, connection, migration, integration-test, and cleanup instructions. |

These references support the core documents but are not separate roadmaps or status reports.

SQL Server is the sole authoritative runtime and operator datastore. Versioned JSON is an explicit, validated, sensitive transfer and fixture format; it is not a live save file, fallback store, or database backup. Start with the root README for local SQL setup, use Operations for application and recovery workflows, and use the SQL Server Runbook for database lifecycle commands.

## Source Artefacts

The original design material is retained under `source/`:

- `Cycles_Vision_Statement.docx`;
- `Cycles_Technical_Design_Document_v0_1.docx`;
- `Cycles_Expanded_History_and_Strategy_Spec.docx`;
- `Cycles_PO_Questions_2026-06-30.docx`.

These Word files preserve source intent and historical product prompts. The Markdown files above are the current working layer and take precedence when they describe implemented behaviour.

## Maintenance Rules

- Put current behaviour and verification only in `project-state.md`.
- Keep concrete scope, ownership, live status, dependencies, and acceptance criteria in GitHub issues. Use `backlog.md` for priorities, sequencing, decision gates, conditional risks, and issue links.
- Put unresolved product choices and accepted product answers only in `product-owner-questions.md`.
- Add a decision-log entry when a choice would otherwise be rediscovered.
- Keep commands in the root README, Operations, or the SQL Server runbook according to audience.
- Keep maintained documentation aligned with current `main`. Do not fork docs per gameplay Cycle or ad hoc test build; record the deployed commit/build in test evidence and use Git tags, commits, or release notes when a historical snapshot is required.
- Keep screenshots in player-facing guidance rather than using them as behavioural proof. Regenerate the maintained captures in the Gameplay Guide when a UI change makes their controls or layout misleading, and remove captures of retired controls promptly.
- Keep promotional footage honest about provenance. Label current-build capture and concept dramatisation in the film, and update the production notes whenever its assets, prompts, timing, or audio sources change.
