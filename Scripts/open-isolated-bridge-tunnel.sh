#!/usr/bin/env bash
# Connect CI to dedicated nonproduction Hetzner test services, never public ports.
set -euo pipefail
: "${SPP_TEST_SSH_HOST:?Set SPP_TEST_SSH_HOST to dedicated test host}"
: "${SPP_TEST_SSH_USER:?Set SPP_TEST_SSH_USER}"
: "${SPP_TEST_SSH_KEY:?Set SPP_TEST_SSH_KEY}"
: "${SPP_TEST_SSH_KNOWN_HOSTS:?Set pinned SSH known_hosts entry}"
[[ "$SPP_TEST_SSH_HOST" =~ ^[a-zA-Z0-9.-]+$ ]] || { echo "Invalid SSH host"; exit 1; }
[[ "$SPP_TEST_SSH_USER" =~ ^[a-zA-Z0-9_-]+$ ]] || { echo "Invalid SSH user"; exit 1; }
umask 077
mkdir -p "$RUNNER_TEMP/spp-bridge-ssh"
key="$RUNNER_TEMP/spp-bridge-ssh/key"
hosts="$RUNNER_TEMP/spp-bridge-ssh/known_hosts"
printf '%s\n' "$SPP_TEST_SSH_KEY" > "$key"
printf '%s\n' "$SPP_TEST_SSH_KNOWN_HOSTS" > "$hosts"
chmod 600 "$key" "$hosts"
# Remote services must bind ONLY to 127.0.0.1 on a separate test VM.
# ExitOnForwardFailure proves port bindings, not upstream application health.
ssh -f -N -T -i "$key" -o BatchMode=yes -o StrictHostKeyChecking=yes \
  -o UserKnownHostsFile="$hosts" -o ExitOnForwardFailure=yes \
  -o ConnectTimeout=15 -o ServerAliveInterval=15 -o ServerAliveCountMax=2 \
  -L 127.0.0.1:18002:127.0.0.1:8002 \
  -L 127.0.0.1:18001:127.0.0.1:8001 \
  -L 127.0.0.1:19094:127.0.0.1:9094 \
  "$SPP_TEST_SSH_USER@$SPP_TEST_SSH_HOST"
echo "Dedicated SSH tunnel established (upstream application verification still required)."
