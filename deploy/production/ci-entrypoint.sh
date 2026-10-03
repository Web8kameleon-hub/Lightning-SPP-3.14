#!/usr/bin/env bash
# Install as /usr/local/sbin/spp-ci-deploy and use as the forced command
# for a dedicated restricted GitHub Actions SSH key. Never grant that key
# an unrestricted root shell.
set -euo pipefail

if [[ ! "${SSH_ORIGINAL_COMMAND:-}" =~ ^deploy\ ([0-9a-f]{40})$ ]]; then
  echo "Only 'deploy <40-character commit SHA>' is permitted." >&2
  exit 1
fi
release="${BASH_REMATCH[1]}"
exec 9>/opt/scanprocessprint/ci-deploy.lock
flock -n 9 || { echo "Another deployment is running." >&2; exit 1; }
cd /opt/scanprocessprint-src
export GIT_SSH_COMMAND='ssh -i /root/.ssh/spp-repository-read -o IdentitiesOnly=yes -o StrictHostKeyChecking=yes -o UserKnownHostsFile=/root/.ssh/spp-github-known-hosts'
test -z "$(git status --porcelain)" || { echo "Production checkout is not clean." >&2; exit 1; }
git fetch origin main
test "$release" = "$(git rev-parse origin/main)" || {
  echo "Only the current origin/main commit may be deployed." >&2
  exit 1
}
git checkout --detach "$release"
exec bash deploy/production/deploy.sh deploy "$release"
