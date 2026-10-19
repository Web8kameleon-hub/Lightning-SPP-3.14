# Product positioning update — 2026-10-19

Scope: homepage positioning, deployment/integration approach comparison, proposed sector workflows, semantic architecture overview, economic evaluation guidance and document benchmark methodology. Existing processing, pricing, authentication and deployment behavior are unchanged.

Validation before commit: git diff --check passed; Python HTMLParser checks passed for tag nesting, unique IDs, one H1, JSON-LD parsing, 68 link/asset references across the changed pages, repository document targets and changed-page anchors. CSS delimiter checks passed and the diagram has a single-column mobile rule. Public /api/health returned ok=true, service=LightningSPP.

Limits: browser rendering was not exercised. No local .NET SDK is installed; this static-content change does not modify runtime code. Required build/test/container checks and deployment health checks remain enforced by the existing CI/CD workflow. This record does not assert their future outcome.

Unfinished evidence work: equivalent real-document benchmarks across commercial plans have not been run. Proposed sector applications are not customer case studies. Assistant source retrieval/citations have not been implemented; the homepage provides direct documentation/evidence links and explains model-answer limitations. Successful real-model ONNX inference and physical scanner/printer operation are not claimed.

Recovery: the existing production deploy script verifies the candidate before promotion and supports deploy/production/deploy.sh rollback. Revert this content commit to restore the earlier homepage and benchmark text.
