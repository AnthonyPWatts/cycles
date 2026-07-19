# Simulation Reference

Last updated: 2026-07-19

This reference records the simulation contracts that need more precision than the project-state summary: authoritative turn processing, determinism, Cycle-end ranking, and the repeatable balance diagnostic. It describes current behaviour, not intended game balance.

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

At closure, the engine snapshots the active fleets and labels submitted human intentions. For each game-AI empire, it applies this ordered policy:

1. attack the weakest locally visible faction that the available local fleets outnumber by at least 25%;
2. Hold if a locally visible attackable faction remains but the AI lacks that advantage;
3. establish the highest-value affordable outpost for which it has leading presence;
4. move along a least-travel-time route towards the highest-value reachable system that is not its home, an existing outpost, or already claimed by one of its fleets;
5. Hold when no earlier legal action exists.

System value combines strategic value, resource output, and historical significance. Equal objectives and equal routes use stable identifiers only as reproducible tie-breakers. The planner uses public system fields and topology plus its own fleets, resources, outposts, and locally visible opponents. It neither reads hidden human intentions nor changes its plan in response to remote enemy fleets. It will not initiate an attack through a non-aggression pact or Alliance. Neutral factions remain positional obstacles and generate deterministic Holds.

Before sealing, the engine evaluates each empire's complete set of otherwise-eligible Colonise intentions against its Population stockpile plus the exact Population income projected from the phase-start world. If that closure budget can fund the full set, every intention reserves its cost. If it can fund none or only some, every intention in the set is durably rejected before sealing and its fleet receives an implicit Hold. Cancellation or replacement before closure changes the set; neither submission time nor a stable identifier selects a survivor.

Every ledger row records its command source, sealed tick, and sealed timestamp. Generated game-AI identifiers derive from the Cycle, tick, fleet, complete intention, and target; generated Hold identifiers derive from the Cycle, tick, fleet, and source. Retrying the same persisted state therefore reproduces the planner intent and ledger identity. Every remaining missing command becomes an implicit Hold.

### Authoritative Processing Order

The phase sequence forms part of the gameplay rules. It determines whether income can be spent, which fleets meet in combat, whether a colony succeeds, and when a doctrine begins to affect influence. A code change must not reorder phases as incidental implementation work. Any proposed change needs a product decision, regression tests, and matching updates to player guidance and result presentation.

| Phase | Server processing | Gameplay consequence |
| --- | --- | --- |
| 1. Resource income | Calculate influence-derived Industry, Research, and Population from active presence in the sealed phase-start world. | Ships completed in phase 2 do not contribute income in this turn. The credited income is available in phase 3. |
| 2. Due construction | Complete ship construction whose delivery tick has arrived. Add ships to a home fleet that sealed Hold, or create a separate reinforcement fleet. | Due ships can defend because combat comes later. They cannot move, attack, or colonise because the server sealed commands before those ships existed. |
| 3. Programme spending | Apply the priorities committed before closure. Spend the current stockpile and start new construction. | Current income can fund current spending. A construction started here receives its first progress in a later tick. |
| 4. Recall, arrivals, movement, and Holds | Process sealed Recall intentions before passive arrivals, then complete remaining journeys and process new Move and Hold intentions. A Recall reverses an outbound fleet and uses the elapsed outward travel as its return duration. One-tick links place a new moving fleet at its destination in this phase; longer links leave it in transit. | A last-turn Recall can prevent an otherwise-due arrival. Movement then fixes the fleet positions used by combat. A fleet can leave a threatened system, and an arriving or one-tick moving fleet can defend at its destination. Its single sealed intention prevents it from moving and attacking in one turn. |
| 5. Combat | Resolve attacks from the shared post-movement state. Combine same-faction attacks against the same opposing faction in one system into one battle. | The server rejects an attack if movement leaves no eligible target. Incoming defenders can fight. Submission time cannot make an attack happen before movement. |
| 6. Colonisation | Revalidate each admitted Colonise intention against the surviving fleet and system presence. Consume its reserved Population only after success. | A fleet committed to colonisation must survive combat and remain eligible. Combat can prevent an outpost without consuming its cost, but the unused reservation cannot transfer to an intention rejected at closure. |
| 7. Derived state | Calculate post-action map-control metrics from the committed fleet, outpost, and influence state. | Rankings and turn summaries describe the world after movement, combat, and colonisation. |
| 8. Next-window progression | Apply research unlocks reached by the turn's credited Research. | Survey Projection unlocked here affects the next command window and later resolution, not income, combat, or metrics from the turn that unlocked it. |
| 9. Publication | Mark the tick complete, commit state and facts in one transaction, then reopen commands. | Players see one committed result. A failure enters recovery without exposing a partly resolved turn or accepting commands into it. |

