# Backlog

Last updated: 2026-07-11

This backlog is grouped by intended development stage. It is not a promise that every item belongs in the next sprint.

## Now: Documentation And Repo Hygiene

- [x] Record current project state.
- [x] Record staged roadmap.
- [x] Record architecture direction.
- [x] Record known decisions.
- [x] Add issue templates once GitHub issue tracking begins.
- [x] Decide whether the original Word documents remain source artefacts long-term or move to `docs/source`.
- [x] Record the partial Q001-Q012 product-owner response received on 2026-07-11.

## Stage 1: Simulation Spine Hardening

- [x] Replace executable console test harness with a standard .NET test project.
- [x] Add tests for empty tick advancement.
- [x] Add tests for future orders not processing early.
- [x] Add tests for processed orders not reprocessing.
- [x] Add tests for rejected orders remaining rejected.
- [x] Add tests for multi-tick movement and arrivals.
- [x] Add tests for in-transit fleets not contributing influence.
- [x] Add tests for destroyed fleets not contributing influence.
- [x] Add tests for multiple fleets aggregating empire presence.
- [x] Add tests for combat determinism.
- [x] Add tests for multi-fleet defender loss distribution.
- [x] Add tests for battle-event fact consistency.
- [x] Add tests for Chronicle source references.
- [x] Split `Simulation.cs` into focused files once tests protect behaviour.
- [x] Define explicit failed-tick recovery semantics.
- [x] Add developer command or procedure for inspecting recovery-required Cycles.
- [x] Document the deterministic random seed contract.

## Stage 2: Relational Persistence

- [x] Add SQLDockerDeployKit-style SQL Server bootstrap image.
- [x] Create initial SQL Server schema.
- [x] Add indexes for due orders, fleets by system, events by tick, and Chronicle lookups.
- [x] Enforce unique tick completion per Cycle/tick.
- [x] Add SQL Server seed data for smoke verification.
- [x] Decide database package approach for initial SQL Server application persistence.
- [ ] Add `Cycles.Application` if use-case orchestration starts to outgrow Core.
- [x] Add `Cycles.Infrastructure.SqlServer` for SQL Server persistence implementation.
- [x] Define persistence interface for loading, replacing, and updating prototype state.
- [x] Map application persistence to the initial SQL Server schema.
- [x] Replace whole-state SQL delete/reinsert writes with targeted row-level sync.
- [x] Move SQL-backed tick command from generic full-state sync to dedicated tick-outcome writes.
- [x] Move SQL-backed tick execution from full database loading to cycle-scoped tick state loading.
- [x] Move tick execution from full-state loading to focused incremental repository operations.
- [x] Create schema versioning and migrations.
- [x] Add schema initialisation/migration command.
- [x] Implement prototype SQL Server application lock for state updates.
- [x] Move CLI seed/tick/show to relational persistence when SQL Server is configured.
- [x] Move API state/order endpoints to relational persistence when SQL Server is configured.
- [x] Add opt-in SQL Server integration test using the local container.
- [x] Add broader SQL Server integration tests for orders and duplicate tick prevention.
- [x] Add admin recovery clear/retry workflow once operator semantics are decided.
- [x] Decide whether temporary SQLite integration tests are still useful.
- [x] Decide whether JSON store remains as dev-only support or is removed.
- [ ] Demote JSON persistence to import/export-only once the SQL-backed flow is stable.
- [ ] Optional: add JSON import/export for developer convenience.

## Stage 3: Strategic Economy

- [x] Define resource semantics for industry, research, and population.
- [x] Store per-tick resource deltas separately from totals.
- [x] Implement priority-based spending allocation.
- [x] Implement automatic ship building from military investment.
- [x] Decide where new ships appear: home fleet, rally fleet, or new reserve fleet.
- [x] Add spending events for material changes.
- [x] Add simple research accumulation effect.
- [x] Add simple expansion/influence projection effect.
- [x] Add UI controls for priority changes.
- [x] Add UI display for last tick gains and spending.
- [x] Add balance tests for growth and non-negative resources.

## Stage 3A: Population And Colonisation

- [x] Select population/colonisation as the next headline gameplay slice.
- [x] Define the smallest colonisation model without introducing binary system ownership.
- [x] Add population-funded colonisation as a durable tick-processed intention.
- [x] Persist colonial outposts in JSON and SQL Server.
- [x] Feed colonial outposts into local influence without creating fleetless permanent control.
- [x] Add factual colonisation events and rejection reasons.
- [x] Expose colonisation through authenticated API response DTOs and commands.
- [x] Add functional dashboard controls and outpost visibility.
- [x] Add domain, API-boundary, persistence, and SQL integration tests.

