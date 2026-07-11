# Cycles Documentation

The maintained documentation has one owner for each kind of information. Update the owning document when behaviour changes instead of copying the same status into several files.

## Core Documents

| Document | Owns |
| --- | --- |
| [Alpha Tester's Guide](alpha-testers-guide.md) | Player-facing tutorial and private-alpha expectations. |
| [Project State](project-state.md) | What is implemented, verified, and still limited. |
| [Architecture Direction](architecture-direction.md) | System boundaries, invariants, and intended technical shape. |
| [Backlog](backlog.md) | The only repository implementation queue and blocker list. |
| [Product Owner Questions](product-owner-questions.md) | Accepted product answers and active product-decision gates. |
| [Decision Log](decision-log.md) | Durable chronological decisions and their consequences. |

The root [README](../README.md) owns project orientation and copy-and-run setup commands. It should link to deeper material rather than duplicate current-state or backlog detail.

## Supporting References

| Document | Owns |
| --- | --- |
| [Simulation Reference](simulation-reference.md) | Determinism, final ranking, and repeatable balance-scenario contracts. |
| [Operations](operations.md) | Worker operation, diagnostics, failed-tick recovery, and guarded profiling. |
| [SQL Server Runbook](../database/sqldockerdeploykit/README.md) | Local database image, connection, migration, integration-test, and cleanup instructions. |

These references support the core documents but are not separate roadmaps or status reports.

## Source Artefacts

The original design material is retained under `source/`:

- `Cycles_Vision_Statement.docx`;
- `Cycles_Technical_Design_Document_v0_1.docx`;
- `Cycles_Expanded_History_and_Strategy_Spec.docx`;
- `Cycles_PO_Questions_2026-06-30.docx`.

These Word files preserve source intent and historical product prompts. The Markdown files above are the current working layer and take precedence when they describe implemented behaviour.

## Maintenance Rules

- Put current behaviour and verification only in `project-state.md`.
- Put unfinished implementation work only in `backlog.md`.
- Put unresolved product choices and accepted product answers only in `product-owner-questions.md`.
- Add a decision-log entry when a choice would otherwise be rediscovered.
- Keep commands in the root README, Operations, or the SQL Server runbook according to audience.
- Do not use screenshots as current-state evidence while the dashboard is changing quickly. Add maintained tutorial images later when the interface stabilises.
