#!/usr/bin/env bash
# Install as /usr/local/sbin/spp-ci-release (root:root, 0755) and use as the
# forced command for the dedicated GitHub Actions deploy key. That key must
# never get an unrestricted root shell.
#
# The key can only ask for `deploy <40-character SHA>`, and only for the
# current tip of origin/main: the shallow fetch brings exactly one commit, so
# any other SHA fails the checkout instead of building old code.
set -euo pipefail

SOURCE=/opt/lightning-spp-test/source-release
LOCK=/opt/lightning-spp-test/ci-deploy.lock

if [[ ! "${SSH_ORIGINAL_COMMAND:-}" =~ ^deploy\ ([0-9a-f]{40})$ ]]; then
  echo "Only 'deploy <40-character commit SHA>' is permitted." >&2
  exit 1
fi
sha="${BASH_REMATCH[1]}"

exec 9>"$LOCK"
flock -n 9 || { echo "Another deployment is running." >&2; exit 1; }

[ -d "$SOURCE/.git" ] || { echo "$SOURCE is not a git checkout." >&2; exit 1; }
git -C "$SOURCE" fetch --depth 1 origin main
if ! git -C "$SOURCE" checkout -f "$sha" 2>/dev/null; then
  echo "Commit $sha is not the current origin/main tip." >&2
  exit 1
fi
[ "$(git -C "$SOURCE" rev-parse HEAD)" = "$sha" ] || { echo "Checkout did not land on $sha." >&2; exit 1; }

exec bash "$SOURCE/deploy/testing/release.sh" deploy "$sha"
