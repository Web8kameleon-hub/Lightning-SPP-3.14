# Lightning SPP 3.14 Release Evidence

## Scope

This document is the release companion for code, UI, billing, licensing, benchmark, edge-case and negative-test evidence. It does not replace external certification or a customer contract.

## Product facts

- Real processing uses SkiaSharp, ML.NET/ONNX integration, Brotli and physical file output.
- The REST service exposes health, metrics, alerts, schema, license status, activation, issuance, signing status and Stripe webhook routes.
- Stripe webhooks use signed payload verification with legacy, snapshot and thin destination secrets.
- Event IDs are deduplicated to prevent immediate replay processing.
- License tiers are Free, Pay as you go, Unlimited and Enterprise.
- The landing page displays the repository pricing model: Free; €0.001/operation with €9.99 monthly minimum; €99.99/year Unlimited; custom Enterprise.

## Evidence index

| Evidence | Location |
|---|---|
| Performance gates | `Tests/LightningSPP.Tests/PerformanceGateTests.cs` |
| Stripe signature and replay tests | `Tests/LightningSPP.Tests/StripeWebhookTests.cs` |
| Stripe boundary tests | `Tests/LightningSPP.Tests/StripeWebhookEdgeCaseTests.cs` |
| REST smoke tests | `Tests/LightningSPP.Tests/ApiSmokeTests.cs` |
| Signing authorization | `Tests/LightningSPP.Tests/ApiSigningStatusIntegrationTests.cs` |
| Golden deterministic outputs | `Tests/LightningSPP.Tests/GoldenPipelineTests.cs` |
| License rotation | `Tests/LightningSPP.Tests/LicenseKeyRotationTests.cs` |
| Stigma profiles | `Tests/LightningSPP.Tests/StigmaExecutionProfileTests.cs` |
| Benchmark method | `Docs/BENCHMARK_PROTOCOL.md` |
| Edge/negative matrix | `Docs/EDGE_CASES_AND_NEGATIVE_TESTS.md` |
| API schema | `Web/wwwroot/openapi.json` |

## Release decision

A release candidate is eligible only when:

- Release build and xUnit suite pass.
- Docker build and container smoke commands pass.
- Benchmark gates pass and publish their actual observed values.
- Pricing matches the configured Stripe products.
- Test-mode purchase, signed webhook, replay protection, key issuance and activation complete end to end.
- No unsupported competitor, medical, compliance or certification claim is introduced.
- Release notes link the commit and evidence documents.

## Known boundary

Current in-memory webhook event deduplication is process-local and expires after one day. Multi-instance production requires a shared durable idempotency store before claiming cross-node exactly-once processing.
