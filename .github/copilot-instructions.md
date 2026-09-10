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
