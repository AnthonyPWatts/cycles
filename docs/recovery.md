# Tick Recovery

Last updated: 2026-06-24

This document records the current failed-tick recovery behaviour and the deliberately small operator surface that exists today.

## Current Semantics

- `TickEngine.RunTick` rejects a Cycle that is not `Active`.
- If tick processing throws after it starts working, the original state is not partially committed.
- The Cycle is marked `RecoveryRequired`.
- A failed `TickLog` is appended with diagnostic text.
- A Cycle in `RecoveryRequired` cannot process another tick until an operator resolves the underlying problem and deliberately returns it to `Active`.
- A `Running` tick log also blocks duplicate tick processing for the same Cycle.
- Failed tick logs remain as history. Retrying a repaired tick reuses the same tick number and writes a new completed log when it succeeds.

## Inspect Recovery State

Use the CLI `recovery` command against either JSON or SQL Server state:

```powershell
dotnet run --project src/Cycles.Cli -- recovery data/cycles-state.json
```

```powershell
dotnet run --project src/Cycles.Cli -- recovery "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True"
```

The summary command is read-only. It reports Cycles in `RecoveryRequired` and any failed or still-running tick logs.

Use `recovery details` to include full diagnostic text instead of only the first diagnostic line:

```powershell
dotnet run --project src/Cycles.Cli -- recovery details data/cycles-state.json
```

## Clear Recovery

After repairing the underlying data problem, an admin can clear recovery by supplying an operator name and reason:

```powershell
dotnet run --project src/Cycles.Cli -- recovery clear data/cycles-state.json <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

The command:

- requires a `RecoveryRequired` Cycle;
- refuses to clear while any tick log for that Cycle is still `Running`;
- marks the Cycle `Active`;
- writes a high-severity `RecoveryCleared` event with the operator, reason, and failed tick numbers in `FactJson`.

## Retry Recovery

When the repair is complete and the failed tick should be retried immediately, use:

```powershell
dotnet run --project src/Cycles.Cli -- recovery retry data/cycles-state.json <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

The retry command clears recovery and then runs the next tick in one state-store update. Because failed ticks do not advance `CurrentTickNumber`, this retries the same tick number. Failed tick logs are preserved; a successful retry writes a separate completed tick log for the same tick number.

## Operator Position

Recovery commands are intentionally CLI-only for now. They are admin operations, not player-facing API actions. Do not clear `RecoveryRequired` in a shared or production-like database without inspecting diagnostics, repairing the root cause, and keeping a backup or equivalent rollback path.
