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

Q115 was answered on 2026-07-14 by keeping the landing page public while protecting the playable dashboard, with a whole-site perimeter retained as a deployment override.

Q116 was answered on 2026-07-14 by requiring the deployed playground to move to managed SQL with database-native backups and a proved restore before further tester invitations.

Q117 was answered on 2026-07-14 by selecting the existing SQL Server provider on managed Azure SQL for the playground and first online test rather than delaying for provider portability.

Q118 was answered on 2026-07-14 by allowing justified Azure-SQL-compatible provider features inside the SQL Server adapter while keeping core contracts provider-neutral.

Q119 was answered on 2026-07-14 by demoting JSON now to explicit import/export, inspection, fixture, and migration use rather than API or Worker runtime persistence.

Q122 was answered on 2026-07-14 by retaining flexible internal fact storage while requiring a typed or validated contract at the first mechanical consumer, query, migration, or public exposure boundary.

Q123 was answered on 2026-07-14 by keeping raw fact storage out of the normal dashboard and ordinary player API, using display text by default, and requiring purpose-built typed contracts for useful player detail.

Q124 was answered on 2026-07-14 by locking camelCase properties and camelCase string enums, while requiring a stable machine-readable error code before freezing the current message-only envelope.

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

## Accepted Q110 And Q120-Q124 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q110](https://github.com/AnthonyPWatts/cycles/issues/98) | Accept the current lifecycle-control default. **Advance turn** remains the only ordinary-player Development exception. Shared/private-alpha timing belongs to the Worker, manual lifecycle actions are limited to audited admins, and recovery or Cycle transitions remain operator-only until their audit and confirmation UX is designed. | The current Development capability is confirmed without broadening player roles or exposing recovery, Cycle end, successor creation, pause, or diagnostics controls. Production operations remain gated by the unanswered auth, hosting, Worker, recovery, and audit decisions. |
| [Q120](https://github.com/AnthonyPWatts/cycles/issues/108) | All player-facing API endpoints use explicit response DTOs before online testing. | The implemented DTO-only boundary and its regression coverage are accepted product contracts. Future player-facing endpoints must follow the same boundary. |
| [Q121](https://github.com/AnthonyPWatts/cycles/issues/109) | Raw domain entities remain internal and are not returned to the dashboard. | Dashboard contracts may expose purpose-built representations, but must not leak `Cycles.Core` entities. |
| [Q122](https://github.com/AnthonyPWatts/cycles/issues/110) | Keep `FactJson` as flexible internal storage for another stage. Introduce a typed or validated fact contract when a payload becomes mechanically consumed, queried, migrated, or publicly exposed, rather than merely because it is displayed. | Do not begin a broad typed-fact migration before diplomacy and narrative shapes stabilise. The opening briefing is the current contract candidate because the dashboard consumes its fields; Q123 subsequently settles its public API boundary. |
| [Q123](https://github.com/AnthonyPWatts/cycles/issues/111) | Keep raw `FactJson` out of the normal dashboard and ordinary player API. Use display text by default and add purpose-built typed detail contracts only where they provide player value. | Event and battle storage may remain flexible internally. The Day One guide must receive a typed opening briefing rather than parse storage JSON, while any raw operator view remains an explicit authorised diagnostic. Implementation is tracked by [issue #127](https://github.com/AnthonyPWatts/cycles/issues/127). |
| [Q124](https://github.com/AnthonyPWatts/cycles/issues/112) | Lock camelCase JSON property names and camelCase string enum values before external clients exist. Before freezing the error contract, extend the current `message` response with a stable machine-readable `code`, retaining optional structured validation detail and a trace identifier. | HTTP status remains authoritative for the broad failure class; clients may branch on stable codes but not message wording. Numeric enum values are not part of the public contract. Implementation is tracked by [issue #128](https://github.com/AnthonyPWatts/cycles/issues/128). |

## Accepted Q111 And Q112 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q111](https://github.com/AnthonyPWatts/cycles/issues/99) | Treat a persisted `Running` tick as suspicious after a configurable threshold that defaults to five minutes. | The threshold is diagnostic only and must not fail, retry, repair, or cancel a tick. [Issue #120](https://github.com/AnthonyPWatts/cycles/issues/120) now targets persisted SQL attempts; Q119 retires the former hosted-JSON runtime metrics, with representative import/export validation tracked by [issue #126](https://github.com/AnthonyPWatts/cycles/issues/126). |
| [Q112](https://github.com/AnthonyPWatts/cycles/issues/100) | Never auto-fail a `Running` tick based only on age. Require admin inspection, continue blocking, and allow an audited admin to mark a confirmed abandoned attempt failed with an operator and reason. | Existing blocking remains the safe default. The missing explicit `Running`-to-`Failed` operator action is tracked by [issue #121](https://github.com/AnthonyPWatts/cycles/issues/121); normal repair and recovery clear/retry remain separate deliberate steps. |

## Accepted Q113-Q119 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q113](https://github.com/AnthonyPWatts/cycles/issues/101) | Use ASP.NET Core cookie authentication backed by an external OpenID Connect provider for private-alpha and Production environments. Correlate the provider's stable issuer and subject to a local player, and keep empire ownership, admin role, and operational permissions in Cycles-owned data. | Invitations or an allowlist govern admission but are not identity proof. The concrete provider remains environment-configurable, Development username login remains Development-only, and implementation is tracked by [issue #122](https://github.com/AnthonyPWatts/cycles/issues/122). Full ASP.NET Core Identity password management is not required for this boundary. |
| [Q114](https://github.com/AnthonyPWatts/cycles/issues/102) | Store admin roles in Cycles-owned player data. Bootstrap named external identities through explicit operator configuration, then require an authenticated admin, target, and reason for every routine grant or revocation. Keep emergency operator access separate. | The Development `isAdmin` login switch remains Development-only and is not accepted for shared environments. Every bootstrap, grant, and revocation must produce an immutable audit record, and routine operations cannot remove the final active admin. Implementation is tracked by [issue #123](https://github.com/AnthonyPWatts/cycles/issues/123). |
| [Q115](https://github.com/AnthonyPWatts/cycles/issues/103) | Keep `/` public and require private-alpha or Production authentication plus invited-player admission before serving `/app.html`. Keep `/health` public and continue protecting every game API independently. | A deployment may temporarily put an explicit whole-site perimeter in front of both pages when it is not ready for public discovery. The trusted playground's access-code gate remains such an override, not the permanent identity or route contract. Implementation is tracked by [issue #124](https://github.com/AnthonyPWatts/cycles/issues/124). |
| [Q116](https://github.com/AnthonyPWatts/cycles/issues/104) | Before further tester invitations, migrate the deployed playground to managed SQL, enable at least seven days of database-native point-in-time recovery, document restoration, and prove one isolated restore. | Do not build a production-style backup system around the hosted JSON file. Preserve one application-consistent pre-cutover JSON snapshot as migration evidence, avoid dual writes, and use SQL backup/restore after the cutover accepts new gameplay. Implementation is tracked by [issue #125](https://github.com/AnthonyPWatts/cycles/issues/125); Q117 selects Azure SQL and Q119 confirms JSON's remaining import/export-only lifecycle. |
| [Q117](https://github.com/AnthonyPWatts/cycles/issues/105) | Use the existing SQL Server provider on managed Azure SQL for the deployed playground and first online test. Do not delay that deployment to build PostgreSQL or MySQL portability. | Run an Azure SQL compatibility smoke test, keep provider-specific code contained in `Cycles.Infrastructure.SqlServer`, and revisit portability when measured cost, licensing, or hosting evidence justifies it. The playground cutover and restore proof remain tracked by [issue #125](https://github.com/AnthonyPWatts/cycles/issues/125). |
| [Q118](https://github.com/AnthonyPWatts/cycles/issues/106) | Do not ban SQL Server-specific features. Permit Azure-SQL-compatible features inside `Cycles.Infrastructure.SqlServer` and SQL migrations when they materially improve correctness, consistency, measured performance, or operations. | Keep `Cycles.Core` and the store contract provider-neutral, document material portability implications, and reject provider-specific features added merely for convenience. The existing `sp_getapplock` transaction boundary is an accepted example. |
| [Q119](https://github.com/AnthonyPWatts/cycles/issues/107) | Demote JSON now to explicit import/export only. API and Worker runtime hosts require explicit SQL configuration after the deployed cutover; normal local development uses the documented SQL Server container. | Keep JSON for versioned state transfer, validation, offline inspection, fixtures, and migration evidence, not as a silently selected runtime store. Implement import/export before using it for issue [#125](https://github.com/AnthonyPWatts/cycles/issues/125), then remove the fallback without breaking deployment. Implementation is tracked by [issue #126](https://github.com/AnthonyPWatts/cycles/issues/126). |

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

- The landing page is public in the normal private-alpha and Production route contract; the playable dashboard requires authentication and admission. `/health` remains public without exposing game state.
- All player-facing endpoints use explicit response DTOs before online testing.
- Raw domain entities remain internal and must not be returned to the dashboard.
- Purpose-built response contracts remain protected by regression coverage against `Cycles.Core` entity leakage.
- `FactJson` may remain flexible internal storage while fact shapes evolve, but a mechanically consumed, queried, migrated, or publicly exposed payload requires a typed or validated boundary.
- Ordinary player responses and the normal dashboard do not expose raw fact storage. Display text is the default presentation; useful structured detail uses a purpose-built typed contract, while raw inspection is limited to an explicit authorised operator surface.
- Player API property names use camelCase and enum wire values use camelCase strings; numeric enum values are not accepted as part of the public contract.
- Handled player API errors retain the correct HTTP status and use a stable machine-readable code plus a safe human-readable message. Optional structured validation detail and trace correlation may be added without exposing internal exception data.

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
- The deployed playground must move to managed SQL before further tester invitations and prove an isolated restore from database-native backup with at least seven days of point-in-time retention.
- The playground and first online test use the existing SQL Server provider on managed Azure SQL. Provider portability is deferred until measured cost, licensing, or hosting evidence justifies it.
- Azure-SQL-compatible provider features are allowed when they solve a material need and remain inside the SQL Server adapter or migrations; the core domain and store contract remain provider-neutral.
- JSON's accepted role is explicit import/export, inspection, fixtures, and migration evidence rather than API or Worker runtime persistence. Normal runtime and local-development hosts require SQL after the safe cutover sequence.
- Online testing follows authentication hardening and a coherent operational boundary.
- Longer-term production hosting, database, and vendor choices remain open beyond the accepted Azure SQL playground and first-online-test boundary.

## Implemented Defaults Awaiting Product Confirmation

The following open questions now have reversible engineering defaults in the Development build or trusted playground. These implementations provide something concrete to test; they are not accepted product answers and do not close the linked issues.

| Open questions | Current implemented default |
| --- | --- |
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
| API and dashboard follow-ons | Q125-Q130 | Scale target, help content, backlog ownership, and saved-game exports. Q120-Q124's DTO, fact, and serialization boundaries are settled. |

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
