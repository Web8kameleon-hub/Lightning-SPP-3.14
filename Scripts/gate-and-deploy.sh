#!/usr/bin/env bash
# Validate the committed main revision before using the existing recovery-aware deploy.
# Usage: gate-and-deploy.sh [--dry-run]
# Isolated Excel/NodeDB integration acceptance remains separate, as in ci-cd.yml.
set -euo pipefail
REPO_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$REPO_DIR"
DRY_RUN=0
for arg in "$@"; do
  case "$arg" in
    --dry-run) DRY_RUN=1 ;;
    *) echo "[gate] unknown argument: $arg" >&2; exit 2 ;;
  esac
done
log() { echo "[gate] $*"; }
die() { echo "[gate] BLOCKED: $*" >&2; exit 1; }
[ -z "$(git status --porcelain)" ] || die "uncommitted changes present"
git fetch origin
COMMIT="$(git rev-parse HEAD)"
[ "$(git branch --show-current)" = main ] || die "primary main checkout required"
[ "$COMMIT" = "$(git rev-parse origin/main)" ] || die "HEAD differs from shared main"
log "repository policy"
python3 Scripts/preflight-policy.py || die "policy gate failed"
log "release build"
dotnet build wwwmmm.csproj -c Release --nologo || die "release build failed"
mkdir -p artifacts/gate
EVIDENCE="$(mktemp -d "$REPO_DIR/artifacts/gate/${COMMIT}.XXXXXX")"
log "CI test scope: native documents enabled; isolated bridge services require separate acceptance"
SPP_TEST_NATIVE_DOCUMENTS=1 dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj \
  -c Release --nologo --filter 'FullyQualifiedName!~BridgeUpstreamTests' \
  --logger 'trx;LogFileName=gate.trx' \
  --results-directory "$EVIDENCE/Tests/LightningSPP.Tests/TestResults" || die "test suite failed"
(cd "$EVIDENCE" && python3 "$REPO_DIR/Scripts/require-all-tests-pass.py") || die "incomplete test execution"
[ -z "$(git status --porcelain)" ] || die "working tree changed during validation"
[ "$COMMIT" = "$(git rev-parse HEAD)" ] || die "HEAD changed during validation"
if [ "$DRY_RUN" -eq 1 ]; then
  log "gate passed for $COMMIT; deployment not attempted; evidence: $EVIDENCE"
  exit 0
fi
exec bash deploy/production/deploy.sh deploy "$COMMIT"
