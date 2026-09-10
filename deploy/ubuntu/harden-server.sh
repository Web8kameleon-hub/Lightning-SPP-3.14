#!/usr/bin/env bash
set -euo pipefail

# Firewall
sudo ufw allow OpenSSH
sudo ufw allow 80/tcp
sudo ufw allow 443/tcp
sudo ufw --force enable

# Fail2ban (basic ssh protection)
sudo systemctl enable fail2ban
sudo systemctl restart fail2ban

# Security updates
sudo dpkg-reconfigure -f noninteractive unattended-upgrades || true

# Nginx hardening snippets
sudo tee /etc/nginx/snippets/lightning-security-headers.conf > /dev/null <<'EOF'
add_header X-Frame-Options "SAMEORIGIN" always;
add_header X-Content-Type-Options "nosniff" always;
add_header Referrer-Policy "strict-origin-when-cross-origin" always;
add_header X-XSS-Protection "1; mode=block" always;
add_header Permissions-Policy "geolocation=(), microphone=(), camera=()" always;
EOF

sudo tee /etc/nginx/snippets/lightning-proxy.conf > /dev/null <<'EOF'
proxy_http_version 1.1;
proxy_set_header Host $host;
proxy_set_header X-Real-IP $remote_addr;
proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
proxy_set_header X-Forwarded-Proto $scheme;
proxy_set_header CF-Connecting-IP $http_cf_connecting_ip;
proxy_connect_timeout 30s;
proxy_send_timeout 60s;
proxy_read_timeout 60s;
EOF

sudo nginx -t
sudo systemctl reload nginx

echo "Hardening complete: UFW + fail2ban + unattended upgrades + nginx security snippets enabled."
