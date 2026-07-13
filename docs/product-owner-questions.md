# Product Owner Questions

Last updated: 2026-07-14

This is the canonical repository record of accepted product answers and unresolved product gates. GitHub owns discussion and assignment; this file owns the answer that implementation may rely on.

Implementation status belongs in [Project State](project-state.md), work sequencing in the [Backlog](backlog.md), and durable technical rationale in the [Decision Log](decision-log.md).

## Active Decision Queue

[GitHub issue #119](https://github.com/AnthonyPWatts/cycles/issues/119) indexes Q013-Q130 by priority and functional area. As of 2026-07-11, all 118 questions are filed as individual issues and assigned to `wsay`.

Q110, Q120, and Q121 were answered on 2026-07-12 by accepting their documented defaults. They are recorded below and no longer form active product gates.

Q107 was answered on 2026-07-13 by accepting the already-implemented Worker sequencing default.

Q108 was answered on 2026-07-14 by accepting the already-implemented Cycle-configured tick cadence.

Q109 was answered on 2026-07-14 by accepting the already-implemented environment-specific manual tick boundary.

Q111 was answered on 2026-07-14 by selecting a configurable persisted-running suspicion threshold with a five-minute default.

Q112 was answered on 2026-07-14 by accepting conservative blocking plus explicit audited abandonment.

Q113 was answered on 2026-07-14 by selecting external OpenID Connect authentication with Cycles-owned authorisation and invited-player admission.

Q114 was answered on 2026-07-14 by selecting explicit Cycles-owned admin grants with audited provisioning and revocation.

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

## Accepted Q107-Q109 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q107](https://github.com/AnthonyPWatts/cycles/issues/95) | Create `Cycles.Worker` before the next gameplay system. | The existing scheduled tick host is confirmed as the correct sequencing choice. This does not settle production hosting, authentication, backup, or Worker deployment topology. |
| [Q108](https://github.com/AnthonyPWatts/cycles/issues/96) | Schedule ticks using the active Cycle's configured `TickLengthMinutes`. The first tick is due at Cycle start; later ticks are due one cadence after the last completed tick. | The Worker runs at most one due tick per check, does not process a catch-up backlog, and does not schedule recovery-required or non-active Cycles. |
| [Q109](https://github.com/AnthonyPWatts/cycles/issues/97) | Allow any authenticated player to use **Advance turn** in Development. In shared private-alpha and Production environments, scheduled Worker timing is normal and only audited admins may trigger a manual tick. | Ordinary Production players cannot execute ticks. The Development exception uses the authoritative store boundary without changing player role, visibility, or empire authority. |

## Accepted Q110, Q120, And Q121 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q110](https://github.com/AnthonyPWatts/cycles/issues/98) | Accept the current lifecycle-control default. **Advance turn** remains the only ordinary-player Development exception. Shared/private-alpha timing belongs to the Worker, manual lifecycle actions are limited to audited admins, and recovery or Cycle transitions remain operator-only until their audit and confirmation UX is designed. | The current Development capability is confirmed without broadening player roles or exposing recovery, Cycle end, successor creation, pause, or diagnostics controls. Production operations remain gated by the unanswered auth, hosting, Worker, recovery, and audit decisions. |
| [Q120](https://github.com/AnthonyPWatts/cycles/issues/108) | All player-facing API endpoints use explicit response DTOs before online testing. | The implemented DTO-only boundary and its regression coverage are accepted product contracts. Future player-facing endpoints must follow the same boundary. |
| [Q121](https://github.com/AnthonyPWatts/cycles/issues/109) | Raw domain entities remain internal and are not returned to the dashboard. | Dashboard contracts may expose purpose-built representations, but must not leak `Cycles.Core` entities. |

## Accepted Q111 And Q112 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q111](https://github.com/AnthonyPWatts/cycles/issues/99) | Treat a persisted `Running` tick as suspicious after a configurable threshold that defaults to five minutes. | The threshold is diagnostic only and must not fail, retry, repair, or cancel a tick. The atomic JSON store does not persist its intermediate `Running` state, so its relevant evidence is total tick duration, state-file size, and representative retained-state benchmarks. Implementation is tracked by [issue #120](https://github.com/AnthonyPWatts/cycles/issues/120). |
| [Q112](https://github.com/AnthonyPWatts/cycles/issues/100) | Never auto-fail a `Running` tick based only on age. Require admin inspection, continue blocking, and allow an audited admin to mark a confirmed abandoned attempt failed with an operator and reason. | Existing blocking remains the safe default. The missing explicit `Running`-to-`Failed` operator action is tracked by [issue #121](https://github.com/AnthonyPWatts/cycles/issues/121); normal repair and recovery clear/retry remain separate deliberate steps. |

## Accepted Q113 And Q114 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q113](https://github.com/AnthonyPWatts/cycles/issues/101) | Use ASP.NET Core cookie authentication backed by an external OpenID Connect provider for private-alpha and Production environments. Correlate the provider's stable issuer and subject to a local player, and keep empire ownership, admin role, and operational permissions in Cycles-owned data. | Invitations or an allowlist govern admission but are not identity proof. The concrete provider remains environment-configurable, Development username login remains Development-only, and implementation is tracked by [issue #122](https://github.com/AnthonyPWatts/cycles/issues/122). Full ASP.NET Core Identity password management is not required for this boundary. |
| [Q114](https://github.com/AnthonyPWatts/cycles/issues/102) | Store admin roles in Cycles-owned player data. Bootstrap named external identities through explicit operator configuration, then require an authenticated admin, target, and reason for every routine grant or revocation. Keep emergency operator access separate. | The Development `isAdmin` login switch remains Development-only and is not accepted for shared environments. Every bootstrap, grant, and revocation must produce an immutable audit record, and routine operations cannot remove the final active admin. Implementation is tracked by [issue #123](https://github.com/AnthonyPWatts/cycles/issues/123). |

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

- Development username authentication is acceptable only for trusted Development testing.
- Private-alpha and Production identity use an external OpenID Connect provider through ASP.NET Core cookie authentication. Stable issuer and subject identify the local player; the concrete provider remains environment-configurable.
- Invitations or an allowlist may control admission, but empire ownership, admin role, and operational permissions remain Cycles-owned authorisation data.
- Shared environments bootstrap named admins explicitly, then audit every local admin grant and revocation with actor, target, reason, and timestamp. Emergency operator access remains separate from routine player-admin accounts.
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
- Scheduled ticks belong to a Worker; ordinary production player actions must not execute ticks.
- **Advance turn** is the only ordinary-player Development exception and invokes the same authoritative store operation without changing the player's role, visibility, or empire authority.
- Shared/private-alpha manual lifecycle actions are limited to audited admins. Recovery and Cycle transitions remain operator-only until their audit and confirmation UX is designed.

### API And Dashboard Contracts

- All player-facing endpoints use explicit response DTOs before online testing.
- Raw domain entities remain internal and must not be returned to the dashboard.
- Purpose-built response contracts remain protected by regression coverage against `Cycles.Core` entity leakage.

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

## Implemented Defaults Awaiting Product Confirmation

The following open questions now have reversible engineering defaults in the Development build or trusted playground. These implementations provide something concrete to test; they are not accepted product answers and do not close the linked issues.

| Open questions | Current implemented default |
| --- | --- |
| [Q115](https://github.com/AnthonyPWatts/cycles/issues/103) | The trusted playground puts both the public landing page and dashboard behind an application access code, leaves `/health` public, and keeps the Development login inside that gate. This does not settle the future production boundary. |
| [Q117](https://github.com/AnthonyPWatts/cycles/issues/105) and [Q119](https://github.com/AnthonyPWatts/cycles/issues/107) | The cost-capped playground persists JSON on its free App Service instance. This is an explicit hosted-test exception and does not select the production provider or reverse the accepted import/export-only direction. |
| [Q123](https://github.com/AnthonyPWatts/cycles/issues/111) | Normal History views show purpose-built event and Chronicle text rather than exposing raw `FactJson`. |
| [Q125](https://github.com/AnthonyPWatts/cycles/issues/113) | The dashboard is tuned for the current small seeded galaxy and bounded lists; larger-scale rendering remains conditional work. |
| [Q126](https://github.com/AnthonyPWatts/cycles/issues/114) | The dashboard prioritises desktop command use while retaining basic responsive behaviour for narrower screens. |
| [Q127](https://github.com/AnthonyPWatts/cycles/issues/115) | A resumable Day One guide explains resources, priorities, fog of war, fleet orders, factual Events, and the narrative Chronicle through the real controls. |

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
| Production access and operations | Q115-Q118 | Hosting, Worker topology, recovery policy, secrets, backups, and test boundary. Q107-Q114 are settled. |
| API and dashboard follow-ons | Q122-Q130 | Typed facts, event-detail UX, frozen API conventions, scale target, help content, backlog ownership, and saved-game exports. Q120-Q121's DTO boundary is settled. |

## Engineering Defaults

No further product call is needed to:

- add regression and live SQL Server integration coverage for established behaviour;
- improve deterministic scenario evidence, profiling, diagnostics, migrations, CI, and documentation;
- fix correctness, data-safety, recovery, and concurrency defects without changing rules;
- keep SQL Server as the present relational proof path;
- keep ordinary tick execution outside player endpoints;
- keep player-facing API responses DTO-only and raw domain entities internal;
- keep factual records authoritative and generated prose non-authoritative;
- extract a new architecture layer only when measured orchestration complexity justifies it.
