# Operations

Last updated: 2026-07-20

This runbook covers local SQL development, external identity configuration, admin recovery, versioned state transfer, tick diagnostics, failed-tick recovery, and guarded profiling. It does not by itself establish production readiness.

## Authoritative Store

API, Worker, and gameplay/operator CLI paths use SQL Server exclusively. CLI commands accept the documented `sqlserver:` store specifier; a raw SQL connection string is also accepted, while file paths are rejected:

```powershell
dotnet run --project src/Cycles.Cli -- show "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
```

Configure SQL Server for the API or Worker through `ConnectionStrings:Cycles` using the connection string without the `sqlserver:` prefix. `Cycles:SqlConnectionString` and `CYCLES_SQL_CONNECTION_STRING` are equivalent configuration paths. Both hosts fail startup clearly if SQL configuration is missing:

```powershell
$connectionString = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=<local-password>;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --ConnectionStrings:Cycles "$connectionString"
dotnet run --project src/Cycles.Worker -- --ConnectionStrings:Cycles "$connectionString"
```

The API, Worker and CLI also validate the immutable code-owned Game profile catalogue at startup. A profile key/version whose calculated content differs from its declared SHA-256 hash fails startup deliberately; change the profile version and persisted provenance rather than replacing historical content in place. Database-authored map or scenario profiles are not supported.

The [SQL Server runbook](../database/sqldockerdeploykit/README.md) owns database setup and integration-test instructions. API, Worker, and gameplay/operator CLI commands use SQL Server exclusively. JSON remains only in explicit versioned transfer, validation, legacy conversion, offline-inspection, fixture, and migration-evidence paths; there is no file-store fallback.

## Development Cold Start

The normal local seed command creates the curated Day One scenario in the local SQL database. Because seeding replaces authoritative state, SQL targets require explicit confirmation:

```powershell
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" --confirm-replace
```

Use explicit generation arguments when a generic galaxy is required:

```powershell
dotnet run --project src/Cycles.Cli -- seed "sqlserver:$connectionString" 24 4 71421 --confirm-replace
```

The curated `development-match-v2` seed is fixed so participant assignments, distinct-sector homes, neutral fleets, tutorial objective identifiers, and first-turn outcomes are reproducible. It creates Tony and Will as selectable human players, Ariadne as a game-AI player, three empires with three fleets and 60 ships apiece, and six weaker neutral Free Captain fleets. Development API and Worker hosts leave a configured empty store empty; they do not provision the curated scenario implicitly. Provision local state deliberately with the CLI seed command above or the ordered Docker migration-and-seed bootstrap; provision shared environments through their guarded operator workflow.

`Cycles:TrustedPlayerSelection:Enabled` controls the fixed Development selector. It is enabled by `appsettings.Development.json`, disabled by default elsewhere, and must be enabled deliberately for an access-restricted hosted playground. A non-Development process with the selector enabled refuses to start unless `CYCLES_PLAYGROUND_ACCESS_CODE` or `Cycles:PlaygroundAccessCode` is present. It selects only existing active human accounts that participate in the match, stores the selected identity in a protected cookie, and is not an account-registration or production-identity mechanism. Defeated and completed participants can sign in to inspect the match but cannot mutate it.

## Scheduled And Manual Ticks

SQL stores Cycle scheduling. `CycleConfigurations.SchedulingMode` records the immutable configured capability, while each materialised `Cycle` stores the matching `SchedulingMode` and its nullable `NextTickAt` deadline. SQL constraints allow these states:

| Cycle state | `SchedulingMode` | `NextTickAt` |
| --- | --- | --- |
| Active scheduled Cycle | `Scheduled` | Required |
| Active self-paced Cycle | `SelfPaced` | `NULL` |
| Completed or recovery-required Cycle | Either configured value | `NULL` |

Migration `025_add_cycle_scheduling` backfills existing configurations and Cycles as `Scheduled`. It derives an active Cycle's first persisted deadline from the latest completed tick plus `TickLengthMinutes`, or from `StartAt` when no tick has completed.

`Cycles.Worker` checks once on startup and then polls every 30 seconds by default. Each poll selects the earliest due active `Scheduled` Cycle from an active Standard Game, ordered by `NextTickAt` and Cycle ID. It processes one work item per poll, so downtime does not trigger an unbounded catch-up batch. Another poll handles the next due item.

Configuration keys:

- `Cycles:Worker:Enabled`;
- `Cycles:Worker:PollIntervalSeconds`;
- `ConnectionStrings:Cycles`.

Before resolution, the store rechecks the Game, Cycle, materialised configuration, scheduling mode, and captured deadline under the Game and Cycle locks. Stale, early, unavailable, and busy work does not run. A completed tick sets `NextTickAt` to its completion time plus `TickLengthMinutes`. A failed tick moves the Cycle to `RecoveryRequired` and clears the deadline.

