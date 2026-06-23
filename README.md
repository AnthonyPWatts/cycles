# Cycles

Cycles is a tick-based strategy prototype about influence, history, and legacy across recurring galactic Cycles.

This implementation covers the technical MVP from the supplied design documents:

- deterministic galaxy seeding with systems, links, empires, fleets, resources, and priorities;
- server-authoritative tick processing;
- influence-based resource generation;
- durable movement, hold, and attack orders;
- simple structured combat with deterministic randomness;
- factual event logging;
- Chronicle candidate scoring and preserved battle entries;
- a local JSON state store with file locking for prototype use;
- a CLI tick runner;
- a minimal API and browser dashboard for viewing state and submitting orders.

## Projects

- `src/Cycles.Core`: domain model, seeding, order validation, simulation, combat, Chronicle scoring, and file-backed state.
- `src/Cycles.Cli`: manual seeding, ticking, inspection, and order submission.
- `src/Cycles.Api`: Minimal API plus a basic browser dashboard.
- `tests/Cycles.Tests`: xUnit tests for the core simulation behaviours.
- `docs`: current state, roadmap, architecture direction, backlog, and decision log.

## Planning Docs

Start here before adding the next substantial feature:

- [Project state](docs/project-state.md)
- [Development roadmap](docs/development-roadmap.md)
- [Architecture direction](docs/architecture-direction.md)
- [Backlog](docs/backlog.md)
- [Decision log](docs/decision-log.md)

## Run

Restore and build:

```powershell
dotnet restore Cycles.slnx --configfile NuGet.Config
dotnet build Cycles.slnx --no-restore
```

Run the tests:

```powershell
dotnet test Cycles.slnx --no-build
```

Seed and tick a local state file:

```powershell
dotnet run --project src/Cycles.Cli -- seed data/cycles-state.json
dotnet run --project src/Cycles.Cli -- tick data/cycles-state.json
dotnet run --project src/Cycles.Cli -- show data/cycles-state.json
```

Run the API and dashboard:

```powershell
dotnet run --project src/Cycles.Api -- --urls http://127.0.0.1:5086 --Cycles:StatePath data/cycles-state.json
```

Open `http://127.0.0.1:5086/`.

## Notes

The current state store is intentionally local and file-backed so the prototype runs without external services or package downloads. The core model keeps explicit IDs, ticks, event facts, battle records, and Chronicle entries so it can be moved behind SQLite, PostgreSQL, or SQL Server later without changing the simulation shape.

The API exposes state and accepts orders. Tick execution remains in the CLI, matching the design principle that public API calls should not decide simulation outcomes.
