# Account security and regression review — 2026-10-18

## Acceptance scope

This review covers signup, verification, password recovery, login and customer
license visibility/activation. A working local SMTP fixture does not establish
production provider configuration or inbox delivery. External customer onboarding
remains unaccepted while the configured sender cannot reach controlled external
recipients. The owner's payment is an internal test, not a customer reference.

## Findings and decisions

| Change | Root Cause | Regression Risk | Scalability Impact | Security Impact | Production Impact | Dynamic Alternative |
|---|---|---|---|---|---|---|
| Email identity | A storage representation was reused for delivery; exact equality coupled ownership checks to one casing. | Existing uppercase identities and paid customers could become unreachable when new writes become lowercase. | Canonicalization is independent of provider/domain; aggregate storage costs are unchanged. | Ambiguous duplicate identities fail closed; verification still controls ownership. | Deploy compatible readers before changing writes; preserve IDs, receipts and keys. | Case-insensitive reads, canonical lowercase new writes, provider-neutral delivery normalization. |
| Login errors | Invalid password and unverified email collapsed into one null result. | Revealing verification status before checking the password would allow account enumeration. | One PBKDF2 password check per attempt; existing per-IP limits remain. | Only a correct password permits `email_not_verified`; no session or paid data is returned. | Verified accounts retain cookie/session behavior; unverified users receive the correct next action. | Explicit authentication outcomes, separate account and license states, machine-readable action codes. |
| Recovery limits | Login, signup and recovery shared one email counter with different thresholds. | Login attempts could prevent a legitimate recovery request earlier than its intended limit. | Independent per-action counters retain the global per-IP budget. | Recovery stays bounded; a different email spelling cannot reset the counter. | No removal of CSRF, origin validation or rate limits. | Keys derived from action plus normalized identity, rather than source names. |
| Password handling | The operator test created recoverable local credentials outside application persistence. | A readable operator artifact can outlive a test and be mistaken for a product password-retrieval feature. | No production retrieval service or new password storage is introduced. | Both operator-created credential files were removed; application state retains PBKDF2 hashes only. | The test account uses the ordinary reset flow; no direct password or verification mutation. | User-chosen passwords, 600,000-iteration Identity V3 PBKDF2, single-use hashed reset tokens and session revocation. |
| License messaging | Paid entitlement state and browser-registration state were presented as one license state. | Existing API clients depend on `state`/`activated`. | New response fields are additive. | Owner verification and signature/expiry/status checks remain required. | Keep legacy fields; show actual validity separately from browser registration. | `licenseState` and `activationRecorded`, derived from verified entitlement metadata. |
| Provider failure | A testing sender is configured; the provider rejects sending from the requested unverified domain. | Silent alternate-provider delivery could disclose tokens, duplicate sends or imply success without evidence. | Unavailable delivery must remain observable and retryable with bounded demand. | Provider rejection never verifies an account or grants a session. | Registration reports a delivery error; public acceptance requires an operational verified sender. | Classify transport/configuration failures and use explicit provider capabilities/configuration; never whitelist recipients in application code. |

The existing email adapter also infers an HTTP transport from one legacy SMTP
hostname. That is architectural debt: changing it requires an explicit transport
configuration migration and transport-contract tests. This account correction
does not introduce new hostname selectors or claim that this legacy shim is a
general provider-discovery mechanism.

## Standalone commercial activation — unresolved

| Root Cause | Regression Risk | Scalability Impact | Security Impact | Production Impact | Dynamic Alternative |
|---|---|---|---|---|---|
| Local activation validates HMAC using issuer signing material; stored runtime license metadata is loaded independently of a fresh signature check. | Sharing issuer secrets, trusting local tier/expiry fields, or replacing existing paid keys during an unreviewed migration would create regressions. | Offline distribution needs verification-only trust; online activation needs an authoritative, bounded service. | Do not distribute signing secrets or claim local cache encryption proves license authenticity. | The account/browser correction does not establish secure offline commercial activation. Existing keys/state remain untouched. | Asymmetric signed grants with configured public verification keys, or authoritative online activation; migrate legacy HMAC through the issuer without another payment. |

