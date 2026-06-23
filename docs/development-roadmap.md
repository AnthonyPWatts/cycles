# Development Roadmap

Last updated: 2026-06-23

## Roadmap Principles

Cycles should grow in a sequence that protects the central design: strategic competition, persistent history, and emergent legends.

The practical sequencing rule is:

1. protect the simulation spine;
2. make state durable and auditable;
3. make player decisions matter at empire scale;
4. preserve meaningful facts;
5. only then add narrative interpretation and richer future systems.

Avoid adding feature breadth before the tick engine and persistence model are trustworthy. A fragile simulation with many systems will be harder to rescue than a narrow simulation with reliable facts.

## Stage 0: Baseline Prototype

Status: complete as a local MVP.

Purpose: prove that the project can run a Cycle, process ticks, move fleets, resolve primitive combat, generate events, and create Chronicle entries.

Implemented:

- .NET solution with Core, CLI, API, and test projects.
- JSON state store with local file lock.
- SQL Server state store behind the same persistence abstraction.
- SQLDockerDeployKit-style SQL Server bootstrap image.
- Galaxy generation.
- Influence-based resources.
- Fleet orders.
- Movement and arrival handling.
- Primitive combat and battle records.
- Event log.
- Chronicle scoring for battles.
- Minimal browser dashboard.

Exit evidence:

- Build passes.
- xUnit test suite passes.
- CLI smoke checks work.
- API and dashboard smoke checks work.
- SQL Server container, CLI, and API smoke checks work.

## Stage 1: Simulation Spine Hardening

Goal: turn the current prototype loop into a reliable local simulation platform.

### Outcomes

- Core behaviours are covered by proper automated tests.
- Tick processing is explicitly idempotent.
- Order state transitions are predictable and recoverable.
- Combat determinism is locked down enough for regression testing.
- Failed ticks have a documented recovery path.

### Work Items

1. Replace the executable test harness with a standard .NET test project.
   - Choose xUnit or NUnit and use it consistently.
   - Keep tests focused on public simulation behaviour.
   - Move reusable test-state builders into test helpers.
   - Keep the current no-network restore path in mind; if adding packages, document restore expectations.

2. Define simulation service boundaries.
   - Keep domain models in `Cycles.Core`.
   - Introduce clear interfaces for state loading, state mutation, clock access, and deterministic random generation.
   - Keep API and CLI as thin hosts over application services.

3. Expand tick tests.
   - Empty tick advances exactly once.
   - Due orders are processed once.
   - Future orders are not processed early.
   - Rejected orders cannot later become processed.
   - In-transit fleets arrive on the correct tick.
   - Failed processing does not partially commit state.

4. Expand influence tests.
   - Multiple fleets from one empire aggregate presence.
   - Destroyed fleets do not contribute presence.
   - In-transit fleets do not contribute presence.
   - Home-system minimum presence applies only to the home empire.
   - Zero-presence systems produce no resources.

5. Expand combat tests.
   - Same inputs produce same battle outcome and losses.
   - Destroyed fleets are marked destroyed.
   - Multi-fleet defender losses are distributed correctly.
   - Combat event facts match the battle record.
   - Chronicle scoring does not contradict battle facts.

6. Add an explicit recovery model.
   - Decide whether a failed tick blocks the Cycle until admin action.
   - Add a documented admin command or developer procedure for clearing/retrying after investigation.
   - Keep automatic retry out of scope until persistence is stronger.

### Suggested File Changes

- `src/Cycles.Core`
  - Split broad simulation concerns out of `Simulation.cs` when tests justify it.
  - Candidate files: `TickEngine.cs`, `InfluenceCalculator.cs`, `CombatResolver.cs`, `ChronicleScoring.cs`.
- `tests/Cycles.Tests`
  - Replace top-level console tests with standard test classes.
- `docs/project-state.md`
  - Update verification commands and known limitations after the test framework is chosen.

### Exit Criteria

- Automated tests cover influence, order lifecycle, movement, combat, Chronicle scoring, and tick failure behaviour.
- Running the same test suite twice gives the same results.
- `dotnet test` is the primary verification command.
- `dotnet build` and tests pass from a clean checkout.

## Stage 2: Relational Persistence And Tick Locking

Goal: replace JSON persistence with a relational store while preserving the current domain behaviour.

### Recommended Direction

Use the SQLDockerDeployKit-style SQL Server container as the first relational target. It is already working locally, matches the user's preferred direction for this stage, and forces the project to model real tables, keys, indexes, transactions, and operational startup behaviour.

SQLite can still be useful later for fast isolated tests, but it is no longer the primary implementation path.

### Outcomes

- The project has an explicit persistence boundary.
- The MVP entities from the technical design exist as relational tables.
- Tick processing happens inside a transaction.
- A duplicate worker cannot complete the same tick twice.
- Events, battle records, and Chronicle entries remain queryable facts.

### Work Items

1. Add a persistence project or folder.
   - Current project: `src/Cycles.Infrastructure.SqlServer`.
   - Keep `Cycles.Core` independent of database packages.
   - Put repository/store implementations and migrations outside Core.

