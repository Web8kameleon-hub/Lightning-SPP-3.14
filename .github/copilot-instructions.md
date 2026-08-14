# Copilot Workspace Instructions

## Purpose
This repository is a .NET 10-based product called Lightning SPP 3.14. The main goal for Copilot in this workspace is to help maintain, extend, and test the product while preserving its existing architecture and conventions.

## Key facts
- Main project: `wwwmmm.csproj`
- Solution root: `wwwmmm.slnx`
- Main CLI and app entrypoint: `Program.cs`
- Primary service layer: `Services/` and `Services/Implementation/`
- Public API facade: `PublicAPI/LightningAPI.cs`
- SDK wrappers: `SDK/`
- Tests: `Tests/` with the main test project at `Tests/LightningSPP.Tests/LightningSPP.Tests.csproj`
- Docker support: `Dockerfile`
- CI build/test pipeline is defined in `.github/workflows/ci-cd.yml`

## Build and test commands
Use the repository's native .NET workflow first.

```bash
dotnet restore
dotnet build --configuration Release --no-restore
dotnet test ./Tests/LightningSPP.Tests/LightningSPP.Tests.csproj --configuration Release --verbosity normal
dotnet run --no-build --configuration Release -- demo
dotnet run --no-build --configuration Release -- test
dotnet run --no-build --configuration Release -- speed
```

Docker validation is also part of CI:

```bash
docker build -t lightning-spp:latest .
docker run --rm lightning-spp:latest demo
docker run --rm lightning-spp:latest test
docker run --rm lightning-spp:latest speed
docker run --rm lightning-spp:latest real
```

## Repository conventions
- Branches targeting features, fixes, docs, performance, tests, or refactors use prefixes such as `feature/`, `fix/`, `docs/`, `perf/`, `test/`, `refactor/`
- Pull requests target `main`
- CI runs on pushes to `main` and `develop`
- Keep code changes minimal and aligned with the existing service/pipeline architecture unless the task explicitly requests a broad refactor

## What to preserve
- Preserve the core `Scan → Process → Print` pipeline semantics
- Do not rewrite proprietary architecture modules without explicit instruction
- Avoid large-scale renames of the `CLX`, `XCL`, `WWWMMM`, `NDB`, and `Stigma` concepts unless the issue/task calls for it
- Keep public API surface and CLI behavior stable for backward compatibility when possible

## Primary documentation to reference
- `README.md` – main overview and quick start
- `CONTRIBUTING.md` – branch rules, PR process, testing guidance
- `QUICKSTART.md` – local setup and diagnostic hints
- `Docs/ARCHITECTURE.md` – architecture and component boundaries
- `Docs/DEPLOYMENT.md` – deployment details
- `Docs/API_Documentation.md` – API usage examples
- `.github/workflows/ci-cd.yml` – CI build/test expectations

## Best practices for Copilot
- Prefer linking to docs instead of copying long sections
- When editing docs, update links and reference paths rather than duplicating content
- When adding behavior, add or update tests in `Tests/`
- Use the existing CLI runner and command structure from `Program.cs`
- If a task touches license or deployment behavior, verify whether it affects `Dockerfile` or CI workflow

## Example prompts
- "Update the `Program.cs` CLI parser to add a new `status` command and include a unit test."
- "Fix the failing `LightningSPP.Tests` test case for batch processing and explain the root cause."
- "Add a short note in `README.md` describing the Docker container validation steps used by CI."
- "Refactor `Services/LightningPipeline.cs` to reduce duplication while keeping the public pipeline behavior identical."

## When to ask for clarification
- If the user asks for a major architecture rewrite
- If the user asks to change the public API contract
- If the user asks to remove or rename core proprietary modules
- If the user asks for non-.NET tooling changes that may affect documentation only
