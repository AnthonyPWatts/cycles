# Simulation Reference

Last updated: 2026-07-17

This reference records the simulation contracts that need more precision than the project-state summary: determinism, Cycle-end ranking, and the repeatable balance diagnostic. It describes current behaviour, not intended game balance.

## Determinism Contract

The goal is reproducible simulation facts. Timestamps, generated database identifiers, and future narrative prose are outside the contract.

### Seeded Galaxy Generation

```powershell
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" [systemCount] [empireCount] [seed] --confirm-replace
```

The default integer seed is `71421`. For the same code, runtime, system count, empire count, and seed, `GameSeeder.CreateDefault` must reproduce:

- system names and order;
- coordinates and resource/strategic/history fields;
- link topology, distances, and travel ticks;
- empire names and home-system assignments.

The `CycleSeeded` event records the seed. Generated Cycle, player, empire, system, fleet, and event identifiers, the Cycle name, and timestamps are not stabilised.

### Tick And Combat Resolution

An active Cycle advances from persisted state to `CurrentTickNumber + 1`.

The submission boundary permits at most one `Pending` order for a fleet at a particular `ExecuteAfterTick`; current player commands use the next tick. An identical resubmission returns the existing order. A different intention must identify the current order it replaces, after which the previous record becomes `Superseded` and links to the new order. This prevents one fleet from accumulating mutually exclusive commands while preserving the full decision history.

This rule does not change the relative processing semantics of orders belonging to different fleets. Simultaneous resolution, initiative, or another cross-fleet ordering model remains a separate design decision.

Combat pseudo-randomness derives from:

- Cycle ID;
- tick number;
- system ID;
- attacking fleet ID.

The same persisted state and tick number must produce the same winner, losses, fleet changes, battle facts, and Chronicle eligibility. A newly seeded galaxy with the same integer seed may produce different combat because its generated identifiers differ.

This is a current-code and target-runtime contract. Exact replay across runtime or algorithm upgrades would require a versioned PRNG and a persisted algorithm version. Historical events, battles, and Chronicle facts remain authoritative and must not be rewritten when algorithms change.

`tests/Cycles.Tests/DeterminismTests.cs` verifies stable seeded layout fields and combat results for stable persisted identifiers.

## Cycle-End Ranking

The first winner metric is `MapControlPercent`: each empire's percentage share of effective presence across the full map at manual cutoff.

For each system:

1. calculate effective presence with `InfluenceCalculator.CalculateEffectivePresence`;
2. assign no control points if total presence is zero;
3. otherwise give each empire its proportional share:

```text
empire system share = empire effective presence / total effective presence in system
```

Then calculate:

```text
MapControlPercent = sum(empire system shares) / total systems in Cycle * 100
```

Every system is worth one control point. Strategic value, resources, battle wins, fleet strength, and Chronicle score do not directly affect the winner metric.

Rank active empires by:

1. highest unrounded `MapControlPercent`;
2. highest total effective presence;
3. highest active ship count;
4. stable `EmpireId` ordering.

Display percentages to two decimal places but compare unrounded values. Pending orders at cutoff have no effect unless a completed tick already applied them.

`cycle end` persists one winner and all standings in `CycleRankings`, records the cutoff tick/time, preserves the top 10% of battles by losses with a minimum of one, and records historical system signals. `cycle next` consumes those facts to preserve player continuity and selected famous-system echoes.

## Balance Diagnostic

The balance runner exercises existing economy, colonisation, movement, combat, Chronicle, and metric rules over repeated ticks. It is not player AI and does not define intended balance.

```powershell
dotnet run --project src/Cycles.Cli -- balance [tickCount] [systemCount] [empireCount] [seed] [balanced|military|expansion|cautious]
dotnet run --project src/Cycles.Cli -- balance compare [tickCount] [systemCount] [empireCount] [seed]
```

Defaults: 48 ticks, 24 systems, four empires, seed `71421`, balanced policy. The runner keeps home fleets in place, launches deterministic 30-ship expeditions when a home fleet reaches 60 ships, and routes expeditions towards a shared central system.

| Strategy | Industry / Research / Military / Expansion | Colonises | Attacks | Avoids hostile destinations |
| --- | --- | --- | --- | --- |
| Balanced | 0 / 0 / 67 / 33 | Yes | Yes | No |
| Military | 0 / 0 / 88 / 12 | No | Yes | No |
| Expansion | 0 / 0 / 13 / 87 | Yes | Yes | No |
| Cautious | 0 / 0 / 33 / 67 | Yes | No | Yes |

These homogeneous policies isolate behaviour. A mixed-strategy scenario, where different policies compete within one Cycle, remains the next useful diagnostic.

### Retained-History Baseline

Seed `71421`, 24 systems, four empires, balanced policy:

| Requested ticks | Completed | Orders | Battles | Colonies | Constructions | Retained records |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 24 | 24 | 68 | 24 | 10 | 83 | 792 |
| 48 | 48 | 131 | 53 | 12 | 176 | 1,491 |
| 96 | 96 | 265 | 115 | 12 | 362 | 2,908 |
| 2,160 | 2,160 | 29,735 | 2,250 | 12 | 8,247 | 110,018 |

The full 2,160-tick scenario completed locally on 2026-07-14 without deleting or archiving history. Order planning took 3.59 seconds and Core tick processing took 5.93 seconds. These are dated engineering measurements, not performance thresholds.

### Strategy Comparison Baseline

Seed `71421`, 96 ticks, 24 systems, four empires:

| Strategy | Orders | Battles | Colonies | Ships completed | Map-control gap | Active-ship range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 265 | 115 | 12 | 1,114 | 4.17 | 55-64 |
| Military | 240 | 105 | 0 | 1,116 | 8.33 | 40-127 |
| Expansion | 240 | 106 | 12 | 1,025 | 4.17 | 52-65 |
| Cautious | 56 | 0 | 8 | 1,369 | 0.00 | 366-456 |
| Mixed | 227 | 86 | 8 | 1,128 | 4.17 | 60-279 |

The results show that movement and engagement policy changes outcomes at least as much as priority weights. Research and Population grow substantially after the available unlock and colonisation targets are exhausted. This evidence does not justify changing ship cost, build delay, colonisation cost, outpost presence, research threshold, or Chronicle threshold in isolation.

This baseline incorporates the inactive-priority lock and the resulting full Military/Expansion allocation. No ship, colonisation, influence, research, combat, or Chronicle constant changed. Further tuning should use mixed-policy competition or private-alpha evidence rather than hide missing long-term resource sinks with a single-number adjustment.
