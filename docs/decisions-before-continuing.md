# Decisions Before Continuing

Last updated: 2026-06-24

This file collects product and architecture choices that should be made before the next substantial implementation pass. It is intentionally decision-oriented: the backlog says what could be built; this file says what needs a clear call before building it.

## Recommended Decision Order

1. Persistence and migrations.
2. Tick recovery and worker operation.
3. Auth and player boundaries.
4. Strategic economy semantics.
5. Deployment posture.
6. Narrative/history depth.

These decisions do not all need perfect answers now. The useful goal is to pick enough direction that the next few implementation stages are coherent.

## 1. Persistence And Migrations

### Decision: Should SQL Server remain the primary relational target for the next stage?

Current state:

- SQL Server bootstrap exists under `database/sqldockerdeploykit`.
- `Cycles.Infrastructure.SqlServer` can read and write the prototype state.
- The SQL store currently writes the whole `GameState` snapshot through relational tables.

Options:

- Continue with SQL Server as the primary relational implementation.
- Add SQLite for faster local integration tests while keeping SQL Server as the production-shaped target.
- Revisit PostgreSQL or another provider before deeper persistence work.

Recommended default:

- Continue with SQL Server for now. It is already working locally and matches the current repo direction.

Questions to answer:

- Is SQL Server the intended long-term database, or only the current local proof target?
- Should SQLite still be added for fast tests, or would that split attention too early?
- Should JSON remain a supported development store, or become import/export only?

### Decision: How should schema versioning work?

Questions to answer:

- Should migrations be plain SQL scripts, a small custom runner, EF Core migrations, DbUp, or another tool?
- Should the Docker bootstrap image apply migrations automatically on startup?
- Should the CLI expose `db init`, `db migrate`, and `db status` commands?
- Should migration history live in a `SchemaMigrations` table?
- Should destructive local reset remain a separate explicit command?

### Decision: When should snapshot persistence be replaced?

Questions to answer:

- Should the next persistence pass replace full-state delete/reinsert writes immediately?
- Which repositories should come first: orders, ticks, fleets, resources, events, or Chronicle records?
- Should tick execution load a focused aggregate rather than the entire `GameState`?
- Should writes be append/update operations only, with immutable event and battle facts?

## 2. Tick Operation And Recovery

### Decision: What is the official failed-tick recovery workflow?

Current state:

- Failed ticks mark the Cycle `RecoveryRequired`.
- A read-only CLI `recovery` command exists.
- There is no clear/retry/admin command yet.

Questions to answer:

- Who is allowed to clear `RecoveryRequired`?
- Should clearing recovery require a reason string and operator identity?
- Should recovery actions be written as events, audit records, or both?
- Should a failed tick be retried with the same tick number or skipped after repair?
- Should there be a `recovery --details` command that prints full diagnostics?
- Should recovery commands be available in the CLI only, or also through an admin API?
- Should local/dev recovery be more permissive than shared/prod recovery?

### Decision: What owns tick execution?

Questions to answer:

- Should the CLI remain the tick runner for now?
- Should a `Cycles.Worker` project be added next?
- Should ticks run on a schedule, manually, or both?
- Should a worker process one active Cycle or many?
- What timeout makes a `Running` tick suspicious or abandoned?
- Should the lock be transaction-scoped, row-based, `sp_getapplock`, or a dedicated `TickLocks` table?

## 3. Auth, Identity, And Player Boundaries

### Decision: What is the next authentication model?

Current state:

- `/auth/login` is prototype-only.
- It creates or finds a local player from a username.
- API calls still accept player/empire identifiers that should later come from auth context.

Questions to answer:

- Should the next auth step be simple development auth, ASP.NET Core Identity, external provider auth, or a custom lightweight model?
- Is email/password needed, or is invite/link-based access enough for the prototype?
- Should local dev support a bypass identity?
- Should the dashboard remember a player locally, or should every request derive identity from server auth?
- Should prototype password hashes and fields be removed until real auth exists?

### Decision: How strict should player and empire authorisation be?

Questions to answer:

- Should order submission derive `EmpireId` from the authenticated player only?
- Can one player control multiple empires?
- Can admins inspect or act as any empire?
- Should read endpoints expose all empires/systems, or only what the current player can see?
- Should fog-of-war or partial information be planned before auth is hardened?

## 4. Strategic Economy

### Decision: What do the three resources mean?

Current state:

- Industry, research, and population are generated from influence.
- Priority weights are stored and editable.
- Priorities do not yet spend resources or change outcomes.

Questions to answer:

