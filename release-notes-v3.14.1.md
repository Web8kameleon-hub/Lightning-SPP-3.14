# Lightning SPP v3.14.1 — Customer clarity and release evidence

## Customer experience

- Replaces the banner-only home page with a production product page.
- Explains Scan → Process → Print in direct customer language.
- Displays Free, Pay as you go, Unlimited and Enterprise pricing.
- Connects paid calls to action to the existing Lightning SPP purchase route.
- Links directly to License Dashboard and REST API documentation.
- Removes fictional testimonials and unsupported competitor comparisons.

## Engineering evidence

- Adds a reproducible benchmark protocol and environment record.
- Documents the difference between recorded results, CI regression gates and guarantees.
- Adds the release evidence index.
- Adds an edge-case and counter-test matrix.
- Adds automated missing-payload, missing-signature, missing-secret and blank-replay-ID tests.
- Adds a release-readiness workflow covering build, complete tests, performance gates, documentation and customer-claim checks.

## Recorded reference results

- Instant: 0.3 ms
- UltraFast: 3.7 ms
- Lightning: 15 ms
- Cloud throughput: 3.1M operations/s

Results remain hardware, payload, profile and concurrency dependent. See `Docs/BENCHMARK_PROTOCOL.md`.

## Operational boundary

Stripe signature verification and replay protection are implemented. The present replay store is process-local; horizontally scaled production requires shared durable idempotency storage.
