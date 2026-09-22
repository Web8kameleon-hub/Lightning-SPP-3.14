# Production hosting

The web/API service requires .NET 10 or a Linux Docker host. Static-only hosting
(such as GitHub Pages) cannot run the API. The default deployment uses the managed
.NET runtime; Native AOT is opt-in and is not validated for these dependencies.

## Docker Compose

From the repository root:

```bash
cp .env.example .env
# Generate two independent secrets; paste them into the matching .env fields:
openssl rand -hex 32
openssl rand -hex 32
chmod 600 .env
docker compose config --quiet
docker compose up -d --build --wait
curl -fsS http://localhost:13080/api/health
```

Set `LSPP_ADMIN_TOKEN` and `LSPP_LICENSE_SIGNING_KEY` before starting. Keep the
signing key stable across updates and back it up securely. Do not commit `.env`.
License issuance, activation and signing status require the admin bearer token.
Open Workspace administration and enter it in the Administrator access code field before activating a license.

The service runs as a non-root user. Compose publishes only to 127.0.0.1, uses a
persistent `lightning-data` volume for licenses/audit logs, and fixes the hostname
because license encryption depends on the machine and user names. Keep both
stable when restoring backups. Do not use `docker compose down -v` during updates.
This deployment is for one instance: license state and usage are local to it.

## Domain and HTTPS

Point your domain's DNS at the host. Install nginx and configure a site like this,
replacing `example.com`:

```nginx
server {
    listen 80;
    server_name example.com;
    client_max_body_size 1m;
    location / {
        proxy_pass http://127.0.0.1:13080;
        proxy_set_header Host localhost;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Real-IP $remote_addr;
        add_header X-Content-Type-Options nosniff always;
    }
}
```

Run `sudo nginx -t`, reload nginx, then configure TLS with
`sudo certbot --nginx -d example.com`. Expose ports 80/443 publicly and keep the
application port private. Check `/`, `/dashboard`, `/swagger`, `/openapi.json`,
and `/api/health` through HTTPS. The app's limiter currently groups proxied
requests under the proxy IP (30 requests/minute); health probes are exempt.

## Other container platforms

Build the root Dockerfile. Its default command starts `serve`, listens on all
container interfaces, and reads `PORT` (default 13080). Set both secrets above.
Attach persistent storage at `/data` and preserve the hostname/user for encrypted
license files. Set `LSPP_BIND_HOST=+` if your platform does not set
`DOTNET_RUNNING_IN_CONTAINER=true`. Configure TLS at the platform ingress.

## Ubuntu with systemd

Review `deploy/ubuntu/install-server.sh` before running it on Ubuntu 24.04. It
installs the .NET SDK required by the deployment script. Create
`/etc/lightning.env` with the two secret variables, restrict it to root (0600),
and customize the domain in `deploy/ubuntu/nginx-lichting.clisonix.com.conf`.
Run `deploy/ubuntu/deploy-app.sh` from the checkout. It publishes into
`/opt/lightning/publish`; systemd stores writable state in `/var/lib/lightning`.
The systemd service listens at `http://127.0.0.1:11400/api/health`.

The deploy script uses fast-forward updates and stops on local/divergent changes.
It does not discard work. Back up the publish directory and state before updates;
restore the previous publish directory and restart `lightning` to roll back.

## Validation and updates

```bash
dotnet test Tests/LightningSPP.Tests/LightningSPP.Tests.csproj -c Release
dotnet publish wwwmmm.csproj -c Release -o artifacts/publish
docker compose logs --tail=100
docker compose ps
```

Keep `.env` and backups outside source control. After each update, verify health,
static pages, and that an unauthenticated license administration request returns
401. The CI workflow also checks the built container's HTTP routes.