## Stage 4: API And Dashboard Hardening

- [x] Replace prototype login with deliberate development auth.
- [ ] Define future production auth option before deployment.
- [x] Add player/empire authorisation checks.
- [x] Stop trusting caller-supplied empire IDs where auth context can derive them.
- [x] Add first active-fleet visibility filtering for system details, events, and Chronicle entries.
- [x] Add pending orders endpoint.
- [x] Add processed/rejected orders endpoint.
- [x] Consider order cancellation before execution.
- [x] Add system detail API response with influence breakdown.
- [x] Add fleet detail API response.
- [x] Add last tick summary endpoint.
- [x] Add dashboard pending order list.
- [x] Add dashboard system detail panel.
- [x] Add dashboard fleet detail panel.
- [x] Add dashboard priority editing.
- [x] Add API tests for order submission boundaries.
- [x] Split public website from playable dashboard.
- [x] Improve dashboard visual polish without changing order flows.

## Stage 5: History And Cycle Continuity

- [x] Define ranking metrics.
- [x] Store per-tick or per-Cycle empire metrics.
- [x] Add Cycle-end processing command.
- [x] Persist final Cycle rankings.
- [x] Select major Cycle events.
- [x] Add historical signal fields or tables for systems.
- [x] Increase historical system significance from repeated conflict.
- [x] Preserve largest battle per Cycle.
- [x] Generate next Cycle from prior historical facts.
- [x] Surface historical markers in API and dashboard.
- [x] Add tests for Cycle end and historical system updates.

## Stage 6: Narrative Generation

- [x] Define narrative generation source DTOs.
- [x] Add deterministic template-based battle reports.
- [x] Add required-fact validation for generated reports.
- [x] Add generation status fields.
- [ ] Queue narrative work outside tick transaction.
- [x] Store generation context snapshots.
- [ ] Decide AI provider boundary.
- [ ] Add provider failure handling.
- [x] Add tests that generated text includes required facts.

## Stage 7: Admirals And Named Figures

- [x] Add `Admiral` model.
- [x] Add admiral assignment to fleet.
- [x] Track admiral battle history.
- [x] Add reputation score.
- [x] Add active/retired/killed/missing/legendary status.
- [x] Feed admiral events into Chronicle scoring.
- [x] Add system associations for famous victories or defeats.
- [x] Add tests for admiral reputation and Chronicle impact.

## Stage 8: Diplomacy

- [x] Define the initial stored diplomacy stance model: Neutral, War, Non-Aggression Pact, and Alliance.
- [ ] Add non-aggression/alliance/war states.
- [ ] Add diplomatic actions as durable orders/events.
- [ ] Add treaty-cancellation and aggression event types.
- [ ] Decide how alliances affect influence and combat.
- [ ] Add tests for diplomacy state changes.
- [ ] Add Chronicle criteria for betrayals and treaties.

Blocked decisions: Q013-Q022 still need to settle action timing, mutual acceptance, unilateral declarations, alliance mechanics, visibility, Chronicle selection, and cross-Cycle diplomatic memory.

## Stage 9: Technology, Doctrine, Cloaking, Detection, Logistics

- [ ] Define doctrine model as the first strategic modifier system.
- [ ] Add limited doctrine unlocks from research.
- [ ] Add influence/combat modifiers from doctrine.
- [ ] Add logistics modifier to travel or projection.
- [ ] Add cloaking only after detection exists.
- [ ] Add hidden/partial influence rules carefully.
- [ ] Add tests for each modifier against influence/combat.

## Technical Debt And Risks

- [x] Replace JSON-backed `GameState.DeepClone` before state grows large.
- [ ] Current `FactJson` strings are flexible but weakly typed. Consider typed facts or validated schemas.
- [ ] Current combat model is not balanced.
- [ ] Current dashboard assumes a small galaxy.
- [ ] Current API returns some domain entities directly.
- [ ] Current development auth is not production security.
- [ ] Current docs are manual and can drift; update them as part of meaningful feature work.

## Parking Lot

These ideas are intentionally parked until the core loop is stronger:

- native mobile clients;
- large-scale multiplayer deployment;
- complex technology trees;
- full diplomacy UI;
- AI-generated character biographies;
- public leaderboards;
- monetisation;
- modding support;
- real-time combat;
- individual planet or building management.