2. Define the persistence abstraction.
   - Load active Cycle metadata.
   - Load due orders for tick.
   - Load systems, links, empires, resources, fleets, events, battle records, and Chronicle entries needed by the tick.
   - Save tick outcomes transactionally.
   - Mark orders processed or rejected.
   - Append events and battle/Chronicle records.

3. Create the first relational schema.
   - `Players`
   - `Cycles`
   - `Empires`
   - `EmpireResources`
   - `EmpirePriorities`
   - `Systems`
   - `SystemLinks`
   - `Fleets`
   - `FleetOrders`
   - `TickLogs`
   - `Events`
   - `BattleRecords`
   - `ChronicleEntries`

4. Add constraints and indexes early.
   - Unique tick log per Cycle and tick number.
   - Order lookup by Cycle, status, and execute-after tick.
   - Fleet lookup by Cycle, empire, status, and current system.
   - Event lookup by Cycle and tick.
   - Battle and Chronicle lookup by Cycle and system.

5. Implement tick locking.
   - The current SQL Server bridge uses transaction-scoped `sp_getapplock` for whole-state updates.
   - Move toward per-Cycle tick locking as the persistence model becomes incremental.
   - Ensure only one running tick per Cycle.
   - Ensure completed tick numbers cannot be completed again.
   - Treat lock timeouts and abandoned running ticks as explicit recovery cases.

6. Add migration/seed tooling.
   - CLI command to initialise the database.
   - CLI command to seed a Cycle.
   - CLI command to run one tick.
   - Keep JSON seed export/import optional, not central.

7. Move API and CLI to the persistence abstraction.
   - Avoid duplicating state mutation logic.
   - Keep the API order-submission path and CLI tick path using the same validation/application services.

8. Add integration tests.
   - Use the local SQL Server container for the first integration tests.
   - Consider temporary SQLite tests only if they materially reduce feedback time.
   - Test a full seed -> submit order -> tick -> query outcome path.
   - Test duplicate tick prevention against the database.

### Data Migration Position

There is no need to migrate existing JSON state as a product requirement yet. If useful, provide a developer-only importer from current JSON files into SQL Server, but do not let importer complexity block the relational implementation.

### Exit Criteria

- SQL Server-backed seed, tick, show, API, and dashboard work.
- JSON store is either removed from default paths or clearly marked as legacy/dev-only.
- Tick execution is transactionally committed.
- Duplicate tick completion is prevented at storage level.
- Integration tests cover the relational happy path and duplicate-tick path.

## Stage 3: Strategic Economy And Player Decisions

Goal: make empire-level decisions matter without introducing planet-level micromanagement.

### Outcomes

- Empire priorities affect resource spending.
- Ships can be built automatically from military investment.
- Expansion and research have simple, visible effects.
- The player has meaningful strategic levers without per-system build queues.

### Work Items

1. Define resource semantics.
   - Industry builds ships and infrastructure-like abstract capacity.
   - Research unlocks future modifiers but initially can accumulate.
   - Population may influence resource output, recovery, or fleet support.
   - Avoid adding more resources until these three have a role.

2. Implement spending priorities.
   - Convert priority weights into per-tick spending allocations.
   - Store both raw resource totals and per-tick spending facts.
   - Generate events for meaningful changes, not noisy per-tick bookkeeping.

3. Add automatic ship building.
   - Military priority converts industry into ship construction.
   - Built ships join an empire's home fleet or a designated rally fleet.
   - Include caps or diminishing returns so growth does not explode.

4. Add expansion pressure.
   - Expansion priority increases influence projection from fleets or home systems.
   - Keep the first version simple and transparent.
   - Ensure influence remains derived, not stored ownership.

5. Add research as a delayed benefit.
   - First implementation can unlock simple doctrine-like modifiers.
   - Avoid a complex tech tree at this stage.

6. Improve UI feedback.
   - Show priority weights and resource trend.
   - Let a player adjust priorities from the dashboard.
   - Display last tick's generated resources and spending.

7. Test balance primitives.
   - Growth is monotonic when uncontested.
   - A player with more influence gains more output.
   - Spending cannot create negative resources.
   - Priority changes affect later ticks, not past ticks.

### Exit Criteria

- A player can set priorities and observe different outcomes over several ticks.
- Military investment can build ships.
- Resource and spending facts are auditable.
- No per-planet or per-system build queue is introduced.

## Stage 4: Game API And Client Hardening

Goal: make the prototype playable through the API/dashboard without weakening the server-authoritative model.

### Outcomes

- Real player identity exists.
- API endpoints enforce player/empire boundaries.
- Orders are submitted through authenticated context rather than arbitrary IDs.
- The dashboard supports the common local play loop.

### Work Items

1. Decide authentication mode.
   - For local/private prototype, use simple development auth first.
   - For deployed prototype, add an authentication provider deliberately.
   - Do not store plain prototype password hashes as if they were real auth.

2. Tighten API contracts.
   - Request DTOs should avoid trusting caller-supplied empire IDs where player context can derive them.
   - Return view models rather than raw domain entities when public contract matters.
   - Standardise error responses.

