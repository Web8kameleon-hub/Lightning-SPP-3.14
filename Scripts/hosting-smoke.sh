#!/usr/bin/env bash
set -euo pipefail
image=${1:-lightning-spp:latest}
container=$(docker run -d --rm "$image")
trap 'docker rm -f "$container" >/dev/null' EXIT
for attempt in $(seq 1 30); do
  if docker exec "$container" curl -fsS http://localhost:13080/api/health >/dev/null; then break; fi
  sleep 1
done
for path in / /dashboard /site.css /dashboard.js /swagger /openapi.json /api/health; do
  docker exec "$container" curl -fsS "http://localhost:13080$path" >/dev/null
done
# /issue requires an admin bearer token; without one it must stay 401 regardless of body.
code=$(docker exec "$container" curl -s -o /dev/null -w '%{http_code}' -d key=invalid "http://localhost:13080/api/license/issue")
test "$code" = 401
# /activate is deliberately self-service (no admin token): LicenseManager.ActivateLicense
# validates the key itself, so an invalid key returns 400, not 401.
code=$(docker exec "$container" curl -s -o /dev/null -w '%{http_code}' -d key=invalid "http://localhost:13080/api/license/activate")
test "$code" = 400
# Monitoring must still work when normal requests exhaust the rate limit.
for attempt in $(seq 1 35); do
  docker exec "$container" curl -s -o /dev/null http://localhost:13080/api/license/status
done
docker exec "$container" curl -fsS http://localhost:13080/api/health >/dev/null
# Search crawlers must retain access after the API budget is exhausted.
for path in / /features /technology /pricing /benchmarks /try /swagger /robots.txt /sitemap.xml /site.css /try.js; do
  docker exec "$container" curl -fsS "http://localhost:13080$path" >/dev/null
done
code=$(docker exec "$container" curl -s -o /dev/null -w '%{http_code}' http://localhost:13080/index.html)
test "$code" = 301
docker exec "$container" curl -fsSI http://localhost:13080/features >/dev/null
docker exec "$container" sh -c 'test "$(id -u)" != 0 && test -w /data'
echo 'Container HTTP, authorization, health and storage checks passed.'
