# Usage route and identity inventory — P0

Status: **source inventory verified**, not production-history reconstruction or
implemented durable usage instrumentation. Inspected the candidate based on
`f475f34` plus `origin/main@186a25d`; source paths below are the authority.
This inventory includes the current CRM/classification continuation.

| Route family / operation | Trusted identity available | Attribution / boundary |
| --- | --- | --- |
| Public pages `/`, `/features`, `/technology`, `/pricing`, `/benchmarks`, `/try`, `/pdf`, `/swagger`, `/openapi.json`, robots/sitemap and static assets | None | Anonymous. A cookie or an IP is not a customer. Canonicalize asset paths as `/static/{asset}`; never retain raw filenames/query strings. |
| `/account`, `/workspace`, `/dashboard` page shells | None from serving HTML | Anonymous shell requests; data APIs make their own authorization decision. |
| `/api/health`, `/api/metrics`, `/api/alerts`, `/api/stigma/schema` | None | Public process/service information, not a customer-usage history. Health may check Longo availability. |
| `/api/admin/summary` GET | Administrator bearer validation | Operator, not an inferred customer. Returns actual aggregates; it does not grant customer impersonation. Rejected requests remain anonymous. |
| `/api/account/csrf` GET | No customer required | Anonymous anti-CSRF preparation; tokens must never enter telemetry. |
| `/api/account` GET | Authenticated session and active account | Internal `NameIdentifier` after server authentication. A supplied user/email/plan field is not authority. Failed authentication stays anonymous. |
| `/api/account/register`, `/login`, `/request-reset`, `/request-verification` POST | Anonymous until authentication succeeds | Never log the supplied email/password/token. A successful login may attach the verified internal identity only after the server establishes it. Do not infer identity on rejected attempts. |
| `/api/account/verify`, `/reset` POST | Purpose-bound consumed account token | Record the canonical operation and outcome. Do not retain the token or infer a browser login from successful verification/reset. Anonymous unless the existing authenticated session is independently validated. |
| `/api/account/logout` POST | Existing authenticated session | Server-resolved account; session ID/cookie excluded. |
| `/api/account/license`, `/activate`, `/retry-email` POST | Authenticated owner plus owner-scoped entitlement lookup | Entitlement/plan belongs to the resolved owner. Never store license keys, device IDs or supplied entitlement IDs in usage events. |
| `/api/account/checkout`, `/billing` POST | Authenticated owner and server-linked Stripe customer | Account identity comes from the session; plan/tier only from verified entitlement or validated pricing configuration, never arbitrary input. A checkout URL is not evidence of payment. |
| `/api/assistant` GET, `/api/assistant/conversation` POST | No customer identity from guest channel | Anonymous. Explicit enablement, same-origin CSRF and confirmed model destination remain required. Questions and screen text are excluded from telemetry. |
| `/api/workflow` GET and `/job`, `/files`, `/report` GET | Authenticated active, verified account; workspace allowlist | Owner derived from session, never a caller-selected root. Unknown/other-owner job/path does not confer that owner's identity. Canonical route excludes job IDs, paths and filenames. |
| `/api/workflow/import`, `/plan`, `/run`, `/cancel`, `/acknowledge` POST | Same owner plus CSRF | Per-tool permissions and consent apply. CRM requires `allowCrm` again at execution. Hashes, source text, plan arguments and customer details are not usage-event fields. |
| `/api/workflow/discover`, `/propose`, `/conversation` POST | Same owner, CSRF and explicit model/remote consent | Keep model claims distinct from observed HTTP outcomes. Do not retain prompts, screen text or provider response bodies. |
| CRM/classification tools inside a workflow | Inherited authenticated workflow owner | No independent public CRM API or identity database is introduced. A successful HTTP workflow request can contain failed/review-required steps; HTTP and document/business outcomes must not be conflated. |
| `/api/try/process`, `/api/try/package`, `/api/try/archive` POST | Anonymous guest activity record, not verified account identity | Anonymous for usage accounting unless an independent authenticated resolver is added and verified. Never derive a customer from IP or document content. A partially processed package is not universal success. |
| `/api/fluid/{pipeline}` POST (`bitpipeline`, `bitunpacker`) | None | Anonymous binary processing. Bounded input/output and concurrency; unsupported methods/pipelines are rejected. No customer or paid license is inferred. |
| `/api/license/status` | Process-active license state only | Anonymous HTTP caller. The active machine license is not proof of the caller's customer identity. |
| `/api/license/activate` POST | Valid signed license key, no session owner | License-validated action, anonymous customer attribution unless a separate trusted owner mapping exists. Never log the key. Account activation uses its separate owner-scoped route above. |
| `/api/license/issue` POST, `/signing-status` GET | Administrator bearer validation | Operator operation. License subject details are not a caller identity and must not enter usage telemetry. |
| `/api/stripe/webhook` POST | Verified Stripe signature and validated fulfillment mapping | Service identity; attach a customer only after server-side signed-event fulfillment resolves it. A customer ID in unverified JSON is not trusted. No payload, signature, email or Stripe secret in telemetry. |
| `/api/stripe/recover` POST | Administrator bearer plus retrieved/validated Stripe event | Operator operation; fulfillment owner is separate from the caller. A replay must not double-count one logical event. |
| Unknown route, unsupported method, preflight, rate rejection, parsing/storage/downstream failure | Only identity already established before failure | Use canonical `unmapped` for unknown routes; never record arbitrary raw URLs. 404/401/403/413/429/5xx and transport cancellation remain actual outcomes, not invented success. |
| CLI/SDK/physical scan/print | Local operator or separately established application identity | Outside HTTP session attribution. No physical completion from a CUPS job ID alone; no customer inferred from workstation environment. |

Source reviewed: `Web/LicenseApiServer.cs`, `CustomerAccounts.cs`,
`WorkflowApi.cs`, `GuestAssistantApi.cs`, `FluidApiHandler.cs`,
`CustomerStore.cs`, `GuestActivity.cs`, `Workflow/WorkflowRunner.cs` and
`Services/LightningMetrics.cs`. This is grouping of the dispatcher branches,
not a claim that every route already emits the proposed event schema.

## Current persistence and historical gaps

- `LightningMetrics` is process-memory state. Its API counter begins after the
  general rate-limit branch, so it is not an all-ingress request counter; public
  metrics omit that API counter. Restart resets it. No daily totals are inferred.
- `CustomerStore`/Longo stores account, entitlement, session and fulfillment
  state. Those rows are not an API usage ledger or evidence for two months of calls.
- Guest activity is separate operational state and is not a trusted customer
  identity or a complete cross-route ledger. Its existing data is not silently
  promoted into the proposed usage contract.
- Job snapshots and Stigma Film Memory prove the recorded workflow cases; they
  do not enumerate all HTTP traffic, public page views or historical outages.
- The public edge and application audit logs are potential retained sources.
  Their retention, timezone, coverage, rotation and privacy have **not been
  inspected on the production host in this verification**. No historical values
  or coverage are claimed. Request bodies/IPs in logs must not be copied to Git.
- Scheduled public daily publication, persistent usage instrumentation and
  backfill remain P2–P5 work. There is no generated daily production JSON here.

P0 verification: the dispatcher and delegated route families were mapped above;
authorization sources and unmapped/failure attribution are explicit. Runtime
tests elsewhere cover exercised authorization cases only. This document's
completion does not establish persistent usage, billing correctness or rollout.
