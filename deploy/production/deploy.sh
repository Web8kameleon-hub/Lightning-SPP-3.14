#!/usr/bin/env bash
# Zero-downtime blue-green deploy for the hel-core-01 production host.
#
# Automates the manual runbook described in README.md: build a new release
# container alongside the currently running one, verify it end-to-end over
# its own internal proxy, only then hot-swap the shared nginx edge
# (neurosonic-web-1) to point at the new release, and finally drain + stop
# the old release. The old release is never touched until the new one has
# passed every check, so a failed deploy leaves production exactly as it
# was (automatic rollback-by-inaction).
#
# Usage:
#   deploy.sh deploy <commit-sha>   Build, verify, and promote a release.
#   deploy.sh rollback              Swap the edge back to the previous release.
#   deploy.sh status                 Print the active/previous release state.
#
# One-time host setup before the first automated deploy:
#   1. Create /opt/scanprocessprint/.env.production (never committed).
#   2. Create /opt/scanprocessprint/active-release.env with the hostname of
#      the already-running hand-deployed container, e.g.:
#        ACTIVE_RELEASE_ID=<current commit sha>
#        LICENSE_HOSTNAME=<current container hostname>
#        PREVIOUS_RELEASE_ID=
#   3. Confirm BACKEND_LINE_REGEX below actually matches the line in
#      /opt/neuroconic/deploy/scanprocessprint.conf that selects the
#      spp_backend upstream - the exact directive lives in a sibling
#      ops repo this script does not control, so the default pattern is a
#      best-effort assumption and MUST be verified by an operator once.
set -euo pipefail

REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
COMPOSE_FILE="$REPO_DIR/deploy/production/compose.yml"
STATE_DIR=/opt/scanprocessprint
STATE_FILE="$STATE_DIR/active-release.env"
ENV_FILE="$STATE_DIR/.env.production"
EDGE_DIR=/opt/neuroconic
EDGE_CONF="$EDGE_DIR/deploy/scanprocessprint.conf"
EDGE_CONTAINER=neurosonic-web-1
DOMAIN=https://scanprocessprint.com

# Line in $EDGE_CONF that pins the active backend, e.g.:
#   set $spp_backend scanprocessprint-<sha>:8081;
# VERIFY this regex matches your real file before relying on this script.
BACKEND_LINE_REGEX='^(\s*set\s+\$spp_backend\s+)scanprocessprint-[a-zA-Z0-9_-]+:8081;'

HEALTH_RETRIES=10
HEALTH_INTERVAL=3
DRAIN_SECONDS=15

log() { echo "[deploy] $*"; }
die() { echo "[deploy] ERROR: $*" >&2; exit 1; }

require_state_file() {
  [ -f "$STATE_FILE" ] || die "$STATE_FILE not found. Bootstrap it first (see header comment)."
  # shellcheck disable=SC1090
  source "$STATE_FILE"
  [ -n "${ACTIVE_RELEASE_ID:-}" ] || die "ACTIVE_RELEASE_ID missing in $STATE_FILE"
  [ -n "${LICENSE_HOSTNAME:-}" ] || die "LICENSE_HOSTNAME missing in $STATE_FILE"
}

write_state_file() {
  cat > "$STATE_FILE" <<EOF
ACTIVE_RELEASE_ID=$1
LICENSE_HOSTNAME=$2
PREVIOUS_RELEASE_ID=$3
EOF
}

compose_project() { echo "scanprocessprint-$1"; }

