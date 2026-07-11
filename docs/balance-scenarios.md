# Balance Scenarios

Last updated: 2026-07-11

The balance scenario is a deterministic engineering diagnostic for exercising the existing economy, colonisation, movement, combat, Chronicle, and metric rules over repeated ticks. It is not a player AI and does not define intended balance.

Run it through the CLI:

```powershell
dotnet run --project src/Cycles.Cli -- balance [tickCount] [systemCount] [empireCount] [seed]
```

The default is 48 ticks, 24 systems, four empires, and seed `71421`. The runner keeps each home fleet in place, launches deterministic 30-ship expedition waves when a home fleet reaches 60 ships, colonises eligible systems, and routes expeditions towards a shared central system. Co-located hostile expeditions attack. The same inputs produce the same report.

The policy deliberately creates sustained conflict so that construction, attrition, Chronicle selection, and state growth are observable. It does not model diplomacy, cautious play, player-specific priorities, target selection, or an optimal strategy.

## Baseline Evidence

Seed `71421`, 24 systems, four empires:

| Requested ticks | Completed ticks | Orders | Battles | Colonies | Completed constructions | Retained records | Active-ship range | Research range | Population range |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 24 | 24 | 60 | 22 | 10 | 84 | 777 | 40-74 | 664-2,215 | 623-1,660 |
| 48 | 48 | 125 | 51 | 11 | 180 | 1,484 | 48-71 | 1,280-4,249 | 1,357-3,586 |
| 96 | 96 | 256 | 107 | 12 | 367 | 2,892 | 52-76 | 3,001-8,402 | 3,147-7,468 |
| 2,160 | 474 | 1,867 | 501 | 12 | 1,827 | 15,001 | 37-972 | 11,309-50,550 | 13,533-50,340 |

The 2,160-tick request stopped before tick 475 at the diagnostic record budget. Before that safeguard was added, two full-Cycle attempts terminated the process before it could return a report. This is evidence of an engineering scalability limit in the current whole-state in-memory tick path, not a simulated balance result for a complete Cycle.

## Current Interpretation

- Military construction and sustained combat broadly offset each other through the first 96 ticks, although one empire can still accumulate a large surviving fleet over longer runs.
- Research and population stockpiles grow continuously after the one research unlock and the available colonisation targets are exhausted. This is expected from the currently implemented sinks, but the quantities become very large long before the configured 90-day Cycle ends.
- Map control remains fairly compressed in this artificial convergence policy. The scenario does not justify changing the colonisation cost, outpost presence, ship cost, build delay, research threshold, or Chronicle threshold in isolation.
- The next evidence-led balance step should compare deliberate priority strategies and less aggressive movement policies. The next engineering step should address retained-history growth before claiming full-Cycle simulation capacity.

No balance constant was changed from this first baseline. The accepted product direction permits uncapped within-Cycle growth, and changing a single number would hide the missing long-term resource sinks rather than establish better balance.
