#!/usr/bin/env bash
# Release deploy for the public host (compose project lightning-spp-test).
#
# Builds the requested commit with the private Six Labors build secret, points
# public.yml at the new image, recreates the app container behind Caddy, and
# only reports success once real smoke checks pass. If any check fails the
# previous public.yml is restored and the old release is brought back, so a
# failed run leaves production exactly as it was.
#
# Usage:
#   release.sh deploy <commit-sha>   Build, verify and promote a release.
#   release.sh rollback              Restore the previously active release.
#   release.sh status                Print active/previous and the live image.
#
# The Six Labors licence is a BUILD-ONLY secret: it is mounted into
# `dotnet publish` through BuildKit and never enters the image or runtime.env.
set -euo pipefail

ROOT=/opt/lightning-spp-test
SOURCE=$ROOT/source-release
PUBLIC=$ROOT/public.yml
STATE=$ROOT/active-release.env
LICENSE_SECRET=$ROOT/secrets/build-license
IMAGE_PREFIX=lightning-spp-test
BASE_URL=http://127.0.0.1:13080
HEALTH_RETRIES=40
HEALTH_INTERVAL=3

log() { echo "[release] $*"; }
die() { echo "[release] ERROR: $*" >&2; exit 1; }

compose() { docker compose -f "$ROOT/compose.yml" -f "$ROOT/public.yml" "$@"; }

active_image_in() { grep -o "$IMAGE_PREFIX:[A-Za-z0-9._-]*" "$1" | head -1; }

read_state() {
  [ -f "$STATE" ] || die "$STATE not found - bootstrap it first (see README.md)"
  # shellcheck disable=SC1090
  source "$STATE"
  ACTIVE_RELEASE_ID="${ACTIVE_RELEASE_ID:-}"
  PREVIOUS_RELEASE_ID="${PREVIOUS_RELEASE_ID:-}"
}

write_state() {
  printf 'ACTIVE_RELEASE_ID=%s\nPREVIOUS_RELEASE_ID=%s\n' "$1" "$2" > "$STATE"
}

# Fetch and check out exactly the requested commit. A clean checkout of the
# commit being deployed is the only source the image is built from.
sync_source() {
  local sha="$1"
  [ -d "$SOURCE/.git" ] || die "$SOURCE is not a git checkout"
  if [ "$(git -C "$SOURCE" rev-parse HEAD)" != "$sha" ]; then
    git -C "$SOURCE" fetch --depth 1 origin main
    git -C "$SOURCE" checkout -f "$sha"
  fi
  [ "$(git -C "$SOURCE" rev-parse HEAD)" = "$sha" ] || die "checkout did not land on $sha"
  [ -z "$(git -C "$SOURCE" status --porcelain)" ] || die "checkout is not clean"
}

build_image() {
  local sha="$1"
  if docker image inspect "$IMAGE_PREFIX:$sha" >/dev/null 2>&1; then
    log "image $IMAGE_PREFIX:$sha already present, reusing"
    return
  fi
  [ -f "$LICENSE_SECRET" ] || die "build licence secret is missing: $LICENSE_SECRET"
  [ -r "$LICENSE_SECRET" ] || die "build licence secret is unreadable: $LICENSE_SECRET"
  log "building $IMAGE_PREFIX:$sha from $SOURCE"
  docker build \
    --secret "id=sixlabors_license,src=$LICENSE_SECRET" \
    --target final \
    -t "$IMAGE_PREFIX:$sha" "$SOURCE"
}