# Internal health/sanity checks against one release's own containers,
# bypassing the shared edge entirely (talks straight to the app over the
# edge docker network alias scanprocessprint-<release>:8081).
verify_release() {
  local release="$1" backend="scanprocessprint-$1:8081" attempt=1
  while [ "$attempt" -le "$HEALTH_RETRIES" ]; do
    if docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
        -fsS "http://$backend/api/health" >/dev/null 2>&1; then
      break
    fi
    log "release $release not healthy yet (attempt $attempt/$HEALTH_RETRIES)"
    attempt=$((attempt + 1))
    sleep "$HEALTH_INTERVAL"
  done
  [ "$attempt" -le "$HEALTH_RETRIES" ] || { log "release $release failed health check"; return 1; }

  # Static asset reachable.
  docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
    -fsS "http://$backend/dashboard" >/dev/null 2>&1 || { log "release $release: /dashboard not reachable"; return 1; }

  local path
  for path in / /features /technology /pricing /benchmarks /try /swagger /robots.txt /sitemap.xml /site.css /try.js; do
    docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
      -fsS "http://$backend$path" >/dev/null 2>&1 || { log "release $release: $path not reachable"; return 1; }
  done

  # License status endpoint responds.
  docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
    -fsS "http://$backend/api/license/status" >/dev/null 2>&1 || { log "release $release: /api/license/status failed"; return 1; }

  # Admin-only endpoint must reject unauthenticated requests (expect 401).
  local status
  status=$(docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
    -s -o /dev/null -w '%{http_code}' --data 'key=invalid' "http://$backend/api/license/issue") || status="000"
  [ "$status" = "401" ] || { log "release $release: expected 401 from unauthenticated /api/license/issue, got $status"; return 1; }

  status=$(docker run --rm --network neurosonic_default curlimages/curl:8.10.1 \
    -s -o /dev/null -w '%{http_code}' --data 'key=invalid' "http://$backend/api/license/activate") || status="000"
  [ "$status" = "400" ] || { log "release $release: expected 400 from invalid license activation, got $status"; return 1; }

  log "release $release passed all internal checks"
}

swap_edge_to() {
  local release="$1" backup
  backup="$EDGE_CONF.bak.$(date +%Y%m%d%H%M%S)"
  [ -f "$EDGE_CONF" ] || die "$EDGE_CONF not found - cannot swap edge backend"
  cp "$EDGE_CONF" "$backup"
  log "backed up edge config to $backup"

  if ! grep -Eq "$BACKEND_LINE_REGEX" "$EDGE_CONF"; then
    die "BACKEND_LINE_REGEX did not match any line in $EDGE_CONF - update the regex, then retry"
  fi
  sed -i -E "s/$BACKEND_LINE_REGEX/\\1scanprocessprint-$release:8081;/" "$EDGE_CONF"

  docker cp "$EDGE_CONF" "$EDGE_CONTAINER:/etc/nginx/conf.d/scanprocessprint.conf"
  if ! docker exec "$EDGE_CONTAINER" nginx -t; then
    log "nginx -t failed on new config - restoring backup"
    cp "$backup" "$EDGE_CONF"
    docker cp "$EDGE_CONF" "$EDGE_CONTAINER:/etc/nginx/conf.d/scanprocessprint.conf"
    docker exec "$EDGE_CONTAINER" nginx -t
    die "edge config invalid for release $release; edge left on previous release"
  fi
  docker exec "$EDGE_CONTAINER" nginx -s reload
  log "edge now routing to release $release (graceful reload, zero dropped connections)"

  # Bake the config into the edge image so a future container recreation
  # keeps it, per the existing manual runbook.
  (cd "$EDGE_DIR" && docker compose build web) || log "WARNING: failed to rebuild edge image with new config - reload is live, but a future recreate would revert it"
}

verify_public_domain() {
  local attempt=1
  while [ "$attempt" -le "$HEALTH_RETRIES" ]; do
    if curl -fsS "$DOMAIN/api/health" >/dev/null 2>&1; then
      log "public domain check passed: $DOMAIN/api/health"
      return 0
    fi
    attempt=$((attempt + 1))
    sleep "$HEALTH_INTERVAL"
  done
  return 1
}

