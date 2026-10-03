# Copilot Instructions for Lightning-SPP-3.14

## Big Picture
- This repo is a `.NET 10` Scan → Process → Print pipeline centered on `Services/LightningPipeline.cs`.
- Public entrypoints are static methods in `PublicAPI/LightningAPI.cs`; most feature work should be exposed there.
- CLI behavior is defined in `Program.cs` (`scan`, `process`, `print`, `pipeline`, `batch`, `serve`, `speed`, `real`, `test`).
- Runtime service wiring is centralized in `Configuration/LightningConfiguration.cs` (`ServiceFactory`).
- “Simulated” mode names remain for compatibility, but both `UseRealServices()` and `UseSimulatedServices()` map to real services.

## Service Boundaries and Data Flow
- Keep stage boundaries strict: `IScanService` (`ScanResult`) → `IProcessingService` (`ProcessingResult`) → `IPrintService` (`PrintResult`).
- `LightningPipeline.ExecuteAsync(...)` is the orchestrator: license check, profile application, metrics, then optional output artifact.
- Stigma profile selection is not cosmetic; `StigmaProfiles.Apply(...)` rewrites request knobs per profile.
- Profile source of truth is env var `WWWMMM_STIGMA_LEVEL` in `Services/StigmaExecutionProfile.cs`.
- Invalid `WWWMMM_STIGMA_LEVEL` values throw at startup/runtime config resolution; do not silently coerce.

## Web/API Conventions
- HTTP server is `HttpListener`-based in `Web/LicenseApiServer.cs` (not ASP.NET Core).
- API responses are largely newline-delimited text (`ok=true\n...`), not JSON, except `/api/stigma/schema` and static OpenAPI files.
- Keep existing security patterns when adding endpoints: CORS headers, per-IP rate limiting, admin bearer auth for sensitive routes.
- Stripe webhook verification is multi-secret (`legacy/snapshot/thin`) in `Web/StripeBilling.cs` with dedup via `TryMarkEventProcessed`.
- Static content must remain path-traversal safe via `ServeStaticAsync(...)` filename checks.

## Real Processing Patterns
- Scan implementation uses `SkiaSharp` in `Services/Implementation/RealImageProcessor.cs` and mode-specific transforms.
- Processing implementation uses Brotli + ML-context pipeline in `Services/Implementation/RealAIProcessor.cs`.
- Print implementation creates `.br` output artifacts in `Services/Implementation/RealPrintService.cs` (not physical printer drivers).
- Prefer extending current deterministic mode/engine switches over introducing parallel alternate pipelines.

## Build, Run, and Test Workflows
- Build main app: `dotnet build ./wwwmmm.csproj`
- Run CLI command args via separator: `dotnet run --project ./wwwmmm.csproj -- speed`
- Start API server: `dotnet run --project ./wwwmmm.csproj -- serve 8080`
- Run xUnit suite: `dotnet test ./Tests/LightningSPP.Tests/LightningSPP.Tests.csproj`
- Quick in-app smoke tests also exist: `dotnet run --project ./wwwmmm.csproj -- test`

## Project-Specific Conventions
- Main project intentionally excludes `Desktop/`, `VSExtension/`, and `Tests/LightningSPP.Tests/` from compile (`wwwmmm.csproj`).
- Preserve naming split documented in `README.md`: repo/root namespace `wwwmmm`, package ID `LightningSPP`.
- Keep async + cancellation token signatures on service/public API methods; existing code consistently exposes `CancellationToken`.
- `LightningMetrics` and `InternalAuditLogger` are operational observability points; update them when changing runtime behavior.
- Prefer concrete examples from existing tests such as `ApiSmokeTests` and `StigmaExecutionProfileTests` when adding coverage.
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
