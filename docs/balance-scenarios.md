# Balance Scenarios

Last updated: 2026-07-11

The balance scenario is a deterministic engineering diagnostic for exercising the existing economy, colonisation, movement, combat, Chronicle, and metric rules over repeated ticks. It is not a player AI and does not define intended balance.

Run it through the CLI:

```powershell
dotnet run --project src/Cycles.Cli -- balance [tickCount] [systemCount] [empireCount] [seed] [balanced|military|expansion|cautious]
dotnet run --project src/Cycles.Cli -- balance compare [tickCount] [systemCount] [empireCount] [seed]
```

The default is 48 ticks, 24 systems, four empires, seed `71421`, and the balanced strategy. The runner keeps each home fleet in place, launches deterministic 30-ship expedition waves when a home fleet reaches 60 ships, and routes expeditions towards a shared central system. The same inputs produce the same report.

The four diagnostic strategies apply one policy to every empire in a run:

| Strategy | Priorities: Industry / Research / Military / Expansion | Colonises | Attacks local hostiles | Avoids hostile destinations |
| --- | --- | --- | --- | --- |
| Balanced | 30 / 25 / 30 / 15 | Yes | Yes | No |
| Military | 10 / 10 / 70 / 10 | No | Yes | No |
| Expansion | 10 / 10 / 10 / 70 | Yes | Yes | No |
| Cautious | 20 / 20 / 20 / 40 | Yes | No | Yes |

These policies create repeatable contrasts; they are not player AI or claims about optimal play. The homogeneous runs isolate policy effects before a future mixed-strategy scenario puts different policies in direct competition.

## Baseline Evidence

Seed `71421`, 24 systems, four empires:

| Requested ticks | Completed ticks | Orders | Battles | Colonies | Completed constructions | Retained records | Active-ship range | Research range | Population range |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 24 | 24 | 60 | 22 | 10 | 84 | 777 | 40-74 | 664-2,215 | 623-1,660 |
| 48 | 48 | 125 | 51 | 11 | 180 | 1,484 | 48-71 | 1,280-4,249 | 1,357-3,586 |
| 96 | 96 | 256 | 107 | 12 | 367 | 2,892 | 52-76 | 3,001-8,402 | 3,147-7,468 |
| 2,160 | 2,160 | 25,766 | 2,298 | 12 | 8,304 | 102,343 | 34-2,193 | 45,812-255,095 | 57,595-247,831 |

The full 2,160-tick run now completes without deleting or archiving historical records. On the local 2026-07-11 verification run, order planning took 3.16 seconds, Core tick processing took 5.37 seconds, and total CLI wall time including build and startup was 18.39 seconds. The fix replaced full-history entity cloning with a focused transactional working copy and replaced per-fleet planner rescans with per-tick indexes and a precomputed route map.

## Strategy Comparison Evidence

Seed `71421`, 96 ticks, 24 systems, four empires:

| Strategy | Orders | Battles | Colonies | Ships completed | Map-control gap | Active-ship range | Industry range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 256 | 107 | 12 | 1,102 | 3.68 | 52-76 | 122.02-314.12 |
| Military | 236 | 96 | 0 | 1,113 | 4.17 | 39-234 | 10.80-87.94 |
| Expansion | 228 | 100 | 12 | 982 | 8.33 | 37-95 | 456.21-1,576.59 |
| Cautious | 54 | 0 | 8 | 1,334 | 0.00 | 358-446 | 419.00-508.00 |

The military policy completed more ships than the expansion policy and left little industry unspent. The expansion policy produced the widest final map-control gap. The cautious policy avoided combat, retained far larger fleets, and completed the most ships despite its lower Military weight because it did not lose ships and continued accumulating industry. Movement and engagement policy therefore changes the outcome as much as the priority split.

## Current Interpretation

- Military construction and sustained combat broadly offset each other through the first 96 ticks, although one empire can still accumulate a large surviving fleet over longer runs.
- Research and population stockpiles grow continuously after the one research unlock and the available colonisation targets are exhausted. This is expected from the currently implemented sinks, but the quantities become very large long before the configured 90-day Cycle ends.
- Map control remains fairly compressed in this artificial convergence policy. The scenario does not justify changing the colonisation cost, outpost presence, ship cost, build delay, research threshold, or Chronicle threshold in isolation.
- Full-Cycle local simulation capacity is proven for the deliberately busy balanced scenario.
- Deliberate priority and engagement policies now produce distinct, repeatable outcomes. A future mixed-strategy scenario can test direct competition, but the current evidence does not justify changing one balance constant in isolation.

No balance constant was changed from this first baseline. The accepted product direction permits uncapped within-Cycle growth, and changing a single number would hide the missing long-term resource sinks rather than establish better balance.