cmd_deploy() {
  local release="${1:?usage: deploy.sh deploy <commit-sha>}"
  require_state_file
  [ "$release" != "$ACTIVE_RELEASE_ID" ] || die "release $release is already active"

  log "building scanprocessprint:$release from $REPO_DIR"
  docker build -t "scanprocessprint:$release" "$REPO_DIR"

  log "starting release $release alongside the active release ($ACTIVE_RELEASE_ID)"
  RELEASE_ID="$release" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
    docker compose -p "$(compose_project "$release")" -f "$COMPOSE_FILE" \
    --env-file "$ENV_FILE" up -d

  if ! verify_release "$release"; then
    log "release $release failed verification - tearing it down, active release is untouched"
    RELEASE_ID="$release" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
      docker compose -p "$(compose_project "$release")" -f "$COMPOSE_FILE" \
      --env-file "$ENV_FILE" down
    die "deploy aborted for release $release"
  fi

  local old_release="$ACTIVE_RELEASE_ID"
  swap_edge_to "$release"

  if ! verify_public_domain; then
    log "public domain check failed after swap - swapping edge back to $old_release"
    swap_edge_to "$old_release"
    die "deploy aborted after edge swap; rolled back to $old_release automatically"
  fi

  log "draining old release $old_release for ${DRAIN_SECONDS}s before stopping it"
  sleep "$DRAIN_SECONDS"
  RELEASE_ID="$old_release" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
    docker compose -p "$(compose_project "$old_release")" -f "$COMPOSE_FILE" \
    --env-file "$ENV_FILE" stop

  # Prune anything older than "previous" so disk usage stays bounded.
  if [ -n "${PREVIOUS_RELEASE_ID:-}" ] && [ "$PREVIOUS_RELEASE_ID" != "$old_release" ]; then
    log "removing stale release $PREVIOUS_RELEASE_ID"
    RELEASE_ID="$PREVIOUS_RELEASE_ID" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
      docker compose -p "$(compose_project "$PREVIOUS_RELEASE_ID")" -f "$COMPOSE_FILE" \
      --env-file "$ENV_FILE" down 2>/dev/null || true
  fi

  write_state_file "$release" "$LICENSE_HOSTNAME" "$old_release"
  log "deploy complete: active=$release previous=$old_release (stopped, not removed - use rollback to bring it back fast)"
}

cmd_rollback() {
  require_state_file
  [ -n "${PREVIOUS_RELEASE_ID:-}" ] || die "no PREVIOUS_RELEASE_ID recorded - nothing to roll back to"
  local target="$PREVIOUS_RELEASE_ID"

  log "restarting previous release $target"
  RELEASE_ID="$target" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
    docker compose -p "$(compose_project "$target")" -f "$COMPOSE_FILE" \
    --env-file "$ENV_FILE" start

  verify_release "$target" || die "previous release $target is not healthy either - manual intervention required"

  local bad_release="$ACTIVE_RELEASE_ID"
  swap_edge_to "$target"
  verify_public_domain || die "public domain still unhealthy after rollback - manual intervention required"

  sleep "$DRAIN_SECONDS"
  RELEASE_ID="$bad_release" LICENSE_HOSTNAME="$LICENSE_HOSTNAME" \
    docker compose -p "$(compose_project "$bad_release")" -f "$COMPOSE_FILE" \
    --env-file "$ENV_FILE" stop

  write_state_file "$target" "$LICENSE_HOSTNAME" "$bad_release"
  log "rollback complete: active=$target (previous bad release=$bad_release stopped, kept for inspection)"
}

cmd_status() {
  require_state_file
  echo "ACTIVE_RELEASE_ID=$ACTIVE_RELEASE_ID"
  echo "PREVIOUS_RELEASE_ID=${PREVIOUS_RELEASE_ID:-<none>}"
  echo "LICENSE_HOSTNAME=$LICENSE_HOSTNAME"
}

case "${1:-}" in
  deploy) shift; cmd_deploy "$@" ;;
  rollback) cmd_rollback ;;
  status) cmd_status ;;
  *) die "usage: deploy.sh {deploy <commit-sha>|rollback|status}" ;;
esac