`SelfPaced` Cycles never enter due discovery. An authorised caller must use `ICycleResolutionStore.ResolveExplicit` with a complete Game command context and the applicable administrator policy. `POST /games/{gameId}/admin/tick` uses that boundary for the selected Game; the temporary `POST /admin/tick` route is only its fixed legacy-Game adapter. The store acquires the Game and Cycle locks, then locks and revalidates the live Game, Cycle, player, enrolment, participant, empire and administrator authority in that order. Those authority rows remain locked until resolution commits or rolls back, so a concurrent revocation cannot race an authorised tick. No Training provisioning route exists yet.

Every authenticated Development session can run the same authoritative store operation from **Close command window and advance**. This is a temporary play-testing capability, not role promotion: normal players keep ordinary visibility and empire authority. In Production, ordinary players cannot advance turns and the endpoint remains admin-only. The CLI remains available for deliberate local operation:

```powershell
dotnet run --project src/Cycles.Cli -- tick "sqlserver:$connectionString"
```

## Diagnostics

```powershell
dotnet run --project src/Cycles.Cli -- diagnostics "sqlserver:$connectionString"
```

The report includes store identity, active-Cycle cadence, next-due time, tick-log health, completed/failed attempt durations, due orders, queued construction, and recovery guidance. Persisted `Running` attempts become suspicious after five minutes by default. Override the diagnostic threshold with a positive `CYCLES_RUNNING_TICK_SUSPICION_MINUTES` value.

Suspicion is read-only: age never fails, retries, repairs, cancels, or clears an attempt. Inspect the attempt and its surrounding infrastructure before using the explicit abandonment operation.

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
dotnet run --project src/Cycles.Cli -- recovery "sqlserver:$connectionString"
dotnet run --project src/Cycles.Cli -- recovery details "sqlserver:$connectionString"
```

`recovery` is read-only. `recovery details` includes full diagnostics rather than only the first diagnostic line.

### Mark A Confirmed Abandoned Attempt Failed

```powershell
dotnet run --project src/Cycles.Cli -- recovery abandon "sqlserver:$connectionString" <tickAttemptId> --operator "admin" --reason "Confirmed the terminated Worker no longer owns this attempt"
```

The command refuses missing, finished, ambiguous, or younger-than-threshold attempts. A successful operation atomically marks the selected attempt failed, sets its completion time, appends operator/reason context, leaves the Cycle `RecoveryRequired`, and writes a high-severity `TickAbandoned` event. It does not repair data, clear recovery, or run another tick.

### Clear After Repair

```powershell
dotnet run --project src/Cycles.Cli -- recovery clear "sqlserver:$connectionString" <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

The command requires a `RecoveryRequired` Cycle, refuses to clear a still-running attempt, returns the Cycle to `Active`, and writes a high-severity `RecoveryCleared` event with operator, reason, and failed tick numbers.

### Retry After Repair

```powershell
dotnet run --project src/Cycles.Cli -- recovery retry "sqlserver:$connectionString" <cycleId> --operator "admin" --reason "Restored missing empire resources"
```

This clears recovery and runs the repaired tick in one store update. Do not clear or retry a shared or production-like database without identifying the cause, repairing the data or code, and retaining a backup or equivalent rollback path.

Recovery administration remains CLI-only. It is not a player-facing API action.

## Versioned State Transfer

Complete state transfer is an operator/developer support path for migration, recovery preparation, debugging, and reproducible fixtures. It is not an ordinary-player endpoint, a player save file, or a substitute for database-native backup and restore.

### Export And Validate

```powershell
# One-time bridge for a retired raw runtime file; the input is not modified.
dotnet run --project src/Cycles.Cli -- state convert-runtime-file C:\secure\legacy-cycles-state.json C:\secure\cycles-state-v7.json

dotnet run --project src/Cycles.Cli -- state export "sqlserver:$connectionString" C:\secure\cycles-state-v7.json
dotnet run --project src/Cycles.Cli -- state validate C:\secure\cycles-state-v7.json
```

`convert-runtime-file` exists only to move the retired unversioned file-store shape into the strict transfer format. It requires every persisted collection, normalises inactive priorities, validates the state, writes atomically, and does not modify the source. SQL export requires a migrated source and applies the same state validation. Both commands refuse an existing destination unless `--confirm-overwrite` is supplied and never print the connection string or payload.

### Import

```powershell
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v7.json "sqlserver:$connectionString" --confirm-import

# A non-empty target additionally requires deliberate replacement confirmation.
dotnet run --project src/Cycles.Cli -- state import C:\secure\cycles-state-v7.json "sqlserver:$connectionString" --confirm-import --confirm-replace
```

Validation happens before the target is opened. It checks the v7 format, every persisted collection, identifiers, references, Game and Cycle lineage, scheduling-mode provenance, `NextTickAt` coherence, empire ownership, tick/recovery invariants, retained history, and embedded JSON. The v7 document can represent several Games, but the current operational importer accepts only the fixed legacy Game identity. After replacement, the CLI reloads and revalidates the SQL state and record count.

### Sensitive-Data Handling

