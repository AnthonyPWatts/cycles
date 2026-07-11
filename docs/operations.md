# Operations

Last updated: 2026-07-11

This runbook covers local development tick operation, diagnostics, failed-tick recovery, and guarded SQL state profiling. It is not a production operations guide.

## Store Selection

CLI commands accept either a JSON path or a `sqlserver:` store specifier:

```powershell
dotnet run --project src/Cycles.Cli -- show data/cycles-state.json
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
```

From the repository root, the API and Worker resolve relative content paths differently. Use the documented forms:

```powershell
# Cycles.Api uses src/Cycles.Api as its content root.
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --Cycles:StatePath ../../data/cycles-state.json

# Cycles.Worker resolves this path from the repository root.
dotnet run --project src/Cycles.Worker -- --Cycles:StatePath data/cycles-state.json
```

Configure SQL Server for the API or Worker through `ConnectionStrings:Cycles` using the connection string without the `sqlserver:` prefix. The [SQL Server runbook](../database/sqldockerdeploykit/README.md) owns database setup and integration-test instructions.

## Development Cold Start

The normal local seed command creates the curated Day One scenario:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json
```

Use explicit generation arguments when a generic galaxy is required:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json 24 4 71421
```

The curated seed is fixed so tutorial objective identifiers and first-turn outcomes are reproducible. Development API and Worker hosts also use it when their configured store does not yet exist. Production hosts retain generic seeding.

## Scheduled And Manual Ticks

`Cycles.Worker` checks once on startup and then polls every 30 seconds by default. It runs at most one tick if the active Cycle is due; it does not process a backlog of catch-up ticks after downtime.

Configuration keys:

- `Cycles:Worker:Enabled`;
- `Cycles:Worker:PollIntervalSeconds`;
- `Cycles:StatePath` or `ConnectionStrings:Cycles`.

The Cycle's `TickLengthMinutes` controls simulation cadence. Recovery-required and non-active Cycles are not scheduled.

Every authenticated Development session can run the same authoritative store operation from **Advance turn**. This is a temporary play-testing capability, not role promotion: normal players keep ordinary visibility and empire authority. In Production, ordinary players cannot advance turns and the endpoint remains admin-only. The CLI remains available for deliberate local operation:

```powershell
dotnet run --project src/Cycles.Cli -- tick data/cycles-state.json
```

## Diagnostics

```powershell
dotnet run --project src/Cycles.Cli -- diagnostics data/cycles-state.json
```

The report includes store identity, active-Cycle cadence, next-due time, tick-log health, due orders, queued construction, and recovery guidance. Run it before manually changing operational state.

## Failed-Tick Recovery

### Semantics

- Tick work does not partially commit when processing fails.
- The failed attempt remains in `TickLogs` with diagnostic text.
- The Cycle becomes `RecoveryRequired` and rejects another tick.
- A running attempt also blocks duplicate execution.
- Retrying after repair uses the same tick number and preserves the failed attempt alongside a later completed attempt.

The in-memory path uses a focused transactional working copy and rolls back appended facts. SQL-backed ticks use a database transaction and per-Cycle application lock.

### Inspect

```powershell
dotnet run --project src/Cycles.Cli -- recovery data/cycles-state.json
dotnet run --project src/Cycles.Cli -- recovery details data/cycles-state.json
```

For SQL Server:

```powershell
dotnet run --project src/Cycles.Cli -- recovery "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
```

`recovery` is read-only. `recovery details` includes full diagnostics rather than only the first diagnostic line.

### Clear After Repair

```powershell
dotnet run --project src/Cycles.Cli -- recovery clear data/cycles-state.json <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

The command requires a `RecoveryRequired` Cycle, refuses to clear a still-running attempt, returns the Cycle to `Active`, and writes a high-severity `RecoveryCleared` event with operator, reason, and failed tick numbers.

### Retry After Repair

```powershell
dotnet run --project src/Cycles.Cli -- recovery retry data/cycles-state.json <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

This clears recovery and runs the repaired tick in one store update. Do not clear or retry a shared or production-like database without identifying the cause, repairing the data or code, and retaining a backup or equivalent rollback path.

Recovery administration remains CLI-only. It is not a player-facing API action.

## Guarded SQL State Profile

`db profile` compares generic whole-state operations with the focused SQL tick path. It replaces all Cycles state in the target database and requires an explicit guard:

```powershell
dotnet run --project src/Cycles.Cli -- db profile `
  "sqlserver:<connection-string>" `
  [systemCount] [empireCount] [historyTicks] [iterations] [seed] `
  --confirm-replace
```

Use only a disposable local database. Each iteration replaces state, optionally accumulates history through focused ticks, measures a whole-state load, measures a no-behaviour-change generic `Update`, and measures one focused tick.

### Dated Local Baseline

Measured against a disposable SQL Server 2022 container on 2026-07-11:

| Systems | Empires | History ticks | Retained records | Iterations | Replace average | Load average | Generic update average | Focused tick average |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 24 | 4 | 0 | 86-87 | 3 | 285.99 ms | 97.36 ms | 167.94 ms | 66.82 ms |
| 96 | 4 | 0 | 270-278 | 3 | 963.16 ms | 53.67 ms | 616.03 ms | 87.85 ms |
| 24 | 4 | 50 | 1,287 | 1 | 1,807.71 ms | 37.32 ms | 956.31 ms | 48.17 ms |

The history-bearing replacement also cleared a larger exploratory state, so its replacement time is not directly comparable with the clean-state rows. The focused tick stayed within 48-88 ms in this small sample, while generic update time grew with retained state.

These measurements justify keeping high-frequency ticks off the generic whole-state path. They do not establish a production threshold or justify a broad repository rewrite. Repeat the profile on representative infrastructure and history before drawing a production conclusion.
