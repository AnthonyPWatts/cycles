# Product Owner Questions

Last updated: 2026-07-19

This is the canonical repository record of accepted product answers and unresolved product gates. GitHub owns discussion and assignment; this file owns the answer that implementation may rely on.

Implementation status belongs in [Project State](project-state.md), work sequencing in the [Backlog](backlog.md), and durable technical rationale in the [Decision Log](decision-log.md).

## Active Decision Queue

[GitHub issue #119](https://github.com/AnthonyPWatts/cycles/issues/119) indexes Q013-Q130 by priority and functional area. As of 2026-07-11, all 118 questions are filed as individual issues and assigned to `wsay`.

Q014 was answered on 2026-07-14 by requiring mutual acceptance for positive bilateral agreements while keeping war declarations and treaty termination unilateral.

Q015 was answered on 2026-07-14 by allowing unilateral war declarations and treaty termination without a separate advance-notice or cooling-off period.

Q016 was answered on 2026-07-14 by making friendly-fire prevention and factual history the only first-version Alliance mechanics, while leaving shared visibility to Q025 and keeping resources, influence, rankings, fleets, and attack control separate.

Q017 was answered on 2026-07-14 by retaining separate per-empire map-control rankings and a single empire winner regardless of Alliances.

Q018 was answered on 2026-07-14 by allowing allied empires to coexist in a system while retaining independent influence, resource shares, and map-control competition.

Q013 was answered on 2026-07-18 by accepting next-tick durable diplomatic actions. Together with Q014-Q018, this settles the first relationship-transition and consent model; Q019-Q022 still gate the complete player-facing lifecycle.

Q023-Q025 were answered on 2026-07-18 by retaining the fully visible galaxy topology, keeping active fleets as the baseline source of detailed visibility, and automatically sharing that visibility between allies. Stale-contact detail and other intelligence rules remain gated by Q026-Q034.

Q060 was answered on 2026-07-18 by sequencing diplomacy before richer combat and retaining the current simple combat model while those systems settle.

Q071 was answered on 2026-07-18 by treating admirals as both narrative anchors and eventual strategic assets, while keeping the next slice narrative-first.

Q082-Q084 were answered on 2026-07-18 and clarified on 2026-07-19: retain the top-10% historical preservation rule, reset player and empire mechanics completely, confer no inherited winner recognition or advantage, and allow accumulated history to create galaxy-level physical, strategic, and naming echoes.

Q058, Q081, and Q092 were answered on 2026-07-19. Factual resource, population, infrastructure, battle, and named-figure history may change later systems or names, but those echoes do not carry participant-specific state or benefits. Q091 still owns decay, caps, compounding, and multi-Cycle thresholds; supplemental [issue #157](https://github.com/AnthonyPWatts/cycles/issues/157) owns the unresolved narrative reason for the roughly century-scale break and restart.

Q097 was answered on 2026-07-18 by requiring a provider-neutral narrative boundary with replaceable live-provider connectors and a deterministic development/test connector. Q094-Q096 and Q098-Q106 still gate the first production narrative pipeline.

Q110, Q120, and Q121 were answered on 2026-07-12 by accepting their documented defaults. They are recorded below and no longer form active product gates.

Q107 was answered on 2026-07-13 by accepting the already-implemented Worker sequencing default.

Q108 was answered on 2026-07-14 by accepting the already-implemented Cycle-configured tick cadence.

Q109 was answered on 2026-07-14 by accepting the already-implemented environment-specific manual tick boundary.

Q111 was answered on 2026-07-14 by selecting a configurable persisted-running suspicion threshold with a five-minute default.

Q112 was answered on 2026-07-14 by accepting conservative blocking plus explicit audited abandonment.

Q113 was answered on 2026-07-14 by selecting external OpenID Connect authentication with Cycles-owned authorisation and invited-player admission.

Q114 was answered on 2026-07-14 by selecting explicit Cycles-owned admin grants with audited provisioning and revocation.

Q115 was answered on 2026-07-14 by keeping the landing page public while protecting the playable dashboard. A whole-site perimeter remains available for a distinct undiscoverable deployment, but is not active on the trusted playground.

Q116 was answered on 2026-07-14 by requiring the deployed playground to move to managed SQL with database-native backups and a proved restore before further tester invitations.

Q117 was answered on 2026-07-14 by selecting the existing SQL Server provider on managed Azure SQL for the playground and first online test rather than delaying for provider portability.

Q118 was answered on 2026-07-14 by allowing justified Azure-SQL-compatible provider features inside the SQL Server adapter while keeping core contracts provider-neutral.

Q119 was answered on 2026-07-14 by demoting JSON now to explicit import/export, inspection, fixture, and migration use rather than API or Worker runtime persistence.

Q122 was answered on 2026-07-14 by retaining flexible internal fact storage while requiring a typed or validated contract at the first mechanical consumer, query, migration, or public exposure boundary.

Q123 was answered on 2026-07-14 by keeping raw fact storage out of the normal dashboard and ordinary player API, using display text by default, and requiring purpose-built typed contracts for useful player detail.

Q124 was answered on 2026-07-14 by locking camelCase properties and camelCase string enums, while requiring a stable machine-readable error code before freezing the current message-only envelope.

Q125's earlier scale answers were explicitly superseded on 2026-07-15. The next player test now targets the canonical 8-sector, 64-system, four-empire territorial galaxy and its three curated map ranges.

The 17 July development-match decision supersedes the four-empire opening without changing the 8-sector, 64-system map boundary. The current seed uses Tony and Will as persistent human players plus Ariadne as a game-AI player, each controlling one of three empires in distinct sectors. The match model permits up to six empire participants and never shares control of an empire. Complex identity, account email, invitations, matchmaking, and production game-AI policy remain deferred; the present Development selector offers only Tony or Will behind the existing access-code boundary.

The approved 19 July [multi-game and tutorial programme](multi-game-and-tutorial-plan.md) separates future Training from standard Games. A Game contains one or more Cycle epochs; a persistent Player may hold several Game enrolments while Cycle participant and empire authority remain separate. Twin Reaches supplies the first one-human Training profile and four-resolution Core journey, while the current canonical galaxy supplies the first standard profile. Players reconfirm during Intermission, the first release uses in-app urgency, and email or push waits for return evidence. Public discovery, queues, no-shows, AI fill, player-created Games, and any private automatic-successor policy remain future decisions. The current Day One guide remains the implemented training path, and this direction adds no scope to issue #138.

The 18 July turn-resolution decision closes the former relative-ordering gap. A command deadline closes human submissions without requiring player readiness; game-AI and neutral intentions are then generated from the same pre-resolution state before the complete ledger is sealed. Resolution uses deterministic resource, economy, construction, movement, combat, colonisation, derived-state, progression, and publication phases. The sequence is an accepted gameplay rule. Engineering cannot adjust it as incidental implementation work because it governs spendable income, reinforcements, escape and defence, colony survival, rankings, and progression timing. Issue #137 implements the boundary and issue #138 owns player-facing explanation.

The 18 July game-AI decision in [issue #146](https://github.com/AnthonyPWatts/cycles/issues/146) replaces Ariadne's first-pass Hold behaviour with an ordered deterministic policy: attack a locally visible faction only with a 25% strength advantage, otherwise Hold against that threat; then prefer an affordable eligible outpost; then advance towards the highest-value reachable expansion objective. The planner may use public system value and topology plus its own state and locally visible fleets, but not hidden human intentions or remote enemy fleet positions. It respects non-aggression pacts and Alliances. Neutral factions remain positional Hold obstacles. Production difficulty, roles, diplomacy, coordinated strategy, forecasting, and adaptive behaviour remain deferred.

[Issue #139](https://github.com/AnthonyPWatts/cycles/issues/139) was answered on 2026-07-19: reserve Colonise Population at command closure and reject the whole otherwise-eligible set when the empire cannot fund every intention in it. No partially affordable intention survives through submission time, stable identifiers, or another hidden priority. [Issue #154](https://github.com/AnthonyPWatts/cycles/issues/154) implements that bounded rule.

Q126 was answered on 2026-07-14 by prioritising desktop and laptop command usability while retaining a functional, readable narrow-screen core loop rather than equal mobile optimisation.

Q127 was answered on 2026-07-14 by keeping the resumable Day One guide as the primary in-dashboard training path and requiring explicit visibility and Cycle-history teaching through the current UI.

Q128 was answered on 2026-07-14 by making GitHub issues authoritative for concrete actionable work while retaining Markdown as the curated roadmap, sequencing summary, and canonical repository record of decisions and implemented state.

Q129 was answered on 2026-07-14 by keeping maintained documentation aligned with current `main` and using Git/build evidence for historical snapshots rather than copying docs per gameplay Cycle.

Q130 was answered on 2026-07-14 by treating complete state export/import as sensitive operator/admin support tooling rather than a player-facing save/restore feature.

Q035-Q037 were answered on 2026-07-14 by retaining Survey Projection as the universal introductory unlock, selecting a hybrid automatic-then-player-selected doctrine model, and spending Research when future selected projects complete while leaving the introductory unlock non-consuming.

Q047-Q049 were answered on 2026-07-14 by confirming Population-funded, fleet-supported outposts as Population's first role without adding a Population priority.

Q053 was answered on 2026-07-14 by selecting a bounded civilian-development and construction-capacity programme as Industry's next role rather than another ship class or a flat output multiplier.

Q057 was answered on 2026-07-14 by keeping raw system resource generation strictly influence-share based for the first version; Population and infrastructure do not multiply system output.

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

The colonisation slice and stored diplomacy foundation authorised by these answers are complete. Q013 is now settled; Q019-Q022 still gate disclosure, Chronicle treatment, AI participation, and cross-Cycle memory for the complete player-facing diplomacy lifecycle.

## Accepted Q013, Q023-Q025, Q058, Q060, Q071, Q081-Q084, Q092, And Q097 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q013](https://github.com/AnthonyPWatts/cycles/issues/1) | Diplomatic actions are durable intentions that resolve on the next authoritative tick rather than changing relationships immediately on submission. | Apply the Q014-Q018 consent and termination rules at tick resolution. Q019-Q022 still gate the complete player-facing lifecycle, so no immediate relationship mutation is introduced as a UI shortcut. |
| [Q023](https://github.com/AnthonyPWatts/cycles/issues/11) | Keep the complete galaxy topology visible. Do not introduce undiscovered systems as the next visibility model. | The current authored map remains a stable player contract. Fog-of-war controls detailed state, not whether systems and routes exist on the map. |
| [Q024](https://github.com/AnthonyPWatts/cycles/issues/12) | Keep active fleets as the baseline source of detailed local visibility. Add stale or estimated contacts after diplomacy rather than broadening live exact visibility through influence, home pressure, historical significance, or doctrine now. | The current active-fleet model remains valid. Q026-Q034 must settle contact precision, persistence, Chronicle disclosure, sensors, and rankings before broader intelligence work. |
| [Q025](https://github.com/AnthonyPWatts/cycles/issues/13) | Allied empires automatically share active-fleet visibility. Detailed losses, admirals, and other private facts remain visibility-gated until the accepted end-of-Cycle disclosure boundary says otherwise. | [Issue #155](https://github.com/AnthonyPWatts/cycles/issues/155) owns automatic allied live visibility without pooling fleets, orders, resources, influence, rankings, or control. Stale allied contacts remain outside that issue. |
| [Q058](https://github.com/AnthonyPWatts/cycles/issues/46) | Factual resource, population, infrastructure, battle, and related system history may create physical or strategic echoes in successor galaxies. | Do not carry player stockpiles, ownership, outposts, infrastructure benefits, or rank. A repeated-war history may eventually leave a system burned out and uncolonisable; Q091 still selects the general long-run accumulation rule. |
| [Q060](https://github.com/AnthonyPWatts/cycles/issues/48) | Implement diplomacy before richer combat choices. Keep combat simple while diplomacy and visibility settle; defer ship classes and tactical postures. | Combat playtesting and clarity work may continue, but richer rule changes remain behind diplomacy, visibility, Q059, and the relevant combat questions. The basic Hold exposure/effect remains explicitly owned by Q064. |
| [Q071](https://github.com/AnthonyPWatts/cycles/issues/59) | Admirals are both narrative anchors and eventual strategic assets. Keep the next slice narrative-first before adding power. | Existing history, reputation, status, and famous-system data remain the safe presentation foundation. Q072 and the later admiral questions must select actual bonuses and management behaviour. |
| [Q081](https://github.com/AnthonyPWatts/cycles/issues/69) | A notable admiral's association may change both the figure's historical record and the later galaxy. | A heavily travelled route may take that admiral's name in a successor Cycle. This is a galaxy-history echo, not a benefit inherited by the former player. |
| [Q082](https://github.com/AnthonyPWatts/cycles/issues/70) | Retain the current top-10% preservation rule for major battles and historical systems. | Preserve the selected completed-Cycle history as factual memory, with a minimum of one qualifying record where the current rule provides it. |
| [Q083](https://github.com/AnthonyPWatts/cycles/issues/71) | Every successor Cycle is a complete reset for players and empires, not for the galaxy. | [Issue #156](https://github.com/AnthonyPWatts/cycles/issues/156) owns the boundary: no prior participant receives inherited power, while selected factual history may intentionally alter successor system state, strategic character, or names. |
| [Q084](https://github.com/AnthonyPWatts/cycles/issues/72) | The winner receives no inherited title, banner, home-system flavour, prestige, or other next-Cycle benefit. Their benefit is the impact recorded in overall history. | End-of-Cycle rankings and history remain authoritative, but the next Cycle does not confer winner-specific recognition or advantage. Q085-Q091 and Q093 still own disclosure, summaries, significance evolution, and exports; Q092 permits historical names to evolve. |
| [Q092](https://github.com/AnthonyPWatts/cycles/issues/80) | Historical names may mutate rather than remaining frozen forever. | Successor places or routes may be renamed for accumulated events or notable figures. Exact selection, mutation vocabulary, and collision rules remain bounded implementation details. |
| [Q097](https://github.com/AnthonyPWatts/cycles/issues/85) | Keep narrative generation provider-neutral through a small interface with replaceable connectors for live providers. Retain a deterministic connector for development and testing rather than coupling the model to one vendor. | Do not select a concrete vendor or build an offline-only content library as the product boundary. Q094-Q096 must still settle the first queued records and runtime failure/fallback behaviour; Q098-Q106 own tone, inference, required facts, review, thresholds, interactions, privacy, and versioning. |

## Accepted Same-Phase Resource Contention Rule

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Issue #139](https://github.com/AnthonyPWatts/cycles/issues/139) | Reserve Colonise Population at command closure. If the budget cannot fund every otherwise-eligible intention in the empire's set, reject the whole set. | A budget sufficient for zero or only some rejects all; a budget sufficient for every intention reserves all. [Issue #154](https://github.com/AnthonyPWatts/cycles/issues/154) implements the rule and player-facing explanation. |

## Accepted Tutorial, Game-Lineage, And Match-Enrolment Direction

| Direction | Accepted answer | Remaining gate |
| --- | --- | --- |
| Tutorial game | Use Twin Reaches as the first compact Training profile: ten systems, thirteen routes, one human seat, ordinary mechanics, a four-resolution Core journey, server-derived progress, and fresh-attempt reset rather than rewind. | Implement and pass the first-outcome, unaided-completion, accessibility, and false-learning pilot gates before replacing the current Day One guide. |
| Standard games | Use the current canonical galaxy as the first standard profile. Operators create the first Games; the first manual lobby permits profile-bounded under-filled starts and does not fill seats with AI. | Implement and measure the curated runway before public discovery, queues, AI fill, or player-created Games. |
| Game lineage | A player-visible Game contains one or more Cycle epochs and at most one operational Cycle. Each Cycle locks its own map, scenario, policy, seed, roster, and provenance. | Implement Intermission and successor creation without changing the accepted participant-reset and galaxy-history boundary. |
| Persistent participation | Allow one persistent Player to hold several durable Game enrolments. Keep Cycle participant and empire authority separate from account identity. Do not impose a platform-wide concurrent-Game cap at launch. | Implement explicit Game selection and the approved authorisation, persistence, migration, and Worker hard gate before creating a second durable Game. Queue offers, no-shows, AI fill, and player-created Games remain future decisions. |
| Successors and return | Require explicit player reconfirmation during Intermission. Use in-app cross-Game urgency in the first release and defer email or push. | Define the recorded reconfirmation expiry during implementation. Revisit an external transport only after return evidence shows a need. |

The approved [multi-game and tutorial plan](multi-game-and-tutorial-plan.md) and [test plan](multi-game-and-tutorial-test-plan.md) define the bounded slices and their dependencies. They do not add scope to #138 or mark any feature implemented. External issue creation still requires separate authority.

## Accepted Q014-Q018 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q014](https://github.com/AnthonyPWatts/cycles/issues/2) | Require mutual acceptance for Alliance, peace, Non-Aggression Pacts, and any future trade or shared-visibility agreement. War declarations and treaty termination are unilateral, and a pending offer may be withdrawn unilaterally before acceptance. | Peace is a mutually accepted transition from War rather than a separate stored relationship state. Trade and shared visibility remain deferred; this answer establishes their future consent rule without adding them to the current vocabulary. |
| [Q015](https://github.com/AnthonyPWatts/cycles/issues/3) | A player may unilaterally declare War or terminate a treaty. Do not add a separate advance-notice or cooling-off period beyond the normal resolution timing selected under Q013. | When the state change becomes authoritative, notify both parties and record a high-severity factual event. Attacks already cancel a breached treaty during attack resolution without advance warning; explicit declaration and voluntary-termination actions remain unimplemented. |
| [Q016](https://github.com/AnthonyPWatts/cycles/issues/4) | In the first version, an active Alliance prevents ordinary direct attacks between its members and records its creation, termination, and betrayal in factual Events/history. A player must terminate the Alliance before deliberately attacking. | Do not pool influence, resources, rankings, fleets, or attack control. Movement needs no Alliance permission because it is not territorially blocked. Q025 owns shared visibility. Existing treaty-breach handling remains a defensive boundary for pending or exceptional conflicts. |
| [Q017](https://github.com/AnthonyPWatts/cycles/issues/5) | Allies remain separately ranked empires. Alliance members do not contribute map control to one another, pool scores, or become joint winners. | The implemented per-empire `MapControlPercent`, `CycleRankings`, and single-winner behaviour are accepted without changes. History may acknowledge a winner's allies, but it does not alter authoritative standings. No implementation issue is required. |
| [Q018](https://github.com/AnthonyPWatts/cycles/issues/6) | Allied empires may both maintain influence in the same system. Their presence remains independently calculated and competes proportionally for influence, resources, and map-control score. | Alliance prevents hostile action; it does not pool occupation or strategic rewards. The implemented relationship-independent influence and economy calculations are accepted without mechanical changes. UI may describe the state as allied coexistence but must not imply shared control. Q013 now settles next-tick timing; Q019-Q022 still gate the complete player-facing lifecycle. |

## Accepted Q107-Q109 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q107](https://github.com/AnthonyPWatts/cycles/issues/95) | Create `Cycles.Worker` before the next gameplay system. | The existing scheduled tick host is confirmed as the correct sequencing choice. This does not settle production hosting, authentication, backup, or Worker deployment topology. |
| [Q108](https://github.com/AnthonyPWatts/cycles/issues/96) | Schedule ticks using the active Cycle's configured `TickLengthMinutes`. The first tick is due at Cycle start; later ticks are due one cadence after the last completed tick. | The Worker runs at most one due tick per check, does not process a catch-up backlog, and does not schedule recovery-required or non-active Cycles. |
| [Q109](https://github.com/AnthonyPWatts/cycles/issues/97) | Allow any authenticated player to use **Close command window and advance** in Development. In shared private-alpha and Production environments, scheduled Worker timing is normal and only audited admins may trigger a manual tick. | Ordinary Production players cannot execute ticks. The Development exception uses the authoritative store boundary without changing player role, visibility, or empire authority. |

## Accepted Q110 And Q120-Q130 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q110](https://github.com/AnthonyPWatts/cycles/issues/98) | Accept the current lifecycle-control default. **Close command window and advance** remains the only ordinary-player Development exception. Shared/private-alpha timing belongs to the Worker, manual lifecycle actions are limited to audited admins, and recovery or Cycle transitions remain operator-only until their audit and confirmation UX is designed. | The current Development capability is confirmed without broadening player roles or exposing recovery, Cycle end, successor creation, pause, or diagnostics controls. Production operations remain gated by the unanswered auth, hosting, Worker, recovery, and audit decisions. |
| [Q120](https://github.com/AnthonyPWatts/cycles/issues/108) | All player-facing API endpoints use explicit response DTOs before online testing. | The implemented DTO-only boundary and its regression coverage are accepted product contracts. Future player-facing endpoints must follow the same boundary. |
| [Q121](https://github.com/AnthonyPWatts/cycles/issues/109) | Raw domain entities remain internal and are not returned to the dashboard. | Dashboard contracts may expose purpose-built representations, but must not leak `Cycles.Core` entities. |
| [Q122](https://github.com/AnthonyPWatts/cycles/issues/110) | Keep `FactJson` as flexible internal storage for another stage. Introduce a typed or validated fact contract when a payload becomes mechanically consumed, queried, migrated, or publicly exposed, rather than merely because it is displayed. | Do not begin a broad typed-fact migration before diplomacy and narrative shapes stabilise. The opening briefing is the current contract candidate because the dashboard consumes its fields; Q123 subsequently settles its public API boundary. |
| [Q123](https://github.com/AnthonyPWatts/cycles/issues/111) | Keep raw `FactJson` out of the normal dashboard and ordinary player API. Use display text by default and add purpose-built typed detail contracts only where they provide player value. | Event and battle storage may remain flexible internally. The Day One guide must receive a typed opening briefing rather than parse storage JSON, while any raw operator view remains an explicit authorised diagnostic. Implementation is tracked by [issue #127](https://github.com/AnthonyPWatts/cycles/issues/127). |
| [Q124](https://github.com/AnthonyPWatts/cycles/issues/112) | Lock camelCase JSON property names and camelCase string enum values before external clients exist. Before freezing the error contract, extend the current `message` response with a stable machine-readable `code`, retaining optional structured validation detail and a trace identifier. | HTTP status remains authoritative for the broad failure class; clients may branch on stable codes but not message wording. Numeric enum values are not part of the public contract. Implementation is tracked by [issue #128](https://github.com/AnthonyPWatts/cycles/issues/128). |
| [Q125](https://github.com/AnthonyPWatts/cycles/issues/113) | Supersede the larger crown. Target a canonical 8-sector, 64-system, four-empire territorial galaxy for the next player test. Each sector contains 8 systems and exactly two gateway systems; an individual gateway may serve several inter-sector lanes, producing occasional strategic hubs. | The irregular sector/system graphs, curated Galaxy/Sector/Local ranges, gateway-aware API, SQL persistence, development seed, deliberate reseed, and blue-violet/gold Galaxy palette are implemented together. Scale beyond 64 systems or materially denser route graphs still requires fresh evidence rather than automatic support. |
| [Q126](https://github.com/AnthonyPWatts/cycles/issues/114) | Prioritise desktop and laptop command usability. Narrow browser layouts must remain readable and support the core loop, but equal mobile optimisation or a touch-first redesign is not required for the next test. | Preserve sign-in, status and History reading, priorities, fleet selection, and basic order submission/cancellation without page-level horizontal scrolling. Revisit mobile parity only if tester usage makes it a primary play surface. The current responsive default needs no implementation issue. |
| [Q127](https://github.com/AnthonyPWatts/cycles/issues/115) | Keep the resumable Day One guide as the primary in-dashboard training. Teach priorities, visibility and fog-of-war, the order lifecycle, factual Events versus the selective Chronicle, and basic tick/Cycle history through the real controls. | The existing guide already covers the playable order loop but needs explicit visibility and Cycle-history steps plus a current-UI copy audit. Keep contextual hints concise and do not build a separate help centre. Implementation is tracked by [issue #129](https://github.com/AnthonyPWatts/cycles/issues/129). |
| [Q128](https://github.com/AnthonyPWatts/cycles/issues/116) | Make GitHub issues authoritative for concrete actionable backlog work: scope, acceptance criteria, owner, status, dependencies, and completion. Keep `docs/backlog.md` as the curated roadmap, sequencing summary, decision-gate overview, and issue index. | Do not duplicate live ticket status or full acceptance criteria in Markdown, and do not file every parked idea prematurely. Accepted answers, implemented state, and durable rationale remain canonical repository documentation. The ownership migration is recorded by [issue #130](https://github.com/AnthonyPWatts/cycles/issues/130). |
| [Q129](https://github.com/AnthonyPWatts/cycles/issues/117) | Maintained documentation describes current `main` behaviour. Do not create a documentation copy for each gameplay Cycle or ad hoc test build. | Record the deployed commit or build identifier in test evidence. Use Git tags, commits, and release notes when a historical documentation snapshot is genuinely needed. A gameplay Cycle is persisted game data, not a software documentation version. No implementation issue is required. |
| [Q130](https://github.com/AnthonyPWatts/cycles/issues/118) | Treat complete game-state export/import as an operator/admin support tool for migration, recovery preparation, debugging, and reproducible fixtures. It may support developer workflows, but it is not a player-facing save/restore feature. | Complete exports contain private state across all empires and must be handled as sensitive data behind explicit operator or admin authorisation. They do not replace database-native backups. [Issue #126](https://github.com/AnthonyPWatts/cycles/issues/126) owns implementation; any future player-sharing format requires a separate redacted design. |

## Accepted Q111 And Q112 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q111](https://github.com/AnthonyPWatts/cycles/issues/99) | Treat a persisted `Running` tick as suspicious after a configurable threshold that defaults to five minutes. | The threshold is diagnostic only and must not fail, retry, repair, or cancel a tick. [Issue #120](https://github.com/AnthonyPWatts/cycles/issues/120) targets persisted SQL attempts; Q119 retired the former hosted-JSON runtime metrics, and [issue #126](https://github.com/AnthonyPWatts/cycles/issues/126) completed representative import/export validation and removal of the runtime file store. |
| [Q112](https://github.com/AnthonyPWatts/cycles/issues/100) | Never auto-fail a `Running` tick based only on age. Require admin inspection, continue blocking, and allow an audited admin to mark a confirmed abandoned attempt failed with an operator and reason. | Existing blocking remains the safe default. The missing explicit `Running`-to-`Failed` operator action is tracked by [issue #121](https://github.com/AnthonyPWatts/cycles/issues/121); normal repair and recovery clear/retry remain separate deliberate steps. |

## Accepted Q113-Q119 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q113](https://github.com/AnthonyPWatts/cycles/issues/101) | Use ASP.NET Core cookie authentication backed by an external OpenID Connect provider for private-alpha and Production environments. Correlate the provider's stable issuer and subject to a local player, and keep empire ownership, admin role, and operational permissions in Cycles-owned data. | Invitations or an allowlist govern admission but are not identity proof. The concrete provider remains environment-configurable, Development username login remains Development-only, and implementation is tracked by [issue #122](https://github.com/AnthonyPWatts/cycles/issues/122). Full ASP.NET Core Identity password management is not required for this boundary. |
| [Q114](https://github.com/AnthonyPWatts/cycles/issues/102) | Store admin roles in Cycles-owned player data. Bootstrap named external identities through explicit operator configuration, then require an authenticated admin, target, and reason for every routine grant or revocation. Keep emergency operator access separate. | The Development `isAdmin` login switch remains Development-only and is not accepted for shared environments. Every bootstrap, grant, and revocation must produce an immutable audit record, and routine operations cannot remove the final active admin. Implementation is tracked by [issue #123](https://github.com/AnthonyPWatts/cycles/issues/123). |
| [Q115](https://github.com/AnthonyPWatts/cycles/issues/103) | Keep `/` public and require private-alpha or Production authentication plus invited-player admission before serving `/app.html`. Keep `/health` public and continue protecting every game API independently. | The trusted playground follows the same page boundary: its shared code protects `/app.html`, dashboard HTML/JavaScript/CSS, authentication routes, and game APIs. The landing page, promotional media, atlas art, interface artwork, and `/health` are public; static artwork contains no player or authoritative game state and is already present in the public repository. A whole-site perimeter remains an explicit option for a distinct deployment that must not be publicly discoverable. Implementation is tracked by [issue #124](https://github.com/AnthonyPWatts/cycles/issues/124). |
| [Q116](https://github.com/AnthonyPWatts/cycles/issues/104) | Before further tester invitations, migrate the deployed playground to managed SQL, enable at least seven days of database-native point-in-time recovery, document restoration, and prove one isolated restore. | Do not build a production-style backup system around the hosted JSON file. The completed [issue #125](https://github.com/AnthonyPWatts/cycles/issues/125) preserved one application-consistent pre-cutover JSON snapshot as migration evidence, avoided dual writes, cut over to Azure SQL, and proved an isolated restore. SQL backup/restore is now authoritative. |
| [Q117](https://github.com/AnthonyPWatts/cycles/issues/105) | Use the existing SQL Server provider on managed Azure SQL for the deployed playground and first online test. Do not delay that deployment to build PostgreSQL or MySQL portability. | The completed [issue #125](https://github.com/AnthonyPWatts/cycles/issues/125) passed the Azure SQL compatibility smoke, cut over the playground, and proved restore. Provider-specific code remains contained in `Cycles.Infrastructure.SqlServer`; revisit portability only when measured cost, licensing, or hosting evidence justifies it. |
| [Q118](https://github.com/AnthonyPWatts/cycles/issues/106) | Do not ban SQL Server-specific features. Permit Azure-SQL-compatible features inside `Cycles.Infrastructure.SqlServer` and SQL migrations when they materially improve correctness, consistency, measured performance, or operations. | Keep `Cycles.Core` and the store contract provider-neutral, document material portability implications, and reject provider-specific features added merely for convenience. The existing `sp_getapplock` transaction boundary is an accepted example. |
| [Q119](https://github.com/AnthonyPWatts/cycles/issues/107) | Demote JSON now to explicit import/export only. API and Worker runtime hosts require explicit SQL configuration after the deployed cutover; normal local development uses the documented SQL Server container. | The completed [issue #126](https://github.com/AnthonyPWatts/cycles/issues/126) retained versioned state transfer, validation, offline inspection, fixtures, legacy conversion, and migration evidence while removing implicit file-store selection and runtime persistence. API, Worker, and gameplay/operator CLI paths now require SQL. |

## Accepted Q035-Q037, Q047-Q049, Q053, And Q057 Answers

| Question | Accepted answer | Consequence |
| --- | --- | --- |
| [Q035](https://github.com/AnthonyPWatts/cycles/issues/23) | Keep Survey Projection as the universal introductory doctrine unlock. | It remains the current automatic threshold effect and is not converted into a selectable branch. |
| [Q036](https://github.com/AnthonyPWatts/cycles/issues/24) | Use a hybrid research model: the introductory unlock is automatic, while later doctrine projects are player-selected. | A selectable doctrine implementation remains gated by Q038-Q046 and separate bounded implementation work. |
| [Q037](https://github.com/AnthonyPWatts/cycles/issues/25) | Do not consume Research for Survey Projection; consume it when a future selected doctrine project completes. | Research keeps its current compatibility behaviour until the first project system exists, then gains an ongoing sink. |
| [Q047](https://github.com/AnthonyPWatts/cycles/issues/35) | Keep Population's first role as direct funding for colonisation. Do not add a Population priority merely to mirror the resource list. | Population remains a stockpile spent by deliberate, location-specific orders. |
| [Q048](https://github.com/AnthonyPWatts/cycles/issues/36) | Keep colonisation as a Population-funded outpost that adds supported local presence rather than ownership or a raw extraction multiplier. | The implemented outpost model is an accepted product rule. Capture, destruction, migration, and infrastructure remain separate decisions. |
| [Q049](https://github.com/AnthonyPWatts/cycles/issues/37) | Require an active supporting fleet for an outpost to provide presence. | Outposts do not create permanent fleetless control outside home systems. |
| [Q053](https://github.com/AnthonyPWatts/cycles/issues/41) | Make Industry's next role a bounded civilian-development and construction-capacity programme, not another ship class or a flat resource-output multiplier. | The persisted Industry weight represents the future Development programme; implementation waits for a bounded design and balance evidence. |
| [Q057](https://github.com/AnthonyPWatts/cycles/issues/45) | Keep raw system resource generation strictly influence-share based for the first version. | Population and infrastructure may affect bounded programmes later but do not multiply Industry, Research, or Population output. |

## Established Product Contracts

These earlier answers remain in force unless a later accepted question explicitly supersedes them.

### Strategy And Economy

- Industry, Research, and Population are non-negative stockpiles.
- Priority weights are percentages totalling 100 and allocate strategic effort across Development, Innovation, Military, and Expansion; they do not map one-to-one to the resource stockpiles.
- The persisted Industry and Research weights remain compatibility names for Development and Innovation. They stay visible but are locked at zero until their accepted programme models are implemented; Military and Expansion share the active 100 points.
- Automatic spending may leave resources reserved.
- Military converts Industry into ships. Development's accepted future role is bounded civilian development or construction capacity rather than a flat output multiplier.
- The first ship costs 25 industry, takes three ticks, and joins the home fleet.
- Research accumulates towards Survey Projection without being consumed; later player-selected doctrine projects will consume Research when completed.
- Population's first role is direct, fleet-supported colonisation. It does not have a priority slider.
- Raw system resource generation remains strictly influence-share based; Population and infrastructure do not multiply it in the first version.
- Uncapped within-Cycle growth is acceptable for the prototype because each Cycle resets, but comeback mechanics remain desirable.
- Engineering may tune named constants from repeatable evidence without seeking approval for each number.

### Identity, Authority, And Visibility

- Development username authentication is acceptable only for trusted Development testing.
- Private-alpha and Production identity use an external OpenID Connect provider through ASP.NET Core cookie authentication. Stable issuer and subject identify the local player; the concrete provider remains environment-configurable.
- Invitations or an allowlist may control admission, but empire ownership, admin role, and operational permissions remain Cycles-owned authorisation data.
- Shared environments bootstrap named admins explicitly, then audit every local admin grant and revocation with actor, target, reason, and timestamp. Emergency operator access remains separate from routine player-admin accounts.
- One participant controls one empire within a game or Cycle. A persistent player may hold memberships in several future game instances; the current runtime still supports one operational active Cycle.
- Admins may inspect all empires and act for support or repair.
- Team/shared control is parked.
- Players see the full galaxy topology.
- Exact local presence, fleets, events, last-tick facts, and Chronicle detail require an active fleet belonging to the player or an allied empire in the relevant system. Automatic allied sharing is accepted but not yet implemented.
- Players continue to see their own empire and audit events; development admins bypass visibility filters.

### Orders And Tick Control

- A fleet has at most one pending next-tick intention. Submitting a different intention requires explicit confirmation of the order being replaced; the previous order becomes `Superseded` and links to its replacement.
- Repeating the identical intention is idempotent rather than creating another order.
- Pending orders may be cancelled before their execution tick by the owning empire or an admin.
- Processed, rejected, cancelled, and superseded order history remains durable.
- Cancellation records an event.
- Scheduled ticks belong to a Worker; ordinary production player actions must not execute ticks.
- **Close command window and advance** is the only ordinary-player Development exception and invokes the same authoritative store operation without changing the player's role, visibility, or empire authority. Its confirmation describes a current-game closure rather than player readiness.
- Shared/private-alpha manual lifecycle actions are limited to audited admins. Recovery and Cycle transitions remain operator-only until their audit and confirmation UX is designed.
- The command deadline first rejects further human submissions. AI and neutral planners then append only their own visibility-respecting intentions before one complete turn ledger is sealed; they do not inspect hidden human commands.
- Missing commands normalise to Hold. Submission time grants no initiative, and each simultaneous phase resolves from a common phase-start snapshot with stable ordering used only for reproducibility.
- Diplomatic intentions resolve during the next authoritative tick rather than changing relationships immediately on submission.
- The accepted phase order is resources; mandatory economy and due construction; new programme spending and construction starts; arrivals and movement; combat; colonisation; derived control, visibility, availability, and defeat state; next-window progression; then facts, Events, and Chronicle selection.
- Any proposal to reorder those phases requires a product decision, focused regression evidence, and updated command forecasts, result presentation, and player guidance.
- Current income may fund current spending. Due ships may defend but cannot receive already-sealed commands; new construction does not progress immediately; successful colonisation alone consumes its reserved population; and progression unlocked during resolution applies from the next command window.
- Colonise Population is reserved at command closure. When the budget cannot fund every otherwise-eligible Colonise intention for the empire, the whole set is rejected; submission time and stable identifiers cannot create a partial winner.
- Movement precedes combat. Route interception and pursuit are not implicit first-version behaviour and require explicit future rules.

### API And Dashboard Contracts

- The landing page is public in the normal private-alpha and Production route contract; the playable dashboard requires authentication and admission. `/health` remains public without exposing game state.
- All player-facing endpoints use explicit response DTOs before online testing.
- Raw domain entities remain internal and must not be returned to the dashboard.
- Purpose-built response contracts remain protected by regression coverage against `Cycles.Core` entity leakage.
- `FactJson` may remain flexible internal storage while fact shapes evolve, but a mechanically consumed, queried, migrated, or publicly exposed payload requires a typed or validated boundary.
- Ordinary player responses and the normal dashboard do not expose raw fact storage. Display text is the default presentation; useful structured detail uses a purpose-built typed contract, while raw inspection is limited to an explicit authorised operator surface.
- Player API property names use camelCase and enum wire values use camelCase strings; numeric enum values are not accepted as part of the public contract.
- Handled player API errors retain the correct HTTP status and use a stable machine-readable code plus a safe human-readable message. Optional structured validation detail and trace correlation may be added without exposing internal exception data.
- The player-scoped dashboard bootstrap exposes typed turn stage, command acceptance, complete phase metadata, empire forecast, and aggregate current-Cycle closure counts. Aggregate game counts do not expose another empire's orders or include another Cycle.
- Forecast income, Colonise reservation, Military programme effects, and next-window progression are labelled as projections; queued construction deliveries are labelled as existing commitments. Event phase metadata may group factual results but does not grant initiative or change simulation order.
- The next player test targets the curated 8-sector, 64-system galaxy with three empire participants and neutral pressure. The model permits up to six empire participants, but support beyond the current three-player Development setup or for materially denser topologies is not implied and requires fresh navigation, balance, and rendering evidence.
- Desktop and laptop browsers are the primary command surface. Narrow layouts retain a readable core loop without page-level horizontal scrolling, but equal mobile optimisation and native mobile clients are not current requirements.
- The resumable Day One guide is the primary in-dashboard training path. It teaches the real command loop and must explicitly explain the accepted visibility model, Events versus Chronicle, and the current tick/Cycle boundary without becoming a separate help system.

### Cycle End And History

- An admin ends a Cycle manually at the current database cutoff.
- Final ranking uses percentage map control derived from effective presence.
- There is one winner and a ranking for every active empire.
- Pending orders do not complete merely because the Cycle ends.
- Preserve roughly the largest 10% of battles, with enough system history to support continuity without promoting every skirmish.
- Reset fleets, resources, population, outposts, doctrines, permissions, and starting power for players and empires; prior rank or winner status does not create a successor advantage.
- Let selected factual history create deterministic galaxy echoes, including changes to historical significance, strategic character, physical system state, and names, under rules available equally to the successor participants.
- Winners receive no inherited recognition or advantage in the next Cycle; their benefit is their recorded impact on overall history.
- Treat the roughly century-scale gap and new equivalent mission as core framing; [issue #157](https://github.com/AnthonyPWatts/cycles/issues/157) still owns the in-world reason for the break and restart.
- Post-Cycle history generation may use complete factual records, including facts that were private during play.

### Chronicle And Narrative

- The Chronicle is player-facing flavour and historical record, not an authority over outcomes.
- Chronicle visibility follows fog-of-war in the current model.
- Importance thresholds should become configurable when the product needs per-Cycle control.
- Deterministic templates are acceptable before AI integration.
- Future AI narrative generation uses a provider-neutral interface with replaceable live-provider connectors and a deterministic development/test connector.
- Future generated prose may infer motive or emotion, but must retain required facts and cannot change simulation outcomes.
- AI work must run outside the tick transaction with durable status.
- Review/safety may be needed after MVP; provider, fallback, and visible failure behaviour remain unanswered.

### Persistence And Deployment

- SQL Server is the current relational implementation; do not add SQLite merely for parity.
- The deployed playground uses managed Azure SQL and has proved an isolated restore from database-native backup with seven days of point-in-time retention. The temporary restore database was deleted after verification.
- The playground and first online test use the existing SQL Server provider on managed Azure SQL. Provider portability is deferred until measured cost, licensing, or hosting evidence justifies it.
- Azure-SQL-compatible provider features are allowed when they solve a material need and remain inside the SQL Server adapter or migrations; the core domain and store contract remain provider-neutral.
- JSON's accepted role is explicit import/export, inspection, fixtures, and migration evidence rather than API or Worker runtime persistence. Normal runtime and local-development hosts require SQL after the safe cutover sequence.
- Complete state exports are sensitive operator/admin artefacts, not player save files or database backups. Any future player-sharing format must be separately designed and redacted.
- Online testing follows authentication hardening and a coherent operational boundary.
- Longer-term production hosting, database, and vendor choices remain open beyond the accepted Azure SQL playground and first-online-test boundary.

### Documentation And Work Tracking

- GitHub issues own concrete actionable work once the backlog migration completes: scope, acceptance criteria, ownership, live status, dependencies, and completion.
- `docs/backlog.md` remains the curated roadmap, priority and sequencing summary, decision-gate overview, and link index rather than a duplicate issue tracker.
- Accepted product answers remain in this document, implemented state in [Project State](project-state.md), and durable rationale in the [Decision Log](decision-log.md).
- Parking-lot ideas and unresolved product choices do not require speculative implementation issues.
- GitHub issues own concrete actionable scope, status, dependencies, and completion; the Markdown backlog owns curated sequence, decision gates, conditional risks, and links.
- Maintained docs describe current `main`; test evidence identifies the deployed commit/build, and Git or release artefacts provide any historical snapshot. Gameplay Cycles do not fork the documentation set.

## Current Gates

Do not expand these areas until the referenced questions have accepted answers:

| Area | Questions | Decision required |
| --- | --- | --- |
| Diplomacy | Q019-Q022 | Disclosure, Chronicle treatment, AI participation, and cross-Cycle memory. Q013-Q018 settle next-tick timing, consent, unilateral hostile or terminating actions, first-version Alliance mechanics, separate empire rankings, and allied influence coexistence; Q025 settles automatic allied live visibility. |
| Visibility and intelligence | Q026-Q034 | Contact precision and persistence, remote control changes, own-empire Events, Chronicle disclosure, destroyed-fleet intelligence, sensors, historical-system visibility, and live ranking exposure. Q023-Q025 retain full topology, active-fleet detail, and automatic allied sharing. |
| Doctrine and technology | Q038-Q046 | First selectable categories, branch count, switching, visibility, modifier scope, logistics, detection, cloaking, and reset behaviour. Q035-Q037 settle the introductory unlock, hybrid choice model, and future Research spending boundary. |
| Population, infrastructure, and comeback | Q050-Q052 and Q054-Q056 | Comeback rules, home-system protection, ship classes, rally points, and capacity limits. Q047-Q049 confirm the current outpost model; Q053 and Q057 settle the next Industry direction and first-version output boundary; Q058 permits history-driven successor-system echoes without participant carry-over. |
| Combat | Q059 and Q061-Q070 | Balance goals, postures, targeting, Hold behaviour, retreat, fleet composition, multi-empire battles, travel modifiers, command latency, and history. Q060 sequences diplomacy before richer combat. |
| Admirals | Q072-Q080 | Bonus scope, transfers, player management, Legendary behaviour, death, diplomacy outcomes, recruitment, biography, and display detail. Q071 establishes narrative-first now with strategic effects later; Q081 permits an admiral association to change both the figure's history and later galaxy naming. |
| Cycle continuity | Q085-Q091, Q093, and [#157](https://github.com/AnthonyPWatts/cycles/issues/157) | Disclosure, cutoff handling, lifecycle controls, successor identity, defeated-empire flavour, summaries, significance evolution, export, and the narrative reason for the break between Cycles. Q082-Q084 reset participants while retaining selected history and galaxy echoes; Q092 permits names to evolve. |
| Chronicle AI | Q094-Q096 and Q098-Q106 | Queue ownership, retry/fallback, failure display, tone, inference, required facts, review, thresholds, interactions, privacy, and versioning. Q097 establishes a provider-neutral connector boundary without selecting a vendor. |
| Tutorial and match enrolment | [Approved multi-game and tutorial programme](multi-game-and-tutorial-plan.md); no implementation issue yet | Issue creation and implementation remain pending. Public discovery, queue offers, no-shows, AI fill, player-created Games, a private automatic-successor policy, and any external notification transport require later evidence or decisions. |

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