A complete export contains stable external identity identifiers, admin audit records, every empire's private state, hidden facts, operational diagnostics, and retained history.

- write it only to an access-restricted destination;
- transfer it over an approved encrypted channel;
- do not paste payloads into logs, issues, chat, or ordinary test evidence;
- record the source build/database and intended use without recording credentials;
- retain it only for the migration/debugging window, then delete it according to the host's secure-retention policy;
- use SQL/Azure backup and point-in-time restore for managed recovery after cutover.

## External Identity And Admin Operation

Development retains the local username login. Every non-Development API host requires external OIDC configuration and fails startup if provider credentials are absent. Use a separate confidential provider registration per environment with the authorisation-code flow, PKCE, and these callbacks:

- sign-in: `/signin-oidc`;
- sign-out: `/signout-callback-oidc`;
- post-authentication dashboard: `/app.html`.

Configure secrets through the deployment platform or environment, never source control. Hierarchical environment-variable examples are:

```text
Cycles__Authentication__Authority
Cycles__Authentication__ClientId
Cycles__Authentication__ClientSecret
Cycles__Authentication__InvitedIdentities__0=https://issuer.example|stable-subject
Cycles__Authentication__AdminBootstrapIdentities__0=https://issuer.example|stable-admin-subject
Cycles__Authentication__DeploymentRevision=deployment-identifier
Cycles__Authentication__KnownProxies__0=10.0.0.10
```

Only exact case-sensitive issuer-and-subject pairs admit or bootstrap a player. Leading or trailing whitespace is unsupported; operator configuration may contain surrounding layout whitespace, but the parsed issuer and subject must both remain non-empty. Migration `024_enforce_external_identity_binary_collation` fails before schema changes if an existing issuer or subject starts or ends with U+0020. Investigate and correct those rows from authoritative provider evidence rather than trimming an ambiguous identity blindly. Email, display name, invitation state, provider groups, and provider role claims do not grant Cycles authority. Known proxy addresses are explicit so forwarded host/protocol values are not trusted from arbitrary clients.

An admitted identity creates or updates only its active Human Player account. Authentication does not create a Game enrolment, participant, empire, or admiral. `/auth/session` is account-level. Canonical gameplay requests use `/games/{gameId}` and resolve the Game's current Cycle, participant, and empire before access. The unscoped bootstrap, focused reads, order routes, priorities route, and `/admin/tick` remain pinned adapters for the fixed legacy Game. They call the same scoped handlers and cannot select another Game.

The browser obtains `gameId` from trusted login or the first pinned bootstrap, then sends selected-Game requests. This client foundation has no Games home or player selection between Games. Training provisioning and a second Game also remain absent. Until the Games home is deployed, do not add a fresh unmapped identity to `InvitedIdentities`; keep invitation rollout to identities that already map to an enrolled legacy Player. A transient account-lock conflict returns a safe retryable `stateConflict` response rather than being flattened into a bad-identity failure.

All application mutations use ASP.NET Core antiforgery validation. The browser first calls `GET /auth/antiforgery`, retains the returned `requestToken` in memory, and sends it as `X-Cycles-Antiforgery` with JSON `POST`, `PUT`, `PATCH`, and `DELETE` requests. The matching cookie is HttpOnly. Missing or mismatched tokens return `400 antiforgeryFailed` before the handler runs. Trusted login rotates the token. Sign-out uses a tokenised `POST /auth/logout`; no GET logout operation exists. The OIDC branch follows the provider sign-out redirect after the protected POST.

The first configured admin bootstrap writes a high-severity audit record with target, reason, source revision, and timestamp. After verifying that initial access and audit record, remove the bootstrap identity from configuration so later routine revocation is not undone by a subsequent sign-in. Routine changes require an authenticated local admin and non-empty reason:

```http
POST /admin/players/{playerId}/roles/admin
Content-Type: application/json
X-Cycles-Antiforgery: <request-token>

{ "reason": "Primary private-alpha operator" }
```

Revocation uses `DELETE` on the same route with a reason body. Successful grants and revocations append actor/target audit records; routine revocation cannot remove the final active Human admin. While operator/CLI whole-state paths remain executable, account and admin mutations deliberately take their global lock before narrower identity/admin locks; a busy response is retryable and performs no partial mutation.

### Break-Glass Admin Recovery

Emergency recovery is infrastructure operation, not a permanent hidden superuser:

1. restrict or quiesce the affected environment and preserve database recovery options;
2. identify the existing player by the provider's exact issuer and subject, not email or display name;
3. add that identity temporarily to the secure `AdminBootstrapIdentities` configuration with a new `DeploymentRevision`;
4. restart and complete one provider sign-in, which reapplies the local role and appends the bootstrap audit record;
5. verify local admin access and the audit record, then remove the temporary bootstrap entry and redeploy;
6. investigate the lockout or compromise and retain the operator/deployment evidence outside application secrets.

Do not edit provider role claims, enable Development login, or create a shared emergency player account. If database integrity or identity correlation is uncertain, restore/investigate rather than improvising a silent role update.

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
