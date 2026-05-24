# Ubuntu Deploy - Lightning SPP (lichting.clisonix.com)

## Server
- IP: `5.223.75.178`
- Host: `ubuntu-4gb-sin-2`
- Region: Singapore
- App port: `11400`

## 1) Connect via SSH
```bash
ssh root@5.223.75.178
```

## 2) Install runtime + nginx + certbot + security tools
```bash
cd /tmp
curl -fsSL https://raw.githubusercontent.com/Web8kameleon-hub/Lightning-SPP-3.14/main/deploy/ubuntu/install-server.sh -o install-server.sh
chmod +x install-server.sh
./install-server.sh
```

## 3) Deploy app
```bash
cd /tmp
curl -fsSL https://raw.githubusercontent.com/Web8kameleon-hub/Lightning-SPP-3.14/main/deploy/ubuntu/deploy-app.sh -o deploy-app.sh
chmod +x deploy-app.sh
./deploy-app.sh
```

## 4) Enable HTTPS
```bash
sudo certbot --nginx -d lichting.clisonix.com
```

## 5) Harden server
```bash
sudo bash /opt/lightning/deploy/ubuntu/harden-server.sh
```

## 6) Verify
```bash
curl -I https://lichting.clisonix.com/api/health
curl -I https://lichting.clisonix.com/swagger
curl -I https://lichting.clisonix.com/openapi.json
```

## DNS requirement
Create an `A` record:
- `lichting.clisonix.com` -> `5.223.75.178`

## Optional hardening checks
```bash
sudo ufw status
sudo systemctl status fail2ban --no-pager
sudo systemctl status lightning --no-pager
sudo nginx -t
