# Decision Log

Last updated: 2026-06-23

This file records decisions that shape future implementation. It is intentionally lightweight; add entries when a choice would otherwise be rediscovered or debated repeatedly.

## 2026-06-23: Build A Runnable MVP Before Production Infrastructure

Decision: start with a local .NET MVP using Core, CLI, API, dashboard, and a JSON state store.

Reasoning:

- The first risk was whether the design could become an executable loop.
- External services and database packages would slow initial validation.
- The technical design allowed a console/worker tick processor before full API/UI maturity.

Consequences:

- The current implementation is useful for proving behaviour, not production durability.
- Relational persistence is the next major infrastructure step.
- Documentation must be clear that JSON persistence is temporary.

## 2026-06-23: Keep Tick Execution Out Of The Public API

Decision: the API accepts orders and exposes state; the CLI runs ticks.

Reasoning:

- The design says clients submit intentions and the tick engine resolves outcomes later.
- Running ticks through ordinary public endpoints risks confusing client actions with authoritative simulation.
- A future worker can replace the CLI without changing the player-facing API principle.

Consequences:

- Local development uses CLI plus API/dashboard together.
- The future `Cycles.Worker` should own scheduled tick execution.

## 2026-06-23: Treat Influence As Derived, Not Ownership

Decision: system control is represented by calculated presence/influence rather than stored ownership.

Reasoning:

- The vision depends on coexistence, border ambiguity, blockades, and cold wars.
- Ownership would push the design toward conventional map painting.
- Future systems can modify influence without replacing the territorial model.

Consequences:

- Future mechanics should add influence inputs/modifiers.
- UI should show influence/presence rather than owned/unowned binary state.

## 2026-06-23: Store Facts Before Narrative

Decision: events, battle records, and Chronicle entries store structured facts before any stylised narrative layer exists.

Reasoning:

- The Chronicle must originate from real gameplay.
- AI or generated prose must not decide outcomes.
- Future narratives need source facts for validation.

Consequences:

- `FactJson` exists as a flexible early form but may need typed fact models later.
- Narrative generation should be asynchronous and non-authoritative.
- Chronicle entries should retain source event/battle IDs.

## 2026-06-23: Prefer SQLite As The First Relational Store

Decision: the first relational implementation should likely use SQLite unless new constraints appear.

Reasoning:

- It satisfies the need for real tables, transactions, indexes, and constraints.
- It keeps local development simple.
- It avoids choosing hosted infrastructure before the schema stabilises.

Consequences:

- The schema and persistence boundary should avoid SQLite-specific assumptions where practical.
- PostgreSQL or SQL Server can be evaluated after relational tick processing is proven.

## 2026-06-23: Do Not Add Future Feature Systems Before Hardening The Simulation Spine

Decision: admirals, diplomacy, technologies, cloaking, and AI narrative generation should wait until persistence and tick semantics are stronger.

Reasoning:

- These systems depend on reliable events, battles, influence, and history.
- Adding them before transactionality and tests would amplify churn.
- The MVP already exposes the key extension points.

Consequences:

- Stage 1 and Stage 2 should focus on tests, persistence, locking, and recovery.
- Feature roadmap remains visible in `backlog.md` without driving immediate implementation.