3. Add order query endpoints.
   - Pending orders for current empire.
   - Recent processed/rejected orders.
   - Order cancellation before execution, if it fits the design.

4. Add tick visibility.
   - Last completed tick.
   - Last tick summary.
   - Failed/recovery-required state.
   - Next expected tick time, once scheduling exists.

5. Improve dashboard flows.
   - Priority editing.
   - Pending order list.
   - Fleet detail panel.
   - System detail panel with influence breakdown.
   - Recent events scoped to the player's empire and region.

6. Add API tests.
   - Use ASP.NET Core test host once dependencies are accepted.
   - Cover auth/player boundaries.
   - Cover order submission validation.

### Exit Criteria

- A local player can run the app, view their empire, adjust priorities, queue movement/attack orders, run ticks, and see results without CLI-only state manipulation except for tick execution.
- API does not allow arbitrary cross-empire mutation.
- Dashboard reflects server state after refresh without console errors.

## Stage 5: History And Cycle Continuity

Goal: make history a system, not just event flavour.

### Outcomes

- Cycle endings preserve rankings and significant events.
- Systems accumulate historical significance from gameplay.
- New Cycles can reference prior events and famous systems.
- Chronicle entries remain grounded in simulation facts.

### Work Items

1. Add rankings and metrics.
   - Resource totals.
   - Fleet strength.
   - Influence breadth.
   - Battle outcomes.
   - Chronicle impact.

2. Add Cycle end processing.
   - Freeze final rankings.
   - Select major events.
   - Persist Cycle summary.
   - Mark Cycle complete.
   - Create next Cycle metadata.

3. Add historical system signals.
   - Battle count.
   - Largest battle in system.
   - Repeated conflict.
   - Decisive Cycle events.
   - Chronicle associations.

4. Add cross-Cycle references.
   - Preserve famous names where appropriate.
   - Generate successor systems or echoes.
   - Surface historical markers in the API and dashboard.

5. Separate facts from interpretation.
   - Keep raw battle/event facts immutable.
   - Store factual summaries separately from narrative prose.
   - Track generated/reviewed/published status for future narrative text.

6. Add history tests.
   - Significant battle increases system historical signal.
   - Cycle end preserves rankings.
   - New Cycle can reference prior Chronicle entries.
   - Narrative fields do not alter simulation facts.

### Exit Criteria

- A completed Cycle produces a durable historical record.
- At least one future Cycle can surface prior events/systems.
- The Chronicle has clear fact provenance.

## Stage 6: Narrative Generation

Goal: use AI or template generation to interpret factual events without giving it authority over outcomes.

### Outcomes

- Battle reports and Chronicle prose are generated from structured facts.
- Generated text is stored separately from authoritative facts.
- Failed generation does not block simulation.
- Validation catches contradictions in required facts.

### Work Items

1. Define narrative source contracts.
   - Battle facts.
   - Related system history.
   - Related empire history.
   - Related admiral/person history when implemented.

2. Start with deterministic templates.
   - Use template text before calling external AI.
   - Establish required-fact validation.
   - Make generated prose reproducible in tests.

3. Add asynchronous generation boundary.
   - Queue narrative work after tick commit.
   - Store generation status.
   - Store prompt/context snapshot if AI is later used.

4. Add contradiction checks.
   - Required system name.
   - Required participants.
   - Required outcome.
   - Required losses.
   - Required tick/Cycle reference.

5. Add AI provider only after templates prove the data shape.
   - Keep provider isolated.
   - Make failure non-fatal.
   - Log but do not expose provider errors to players by default.

### Exit Criteria

- Chronicle and battle reports have readable narrative text.
- Narrative generation cannot change battle outcomes or resource changes.
- Tests prove generated text includes required facts.

## Stage 7: Admirals, Diplomacy, Technology, And Deeper Strategy

Goal: add the future systems from the design documents as extensions of influence, events, and history.

### Order Of Introduction

1. Admirals and named figures.
2. Diplomacy and alliances.
3. Doctrines and light technology.
4. Cloaking/detection/logistics.

This order favours history first. Admirals create narrative anchors; diplomacy creates history-rich events; technology and cloaking then add strategic texture.

### Guardrails

- Do not add systems that bypass influence.
- Do not add systems that require constant online presence.
- Do not introduce per-planet micromanagement.
- Do not let AI invent outcomes.
- Do not make future extensibility more important than current clarity.

## Suggested Immediate Next Sprint

The next development sprint should continue Stage 2 before moving to Stage 3. A sensible next sprint:

1. Replace SQL whole-state delete/reinsert writes with targeted persistence operations.
2. Add schema versioning and an initialisation/migration command.
3. Add SQL Server integration tests for seed, show, tick, order submission, and duplicate tick prevention.
4. Define the failed-tick recovery procedure and admin inspection command.
5. Split `Simulation.cs` into focused files only where the existing tests make that refactor low-risk.
6. Update `docs/project-state.md` after verification.

This produces a better foundation for the relational persistence work that follows.
