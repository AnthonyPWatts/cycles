# Product Owner Questions

Last updated: 2026-06-24

This file collects the next product-owner questions that should be answered before another broad implementation pass. Earlier architecture and implementation decisions are recorded in `decision-log.md` and `Decisions.txt`; this document focuses on choices that affect player-visible behaviour.

The most useful next answers are in Priority 1 and Priority 2. Later sections can remain open until the core economy, identity, and visibility model are coherent.

## Priority 1: Strategic Economy MVP

### What should each resource do in the first playable economy loop?

Current implementation:

- Industry, research, and population are generated from influence.
- Empire priorities are stored and editable.
- Priorities do not yet spend resources or change outcomes.

Questions:

- Should industry primarily build ships, infrastructure-like capacity, logistics, or a mix? A mix
- Should research initially accumulate toward a future unlock, or provide a simple immediate modifier? Future unlock
- Should population affect production, fleet support, recovery, colonisation, or something else?  Colonisation
- Are resources stockpiles, per-tick capacities, or both? Stockpiles
- Can any resource go negative, or should all spending clamp at available resources? No negative resources

Decision needed to unblock:

- A one-sentence first role for each resource.
- Whether resource totals should be treated as spendable stockpiles.
- Whether per-tick generated and spent amounts should be stored separately from totals.

### How should priority spending work?

Questions:

- Are priority weights percentages that must total 100, or relative weights where any positive total is valid? Total 100
- Should spending happen automatically every tick? Yes
- Should all generated output be allocated, or can players reserve unspent resources? Can be reserved
- Should changing priorities affect the next tick only? Yes
- Should priority changes create public events, private events, or only audit-style records? Don't care, whatever works.

Decision needed to unblock:

- The first spending formula.
- Whether priority changes are visible to other players.
- Whether the UI should enforce a total of 100.

### How should ships be built?

Questions:

- What is the initial industry cost per ship? Pick a sensible figure
- How many ticks should ship construction take? Longer for more powerful ships, never more than 24 ticks.
- Do completed ships join the home fleet, a reserve fleet, a rally fleet, or newly created fleets? By default join the home fleet.
- Should military spending only create ships, or also add defensive/home-system pressure? Ships, keep it simple by only having ships rather than ground defences etc.
- Should population or logistics cap the size or rate of fleet growth? No.

Decision needed to unblock:

- Initial ship cost.
- Initial build delay.
- Initial spawn location.
- Whether ship construction should emit events when queued, completed, or both.

### What are the first balance constraints?

Questions:

- What should prevent runaway exponential growth? Nothing, the game will be reset every "Cycle" this limits.
- Should home systems have a soft recovery advantage beyond the current minimum presence rule? Sure.
- Should low-population or isolated empires have a comeback mechanic? Yes this would be useful.
- Is it acceptable for the first implementation to be deliberately simple and rebalanced later? Yes

Decision needed to unblock:

- A first-pass cap, diminishing return, or explicit decision to defer balance beyond non-negative resources and deterministic tests.

## Priority 2: Identity, Authorisation, And Visibility

### What auth model is needed before the next playable test?

Current implementation:

- `/auth/login` is prototype-only.
- It creates or finds a local player by username.
- Some API calls still trust caller-supplied identifiers.

Questions:

- Is simple development auth acceptable for the next testable build? Yes
- Should eventual production auth target OAuth/OpenID Connect, ASP.NET Core Identity, invite links, or another model? Yes
- Should local development keep a bypass identity? Yes
- Should the password fields currently in the prototype model be removed until real auth exists? No
- Does the next build need an admin identity distinct from player identities? Yes

Decision needed to unblock:

- The next auth step: development-only auth hardening, ASP.NET Core Identity, external provider integration, or no change yet.

### What should one player be allowed to control?

Questions:

- Is the rule always one player to one empire? Yes
- Can admins inspect all empires? Yes
- Can admins act as an empire for repair/support/debugging? Yes 
- Should shared/team control ever be supported, or explicitly parked? Parked.

Decision needed to unblock:

- The player-to-empire rule for order submission and dashboard reads.
- The admin exception rule.

### What does fog of war mean in the first version?

Current direction:

- A player should only get detailed information about who is in a system and in what numbers if they have resources there.

Questions:

- Does "resources there" mean active fleets, home-system influence, any effective presence, or future sensors? Active fleets
- Can a player see the full galaxy map but only partial system details? Yes
- Should fleet counts be exact, approximate, or hidden outside visible systems? Hidden outside of visible systems
- Should events be filtered by visibility, or remain globally visible until fog of war is implemented? Events should be filtered by visibility.
- Should Chronicle entries reveal hidden facts, or only public summaries? Public summaries

Decision needed to unblock:

- A precise first visibility rule for system details, fleet details, events, and Chronicle entries.

## Priority 3: Orders And Dashboard Workflow

### Should order cancellation exist before deeper economy work?

Questions:

- Can pending orders be cancelled before their execute-after tick? Yes
- Can processed or rejected orders ever be hidden, archived, or cleared? No
- Should cancellation create an event? Yes
- Should cancellation be restricted to the owning empire and admins? Yes