- Does industry build ships, infrastructure, logistics, or all of those?
- Does research accumulate toward doctrines/technology, or provide passive modifiers?
- Does population affect resource output, fleet support, recovery, colonisation, or something else?
- Are resources stockpiles, per-tick capacities, or both?
- Can resources go negative?
- Should each tick store resource deltas separately from totals?

### Decision: How should priority spending work?

Questions to answer:

- Should priority weights be percentages that must total 100, or relative weights where any positive total is valid?
- Should spending happen automatically every tick?
- Should players be able to reserve resources instead of spending all generated output?
- Should changing priorities take effect immediately or next tick?
- Should priority changes generate public events, private events, or both?

### Decision: How should ships be built?

Questions to answer:

- What is the industry cost per ship?
- Do ships appear in the home fleet, a reserve fleet, a rally point, or newly created fleets?
- Is there a build delay, or are ships available after the next tick?
- Should population or logistics cap fleet size?
- Should military investment create only ships, or also defensive/home-system pressure?

### Decision: What should research and expansion do first?

Questions to answer:

- What is the first research benefit: doctrine, travel, influence, combat, detection, or economy?
- Should expansion increase influence projection, discover systems, create outposts, or reduce travel friction?
- Should these effects be visible in system details and event logs immediately?

## 5. API And Dashboard Scope

### Decision: Should dashboard view models diverge from domain models now?

Current state:

- Several API responses still return domain entities directly.
- The dashboard already consumes purpose-built responses for some views.

Questions to answer:

- Should all public API endpoints return explicit response DTOs?
- Should the map receive a dedicated system-detail response instead of computing details client-side?
- Should event facts be exposed as raw JSON strings, parsed DTOs, or hidden from the dashboard?
- Should API response casing and enum format be locked before external clients exist?

### Decision: What dashboard workflow matters next?

Questions to answer:

- Should fleet details come before order cancellation?
- Should pending/processed/rejected orders be filterable?
- Should the dashboard include a tick button for local development, while production keeps ticks worker-owned?
- Should system details show all influence, only player-visible influence, or future fog-of-war values?
- Should the public site and dashboard share navigation, or remain deliberately separate?

## 6. Deployment And Operations

### Decision: Is deployment in scope for the next stage?

Questions to answer:

- Should this remain a local/private prototype for now?
- Is the public website intended to be deployed before the app is production-ready?
- Where should it deploy: Azure App Service, container host, static site plus API, or something else?
- Should SQL Server run locally only, in Docker, or in a hosted database?
- What environment variables and secrets are required?
- What backup/restore story is acceptable before inviting testers?

### Decision: What should be public?

Questions to answer:

- Should `/` be publicly reachable while `/app.html` stays private?
- Should the repo remain private?
- Should issue tracking/project docs be public, private, or selectively copied to a public site?
- Should collaborator/admin access be limited before external deployment?

## 7. Cycle Lifecycle And History

### Decision: How does a Cycle end?

Questions to answer:

- Is Cycle duration fixed by date, tick count, player goal, or admin action?
- What makes an empire win, lose, or survive?
- Can a Cycle end early?
- What happens to active orders and fleets at Cycle end?

### Decision: What history survives into the next Cycle?

Questions to answer:

- Which metrics define final rankings?
- Which battles/events become cross-Cycle history?
- Should systems retain names, scars, strategic value, or historical significance?
- How much continuity should players see at the start of a new Cycle?
- Should the next Cycle be generated deterministically from prior facts?

## 8. Narrative And AI

### Decision: Should narrative generation start with templates or AI?

Questions to answer:

- Should battle reports use deterministic templates first?
- Which facts must every generated report include?
- Should generated narrative require review before being shown?
- Should AI generation run inside or outside the tick transaction? Current direction says outside.
- Which provider boundary should be used if AI is added?
- How should provider failure be recorded without affecting simulation outcomes?

### Decision: What is the Chronicle's product role?

Questions to answer:

- Is the Chronicle primarily player-facing flavour, strategic intelligence, or long-term historical record?
- Should Chronicle entries be visible to all players immediately?
- Can entries be private, delayed, disputed, or discovered?
- Should Chronicle importance thresholds be configurable per Cycle?

## 9. Near-Term Implementation Gate

Before the next substantial coding pass, answer at least these five:

1. Is SQL Server the primary relational path for the next stage?
2. What migration/schema-versioning approach should be used?
3. What is the admin recovery clear/retry policy?
4. What auth model should replace prototype username login?
5. What should industry, research, population, and priority weights do on a tick?

If these are answered, the next implementation sequence can proceed without much product guessing:

1. Add schema versioning and migration tooling.
2. Replace snapshot SQL writes with targeted persistence operations.
3. Add per-Cycle tick locking and worker semantics.
4. Harden auth/player/empire boundaries.
5. Implement the first strategic economy loop.
