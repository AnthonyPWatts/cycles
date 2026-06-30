# Ranking Metrics

Last updated: 2026-06-30

This document defines the first Cycle-end ranking metric. It is a product and implementation contract for Cycle-end processing; per-tick metric snapshots, the manual Cycle-end command, and final ranking persistence now use this shape.

## First Ranking Metric

The first final ranking is `MapControlPercent`.

`MapControlPercent` measures each empire's percentage control of the current Cycle map at the manual cutoff point. It is based on derived effective presence, not stored ownership.

For each system in the Cycle:

1. Calculate effective presence with `InfluenceCalculator.CalculateEffectivePresence`.
2. If no empire has effective presence, the system contributes no control points.
3. If one or more empires have effective presence, each empire receives its share of that system:

```text
empire system share = empire effective presence / total effective presence in system
```

The final score is:

```text
MapControlPercent = sum(empire system shares) / total systems in Cycle * 100
```

The first version treats every system as one map-control point. Strategic value, resources, battle wins, fleet strength, and Chronicle score are not part of the winner metric.

## Ordering

The winner is the empire with the highest `MapControlPercent`.

Rank all active empires by:

1. highest unrounded `MapControlPercent`;
2. highest total effective presence across all systems;
3. highest active ship count;
4. stable `EmpireId` ordering as the final deterministic tie-breaker.

Display percentages rounded to two decimal places, but store or compare the unrounded value where practical.

## Consequences

- Expansion priority affects final standings because it changes derived effective presence.
- Home-system minimum presence helps prevent an empire from being completely erased from the map-control calculation.
- Pending orders at cutoff are ignored unless they have already been processed into state by a completed tick.
- Resources, fleets, battles, and Chronicle entries can still be preserved as history categories, but they do not decide the first winner.

## Implementation Notes

The first Cycle-end implementation:

- calculates standings from the current state snapshot at manual cutoff;
- reuses the same `MapControlPercent`, total effective presence, active ship count, and deterministic tie-break ordering used by per-tick `EmpireMetrics`;
- persists one winner and ranked standings for every active empire in `CycleRankings`;
- records the tick number and cutoff time used for the calculation;
- preserves the top 10% of battles by total losses, with a minimum of one battle, in `CycleMajorEvents`;
- increases system historical significance for repeated battles and systems that hosted one of the largest-loss battles;
- tests final ranking persistence and the completed-Cycle guard.

Future history work should add selected systems, dedicated historical-system signals, and next-Cycle continuity.
