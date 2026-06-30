# Product Owner Questions

Last updated: 2026-06-30

This file collects product-owner questions and answers that shape player-visible behaviour. Earlier architecture and implementation decisions are recorded in `decision-log.md` and `Decisions.txt`.

The Priority 1 economy answers have been implemented as the first strategic economy slice: resources are stockpiles, priority weights must total 100, military industry spending queues ships, queued ships complete into the home fleet, expansion priority projects influence, and resources cannot go negative.

## Priority 1: Strategic Economy MVP

### What should each resource do in the first playable economy loop?

Current implementation:

- Industry, research, and population are generated from influence.
- Empire priorities are stored and editable.
- Military priority spending now consumes industry stockpile into queued ship construction.
- Research and population remain stockpiles for future unlock and colonisation effects.

Questions and answers:

- Should industry primarily build ships, infrastructure-like capacity, logistics, or a mix? A mix.
- Should research initially accumulate toward a future unlock, or provide a simple immediate modifier? Future unlock.
- Should population affect production, fleet support, recovery, colonisation, or something else? Colonisation.
- Are resources stockpiles, per-tick capacities, or both? Stockpiles.
- Can any resource go negative, or should all spending clamp at available resources? No negative resources.

Decision:

- Industry is the first spendable stockpile and currently builds ships through military priority spending.
- Research accumulates toward future unlocks.
- Population accumulates toward future colonisation.
- Per-tick generated and spent amounts are stored separately from stockpile totals.

### How should priority spending work?

Questions and answers:

- Are priority weights percentages that must total 100, or relative weights where any positive total is valid? Total 100.
- Should spending happen automatically every tick? Yes.
- Should all generated output be allocated, or can players reserve unspent resources? Can be reserved.
- Should changing priorities affect the next tick only? Yes.
- Should priority changes create public events, private events, or only audit-style records? Don't care, whatever works.

Decision:

- Priority weights must total 100.
- Spending happens automatically during tick processing after resource generation.
- Military spending uses its percentage of the current industry stockpile.
- Industry that is not spent remains reserved.
- Priority changes are low-severity audit-style events for now.

### How should ships be built?

Questions and answers:

- What is the initial industry cost per ship? Pick a sensible figure.
- How many ticks should ship construction take? Longer for more powerful ships, never more than 24 ticks.
- Do completed ships join the home fleet, a reserve fleet, a rally fleet, or newly created fleets? By default join the home fleet.
- Should military spending only create ships, or also add defensive/home-system pressure? Ships; keep it simple by only having ships rather than ground defences.
- Should population or logistics cap the size or rate of fleet growth? No.

Decision:

- The first ship type costs 25 industry.
- Ship construction takes 3 ticks.
- Completed ships join the empire's home fleet.
- Queue and completion events are emitted.

### What are the first balance constraints?

Questions and answers:

- What should prevent runaway exponential growth? Nothing, the game will be reset every Cycle.
- Should home systems have a soft recovery advantage beyond the current minimum presence rule? Sure.
- Should low-population or isolated empires have a comeback mechanic? Yes, this would be useful.
- Is it acceptable for the first implementation to be deliberately simple and rebalanced later? Yes.

Decision:

- The first implementation deliberately avoids growth caps beyond non-negative resources and deterministic tests.
- Home-system minimum presence remains the first recovery advantage.
- Explicit comeback mechanics remain future work.

## Priority 2: Identity, Authorisation, And Visibility

### What auth model is needed before the next playable test?

Current implementation:

- `/auth/login` establishes a deliberate development-auth cookie.
- It creates or finds a local player by username.
- Players now have explicit `Player` or `Admin` roles.
- Player order and priority calls derive empire authority from the authenticated context.
- This is still not production auth.

Questions and answers:

- Is simple development auth acceptable for the next testable build? Yes.
- Should eventual production auth target OAuth/OpenID Connect, ASP.NET Core Identity, invite links, or another model? Yes.
- Should local development keep a bypass identity? Yes.
- Should the password fields currently in the prototype model be removed until real auth exists? No.
- Does the next build need an admin identity distinct from player identities? Yes.

