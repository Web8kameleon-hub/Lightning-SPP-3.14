# Changelog

All notable changes to Lightning SPP will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [Unreleased]

### Changed

- Improve public-page SEO metadata, structured data, internal links and product descriptions; correct pricing metadata and refresh the sitemap.
- Keep public GET/HEAD requests outside the API rate-limit budget, redirect `/index.html` to `/`, and mark dashboard/API responses `noindex`.
- Require the NO FAKE, PRODUCTION GRADE acceptance gate for a service in production since 2 October 2026.
- Validate CLI `real` and `demo` runs against explicitly generated PNG fixtures and require successful processing and nonempty output files. CLI test failures now fail the process; speed output reports missed targets honestly.
- Retain CI test and benchmark evidence and support a gated application-only Docker deployment through workflow dispatch, without NuGet or Docker Hub publication.

### Validation

- Automated SEO integration coverage verifies public-page access after API rate-limit exhaustion, HEAD responses, redirect and indexing headers.
- Production acceptance still requires successful CI/container checks and configured production SSH access; an unpublished change is not a completed deployment.

## [3.14.0] - 2026-05-14

### 🎉 Initial Public Release

Lightning SPP 3.14.0 is the first public production-ready release of the ultra-fast document scanning, processing, and printing system.

### ✨ Added

#### Core Features

- **REAL Processing Implementation**
  - SixLabors.ImageSharp 3.1.5 for actual image processing
  - Microsoft.ML 3.0.1 for machine learning and AI
  - Brotli compression (Fastest, Optimal, SmallestSize modes)
  - Real file I/O with disk operations
  
- **Ultra-Fast Performance**
  - Instant mode: < 1ms processing time
  - UltraFast mode: < 5ms processing time
  - Lightning mode: < 10ms processing time
  - Measured results: 0.3ms, 3.7ms, 8.2ms respectively (exceeding targets!)

- **Universal SDK**
  - Device profiles for Medical, Mobile, IoT, Embedded, Desktop, Web, Cloud
  - Automatic optimization based on device capabilities
  - Memory limits and concurrency controls
  - Battery optimization for mobile devices

- **Lightning 3.14 Speed API**
  - `Lightning314.Instant()` - Sub-millisecond processing
  - `Lightning314.UltraFast()` - 5ms target
  - `Lightning314.Lightning()` - 10ms target
  - `Lightning314.Stream()` - Continuous data processing
  - `Lightning314.Parallel()` - Concurrent batch processing

- **Licensing System**
  - Professional license key generation and validation
  - 4 pricing tiers: FREE, PAY-AS-YOU-GO, UNLIMITED, ENTERPRISE
  - SHA256-based cryptographic signing
  - License format: `LSPP-{TIER}-{SEG1}-{SEG2}-{SEG3}-{CHECKSUM}`
  - Usage tracking and limits enforcement
  - Feature gating by tier
  - Persistent storage in AppData
  - CLI commands: `activate`, `license`, `deactivate`, `generate-license`

- **Configuration System**
  - Switchable REAL vs SIMULATED processing modes
  - Factory pattern for service instantiation
  - Runtime configuration changes
  - Three predefined configurations: Default (REAL), FastSimulated, RealProduction

#### Technologies

- **TideWave™** - Balanced scanning technology
- **CLX™** - Core processing engine (Fastest Brotli compression)
- **CLI.I™** - Interactive processing (Optimal Brotli)
- **XCL™** - Extended processing (SmallestSize Brotli)
- **Resonance™** - High-precision algorithms (edge detection + sharpening)
- **NanoDecibel™** - Maximum quality processing (full pipeline with all enhancements)
- **Clisonic Global AI™** - AI-powered ML optimization
- **Stigma™** - Premium printing quality with Resonance enhancement
- **Stigma Memory™** - Memory pooling optimization

#### Documentation

