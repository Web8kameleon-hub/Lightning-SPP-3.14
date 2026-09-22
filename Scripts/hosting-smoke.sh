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
for endpoint in issue activate; do
  code=$(docker exec "$container" curl -s -o /dev/null -w '%{http_code}' -d key=invalid "http://localhost:13080/api/license/$endpoint")
  test "$code" = 401
done
# Monitoring must still work when normal requests exhaust the rate limit.
for attempt in $(seq 1 35); do
  docker exec "$container" curl -s -o /dev/null http://localhost:13080/api/license/status
done
docker exec "$container" curl -fsS http://localhost:13080/api/health >/dev/null
docker exec "$container" sh -c 'test "$(id -u)" != 0 && test -w /data'
echo 'Container HTTP, authorization, health and storage checks passed.'
