# Determinism Contract

Last updated: 2026-06-24

This document records what "deterministic" currently means in Cycles. The goal is replayable simulation facts, not deterministic timestamps, database identifiers, or generated prose.

## Seeded Galaxy Generation

The CLI seed command accepts an optional integer seed:

```powershell
dotnet run --project src/Cycles.Cli -- seed [statePath] [systemCount] [empireCount] [seed]
```

The default seed is `71421`.

For the same code, runtime, system count, empire count, and seed, `GameSeeder.CreateDefault` is expected to produce the same stable galaxy fields:

- system names and order;
- system coordinates;
- industry, research, population, strategic value, and historical significance;
- system-link topology, distances, and travel ticks;
- empire names and home-system assignment.

The seed is also recorded in the `CycleSeeded` event facts.

The seed does not stabilise generated identifiers or wall-clock fields. Cycle IDs, player IDs, empire IDs, system IDs, fleet IDs, event IDs, cycle names, and timestamps are created during seeding and are not part of the seed contract.

## Tick And Combat Resolution

Tick execution advances from the persisted Cycle state. The next tick is always `CurrentTickNumber + 1` for an active Cycle.

Combat uses deterministic pseudo-randomness derived from persisted authoritative inputs:

- Cycle ID;
- tick number;
- system ID;
- attacking fleet ID.

Given the same persisted state and tick number, combat should produce the same winner, losses, fleet status changes, battle facts, and Chronicle eligibility. A newly seeded galaxy with the same integer seed may still produce different combat outcomes because its generated IDs are different.

## Replay Boundaries

Current deterministic behaviour is a local implementation contract for the current code and target runtime. If exact long-term replay across runtime upgrades, database migrations, or published historical archives becomes a product requirement, replace `System.Random` and the current hash-based combat seed with an explicit versioned PRNG and persist the algorithm version on each Cycle.

Historical facts already written to events, battle records, and Chronicle entries should be treated as authoritative. Future changes to seeding or combat algorithms must not mutate historical outcomes.

## Verification

`tests/Cycles.Tests/DeterminismTests.cs` asserts the current contract:

- same seed produces the same stable galaxy layout fields;
- combat resolution is deterministic for the same persisted IDs and tick number.