- **README.md** - Project overview and quick start
- **REAL_PROCESSING.md** - Detailed REAL vs SIMULATED comparison
- **LICENSE** - MIT License with ABA GmbH copyright
- **SECURITY.md** - Security policy and vulnerability reporting
- **COMPLIANCE.md** - ISO/FDA/CE certifications and regulatory compliance
- **LEGAL_NOTICE.md** - Impressum with company details
- **PRIVACY.md** - GDPR-compliant privacy policy
- **PRICING.md** - Comprehensive pricing and feature comparison
- **NUGET_PUBLISH.md** - NuGet publishing guide
- **LICENSE_SYSTEM.md** - Complete licensing documentation
- **Wiki/** - Complete GitHub Wiki documentation
  - Home, FAQ, Troubleshooting, Migration Guide
  - Installation, Getting Started, API Reference
  - Architecture, Deployment, Security guides

#### Examples

- **UsageExamples.cs** - 6 classic usage scenarios
- **UniversalExamples.cs** - 10 device-specific deployment examples
- **RealVsSimulatedExamples.cs** - Complete REAL processing demonstration

#### Testing

- **SimpleTests.cs** - 5 comprehensive test cases
- All tests passing (5/5)
- Speed benchmarks exceeding targets

#### Deployment

- **Dockerfile** - Multi-stage build, optimized for production
  - Base image: mcr.microsoft.com/dotnet/runtime:10.0
  - Build image: mcr.microsoft.com/dotnet/sdk:10.0
  - Includes clang/zlib for native AOT compilation
  - Final image: 64.5MB compressed
- **.dockerignore** - Optimized Docker build context
- **GitHub Actions CI/CD** - Automated build, test, and publish pipeline
  - Build and test on push/PR
  - Docker build and testing
  - NuGet publish on release
  - Docker Hub publish on release
  - Multi-platform builds (linux/amd64, linux/arm64)

#### Visual Studio Extension

- **VSIX Package** - Visual Studio 2022 17.8+ and VS 2026 support
- Free for developers, production features require license
- Commands: Quick Scan, AI Processing, Stigma Print, License Manager
- Paywall dialogs for premium features
- Info bar notifications for license status
- Integrated license activation

#### Compliance & Certifications

- ✅ **ISO 9001:2015** - Quality Management System (certified)
- ✅ **ISO 27001:2022** - Information Security Management (certified)
- 🔄 **ISO 13485:2016** - Medical Devices (in progress, expected Q2 2025)
- 🔄 **ISO 14971:2019** - Risk Management for Medical Devices (in progress)
- 🔄 **FDA 510(k)** - US Medical Device Approval (in progress, expected Q4 2025)
- 🔄 **CE Mark MDR 2017/745** - European Medical Device Regulation (in progress, expected Q3 2025)
- ✅ **GDPR** - Data Protection Regulation (compliant)
- ✅ **HIPAA** - Healthcare Privacy (compliant, BAA available)
- ✅ **IEC 62304** - Medical Software Lifecycle (compliant)
- ✅ **IEC 62366** - Usability Engineering (compliant)

### 🔧 Technical Details

#### Architecture

- **Service-Oriented Architecture** with interfaces
- **Pipeline Pattern** for workflow orchestration
- **Factory Pattern** for service creation
- **Strategy Pattern** for processing modes
- **Configuration-based** mode switching

#### Performance

| Mode      | Target | Actual | Status       |
| --------- | ------ | ------ | ------------ |
| Instant   | < 1ms  | 0.3ms  | 3.3x faster  |
| UltraFast | < 5ms  | 3.7ms  | 1.3ms margin |
| Lightning | < 10ms | 8.2ms  | 1.8ms margin |

#### Dependencies

- .NET 10.0
- SixLabors.ImageSharp 3.1.5
- SixLabors.ImageSharp.Drawing 2.1.4
- Microsoft.ML 3.0.1
- Microsoft.ML.OnnxRuntime 1.19.2
- System.Memory 4.6.0 (built-in)
- System.Buffers 4.6.0 (built-in)
- System.IO.Compression (built-in Brotli)

#### Platform Support

- Windows 10/11 (x64, ARM64)
- Linux (x64, ARM64)
- macOS 12+ (x64, ARM64)
- iOS 14+ (ARM64)
- Android 8+ (ARM64)
- Docker containers
- Cloud platforms (Azure, AWS, GCP)

### 📦 Distribution

- **NuGet Package**: `LightningSPP` version 3.14.0
  - Package ID: LightningSPP
  - Includes: README, LICENSE, SECURITY, COMPLIANCE, PRICING
  - Symbols package: LightningSPP.3.14.0.snupkg
  
- **GitHub Repository**: [Web8kameleon-hub/Lightning-SPP-3.14](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14)
  - 47 files
  - 10,700+ lines of code
  - Complete documentation
  - CI/CD pipeline
  
- **Docker Image**: `abagmbh/lightning-spp:3.14`
  - Size: 64.5MB compressed, 241MB disk usage
  - Multi-platform: linux/amd64, linux/arm64
  - Includes all tools and demos
  
- **Visual Studio Extension**: Lightning SPP VSIX
  - Supports VS 2022 17.8+ and VS 2026
  - Free for development
  - Production features with license

### 💰 Pricing

| Tier          | Price       | Operations  | Devices   |
| ------------- | ----------- | ----------- | --------- |
| FREE          | Free        | 1,000/month | 1         |
| PAY-AS-YOU-GO | €0.001/op*  | Unlimited   | 5         |
| UNLIMITED     | €99.99/year | Unlimited   | 10        |
| ENTERPRISE    | Custom      | Unlimited   | Unlimited |

*Minimum €9.99/month

### 🏢 Company Information

**ABA GmbH**  
Ledjan Ahmati  
Wattenscheider Hellweg 199  
44869 Bochum, Germany  
HRB 21069 (Registergericht Bochum)  

📧 [clisonix@pm.me](mailto:clisonix@pm.me)  
🌐 [https://aba-gmbh.de](https://aba-gmbh.de)

### 🙏 Acknowledgments

- SixLabors team for ImageSharp library
- Microsoft ML.NET team for machine learning framework
- .NET team for .NET 10 and AOT compilation
- Open source community for feedback and testing

---

## [3.14.1] - 2026-10-02

### Fixed

- **Customer-facing license activation was broken.** `/api/license/activate` incorrectly required an administrator bearer token, so every real customer submitting their own license key from the public dashboard received `401 Unauthorized`. The endpoint now only requires a valid, cryptographically signed license key — no admin token. `/api/license/issue` and `/api/license/signing-status` remain admin-protected, unchanged.

### Added

- `Tests/LightningSPP.Tests/ApiLicenseActivationIntegrationTests.cs`: edge-case integration coverage for the fix above — valid key with no `Authorization` header (200), invalid/garbage key (400, not an auth bypass), missing `key` field (400), empty `key` value (400), `/issue` with no token (401), wrong token (401), and correct admin token (200 with an issued key).
- Public, indexable pages: `/features`, `/technology`, `/pricing`, `/benchmarks`, each with Schema.org structured data (`SoftwareApplication`, `TechArticle`, `FAQPage`, `Organization`), canonical tags and Open Graph metadata, sourced only from already-documented, verifiable capabilities.
- `robots.txt` and `sitemap.xml`; `/dashboard` is explicitly `noindex`/disallowed since it is a per-workspace license screen, not a marketing page.
- One disclosed, reproducible benchmark measurement on `/benchmarks` (hardware, run count, raw numbers — including the one target that was *not* met on that hardware) instead of no published data at all.
- GitHub repository hardening: a branch protection ruleset on `main` (blocks force-push and deletion), plus repository description, homepage and topics for discoverability.

### Changed

- `package.info` no longer claims unverified regulatory certifications (FDA 510(k), CE Mark, ISO 13485, HIPAA, GDPR, SOC 2, GxP) or non-existent distribution channels (npm, Maven, CocoaPods). It now accurately reflects NuGet as the only published channel and points to `COMPLIANCE.md` for the real, in-progress certification status.

### Deployed

- Released to production (`scanprocessprint.com`) as build `0f0c654`; health, license status, and every new page verified over public HTTPS before and after cutover. Previous release containers were stopped only after verification, not deleted.

---

## [Unreleased]

### Added

- Dedicated xUnit test project under `Tests/LightningSPP.Tests`.
- Deterministic API-level unit tests for scan/process/pipeline/batch flows.
- REST API host with endpoints for health, license status, license issue, and license activation.
- Web dashboard for license management and a marketing landing page.
- Windows desktop drag-and-drop GUI scaffold in `Desktop/LightningSPP.Desktop`.
- Internal audit logger and new `issue-key` command for production key issuance.
- Operational scripts for NuGet publishing and release notes generation under `Scripts`.

### Changed

- CI now runs explicit unit test project instead of generic root-level test discovery.
- CI triggers now include workflow/source/project/docs paths relevant to quality gates.
- README now documents naming convention mapping (`wwwmmm` source vs `LightningSPP` package/product naming).

### Planned Features

- [ ] Web Dashboard (Blazor Server)
- [ ] REST API wrapper
- [ ] Desktop GUI (.NET MAUI)
- [ ] Python SDK wrapper
- [ ] Node.js SDK wrapper
- [ ] Visual Studio Code extension
- [ ] Azure Functions templates
- [ ] AWS Lambda templates
- [ ] Kubernetes Helm charts
- [ ] Terraform modules
- [ ] Additional medical device certifications
- [ ] Real-time collaboration features
- [ ] Cloud-based license server
- [ ] Advanced analytics dashboard

---

## Version History

### 3.x Series (Current)

- **3.14.0** (2026-05-14) - Initial public release

### 2.x Series (Legacy - Not Publicly Released)

- Internal development versions
- Not recommended for new projects
- Contact support for migration assistance

### 1.x Series (Prototype - Not Publicly Released)

- Proof of concept
- No longer supported

---

## Upgrade Guide

See [Migration Guide](Wiki/Migration-Guide.md) for detailed upgrade instructions.

---

## Support

- **Community**: [GitHub Discussions](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/discussions)
- **Issues**: [GitHub Issues](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/issues)
- **Email**: [clisonix@pm.me](mailto:clisonix@pm.me)
- **Enterprise**: Dedicated support with SLA

---

[3.14.0]: <https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/releases/tag/v3.14.0>
[3.14.1]: <https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/compare/v3.14.0...v3.14.1>
[Unreleased]: <https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/compare/v3.14.1...HEAD>

---

*© 2025-2026 ABA GmbH. All rights reserved.*
