# Production hosting checklist

Canonical host: `scanprocessprint.com`. The `.solutions` domain redirects to the canonical host.

## Release gate

- Require the `Build & Test` and `Docker Build & Test` checks on `main`.
- Deploy an immutable commit SHA; do not deploy a moving branch checkout.
- Build and scan the container image before promotion.
- Verify `/api/health`, `/`, `/dashboard`, `/swagger`, and `/openapi.json` from outside the host.
- Keep rollback to the previous image tag available until post-deploy checks pass.

## DNS and TLS

Create `A`/`AAAA` records for both apex domains and `www` names. Confirm DNS points to the intended production IP before requesting certificates. Install `deploy/ubuntu/nginx-scanprocessprint.conf`, then request one certificate covering:

```bash
sudo certbot --nginx \
  -d scanprocessprint.com \
  -d www.scanprocessprint.com \
  -d scanprocessprint.solutions \
  -d www.scanprocessprint.solutions
```

Do not treat a registrar status such as “in Bearbeitung” as proof that DNS or SSL is active.

## Runtime

```bash
cp .env.production.example .env.production
chmod 600 .env.production
docker compose -f docker-compose.production.yml config
docker compose -f docker-compose.production.yml build --pull
docker compose -f docker-compose.production.yml up -d
docker compose -f docker-compose.production.yml ps
curl --fail http://127.0.0.1:11400/api/health
```

Generate unique production secrets. Never reuse the example values. Configure all Stripe webhook destinations in Stripe and retain only the secrets actually in use.

## Known production gaps

- `main` does not expose scan/process/pipeline operations through REST. Those operations are currently SDK/CLI only; the separate Fabric API PR must be reviewed, tested, and merged deliberately.
- The current “Clisonic AI” implementation does not load, train, or execute an ML/ONNX model. `MLContext` is instantiated, but the processing path is a byte heuristic. Do not advertise model inference until a versioned model, preprocessing contract, evaluation dataset, and inference tests are present.
- Stripe replay protection and usage counters are in-memory; multiple replicas need shared durable storage before horizontal scaling.
- The current server is a single `HttpListener` process. Zero-downtime rollout requires a second instance behind the proxy or an orchestrator with readiness checks.
- License activation is local to one instance. Mount and back up the `lightning-data` volume.
- Medical use remains out of scope until the applicable validation and certification evidence exists.
- Benchmark targets are not service-level guarantees; publish hardware, dataset, configuration, warm-up, and repetition count with results.