Decision:

- Development-only auth hardening can come before real production auth.
- A distinct admin identity is needed before admin dashboard actions are exposed.
- Production auth remains a future pre-deployment decision, likely through ASP.NET Core authentication with OAuth/OpenID Connect or Identity rather than the development cookie.

### What should one player be allowed to control?

Questions and answers:

- Is the rule always one player to one empire? Yes.
- Can admins inspect all empires? Yes.
- Can admins act as an empire for repair/support/debugging? Yes.
- Should shared/team control ever be supported, or explicitly parked? Parked.

Decision:

- Enforce one player to one empire for order submission and dashboard reads.
- Add explicit admin exceptions for inspection and support/debug actions.

### What does fog of war mean in the first version?

Current implementation:

- A player should only get detailed information about who is in a system and in what numbers if they have resources there.
- The first implementation interprets "resources there" as active fleets.
- The full map structure remains visible to logged-in players.
- Exact effective presence and local fleet details are returned only for systems where the player has an active fleet.
- Recent events, last-tick summaries, and Chronicle entries are filtered through the same active-fleet visibility model.
- A player's own empire/audit events remain visible to that player.
- Admin development users can inspect everything.

Questions and answers:

- Does "resources there" mean active fleets, home-system influence, any effective presence, or future sensors? Active fleets.
- Can a player see the full galaxy map but only partial system details? Yes.
- Should fleet counts be exact, approximate, or hidden outside visible systems? Hidden outside visible systems.
- Should events be filtered by visibility, or remain globally visible until fog of war is implemented? Events should be filtered by visibility.
- Should Chronicle entries reveal hidden facts, or only public summaries? Public summaries.

Decision:

- Implement full-map visibility with hidden fleet details outside systems where the player has active fleets.
- Filter events and Chronicle entries through the same first visibility model.

## Priority 3: Orders And Dashboard Workflow

### Should order cancellation exist before deeper economy work?

Questions and answers:

- Can pending orders be cancelled before their execute-after tick? Yes.
- Can processed or rejected orders ever be hidden, archived, or cleared? No.
- Should cancellation create an event? Yes.
- Should cancellation be restricted to the owning empire and admins? Yes.

Decision:

- Add pending-order cancellation when the next order workflow slice starts.

### Should the dashboard expose tick controls for local testing?

Current direction:

- Public player-facing API calls should not decide simulation outcomes.
- CLI remains the current tick runner.
- A worker is likely needed later for scheduled ticks.

Questions and answers:

- Should the dashboard include an admin-only or development-only tick button? Yes.
- Should manual tick triggering remain CLI-only until auth/admin boundaries exist? Yes.
- Should the next worker run hourly by default, or should scheduling wait until economy behaviour is implemented? Scheduling can wait for now.

Decision needed:

- Keep manual tick triggering CLI-only until admin auth boundaries exist.
- Add an admin/development dashboard tick control only after those boundaries exist.

### What dashboard feedback matters next?

Questions and answers:

- Should the dashboard show last tick generated resources and spending before spending is implemented? Don't mind.
- Should order history be filterable by pending, processed, rejected, and cancelled? Don't mind.
- Should system detail replace the current client-side detail calculation immediately? Don't mind.
- Should the player see raw event facts, summarised facts, or display text only? Don't mind.

Decision:

- The dashboard now shows last-tick generated and spent resource amounts on resource cards.
- Further dashboard slices can follow the auth, visibility, or order-cancellation priorities.

## Priority 4: Cycle End And Continuity

### What makes an empire rank well at Cycle end?

Current direction:

- Cycle ends are manual and admin-run.
- Cycle end is effectively a database freeze/cutoff.
- The first `cycle end` command marks the Cycle completed and persists one winner plus ranked standings for every active empire.
- Cycle completion now increases system historical significance from repeated battles and largest-loss battle locations.
- Cycle completion now preserves the top 10% of battles by total losses, with a minimum of one battle when battles occurred.