Decision needed to unblock:

- Whether to build order cancellation now or leave orders append-only for the next sprint.

### Should the dashboard expose tick controls for local testing?

Current direction:

- Public player-facing API calls should not decide simulation outcomes.
- CLI remains the current tick runner.
- A worker is likely needed later for scheduled ticks.

Questions:

- Should the dashboard include an admin-only or development-only tick button? Yes
- Should manual tick triggering remain CLI-only until auth/admin boundaries exist? Yes
- Should the next worker run hourly by default, or should scheduling wait until economy behaviour is implemented? Scheduling can wait for now.

Decision needed to unblock:

- Whether any tick execution control belongs in the browser before admin auth exists.

### What dashboard feedback matters next?

Questions:

- Should the dashboard show last tick generated resources and spending before spending is implemented? Dont mind
- Should order history be filterable by pending, processed, rejected, and cancelled? Dont mind
- Should system detail replace the current client-side detail calculation immediately? Dont mind
- Should the player see raw event facts, summarised facts, or display text only? Dont mind

Decision needed to unblock:

- The next dashboard slice after the current map, fleet, order, priority, event, and Chronicle views.

## Priority 4: Cycle End And Continuity

### What makes an empire rank well at Cycle end?

Current direction:

- Cycle ends are manual and admin-run.
- Cycle end is effectively a database freeze/cutoff.

Questions:

- Which metrics define final rankings: influence, fleets, resources, battles won, Chronicle score, survival, or a blend? Influence across systems (% control of map)
- Should there be one winner, ranked standings, or several categories of legacy? One winner, with rankings for every player
- Can an empire be defeated before the Cycle ends? No empire can be fully defeated, as you shouldn't be able to conquer someone's home planet. You can drive them back to their own system. 
- What happens to pending orders at cutoff? Never completed.

Decision needed to unblock:

- Initial ranking metrics and whether the first Cycle-end command should produce one winner or a standings table.

### What history should survive into the next Cycle?

Questions:

- Which battles are important enough to preserve? Maybe the largest 10% of battles, whatever that is
- Should systems retain names, scars, strategic value changes, or historical significance? Yes
- Should surviving empires influence successor factions, starting positions, or only flavour text? Flavour text only
- How much private information can be used for history generation after the Cycle ends? All of it
- Should the next Cycle be generated deterministically from prior facts? The flavour text should be, not fully deterministically, but largely driven by facts. I see it as an LLM integration.

Decision needed to unblock:

- The first persistent history schema: rankings, selected battles, selected systems, and any cross-Cycle signals.

## Priority 5: Chronicle And Narrative

### What is the Chronicle for?

Questions:

- Is the Chronicle primarily flavour, strategic intelligence, historical record, or all three? Flavour and historical record
- Should Chronicle entries be visible to all players immediately? Unless hidden by fog of war.
- Can Chronicle entries be private, delayed, disputed, discovered, or redacted by fog of war? Hidden by fog of war
- Should Chronicle importance thresholds be configurable? Yes

Decision needed to unblock:

- Whether Chronicle visibility follows public events, fog of war, or a separate rule.

### What should generated narrative be allowed to say?

Current direction:

- AI-generated narrative is desirable later.
- Deterministic templates are acceptable for early development.
- Generated prose must not decide simulation outcomes.

Questions:

- Which facts must every generated battle report include? Admirals involved, any interesting events (did an underdog win etc?) 
- Can generated text infer motive, emotion, or strategy, or must it stay factual? Infer away, make it interesting. Build a narrative
- Should generated entries require review before being shown? Not in mvp. Later, will probably need a safety/review feature
- Should AI generation be queued outside the tick transaction with status tracking? Yes
- What should players see when generation fails? It shouldn't fail.

Decision needed to unblock:

- Required-fact rules and whether the first generated reports should be template-only, AI-backed, or template-first with an AI boundary prepared.

## Priority 6: Deployment And Test Access

### When should the prototype be testable online?

Current direction:

- No immediate deployment is planned.
- Being able to run and test online would be useful.
- Vendor lock-in should be avoided where practical.

Questions:

- Is online testing needed before auth is hardened? No
- Should `/` be public while `/app.html` is private? Whatever is most sensible
- Should the app deploy as a container, an app service, or another simple host? Don't mind.
- Should SQL Server remain the online test database, or should provider portability be prioritised first? SQL server is fine for now, for production/licensing it'd need to be something free.
- What backup/restore expectation is acceptable before inviting testers? Limited. 

Decision needed to unblock:

- Whether deployment work belongs before or after auth and the first economy loop.

## Engineering Defaults Unless Overridden

These do not need product-owner decisions unless the product behaviour should differ:

- Keep SQL Server as the current relational implementation.
- Keep JSON persistence only as development/import/export support.
- Keep tick execution outside public player-facing endpoints.
- Keep recovery administration CLI-only until admin auth exists.
- Prefer explicit API response DTOs over returning domain entities directly.
- Keep facts authoritative and generated prose non-authoritative.
- Keep future diplomacy, admirals, doctrine, cloaking, logistics, and complex AI narrative parked until the simulation spine and economy are stronger.
