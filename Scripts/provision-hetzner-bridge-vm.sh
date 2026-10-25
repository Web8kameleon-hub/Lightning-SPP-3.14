#!/usr/bin/env bash
# Provision a NEW dedicated test VM. Requires operator-supplied Hetzner API token and SSH key ID.
# No production server modification. Never prints credentials.
set -euo pipefail
: "${HCLOUD_TOKEN:?Hetzner Cloud API token required}"
: "${SPP_TEST_SSH_KEY_ID:?Provide the existing Hetzner SSH key ID}"
: "${SPP_TEST_LOCATION:=fsn1}"
: "${SPP_TEST_SERVER_TYPE:=cx23}"
: "${SPP_TEST_SERVER_NAME:=spp-isolated-bridge-ci}"
: "${SPP_TEST_IMAGE:=ubuntu-24.04}"
command -v hcloud >/dev/null || { echo "Install official hcloud CLI first"; exit 1; }
[[ "$SPP_TEST_SERVER_NAME" =~ ^[a-z0-9][a-z0-9-]{2,62}$ ]] || exit 1
if hcloud server describe "$SPP_TEST_SERVER_NAME" >/dev/null 2>&1; then
  echo "Server name already exists: refusing to modify or recreate it."
  exit 1
fi
echo "NEW billable Hetzner Cloud VM: $SPP_TEST_SERVER_NAME ($SPP_TEST_SERVER_TYPE, $SPP_TEST_LOCATION, $SPP_TEST_IMAGE)"
echo "Confirm creation by setting SPP_TEST_CREATE_CONFIRMED=yes."
[[ "${SPP_TEST_CREATE_CONFIRMED:-}" == yes ]] || exit 2
hcloud server create --name "$SPP_TEST_SERVER_NAME" --type "$SPP_TEST_SERVER_TYPE" \
  --image "$SPP_TEST_IMAGE" --location "$SPP_TEST_LOCATION" \
  --ssh-key "$SPP_TEST_SSH_KEY_ID" --label purpose=spp-isolated-ci \
  --label environment=test
echo "VM created. Do not connect CI until OS firewall, separate upstream applications, SSH key pinning and test-only credentials are configured."