Evidence: `LicenseManager.ActivateLicense` calls `LicenseKey.Validate`, whose
`ResolveSigningKeys` requires issuer HMAC secrets. `GetActiveLicense` reads local
storage through `FromStorageString`; `HasValidLicense` then uses `IsValid`, which
checks active/expiry fields rather than revalidating the stored key signature.
This is a code-review finding, not a claim of an observed production exploit.
The authenticated hosted account separately validates its signed entitlement.
Runtime archive/demo checks must not be reported as offline licensing acceptance.
The cryptographic protocol requires a separate reviewed migration; this account
messaging change does not silently replace it or distribute private material.

## Backward compatibility and recovery

Commit `f0fc737` is the compatibility-reader stage. It accepts schema-1 identities
and paid-customer addresses in uppercase, lowercase or mixed case, including
different representations on the two records. It must be deployed before the
lowercase-write/authentication-outcome stage. After lowercase writes begin,
`f0fc737` is the minimum compatible recovery version; an earlier exact-comparison
reader is not a safe application rollback for new records.

The existing HTTP 401 contract for unsuccessful login is preserved; the additive
`code` field distinguishes a correct-password unverified account for new clients.
No bulk identity rewrite is required. The new version reads old records, writes
new identities in canonical lowercase, preserves receipt/license identity and
keeps existing API fields. Production deployment retains the preceding runtime
and its normal health-checked recovery path. Protect the existing Longo state
and data-protection keys throughout upgrades and recovery.

## Test evidence to run before acceptance

- `EmailCompatibilityTests`: real isolated Longo CAS state seeded with mixed old
  and new representations, reopening, repeat registration, verified ownership,
  paid-license stability and workspace authorization. Run on both reader stages.
- `AccountFlowTests`: real local HTTP/cookie/CSRF, SMTP transport and Longo;
  signup, lowercase persistence, PBKDF2 parameters, correct-password unverified
  response, wrong-password privacy, verification replay, case variants, password
  reset, token purpose binding/replay, old-session revocation, stable entitlement,
  unavailable delivery and uniform unknown-account recovery response.
- Reset-before-verification retains the unverified state and invalidates older
  tokens. The completion message explicitly requests a fresh verification link;
  reset is not used as an implicit verification bypass.
- `CustomerLifecycleTests`: expiry, hashed tokens, duplicate signup, sessions,
  concurrent payment idempotency, signed activation, owner isolation and expiry/
  subscription failures. `licenseState` is active before optional browser
  registration only when its existing signature/expiry/status checks pass.
- Required build, complete managed tests, Linux native-document tests, public
  health and authentication boundary checks. Record actual runs; this list is
  not a statement that an unrun check passed.

`SPP_ACCOUNT_BROWSER=1` additionally runs the headless browser fixture against the
real isolated API. It checks status/action text, recovery prefill, wrong-password
privacy, active-license presentation, password-field clearing and logout state.
The SMTP and purchase records in that run remain test fixtures.

Fixtures use test-only passwords/tokens in memory and synthetic purchases. They
must not be called production signup completion, real payments or inbox proof.

Executed local checks on the final change tree: release build passed with zero
warnings/errors; managed suite passed 213 tests with 13 skipped; Linux native
suite passed 219 with 7 external-bridge skips (226 total in each environment).
The optional headless account browser run passed all seven recorded UI checks.
JavaScript syntax, OpenAPI JSON, changed-page local links/IDs/structured data and
release-companion validation passed. CI/deployment of the final commit and actual
external-recipient onboarding must be recorded separately.

## First-visit reading of the current public copy

This is an editorial first-impression review, not a recorded external user study.
The home page describes a .NET SDK and CLI, while pricing/activation copy describes
a shared workspace. A first visitor can understand document/image processing but
may not know whether they are buying a browser application, an SDK or an installed
product. The transition from a free demo to account, shared dashboard and customer
license needs clearer product boundaries.

The pricing page's email-delivery promise is not currently supported for external
recipients. A valid paid key, a verified account and an activated installed runtime
are separate facts. Copy must identify each next action without claiming a
customer's paid key vanished because their account is unverified.

## General source-failure policy

Future content-source adapters must select decoding/parsing by response metadata
and actual content validation, not organization names, hostnames or language
lists. A parser can negotiate a supported representation when the source exposes
one. A timeout/5xx may justify bounded retry; a 429 uses provider retry metadata.
A 401/403 is an access/configuration failure, not permission to evade controls or
substitute unrelated content. Unavailable capabilities remain explicit failures.

This policy applies equally to any organization or personal site. It cannot
guarantee access to every external service: authentication, permissions, supported
formats and source availability remain prerequisites. Identity, payment and
verification operations fail closed; they do not use content-source fallbacks.