# Real checks against the running container, bypassing Caddy.
smoke() {
  local attempt=1
  while [ "$attempt" -le "$HEALTH_RETRIES" ]; do
    curl -fsS -o /dev/null "$BASE_URL/api/health" 2>/dev/null && break
    log "app not healthy yet (attempt $attempt/$HEALTH_RETRIES)"
    attempt=$((attempt + 1))
    sleep "$HEALTH_INTERVAL"
  done
  [ "$attempt" -le "$HEALTH_RETRIES" ] || { log "app never answered /api/health"; return 1; }

  local path
  for path in / /try /pdf /features /pricing /benchmarks /openapi.json \
              /site.css /theme.css /reveal.js /assets/landing-hero.png; do
    curl -fsS -o /dev/null "$BASE_URL$path" || { log "$path is not reachable"; return 1; }
  done

  local code
  code=$(curl -s -o /dev/null -w '%{http_code}' --data 'key=invalid' "$BASE_URL/api/license/issue")
  [ "$code" = "401" ] || { log "unauthenticated /api/license/issue returned $code, expected 401"; return 1; }

  code=$(curl -s -o /dev/null -w '%{http_code}' --data 'key=invalid' "$BASE_URL/api/license/activate")
  [ "$code" = "400" ] || { log "invalid activation returned $code, expected 400"; return 1; }

  # The customer-facing download format must produce a real TIFF, not a PNG.
  local sample="$SOURCE/Web/wwwroot/favicon-48x48.png"
  [ -f "$sample" ] || die "smoke sample image is missing: $sample"
  curl -s -X POST "$BASE_URL/api/try/process?format=tiff" \
       -H 'Content-Type: image/png' -H 'X-Stigma-Profile: lightning' \
       --data-binary @"$sample" | grep -q 'image/tiff' \
    || { log "format=tiff did not return a TIFF"; return 1; }

  log "smoke checks passed"
}

promote() {
  local sha="$1" backup
  backup="$PUBLIC.bak.$(date -u +%Y%m%dT%H%M%SZ)"
  cp -p "$PUBLIC" "$backup"
  sed -i "s|$IMAGE_PREFIX:[A-Za-z0-9._-]*|$IMAGE_PREFIX:$sha|" "$PUBLIC"
  compose config --quiet || { cp -p "$backup" "$PUBLIC"; die "compose config rejected the new public.yml"; }

  log "recreating app on $IMAGE_PREFIX:$sha"
  if ! compose up -d --wait; then
    log "compose up failed - restoring $backup"
    cp -p "$backup" "$PUBLIC"
    compose up -d --wait || log "WARNING: restore also failed, manual intervention required"
    return 1
  fi

  if ! smoke; then
    log "smoke checks failed - restoring $backup"
    cp -p "$backup" "$PUBLIC"
    compose up -d --wait || log "WARNING: restore also failed, manual intervention required"
    return 1
  fi
  return 0
}

cmd_deploy() {
  local sha="${1:-}"
  [[ "$sha" =~ ^[0-9a-f]{40}$ ]] || die "usage: release.sh deploy <40-character commit SHA>"
  read_state
  [ "$sha" != "$ACTIVE_RELEASE_ID" ] || die "release $sha is already active"

  sync_source "$sha"
  build_image "$sha"
  promote "$sha" || die "deploy of $sha aborted; previous release left running"
  write_state "$sha" "${ACTIVE_RELEASE_ID:-}"
  log "deploy complete: active=$sha previous=${ACTIVE_RELEASE_ID:-<none>}"
}

cmd_rollback() {
  read_state
  [ -n "${PREVIOUS_RELEASE_ID:-}" ] || die "no PREVIOUS_RELEASE_ID recorded - nothing to roll back to"
  local target="$PREVIOUS_RELEASE_ID" bad="$ACTIVE_RELEASE_ID"
  docker image inspect "$IMAGE_PREFIX:$target" >/dev/null 2>&1 \
    || die "previous image $IMAGE_PREFIX:$target is gone - redeploy it explicitly"
  log "rolling back to $target"
  sync_source "$target"
  promote "$target" || die "rollback to $target failed"
  write_state "$target" "$bad"
  log "rollback complete: active=$target previous=$bad"
}

cmd_status() {
  read_state
  echo "ACTIVE_RELEASE_ID=${ACTIVE_RELEASE_ID:-<none>}"
  echo "PREVIOUS_RELEASE_ID=${PREVIOUS_RELEASE_ID:-<none>}"
  echo "public.yml image=$(active_image_in "$PUBLIC")"
  echo "running containers:"
  docker ps --filter "name=${IMAGE_PREFIX}" --format '  {{.Names}} {{.Image}} {{.Status}}'
}

case "${1:-}" in
  deploy) shift; cmd_deploy "$@" ;;
  rollback) cmd_rollback ;;
  status) cmd_status ;;
  *) die "usage: release.sh {deploy <commit-sha>|rollback|status}" ;;
esac
