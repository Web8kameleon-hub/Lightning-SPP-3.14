# Lightning SPP 3.14 - Production Deployment

This guide describes two production-ready hosting options for `wwwmmm`:

1. Docker Compose container deployment
2. Ubuntu VM / bare-metal deployment with systemd + nginx reverse proxy

## Option 1: Docker Compose

Use the repository root `docker-compose.yml` to run `wwwmmm` as a container.

```bash
docker compose build
docker compose up -d
```

The service exposes:

- `http://localhost:13080/api/health`
- `http://localhost:13080/swagger`
- `http://localhost:13080/openapi.json`

### Verify

```bash
curl -I http://localhost:13080/api/health
```

### Notes

- The image is built from `Dockerfile`
- The container command overrides the default entrypoint and starts the REST API server on port `13080`
- If you need license administration, provide `LSPP_ADMIN_TOKEN` in your host environment

## Option 2: Ubuntu host with systemd + nginx

The repository already includes Ubuntu deployment assets under `deploy/ubuntu`.

### Install prerequisites

```bash
cd /tmp
curl -fsSL https://raw.githubusercontent.com/Web8kameleon-hub/Lightning-SPP-3.14/main/deploy/ubuntu/install-server.sh -o install-server.sh
chmod +x install-server.sh
./install-server.sh
```

### Deploy the app

```bash
cd /tmp
curl -fsSL https://raw.githubusercontent.com/Web8kameleon-hub/Lightning-SPP-3.14/main/deploy/ubuntu/deploy-app.sh -o deploy-app.sh
chmod +x deploy-app.sh
./deploy-app.sh
```

This script publishes the app to `/opt/lightning/publish`, installs a systemd service, and configures nginx.

### Enable HTTPS

```bash
sudo certbot --nginx -d <your-domain>
```

### Harden the server

```bash
sudo bash /opt/lightning/deploy/ubuntu/harden-server.sh
```

## Systemd service

The Ubuntu deployment uses a service unit at `/etc/systemd/system/lightning.service`.

### Example settings

- Working directory: `/opt/lightning`
- Command: `/usr/bin/dotnet /opt/lightning/wwwmmm.dll serve 11400`
- Restart: `always`
- Environment: `ASPNETCORE_ENVIRONMENT=Production`

## Reverse proxy

A reverse proxy is recommended for production. The repository includes an nginx example at:

- `deploy/ubuntu/nginx-lichting.clisonix.com.conf`

That config forwards traffic to the local app port.

## Healthcheck and monitoring

Use the built-in endpoint for readiness checks:

```bash
curl -f http://127.0.0.1:13080/api/health
```

For Ubuntu systemd deployment the healthcheck target is:

```bash
curl -f http://127.0.0.1:11400/api/health
```

## What this repo already has

- `Dockerfile` for container builds
- `deploy/ubuntu/install-server.sh`
- `deploy/ubuntu/deploy-app.sh`
- `deploy/ubuntu/lightning.service`
- `deploy/ubuntu/nginx-lichting.clisonix.com.conf`
- `deploy/ubuntu/harden-server.sh`

## Recommended next step

- If you want a cloud-ready deployment, use Docker Compose for container hosting.
- If you want a managed Ubuntu VM host, use the existing `deploy/ubuntu` scripts and update the nginx domain for your environment.
