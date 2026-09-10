# Edge Cases and Negative-Test Matrix

## Release rule

Every release must run positive, boundary, negative, replay and recovery tests. A successful happy path alone is not release evidence.

| Area | Edge or hostile condition | Expected result | Automated evidence |
|---|---|---|---|
| Stripe | Empty payload | Reject without exception | StripeWebhookEdgeCaseTests |
| Stripe | Missing signature | Reject without exception | StripeWebhookEdgeCaseTests |
| Stripe | No webhook secret | Reject with configuration error | StripeWebhookEdgeCaseTests |
| Stripe | Invalid HMAC | Reject | StripeWebhookTests |
| Stripe | Replayed event ID | Acknowledge once; no duplicate processing | StripeWebhookTests |
| Stripe | Duplicate secret in destinations | Verify once without leaking secret | StripeWebhookEdgeCaseTests |
| License API | Missing admin bearer | 401 + WWW-Authenticate | ApiSigningStatusIntegrationTests |
| License | Key rotation | Old/new behavior follows rotation policy | LicenseKeyRotationTests |
| Pipeline | Missing input | Structured failure; no crash | API tests |
| Pipeline | Empty payload | Deterministic behavior; no corruption | Release readiness tests |
| Pipeline | Large payload | Bounded memory and explicit timeout budget | Performance suite |
| Output | Bit flip/corruption | SHA-256 mismatch | GoldenPipelineTests |
| Stigma | Invalid profile value | Fail at startup; never silently downgrade | StigmaExecutionProfileTests |
| Static UI | Mobile viewport | Pricing and CTA remain readable | Manual release checklist |
| Configuration | Missing Stripe keys | Billing unavailable; processing remains isolated | Stripe configuration tests |

## Counter-tests

Counter-tests are designed to prove that safeguards fail closed:

1. Sign a webhook with the wrong secret and confirm rejection.
2. Replay a previously accepted event ID and confirm it is not processed twice.
3. Remove every Stripe webhook secret and confirm verification cannot succeed.
4. Call administrative license endpoints without the bearer token and confirm 401.
5. Change a golden output byte and confirm the hash test fails.
6. Set an unsupported Stigma level and confirm startup rejects it.
7. Run benchmarks on an intentionally constrained runner and confirm regression gates report the measured value.

## Manual release checks

- Verify the customer pricing page and Stripe checkout show the same currency, interval and tier.
- Complete one Stripe test-mode checkout and verify one license is issued.
- Replay its webhook and verify no second license or credit is created.
- Activate the issued key from the dashboard.
- Confirm secrets, bearer tokens, license keys and customer data never appear in logs.
- Restore a backup into an isolated environment and execute the smoke suite.
