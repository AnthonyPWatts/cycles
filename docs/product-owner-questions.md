# Product Owner Questions

Last updated: 2026-07-11

This is the canonical repository record of accepted product answers and unresolved product gates. GitHub owns discussion and assignment; this file owns the answer that implementation may rely on.

Implementation status belongs in [Project State](project-state.md), work sequencing in the [Backlog](backlog.md), and durable technical rationale in the [Decision Log](decision-log.md).

## Active Decision Queue

[GitHub issue #119](https://github.com/AnthonyPWatts/cycles/issues/119) indexes Q013-Q130 by priority and functional area. As of 2026-07-11, all 118 questions are filed as individual issues and assigned to `wsay`.

When an answer is accepted:

1. record the concise answer and any authorised default here;
2. add a decision-log entry if it creates a durable rule or boundary;
3. update the backlog to show what became ready or remains blocked;
4. implement only the behaviour the answer actually settles.

## Accepted Q001-Q012 Answers

The partial response in `source/Cycles_PO_Questions_2026-06-30.docx` settled the first twelve questions.

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| Q001 | Population and colonisation was the next headline slice. | Build a bounded population-funded outpost loop. |
| Q002 | The next playable test should prove strategic choice. | Priorities, influence, movement, combat, and colonisation must produce traceable trade-offs. |
| Q003 | Target a private alpha. | Improve repeatable operation without treating development auth as production security. |
| Q004 | Engineering may select the two data-model areas changed next. | Keep changes bounded and compatible with existing influence/history rules. |
| Q005 | Engineering may choose sensible balance constants, thresholds, and UI ordering. | Use named, test-covered defaults and change them only when evidence supports it. |
| Q006 | Mechanically complete but visually rough behaviour is acceptable. | Complete domain, persistence, API, UI, and verification paths before polish. |
| Q007 | Success after repeated ticks means influence affects decisions. | Scenario and alpha evidence should focus on choice, not cosmetic activity. |
| Q008 | Unanswered decisions may be tracked as GitHub issues. | GitHub is the active queue; this file remains the accepted-answer record. |
| Q009 | Use the proposed minimum diplomacy states. | Neutral, War, Non-Aggression Pact, and Alliance form the stored vocabulary. |
| Q010 | Empires are Neutral by default. | An absent relationship row means Neutral. |
| Q011 | An attack does not automatically create War. | Record aggression; the attacked empire controls escalation. |
| Q012 | Attacking through a treaty cancels it and may lead to War. | Cancel a pact or alliance to Neutral and record the breach without inferring War. |

The colonisation slice and diplomacy foundation authorised by these answers are complete. Q013-Q022 still gate player-facing diplomacy.

## Established Product Contracts

These earlier answers remain in force unless a later accepted question explicitly supersedes them.

### Strategy And Economy

- Industry, Research, and Population are non-negative stockpiles.
- Priority weights are percentages totalling 100 and affect the next tick.
- Automatic spending may leave resources reserved.
- Industry has mixed future roles; the first spend converts Military allocation into ships.
- The first ship costs 25 industry, takes three ticks, and joins the home fleet.
- Research accumulates towards future unlocks; Survey Projection is the first automatic threshold effect.
- Population's first role is colonisation.
- Uncapped within-Cycle growth is acceptable for the prototype because each Cycle resets, but comeback mechanics remain desirable.
- Engineering may tune named constants from repeatable evidence without seeking approval for each number.

### Identity, Authority, And Visibility

- Development authentication is acceptable for trusted private testing.
- Production identity should use a deliberate ASP.NET Core authentication boundary, likely OAuth/OpenID Connect or Identity; the provider is not selected.
- One player controls one empire.
- Admins may inspect all empires and act for support or repair.
- Team/shared control is parked.
- Players see the full galaxy topology.
- Exact local presence, fleets, events, last-tick facts, and Chronicle detail require an active fleet in the relevant system.
- Players continue to see their own empire and audit events; development admins bypass visibility filters.

### Orders And Tick Control

- Pending orders may be cancelled before their execution tick by the owning empire or an admin.
- Processed, rejected, and cancelled order history remains durable.
- Cancellation records an event.
- Ordinary player actions must not execute ticks.
- Scheduled ticks belong to a Worker; development admins may trigger the same authoritative operation manually.

### Cycle End And History

- An admin ends a Cycle manually at the current database cutoff.
- Final ranking uses percentage map control derived from effective presence.
- There is one winner and a ranking for every active empire.
- Pending orders do not complete merely because the Cycle ends.
- Preserve roughly the largest 10% of battles, with enough system history to support continuity without promoting every skirmish.
- Successor Cycles may preserve flavour and historical system echoes, but not mechanical empire advantages.
- Post-Cycle history generation may use complete factual records, including facts that were private during play.

### Chronicle And Narrative

- The Chronicle is player-facing flavour and historical record, not an authority over outcomes.
- Chronicle visibility follows fog-of-war in the current model.
- Importance thresholds should become configurable when the product needs per-Cycle control.
- Deterministic templates are acceptable before AI integration.
- Future generated prose may infer motive or emotion, but must retain required facts and cannot change simulation outcomes.
- AI work must run outside the tick transaction with durable status.
- Review/safety may be needed after MVP; provider, fallback, and visible failure behaviour remain unanswered.

### Persistence And Deployment

- SQL Server is the current relational implementation; do not add SQLite merely for parity.
- A future production deployment may choose PostgreSQL or MySQL to avoid SQL Server licensing.
- JSON's accepted long-term role is import/export rather than production runtime persistence. Q119 must still settle transition timing and development compatibility.
- Online testing follows authentication hardening and a coherent operational boundary.
- Hosting form, production database, backup/restore expectations, and vendor choices remain open.

## Current Gates

Do not expand these areas until the referenced questions have accepted answers:

| Area | Questions | Decision required |
| --- | --- | --- |
| Diplomacy | Q013-Q022 | Timing, mutual acceptance, declarations, alliance effects, ranking, visibility, Chronicle treatment, memory. |
| Visibility and intelligence | Q023-Q034 | Sensors, stale or estimated contacts, alliance sharing, public Chronicle detail, live ranking visibility. |
| Doctrine and technology | Q035-Q046 | Unlock choice, research spending, modifier scope, logistics, detection, cloaking, reset behaviour. |
| Population, infrastructure, and comeback | Q047 onward in that section | Outpost evolution, further resource roles, recovery mechanics, home-system protection. |
| Combat | Combat question group | Target complexity, balance goals, retreat, fleet composition, and evidence threshold. |
| Chronicle AI | Q094-Q101 | Provider, queue ownership, retry, fallback, review, safety, and failure display. |
| JSON lifecycle | Q119 | Transition timing and compatibility for the import/export-only direction. |
| Production access | Deployment/auth question groups | Identity provider, admin provisioning, hosting, Worker topology, secrets, backups, and test boundary. |

## Engineering Defaults

No further product call is needed to:

- add regression and live SQL Server integration coverage for established behaviour;
- improve deterministic scenario evidence, profiling, diagnostics, migrations, CI, and documentation;
- fix correctness, data-safety, recovery, and concurrency defects without changing rules;
- keep SQL Server as the present relational proof path;
- keep ordinary tick execution outside player endpoints;
- keep factual records authoritative and generated prose non-authoritative;
- extract a new architecture layer only when measured orchestration complexity justifies it.
