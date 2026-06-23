# Tick Recovery

Last updated: 2026-06-23

This document records the current failed-tick recovery behaviour and the deliberately small operator surface that exists today.

## Current Semantics

- `TickEngine.RunTick` rejects a Cycle that is not `Active`.
- If tick processing throws after it starts working, the original state is not partially committed.
- The Cycle is marked `RecoveryRequired`.
- A failed `TickLog` is appended with diagnostic text.
- A Cycle in `RecoveryRequired` cannot process another tick until an operator resolves the underlying problem and deliberately returns it to `Active`.
- A `Running` tick log also blocks duplicate tick processing for the same Cycle.

## Inspect Recovery State

Use the CLI `recovery` command against either JSON or SQL Server state:

```powershell
dotnet run --project src/Cycles.Cli -- recovery data/cycles-state.json
```

```powershell
dotnet run --project src/Cycles.Cli -- recovery "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

The command is read-only. It reports Cycles in `RecoveryRequired` and any failed or still-running tick logs.

## Manual Recovery Position

There is intentionally no automatic clear/retry command yet. Clearing recovery is a product and operator decision, because the right action depends on why the tick failed.

Reasonable next implementation options:

- add a read-only `recovery --details` mode with full diagnostic output;
- add an admin-only command that marks a Cycle active after a named operator supplies a reason;
- add a retry command that only works when the failed tick left no partial state changes;
- add a data repair command for known validation failures;
- store recovery actions as auditable events.

Until that exists, recovery should be handled manually in a disposable/local environment only after inspecting the failed tick diagnostic. Do not clear `RecoveryRequired` in a shared or production-like database without a backup and an audit note.
