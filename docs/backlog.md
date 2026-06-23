# Backlog

Last updated: 2026-06-23

This backlog is grouped by intended development stage. It is not a promise that every item belongs in the next sprint.

## Now: Documentation And Repo Hygiene

- [x] Record current project state.
- [x] Record staged roadmap.
- [x] Record architecture direction.
- [x] Record known decisions.
- [ ] Add issue templates once GitHub issue tracking begins.
- [ ] Decide whether the original Word documents remain source artefacts long-term or move to `docs/source`.

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
- [ ] Split `Simulation.cs` into focused files once tests protect behaviour.
- [ ] Define explicit failed-tick recovery semantics.
- [ ] Add developer command or procedure for inspecting recovery-required Cycles.
- [ ] Document the deterministic random seed contract.

## Stage 2: Relational Persistence

- [ ] Decide database package approach for SQLite.
- [ ] Add `Cycles.Application` if use-case orchestration starts to outgrow Core.
- [ ] Add `Cycles.Infrastructure` for persistence implementation.
- [ ] Define persistence interfaces for loading state and committing tick outcomes.
- [ ] Create initial SQLite schema.
- [ ] Add schema initialisation/migration command.
- [ ] Add indexes for due orders, fleets by system, events by tick, and Chronicle lookups.
- [ ] Enforce unique tick completion per Cycle/tick.
- [ ] Implement relational tick lock.
- [ ] Move CLI seed/tick/show to relational persistence.
- [ ] Move API state/order endpoints to relational persistence.
- [ ] Add temporary SQLite integration tests.
- [ ] Decide whether JSON store remains as dev-only support or is removed.
- [ ] Optional: add JSON import/export for developer convenience.

## Stage 3: Strategic Economy

- [ ] Define resource semantics for industry, research, and population.
- [ ] Store per-tick resource deltas separately from totals.
- [ ] Implement priority-based spending allocation.
- [ ] Implement automatic ship building from military investment.
- [ ] Decide where new ships appear: home fleet, rally fleet, or new reserve fleet.
- [ ] Add spending events for material changes.
- [ ] Add simple research accumulation effect.
- [ ] Add simple expansion/influence projection effect.
- [ ] Add UI controls for priority changes.
- [ ] Add UI display for last tick gains and spending.
- [ ] Add balance tests for growth and non-negative resources.

## Stage 4: API And Dashboard Hardening

- [ ] Replace prototype login with deliberate development auth.
- [ ] Define future production auth option before deployment.
- [ ] Add player/empire authorisation checks.
- [ ] Stop trusting caller-supplied empire IDs where auth context can derive them.
- [ ] Add pending orders endpoint.
- [ ] Add processed/rejected orders endpoint.
- [ ] Consider order cancellation before execution.
- [ ] Add system detail API response with influence breakdown.
- [ ] Add fleet detail API response.
- [ ] Add last tick summary endpoint.
- [ ] Add dashboard pending order list.
- [ ] Add dashboard system detail panel.
- [ ] Add dashboard fleet detail panel.
- [ ] Add dashboard priority editing.
- [ ] Add API tests for order submission boundaries.

## Stage 5: History And Cycle Continuity

- [ ] Define ranking metrics.
- [ ] Store per-tick or per-Cycle empire metrics.
- [ ] Add Cycle-end processing command.
- [ ] Persist final Cycle rankings.
- [ ] Select major Cycle events.
- [ ] Add historical signal fields or tables for systems.
- [ ] Increase historical system significance from repeated conflict.
- [ ] Preserve largest battle per Cycle.
- [ ] Generate next Cycle from prior historical facts.
- [ ] Surface historical markers in API and dashboard.
- [ ] Add tests for Cycle end and historical system updates.

## Stage 6: Narrative Generation

- [ ] Define narrative generation source DTOs.
- [ ] Add deterministic template-based battle reports.
- [ ] Add required-fact validation for generated reports.
- [ ] Add generation status fields.
- [ ] Queue narrative work outside tick transaction.
- [ ] Store generation context snapshots.
- [ ] Decide AI provider boundary.
- [ ] Add provider failure handling.
- [ ] Add tests that generated text includes required facts.

## Stage 7: Admirals And Named Figures

- [ ] Add `Admiral` model.
- [ ] Add admiral assignment to fleet.
- [ ] Track admiral battle history.
- [ ] Add reputation score.
- [ ] Add active/retired/killed/missing/legendary status.
- [ ] Feed admiral events into Chronicle scoring.
- [ ] Add system associations for famous victories or defeats.
- [ ] Add tests for admiral reputation and Chronicle impact.

## Stage 8: Diplomacy

- [ ] Define diplomacy stance model.
- [ ] Add non-aggression/alliance/war states.
- [ ] Add diplomatic actions as durable orders/events.
- [ ] Add betrayal event type.
- [ ] Decide how alliances affect influence and combat.
- [ ] Add tests for diplomacy state changes.
- [ ] Add Chronicle criteria for betrayals and treaties.

## Stage 9: Technology, Doctrine, Cloaking, Detection, Logistics

- [ ] Define doctrine model as the first strategic modifier system.
- [ ] Add limited doctrine unlocks from research.
- [ ] Add influence/combat modifiers from doctrine.
- [ ] Add logistics modifier to travel or projection.
- [ ] Add cloaking only after detection exists.
- [ ] Add hidden/partial influence rules carefully.
- [ ] Add tests for each modifier against influence/combat.

## Technical Debt And Risks

- [ ] Current `GameState.DeepClone` serialises the whole state. Replace before state grows large.
- [ ] Current `FactJson` strings are flexible but weakly typed. Consider typed facts or validated schemas.
- [ ] Current combat model is not balanced.
- [ ] Current dashboard assumes a small galaxy.
- [ ] Current API returns some domain entities directly.
- [ ] Current prototype login is not security.
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