The [Gameplay Guide](alpha-testers-guide.md#how-the-server-processes-a-turn) presents this order beside the Command controls, and the [Player API Contract](api-contract.md#turn-resolution-presentation) defines the typed phase and forecast metadata used by the dashboard. That presentation layer does not add a simulation phase or change the resolver. It calculates expected income, Colonise admission, Military spending, and next-window progression from the current authoritative snapshot; queued construction deliveries remain separate commitments created by earlier ticks. Event phase metadata maps factual Event types back to this table for display and leaves command-window or operational facts unphased.

Submission timestamps do not confer initiative. Fleets and orders use stable identifier ordering inside a phase to reproduce the same state transition, but those identifiers do not grant player priority. Colonise contention is settled for the complete otherwise-eligible empire set at command closure, before the ledger is sealed. Same-faction attacks use the grouped combat rule above. Combat involving more than two independently hostile factions remains a separate product decision; the current pairwise model does not invent alliance sides, interception, or pursuit.

Recall is distinct from cancelling a pending Move. The original Move remains processed history after dispatch. An outbound in-transit fleet may instead queue one Recall to its last occupied system; cancelling that still-pending Recall leaves the outward journey unchanged. Recall does not provide arbitrary diversion, a mid-route holding position, interception, pursuit, or multi-hop routing.

These examples follow from the processing order:

- A defender that submits Move can leave before a hostile Attack checks the system. If no eligible target remains, the attack is rejected.
- A fleet that reaches a system during the movement phase can be drawn into defence there, even though its own intention was Move rather than Attack.
- A home reinforcement delivered in phase 2 can defend in phase 5. It neither generated phase-1 income nor received the home fleet's sealed Move or Attack.
- An empire can cross the research threshold with phase-1 income. Survey Projection still waits for phase 8 and changes play from the next command window.

Combat pseudo-randomness derives from:

- Cycle ID;
- tick number;
- system ID;
- attacking fleet ID.

The same persisted state and tick number must produce the same winner, losses, fleet changes, battle facts, and Chronicle eligibility. A newly seeded galaxy with the same integer seed may produce different combat because its generated identifiers differ.

This is a current-code and target-runtime contract. Exact replay across runtime or algorithm upgrades would require a versioned PRNG and a persisted algorithm version. Historical events, battles, and Chronicle facts remain authoritative and must not be rewritten when algorithms change.

`tests/Cycles.Tests/DeterminismTests.cs` verifies stable seeded layout fields and combat results for stable persisted identifiers. `tests/Cycles.Tests/GameAiPlannerTests.cs` verifies the policy order, tie-breaks, visibility restrictions, neutral behaviour, stable planner identities, and an unattended eight-tick map change. `tests/Cycles.Tests/TurnResolutionTests.cs` verifies deterministic ledger identities, command sources, movement-before-combat semantics, grouped same-faction attacks, reinforcement isolation, and next-window progression.

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

`cycle end` persists one winner and all standings in `CycleRankings`, records the cutoff tick/time, preserves the top 10% of battles by losses with a minimum of one, and records historical system signals. `cycle next` consumes those facts to carry participating players into new empires and preserve selected famous-system names, significance, and strategic character. It does not carry participant fleets, resources, ownership, rank benefits, or other player/empire power. Richer accepted galaxy echoes remain future work.

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
| 24 | 24 | 197 | 18 | 10 | 82 | 924 |
| 48 | 48 | 425 | 44 | 10 | 175 | 1,784 |
| 96 | 96 | 839 | 96 | 10 | 361 | 3,478 |
| 2,160 | 2,160 | 17,144 | 2,434 | 12 | 8,336 | 75,058 |

The full 2,160-tick scenario completed locally on 2026-07-18 without deleting or archiving history. Order planning took 5.57 seconds and Core tick processing took 10.97 seconds. The order total now includes the complete sealed ledger, including implicit Holds; same-faction attacks in one system form one battle. These are dated engineering measurements, not performance thresholds.

### Strategy Comparison Baseline

Seed `71421`, 96 ticks, 24 systems, four empires:

| Strategy | Orders | Battles | Colonies | Ships completed | Map-control gap | Active-ship range |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Balanced | 839 | 96 | 10 | 1,112 | 4.17 | 50-76 |
| Military | 734 | 99 | 0 | 1,087 | 3.33 | 46-93 |
| Expansion | 712 | 97 | 11 | 1,012 | 5.79 | 54-78 |
| Cautious | 2,651 | 0 | 8 | 1,367 | 0.00 | 366-454 |
| Mixed | 1,022 | 72 | 8 | 1,093 | 7.38 | 48-98 |

The results show that movement and engagement policy changes outcomes at least as much as priority weights. Research and Population grow substantially after the available unlock and colonisation targets are exhausted. This evidence does not justify changing ship cost, build delay, colonisation cost, outpost presence, research threshold, or Chronicle threshold in isolation.

This baseline incorporates the inactive-priority lock and the resulting full Military/Expansion allocation. No ship, colonisation, influence, research, combat, or Chronicle constant changed. Further tuning should use mixed-policy competition or private-alpha evidence rather than hide missing long-term resource sinks with a single-number adjustment.
