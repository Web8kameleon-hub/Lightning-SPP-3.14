#!/usr/bin/env bash
set -euo pipefail

REPO_URL="${REPO_URL:-https://github.com/Web8kameleon-hub/Lightning-SPP-3.14.git}"
REPO_DIR="${REPO_DIR:-/opt/lightning/repo}"
SITE_CONF_TARGET="/etc/nginx/sites-available/scanprocessprint.com"
SITE_CONF_SOURCE_REL="deploy/ubuntu/nginx-scanprocessprint.conf"
SYSTEMD_TEMPLATE_TARGET="/etc/systemd/system/lightning@.service"
SYSTEMD_TEMPLATE_SOURCE_REL="deploy/ubuntu/lightning@.service"
ACTIVE_UPSTREAM_SNIPPET="/etc/nginx/snippets/lightning-active-upstream.conf"

BLUE_SLOT="blue"
GREEN_SLOT="green"
BLUE_PORT=11400
GREEN_PORT=11401

mkdir -p /opt/lightning /etc/lightning /etc/nginx/snippets

if [ ! -d "$REPO_DIR/.git" ]; then
  rm -rf "$REPO_DIR"
  git clone "$REPO_URL" "$REPO_DIR"
fi

cd "$REPO_DIR"
git fetch --prune origin
git checkout main
git reset --hard origin/main

COMMIT_SHA="$(git rev-parse --short HEAD)"
STAMP="$(date +%Y%m%d_%H%M%S)"

if [ -f "$ACTIVE_UPSTREAM_SNIPPET" ] && grep -q ":$GREEN_PORT" "$ACTIVE_UPSTREAM_SNIPPET"; then
  ACTIVE_SLOT="$GREEN_SLOT"
  ACTIVE_PORT="$GREEN_PORT"
  TARGET_SLOT="$BLUE_SLOT"
  TARGET_PORT="$BLUE_PORT"
else
  ACTIVE_SLOT="$BLUE_SLOT"
  ACTIVE_PORT="$BLUE_PORT"
  TARGET_SLOT="$GREEN_SLOT"
  TARGET_PORT="$GREEN_PORT"
fi

TARGET_BASE="/opt/lightning/$TARGET_SLOT"
TARGET_RELEASE_DIR="$TARGET_BASE/releases/${STAMP}_${COMMIT_SHA}"
TARGET_CURRENT_LINK="$TARGET_BASE/current"

mkdir -p "$TARGET_BASE/releases"

dotnet restore
dotnet publish wwwmmm.csproj -c Release -o "$TARGET_RELEASE_DIR"
ln -sfn "$TARGET_RELEASE_DIR" "$TARGET_CURRENT_LINK"

cat > "/etc/lightning/${TARGET_SLOT}.env" <<EOF
LIGHTNING_PORT=${TARGET_PORT}
LSPP_ENVIRONMENT=Production
LSPP_DATA_DIR=/var/lib/lightning
XDG_CONFIG_HOME=/var/lib/lightning
LSPP_TRUST_PROXY_HEADERS=true
EOF

sudo cp -f "$REPO_DIR/$SYSTEMD_TEMPLATE_SOURCE_REL" "$SYSTEMD_TEMPLATE_TARGET"
sudo cp -f "$REPO_DIR/$SITE_CONF_SOURCE_REL" "$SITE_CONF_TARGET"
sudo ln -sf "$SITE_CONF_TARGET" /etc/nginx/sites-enabled/scanprocessprint.com

if [ ! -f "$ACTIVE_UPSTREAM_SNIPPET" ]; then
  echo "server 127.0.0.1:${ACTIVE_PORT};" | sudo tee "$ACTIVE_UPSTREAM_SNIPPET" >/dev/null
fi

sudo systemctl daemon-reload
sudo systemctl enable "lightning@${TARGET_SLOT}" >/dev/null 2>&1 || true
sudo systemctl restart "lightning@${TARGET_SLOT}"

for attempt in $(seq 1 40); do
  if curl -fsS "http://127.0.0.1:${TARGET_PORT}/api/health" >/dev/null; then
    break
  fi
  if [ "$attempt" -eq 40 ]; then
    echo "New slot ${TARGET_SLOT} on port ${TARGET_PORT} failed health check." >&2
    sudo journalctl -u "lightning@${TARGET_SLOT}" -n 50 --no-pager || true
    exit 1
  fi
  sleep 1
done

echo "server 127.0.0.1:${TARGET_PORT};" | sudo tee "$ACTIVE_UPSTREAM_SNIPPET" >/dev/null

sudo nginx -t
sudo systemctl reload nginx

if sudo systemctl is-active --quiet "lightning@${ACTIVE_SLOT}"; then
  sudo systemctl stop "lightning@${ACTIVE_SLOT}"
fi

echo "Zero-downtime deploy completed."
echo "Active slot: ${TARGET_SLOT}"
echo "Active port: ${TARGET_PORT}"
echo "Release: ${TARGET_RELEASE_DIR}"
