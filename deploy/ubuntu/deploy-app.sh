#!/usr/bin/env bash
set -euo pipefail

APP_DIR=/opt/lightning
REPO_URL=https://github.com/Web8kameleon-hub/Lightning-SPP-3.14.git

if [ ! -d "$APP_DIR/.git" ]; then
  git clone "$REPO_URL" "$APP_DIR"
else
  cd "$APP_DIR"
  git fetch origin
  git merge --ff-only origin/main
fi

cd "$APP_DIR"
dotnet publish wwwmmm.csproj -c Release -o /opt/lightning/publish /p:UseAppHost=false
if ! sudo test -f /etc/lightning.env; then
  echo 'Create /etc/lightning.env with LSPP_ADMIN_TOKEN and LSPP_LICENSE_SIGNING_KEY before deployment.' >&2
  exit 1
fi

# Nginx snippets used by site config
sudo mkdir -p /etc/nginx/snippets
sudo cp -f deploy/ubuntu/nginx-lichting.clisonix.com.conf /etc/nginx/sites-available/lichting.clisonix.com
sudo ln -sf /etc/nginx/sites-available/lichting.clisonix.com /etc/nginx/sites-enabled/lichting.clisonix.com
sudo rm -f /etc/nginx/sites-enabled/default

# Systemd service
sudo cp -f deploy/ubuntu/lightning.service /etc/systemd/system/lightning.service
sudo systemctl daemon-reload
sudo systemctl enable lightning
sudo systemctl restart lightning
sudo systemctl status lightning --no-pager

sudo nginx -t
sudo systemctl reload nginx

echo "Deploy finished."
echo "Next:"
echo "  1) sudo certbot --nginx -d lichting.clisonix.com"
echo "  2) sudo bash /opt/lightning/deploy/ubuntu/harden-server.sh"
