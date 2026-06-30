# Cycles Agent Notes

This file captures Cycles-specific guidance for coding agents. Anthony's global Codex instructions still apply; keep this file focused on repo details that are easy to forget.

## Start Here

- Read `README.md`, `docs/project-state.md`, `docs/backlog.md`, and `docs/decision-log.md` before substantial changes.
- Treat `docs/product-owner-questions.md` as the current source for product calls that have been answered or are still blocked.
- Keep changes small and tied to the backlog or the user's explicit request.

## Verification

- Prefer the repo test helper for normal test runs:

```powershell
.\eng\test.ps1
.\eng\test.ps1 -Filter InfluenceTests
```

- The helper writes build outputs to `%TEMP%\cycles-test-bin\`, avoiding locked DLLs when a local `Cycles.Api` process is serving from the normal `bin\Debug` directory.
- Do not repeatedly run full solution tests if a focused filter is enough for the slice.
- SQL Server integration tests are opt-in through `CYCLES_SQL_INTEGRATION_CONNECTION_STRING`; do not treat them as part of every local pass unless the change touches SQL persistence.
- If bare `pwsh` behaves oddly in Codex on this machine, use `C:\Program Files\PowerShell\7\pwsh.exe` rather than the WindowsApps alias.

## Architecture Guardrails

- `Cycles.Core` owns simulation/domain behaviour and should stay independent of database packages.
- `Cycles.Api` exposes state and accepts player intentions; do not casually expose tick execution through public API endpoints.
- Tick execution is currently CLI-owned and should eventually move to a worker/admin boundary, not to ordinary dashboard actions.
- SQL Server is the current primary relational path. Plain SQL migrations live under `database/migrations` and are applied through the small migration runner.
- JSON persistence remains useful for local/dev convenience, but the architecture direction is SQL-backed runtime with possible future import/export.
- Recovery handling is explicit and audited; failed ticks should not silently auto-retry.

## Dashboard And UI

- The playable dashboard is static HTML/CSS/JS under `src/Cycles.Api/wwwroot`.
- Keep dashboard polish narrow unless the user explicitly asks for a redesign or gameplay change.
- Avoid backend/gameplay drift when doing visual-only work.
- For screenshot verification on this machine, direct Playwright through the bundled Codex Node runtime is reliable after installing the expected browser revision. The gstack `browse.exe` path is still flaky: it needs `BROWSE_SERVER_SCRIPT` pointed at `C:\Users\antho\.codex\skills\gstack\browse\dist\server-node.mjs`, then may still miss its server startup window.
- If screenshots are needed and `browse.exe` flakes, use Playwright directly and save PNGs under `%TEMP%`; inspect the generated PNG before treating the screenshot as evidence.

## Docs And Backlog Hygiene

- Update `docs/project-state.md`, `docs/backlog.md`, and `docs/decision-log.md` when a feature slice materially changes behaviour or durable direction.
- Do not mark planned systems as implemented until code and relevant verification exist.
- Keep original design documents under `docs/source`; the Markdown docs are the working development layer.

## Future-System Discipline

- Admirals, diplomacy, deeper technology, cloaking, logistics, and AI narrative generation should wait until persistence and tick semantics are strong enough to support them.
- If a broad feature seems tempting, prefer the smallest tested extension point and document the remaining decision.
