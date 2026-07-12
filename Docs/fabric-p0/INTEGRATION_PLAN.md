# WWWMMM Integration Plan (P0)

## Qellimi

WWWMMM te jete gateway ultra-thin per scan/print mbi CLX event contracts.

## Endpoints minimale

- `GET /v1/pulse`
- `GET /v1/trace/{id}`
- `POST /v1/scan/context`
- `POST /v1/print/action`

## Gateway rules

- Auth + rate limit + routing vetem.
- Nuk ben heavy reasoning lokalisht.
- Delegon te `ai-execution-layer` dhe `node-orchestrator`.

## Headers detyruese

- `X-Trace-Id`
- `X-Span-Id`
- `X-Correlation-Id`
- `X-CLX-Request-Id`

## Event mapping

- `/v1/scan/context` -> `clx.scan.request`
- `/v1/print/action` -> `clx.print.request`
- response -> `clx.scan.response` / `clx.print.response`

## Metrics detyruese

- `queue_depth`
- `inflight_requests`
- `rejected_requests`
- `avg_latency_ms`
- `p95_latency_ms`

## Done when

- [ ] Scan/print endpointet gjenerojne evente valid sipas CLX contract.
- [ ] Trace id ruhet ne round-trip.
- [ ] Backpressure metrikat ekspozohen ne observability.
