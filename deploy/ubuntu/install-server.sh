#!/usr/bin/env bash
set -euo pipefail

sudo apt-get update
sudo apt-get install -y nginx certbot python3-certbot-nginx git curl ca-certificates ufw fail2ban unattended-upgrades

# .NET 10 runtime
wget https://packages.microsoft.com/config/ubuntu/24.04/packages-microsoft-prod.deb -O /tmp/packages-microsoft-prod.deb
sudo dpkg -i /tmp/packages-microsoft-prod.deb
sudo apt-get update
sudo apt-get install -y dotnet-runtime-10.0

sudo mkdir -p /opt/lightning
sudo chown -R $USER:$USER /opt/lightning

echo "Install finished. Next steps: deploy-app.sh then harden-server.sh"
