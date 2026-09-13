#!/usr/bin/env bash
set -euo pipefail

APP_DIR=/opt/lightning
REPO_URL=https://github.com/Web8kameleon-hub/Lightning-SPP-3.14.git

if [ ! -d "$APP_DIR/.git" ]; then
  rm -rf "$APP_DIR"
  git clone "$REPO_URL" "$APP_DIR"
else
  cd "$APP_DIR"
  git fetch origin
  git reset --hard origin/main
fi

cd "$APP_DIR"
dotnet publish -c Release -o /opt/lightning/publish
sudo cp -f /opt/lightning/publish/* /opt/lightning/

# Nginx snippets used by site config
sudo mkdir -p /etc/nginx/snippets
sudo cp -f deploy/ubuntu/nginx-scanprocessprint.conf /etc/nginx/sites-available/scanprocessprint.com
sudo ln -sf /etc/nginx/sites-available/scanprocessprint.com /etc/nginx/sites-enabled/scanprocessprint.com
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
echo "  1) sudo certbot --nginx -d scanprocessprint.com -d www.scanprocessprint.com -d scanprocessprint.solutions -d www.scanprocessprint.solutions"
echo "  2) sudo bash /opt/lightning/deploy/ubuntu/harden-server.sh"