Questions and answers:

- Which metrics define final rankings: influence, fleets, resources, battles won, Chronicle score, survival, or a blend? Influence across systems, as percentage control of the map.
- Should there be one winner, ranked standings, or several categories of legacy? One winner, with rankings for every player.
- Can an empire be defeated before the Cycle ends? No empire can be fully defeated, as a home planet should not be conquerable. Players can be driven back to their own system.
- What happens to pending orders at cutoff? Never completed.

Decision:

- Add a first Cycle-end command that produces one winner plus ranked standings from influence across systems.

### What history should survive into the next Cycle?

Questions and answers:

- Which battles are important enough to preserve? Maybe the largest 10% of battles.
- Should systems retain names, scars, strategic value changes, or historical significance? Yes.
- Should surviving empires influence successor factions, starting positions, or only flavour text? Flavour text only.
- How much private information can be used for history generation after the Cycle ends? All of it.
- Should the next Cycle be generated deterministically from prior facts? The flavour text should be largely driven by facts, likely through an LLM integration.

Decision:

- The first persistent history schema should cover rankings, selected largest battles, selected systems, and historical system signals.
- Rankings, selected largest battles, first system historical-significance updates, and dedicated system historical signals now have persistence; selected systems remain future work.

## Priority 5: Chronicle And Narrative

### What is the Chronicle for?

Questions and answers:

- Is the Chronicle primarily flavour, strategic intelligence, historical record, or all three? Flavour and historical record.
- Should Chronicle entries be visible to all players immediately? Unless hidden by fog of war.
- Can Chronicle entries be private, delayed, disputed, discovered, or redacted by fog of war? Hidden by fog of war.
- Should Chronicle importance thresholds be configurable? Yes.

Decision needed:

- Chronicle visibility should follow fog of war.
- Importance thresholds should become configurable.

### What should generated narrative be allowed to say?

Current direction:

- AI-generated narrative is desirable later.
- Deterministic templates are acceptable for early development.
- Generated prose must not decide simulation outcomes.

Questions and answers:

- Which facts must every generated battle report include? Admirals involved and interesting events, such as underdog victories.
- Can generated text infer motive, emotion, or strategy, or must it stay factual? It can infer and build narrative.
- Should generated entries require review before being shown? Not in MVP; likely later.
- Should AI generation be queued outside the tick transaction with status tracking? Yes.
- What should players see when generation fails? It shouldn't fail.

Decision needed:

- Generated narrative should be queued outside the tick transaction.
- The first AI boundary should include required-fact validation and failure status, even if player-facing fallback handling is revisited later.

## Priority 6: Deployment And Test Access

### When should the prototype be testable online?

Current direction:

- No immediate deployment is planned.
- Being able to run and test online would be useful.
- Vendor lock-in should be avoided where practical.

Questions and answers:

- Is online testing needed before auth is hardened? No.
- Should `/` be public while `/app.html` is private? Whatever is most sensible.
- Should the app deploy as a container, an app service, or another simple host? Don't mind.
- Should SQL Server remain the online test database, or should provider portability be prioritised first? SQL Server is fine for now; for production/licensing it would need to be something free.
- What backup/restore expectation is acceptable before inviting testers? Limited.

Decision needed:

- Deployment work belongs after auth hardening and the first economy loop.
- SQL Server is acceptable for now, but future production hosting should revisit a free relational provider.

## Engineering Defaults Unless Overridden

These do not need product-owner decisions unless product behaviour should differ:

- Keep SQL Server as the current relational implementation.
- Keep JSON persistence only as development/import/export support.
- Keep tick execution outside public player-facing endpoints.
- Keep recovery administration CLI-only until admin auth exists.
- Prefer explicit API response DTOs over returning domain entities directly.
- Keep facts authoritative and generated prose non-authoritative.
- Keep future diplomacy, admirals, doctrine, cloaking, logistics, and complex AI narrative parked until the simulation spine and economy are stronger.
