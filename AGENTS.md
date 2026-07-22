# Cycles Agent Notes

This file captures Cycles-specific guidance for coding agents. Anthony's global Codex instructions still apply; keep this file focused on repo details that are easy to forget.

## Start Here

- Read `README.md`, `docs/project-state.md`, `docs/backlog.md`, and `docs/decision-log.md` before substantial changes.
- Treat `docs/product-owner-questions.md` as the current source for product calls that have been answered or are still blocked.
- Keep changes small and tied to an authoritative GitHub issue, the curated roadmap in `docs/backlog.md`, or the user's explicit request.

## Delivery And Ready State

- This repository favours frequent delivery. Commit each coherent, verified slice promptly and push it promptly; do not leave completed work accumulating only in the working tree or in local commits.
- Playground releases are the deliberate exception to commit frequency. Do not dispatch `Deploy playground` after every commit. Batch verified changes into one intentional release window, select `database_action=none` unless the revision genuinely needs `migrate`, `migrate-and-upgrade`, or `reseed`, and cluster deployed smoke/browser checks immediately after that release so they share one SQL wake window.
- Direct commits and pushes to `main` are the normal path when the user has authorised the work. A feature branch or pull request is not required unless the user asks for one, branch protection requires one, or concurrent work makes isolation materially safer. Do not introduce branch or pull-request ceremony as a default gate.
- When a branch or pull request is used, carry it through to completion without waiting for routine approval: land it, confirm the intended commits are on `origin/main`, return the local checkout to `main`, and remove temporary local branch state when safe. Do not finish with a needlessly divergent local `main`.
- A pushed commit is not normally the stopping point. Leave local `main` clean and synchronised with relevant migrations, seed data, tests, and local smoke checks applied. When a deliberate playground release window is in scope, verify that batched deployment and its live smoke checks; otherwise report the pushed revision as queued for the next release rather than spending a SQL wake window on repository-only or intermediate work.
- If deployment or local readiness is blocked, report the exact blocker and preserve the furthest safe ready state. Do not describe work as complete while either the expected local state or deployed state is knowingly stale.
- These are Cycles-specific defaults and should not drift back to a branch-first, pull-request-first, or push-at-the-end workflow without an explicit user decision.

## Verification

- Prefer the repo test helper for normal test runs:

```powershell
.\eng\test.ps1
.\eng\test.ps1 -Filter InfluenceTests
```

- The helper writes build outputs to `%TEMP%\cycles-test-bin\`, avoiding locked DLLs when a local `Cycles.Api` process is serving from the normal `bin\Debug` directory.
- Do not repeatedly run full solution tests if a focused filter is enough for the slice.
- SQL Server integration tests are opt-in through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; do not treat them as part of every local pass unless the change touches SQL persistence.
- On this machine the SQLDockerDeployKit-derived local container is normally `cycles-sql` on `localhost,14333`, database `CyclesDb`, user `sa`, password `YourStrong!Passw0rd`. With Microsoft.Data.SqlClient in this Codex environment, include `Encrypt=False` as well as `TrustServerCertificate=True`; without it, local SQL Server may fail with "requires encryption but this machine does not support it."
- If Docker is not running, Docker Desktop is installed at `C:\Program Files\Docker\Docker\Docker Desktop.exe`. Start it, wait for `docker info`, then `docker start cycles-sql`. Apply migrations before SQL-backed tests:

```powershell
dotnet run --no-restore --project src\Cycles.Cli -- db migrate "sqlserver:Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
$env:CYCLES_SQL_INTEGRATION_CONNECTION_STRING = "Server=localhost,14333;Database=CyclesDb;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=True;Encrypt=False;Connect Timeout=10"
.\eng\test.ps1
```

- Docker CLI checks from Codex Desktop may require sandbox escalation even when they are read-only.
- If bare `pwsh` behaves oddly in Codex on this machine, use `C:\Program Files\PowerShell\7\pwsh.exe` rather than the WindowsApps alias.

## Architecture Guardrails

- `Cycles.Core` owns simulation/domain behaviour and should stay independent of database packages.
- `Cycles.Api` exposes state and accepts player intentions; do not casually expose tick execution through public API endpoints.
- Scheduled tick execution is Worker-owned. The CLI remains an operator convenience. As a temporary development-only exception, any authenticated player can invoke the same authoritative store boundary without gaining admin visibility or cross-empire authority; ordinary production players must not execute ticks.
- SQL Server is the current primary relational path. Plain SQL migrations live under `database/migrations` and are applied through the small migration runner.
- Normal local development uses SQL Server. The API, Worker, and gameplay/operator CLI paths are SQL-only and no executable file-store fallback remains. JSON is limited to explicit versioned import/export, offline inspection, deterministic fixtures, legacy conversion, and migration evidence.
- Recovery handling is explicit and audited; failed ticks should not silently auto-retry.

## Dashboard And UI

- The playable dashboard is static HTML/CSS/JS under `src/Cycles.Api/wwwroot`.
- The curated Day One guide reads objective identifiers from the empire-scoped `OpeningBriefingIssued` fact. Keep it tied to real orders and authoritative tick outcomes rather than display-name matching or scripted results.
- Keep dashboard polish narrow unless the user explicitly asks for a redesign or gameplay change.
- Avoid backend/gameplay drift when doing visual-only work.
- Do not maintain dashboard screenshots as current-state evidence while the interface is changing quickly. Prefer source, contract, and smoke-test evidence unless the user explicitly asks for maintained tutorial captures.
- If a screenshot is explicitly required, inspect the generated image before treating it as evidence and avoid turning a documentation check into a browser-tooling exercise.

## Docs And Backlog Hygiene

- Update `docs/project-state.md`, `docs/backlog.md`, and `docs/decision-log.md` when a feature slice materially changes behaviour or durable direction.
- Do not mark planned systems as implemented until code and relevant verification exist.
- Keep original design documents under `docs/source`; the Markdown docs are the working development layer.
- GitHub issues own concrete actionable scope, acceptance criteria, ownership, live status, dependencies, and completion. `docs/backlog.md` owns priorities, sequencing, decision gates, conditional risks, and links; do not duplicate live ticket status there.
- Treat local or ignored QA reports, outcome summaries, checkpoints, and tool-generated handoffs as temporary working artefacts rather than durable project records. Before finishing, migrate each unresolved actionable item to an existing or new GitHub issue, preserve durable decisions or implemented state in the appropriate repository documentation, and remove superseded local documents. Do not create a parallel local backlog; retain screenshots, logs, baselines, active specifications, and reusable design inputs only while they still serve a practical purpose.

## Future-System Discipline

- First-pass admirals and the diplomacy storage/aggression foundation exist. Player-facing diplomacy, deeper doctrine/technology, cloaking, logistics, and AI narrative generation remain decision-gated.
- If a broad feature seems tempting, prefer the smallest tested extension point and document the remaining decision.
