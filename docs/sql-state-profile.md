# SQL State Path Profile

Last updated: 2026-07-11

The SQL state profiler compares the prototype's generic whole-state operations with the focused SQL tick path. It is an engineering diagnostic, not a stable performance benchmark or CI threshold.

The command deliberately replaces all Cycles state in the target database and therefore requires an explicit guard:

```powershell
dotnet run --project src/Cycles.Cli -- db profile `
  "sqlserver:<connection-string>" `
  [systemCount] [empireCount] [historyTicks] [iterations] [seed] `
  --confirm-replace
```

Use only a disposable local database. Each iteration:

1. replaces the database with a newly seeded state;
2. optionally runs focused SQL ticks to accumulate history;
3. measures a whole-state load;
4. measures a no-behaviour-change generic `Update`, which still exercises whole-state synchronisation;
5. measures one focused SQL tick.

## Local Baseline

Measured against a disposable SQL Server 2022 container on 2026-07-11. Times include local Docker and database variability and should be read directionally.

| Systems | Empires | History ticks | Retained records | Iterations | Replace average | Load average | Generic update average | Focused tick average |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 24 | 4 | 0 | 86-87 | 3 | 285.99 ms | 97.36 ms | 167.94 ms | 66.82 ms |
| 96 | 4 | 0 | 270-278 | 3 | 963.16 ms | 53.67 ms | 616.03 ms | 87.85 ms |
| 24 | 4 | 50 | 1,287 | 1 | 1,807.71 ms | 37.32 ms | 956.31 ms | 48.17 ms |

The history-bearing replacement also cleared the larger state left by an earlier exploratory run, so its replacement time is not directly comparable with the clean-state rows. The generic update and focused tick were measured against the reported 1,287-record state.

## Interpretation

- Moving from roughly 87 to 274 clean-state records increased generic replacement by about 3.4 times and generic update by about 3.7 times.
- At 1,287 retained records, generic update took about 956 ms, roughly 5.7 times the small clean-state baseline.
- Focused tick time stayed within 48-88 ms across these samples. This supports continuing to move high-frequency use cases away from generic whole-state synchronisation.
- Whole-state load did not show the same monotonic growth in this small local sample, likely because connection, SQL compilation, and cache noise dominate these sub-100-ms reads. More iterations are needed before drawing a load-specific conclusion.
- The evidence does not by itself justify a broad repository or `Cycles.Application` rewrite. It does justify treating generic `Update` as an admin/prototype bridge and profiling any new high-frequency caller before it adopts that path.

No production performance threshold is established yet. Repeat the profile on production-shaped infrastructure and representative retained history before setting one.
