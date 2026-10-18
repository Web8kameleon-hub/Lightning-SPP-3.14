# Dynamic Lightning workspace runtime

The current plan is [INTEGRATION_PLAN.md](fabric-p0/INTEGRATION_PLAN.md), with
[scope](../scope.md), [TODO](../todo.md) and [channel contracts](../chanellplan.md).
`/workspace` is a customer tool workspace, separate from the legacy shared
license dashboard. Existing account cookies, session revocation, CSRF and Longo
identity are reused. Customers cannot submit another customer's root directory.

## Enablement and data flow

Production workflows are off unless `LSPP_WORKFLOW_ENABLED=true`. Enabling them
requires local acceptance, CI and deployment/rollback checks. In production the flag
alone admits nobody: `LSPP_WORKFLOW_ALLOWED_EMAILS` must list verified account emails
(comma-separated; `*` admits every verified account). State lives under
`LSPP_DATA_DIR/workspaces/<authenticated-user-id>`. Imported bytes and immutable
snapshots are private files, not a replacement database for customer identity.
No SQL persistence is introduced. Back up this directory with the existing
customer identity store and data-protection keys; test restore before acceptance.

`LSPP_MODEL_ROUTES_FILE` names an absolute, operator-owned JSON array. Each route
has `id`, `endpoint`, `model`, `provider` (`ollama` or `openai-compatible`), optional
`apiKeyEnvironment`, and `remote`. Ollama endpoints use their root URL; compatible
endpoints include their API prefix. Exact model names are discovered, not guessed.
Tokens are read from the named server environment variable and are never returned
to the browser. Do not place route configuration or credentials in a workspace.

The local product must use model endpoints on the customer's own infrastructure.
Remote routes require the user's explicit consent; web access also requires
`LSPP_WORKFLOW_ALLOW_REMOTE_MODELS=true`. No silent provider fallback occurs.
Discovery reports provider-supplied identity/digest, not independently audited
model weights or proof that a model is qualified for a domain.

`LSPP_TOOL_ADAPTERS_FILE` names an operator-owned JSON array of read-only service
adapters: `id`, `description`, `endpoint`, fixed relative `path`, `arguments`
(query parameter names), optional `apiKeyEnvironment`, and `remote`.
Only configured hosts/paths are called; arguments are escaped. Redirects are not
followed. Endpoints must actually be read-only; a GET declaration alone cannot
prove upstream semantics. Selecting a mailbox/bank connector, OAuth scopes,
credential lifecycle, checkpoints and attachment normalization remains required.
The generic adapter is not a claim that Gmail, a bank or another service is connected.

## Review and execution

The UI imports originals, allows selected document context and explicit model
consent, proposes a JSON plan, and saves it only after user review. Saving does
not execute the plan. Every step declares dependencies and a registered tool.
Argument objects may refer to a successful dependency using `$step` and a JSON
`pointer`. Unknown tools, arguments, permissions, missing dependencies and cycles
are rejected. Model and document text cannot grant permissions.

Jobs retain a plan hash, runtime assembly/contract hash, result hashes and actual
source hashes. Independent steps may finish after another step fails; dependants
are blocked. Retry resumes completed steps rather than rerunning them. A changed
runtime requires a new reviewed plan. Cancellation is recorded, not reported as
success. External side effects with uncertain outcomes require manual reconciliation.
The CLI grants local read/report/model permissions; external access and printing
need explicit `--allow-remote`, `--allow-services` or `--allow-print` flags.

The worker count is configured by `LSPP_WORKFLOW_WORKERS` (default 2). Busy jobs
remain persisted for retry instead of creating an unbounded queue. File input is
bounded to 8 MiB in the web workspace, model context to a small selected batch,
job evidence to 16 MiB, and plans to 256 steps per batch. Lists are paginated;
search/audit results explicitly report truncation. These are resource controls,
not evidence of a 30,000-document capacity or restart-recovery benchmark.

## Available tool families

- Files: list, UTF-8 read, literal search, line comparison and customer-supplied
  audit patterns. A pattern hit is a candidate; it does not prove fake production
  processing, reachability, a vulnerability, or semantic correctness.
- Native PDF text uses actual `pdftotext` and immutable source bytes, with page
  references. `SPP_PDFTOTEXT_PATH` can identify its installed executable. No text
  from scans is invented. Vision requires a discovered vision-capable Ollama
  model and returns model claims requiring extraction-quality review.
- Reports: JSON/CSV/PDF plus evidence sidecars and dependency source hashes.
  CSV cells neutralize formula prefixes. PDF reports use Skia and an installed
  monospace font. Large reports must be split into referenced volumes.
- Financial reconciliation accepts structured `documents`, `transactions`,
  reviewed `allocations` and reviewed `credits`. Explicit kinds and the amount
  convention `positive_amount_explicit_kind` are required. Decimal arithmetic,
  currency/counterparty checks and allocation budgets prevent sign guessing and
  double allocation. Source extraction and a bank connection are separate stages.
- Economic indicators require uniquely dated values, units and sources. They
  calculate actual changes, including undefined percent change from zero; they
  do not establish recession prediction or causal inference.
- Review rules require `protocolId`, `version`, `reviewOwner` and rules with
  `field`, `unit`, explicit `min`/`max` and `severity`. Observations require a
  subject identity and explicit verified value/unit. Missing/unverified values
  require review. Rule ownership metadata is not independent clinical validation.
- Requirements trace links requirement IDs/revisions to test records and actual
  source files. Reported pass states remain documented assertions requiring review;
  no physical aircraft inspection or structural certification is performed.
- Device tools reuse Linux CUPS adapters. Web printing additionally requires
  `LSPP_WORKFLOW_ALLOW_PRINT=true` and user consent. Queue submission/history do not
  prove physical output. No device acceptance is claimed without configured hardware.

## Evidence and remaining inputs

The supplied 13 October verification document refers to source absent from the
starting checkout. Its passing counts cannot establish acceptance for this branch.
Current verification is recorded separately in `evidence/workflow-verification.json`.
Live source inference, vision/OCR quality, mailbox/bank provider contracts,
representative redacted records and specialist-owned validation protocols are
required before completing the corresponding business/domain acceptance gates.
