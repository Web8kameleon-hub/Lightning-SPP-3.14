# Production release on hel-core-01

Everything in this directory ships the application to the single production host
`hel-core-01` and is executed *from* a checkout on that host - CI never builds
the production image itself, it only asks the host to deploy a commit it has
already validated.

Runtime topology:

| Piece | Name | Notes |
| --- | --- | --- |
| Public edge | `neurosonic-web-1` container, external network `neurosonic_default` | Owned by a sibling ops repo. Routes `scanprocessprint.com` to the `spp_backend` upstream - a line naming one release. |
| Application | `scanprocessprint-<commit>` compose project (`compose.yml`) | Listens on 8080 inside the edge network only; no published host port. Reachable by its network alias `scanprocessprint-<commit>`. |
| Durable state | `scanprocessprint-state` compose project (`persistence.yml`) | Longo behind an internal-only network `scanprocessprint_state`. Release-independent lifetime. |
| Secrets | `/opt/scanprocessprint/.env.production` | Never committed. Read by the app container as `env_file`. |
| Build secret | `/opt/scanprocessprint/.env.build` (0600) | `SIXLABORS_LICENSE` only, BuildKit secret, never mounted into the runtime container. |
| Deploy state | `/opt/scanprocessprint/active-release.env` | Which release is live, which is the rollback target, plus the license hostname. |
| Source clone | `/opt/scanprocessprint-src` | Plain clone of this repository; the CI job only `git fetch` + `checkout <sha>` there. |

The running site therefore depends on three things this directory does not
contain: the shared edge container, the external `neurosonic_default` network,
and the `.env.production` secret file.

## Contents of this directory

| File | Purpose |
| --- | --- |
| `deploy.sh` | Blue-green deploy, status and rollback. The only entry point that changes production. |
| `compose.yml` | One release of the application. Requires `RELEASE_ID`, `LICENSE_HOSTNAME` and `LSPP_TRUSTED_PROXY_IPS`; fails closed when any is missing. |
| `persistence.yml` | Longo state project. Requires `LONGO_RELEASE_ID`. Creates the internal `scanprocessprint_state` network. |
| `ci-entrypoint.sh` | Forced command for the restricted GitHub Actions key. Accepts only `deploy <40-character SHA>`. |
| `internal-proxy.conf` | nginx snippet for the legacy 8081 -> 8080 loopback proxy used by the hand-deployed release. Not used by `compose.yml`. |
| `enable-workspace.sh` | Operator script that proves the app can reach a model runtime and writes the private-workspace configuration. |
| `workspace-preflight.sh` | Read-only preflight for the private workspace; prints pass/fail and never prints values. |

## Automated zero-downtime deploy

[`deploy.sh`](./deploy.sh) automates the flow described under "Manual flow"
below: it builds the new release alongside the active one, runs internal
health/static/license/auth-rejection checks against it directly (bypassing the
edge), only then hot-swaps the edge's `spp_backend` upstream with a graceful
`nginx -s reload`, re-verifies over the public domain, and finally drains and
stops the old release. If any check fails, the script tears down the new release
(or swaps the edge back) and exits non-zero - the active release is never
touched until the replacement has proven itself, so a failed run leaves
production exactly as it was.

```bash
deploy/production/deploy.sh deploy <commit-sha>   # build, verify, promote
deploy/production/deploy.sh status                # show active/previous release
deploy/production/deploy.sh rollback              # swap back to the previous release
```

A `deploy` run does, in order:

1. Refuse a release that is already active.
2. Validate `.env.build`, then `docker build` the application image with
   `SIXLABORS_LICENSE` supplied as a BuildKit secret.
3. Build `scanprocessprint-longo:<commit>` and bring up the shared Longo project
   (idempotent - Longo outlives releases).
4. Start the new release compose project next to the active one.
5. Verify the new release internally: `/api/health` with retries, the public
   pages and static assets, `/api/license/status`, unauthenticated rejection of
   `/api/license/issue` (401) and `/api/account` (401), invalid activation
   (400), and an `/openapi.json` contract check.
6. Back up the edge config, rewrite the `spp_backend` line, `docker cp` it into
   the edge, `nginx -t`, then `nginx -s reload`. An invalid config is restored
   from the backup and the run aborts.
7. Verify `https://scanprocessprint.com/api/health`; on failure swap the edge
   back to the previous release and abort.
8. Wait out the drain period, stop the old release (kept, not removed), prune the
   release older than "previous", and record the new state.

`rollback` restarts the previous release, re-verifies it, points the edge back at
it, verifies the public domain, stops the failed release for inspection and
records the swap. Because the previous release was stopped rather than removed,
rollback is a restart, not a rebuild.

Both paths are serialized on the host by the `flock` in `ci-entrypoint.sh` and in
CI by the `scanprocessprint-production` concurrency group.

## One-time host bootstrap before the first automated run

1. `/opt/scanprocessprint/.env.production` exists and contains, at minimum,
   `LSPP_TRUSTED_PROXY_IPS` (the current edge container IP), the admin/license
   signing secrets and the subscription settings. `compose.yml` rejects the
   release if `LSPP_TRUSTED_PROXY_IPS` is unset. Never commit this file.
2. `/opt/scanprocessprint/.env.build` (mode 0600, not a symlink) contains exactly
   one `SIXLABORS_LICENSE=` line.
3. `/opt/scanprocessprint/longo` exists, owned by UID 10001, mode 0700. Back it up
   together with `/opt/scanprocessprint/data` before any persistence change.
4. `/opt/scanprocessprint/data` exists and is writable by the app user; it is
   mounted at `/home/app/.config/LightningSPP`.
5. The external network `neurosonic_default` exists (it belongs to the edge).
   `scanprocessprint_state` is created by the first `persistence.yml` run, which
   `deploy.sh` always performs before starting a release.
6. Create `/opt/scanprocessprint/active-release.env` describing the
   already-running hand-deployed container:

   ```text
   ACTIVE_RELEASE_ID=<current commit sha>
   LICENSE_HOSTNAME=<current container hostname>
   PREVIOUS_RELEASE_ID=
   ```

   Optional `ACTIVE_BACKEND_PORT` / `PREVIOUS_BACKEND_PORT` default to `8081`,
   which is the legacy loopback-proxy topology; automated deploys record `8080`.
7. `/opt/scanprocessprint-src` is a working clone of this repository (`git clone`
   once; the CI job only runs `git fetch` + `git checkout <sha>` there).
8. Confirm the host paths `deploy.sh` hard-codes actually exist:
   - `EDGE_DIR=/opt/neuroconic` and
     `EDGE_CONF=$EDGE_DIR/deploy/scanprocessprint.conf`.
   - `BACKEND_LINE_REGEX` at the top of `deploy.sh` matches the `spp_backend`
     line in that file.

   The edge belongs to a sibling ops repo this script does not control, so both
   the directory name and the regex are best-effort assumptions and must be
   confirmed once by an operator with shell access to `hel-core-01`. If either is
   wrong the script fails closed: `swap_edge_to` aborts before touching the
   running configuration.

`LICENSE_HOSTNAME` is preserved across every deploy because persisted license
encryption is bound to the container hostname. Never change it while releases
that hold licensed state are being migrated.

## CI wiring

`.github/workflows/ci-cd.yml` runs on every push to `main`, `develop` and
`deploy/production`, every pull request to `main`, every published release, and a
manual dispatch. Releases and dispatches do not by themselves deploy.

The `deploy-production` job needs `select-production`, `build-and-test` and
`docker-build`, and runs only when the selection policy says deploy **and** the
ref is `main` or `deploy/production` **and** the run is either a push or a
dispatch with `deploy_production=true` and `publish_ghcr=false`. It uses the
`production` environment, is serialized under `scanprocessprint-production`
(never cancelled midway), and:

1. Fails early if any of the three SSH secrets is empty.
2. Runs `deploy ${{ github.sha }}` over SSH (`appleboy/ssh-action`, 15-minute
   command timeout), which the forced command turns into
   `deploy/production/deploy.sh deploy <sha>` on the host.
3. Re-checks `https://scanprocessprint.com/api/health` from CI afterwards.

Required repository secrets:

- `PROD_SSH_HOST` - `hel-core-01`'s address.
- `PROD_SSH_USER` - SSH user with Docker access on that host.
- `PROD_SSH_KEY` - private key for that user (deploy-only key, not a personal one).

For an application-only Docker deployment, use
`gh workflow run ci-cd.yml --ref main` with `deploy_production=true` and
`publish_ghcr=false` after the required secrets are configured. The dispatch runs
the build, complete test suite, CLI benchmark, Docker build, container checks and
blue-green deployment. NuGet and Docker Hub publication remain restricted to
published releases. Validation logs are retained as a commit-specific Actions
artifact; benchmark targets are not performance guarantees.

The 15-minute SSH timeout covers a full source build plus the health waits and
the 15s drain, so a cold or unusually slow build fails the job rather than
hanging it; re-running is safe because nothing is promoted before verification.

### Restricted entrypoint

Install `ci-entrypoint.sh` as root-owned `/usr/local/sbin/spp-ci-deploy` with
mode `0755` and give the dedicated Actions public key this `authorized_keys`
prefix: `restrict,command="/usr/local/sbin/spp-ci-deploy"`. The key can only
request `deploy <40-character SHA>`; the entrypoint takes the deploy lock,
verifies a clean checkout, re-fetches `origin/main` and requires the requested SHA
to still be its tip, then invokes `deploy.sh`. Published releases must therefore
point at the current verified `main` commit.

For a private repository the host uses a separate read-only deploy key at
`/root/.ssh/spp-repository-read` and GitHub's verified host key at
`/root/.ssh/spp-github-known-hosts`. That key cannot write to the repository.

## Selective production deployment

CI validates every push to main/develop and every pull request to main.
Documentation and test changes still receive validation but do not by themselves
request deployment. Automatic production deployment compares the entire current
main tree against the last successful GitHub deployment in the `production`
environment, rather than only the latest commit. Renames are evaluated as removal
plus addition. Missing baseline evidence, API errors, divergent history, and
unknown paths block automatic deployment.

Only changes under `Services/`, `PublicAPI/`, `SDK/`, and `Web/wwwroot/` are
eligible for automatic deployment after Build & Test and Docker Build & Test
succeed. Changes to auth/account server code, licensing, configuration,
persistence, dependencies, Docker, workflows, deployment scripts, or other paths
require an explicit deployment. A sensitive change remains a blocker in later
pushes until a successful controlled deployment establishes a new baseline. See
`Scripts/select-production-deploy.py`.

For the initial rollout of this policy, or another reviewed sensitive change, run
**Lightning SPP CI/CD** on **main** with **deploy_production=true** and
**publish_ghcr=false**. Default manual runs only validate. Release publication no
longer requests a production deployment. Existing NuGet, Docker Hub, and GHCR
publication conditions remain separate. Never select both publication and
deployment. The deployment still uses the restricted SSH entrypoint, production
environment, serial deployment lock, candidate checks, public health check, and
existing rollback command. This policy does not change persistence formats or
application behavior.

An alternative explicit deployment request is to create or fast-forward the
reserved `deploy/production` branch to the exact current `origin/main` commit. CI
fetches full history and rejects a deployment branch whose SHA differs from main.
Build & Test and Docker Build & Test remain mandatory. The server forced command
independently fetches main again and rejects stale commits before building. Never
force-push this branch or use it for development. Its commit is the deployment
request and audit record; no application commit is needed merely to request
deployment.

Acceptance: verify the PR validation runs before merge; after merge use the
explicit production run, inspect its deployment log and public health
verification, and record the successful SHA. A successful CI run with a skipped
deployment is not deployment evidence. If a deployment fails, inspect state
before retrying; do not bypass gates.

## Manual flow (what the script automates)

Build each committed revision as `scanprocessprint:<commit>`. Set `RELEASE_ID`
and `LICENSE_HOSTNAME` when starting this Compose project. Preserve the hostname
of the currently licensed container because persisted license encryption uses it.
The application starts alongside the active release and the edge reaches it
directly on port 8080 through its network alias. Configure
`LSPP_TRUSTED_PROXY_IPS` with the current edge IP; keep Longo on the private
`scanprocessprint_state` network.

Before routing traffic, verify health, all static assets, license validity and
unauthenticated rejection of license issue/activation. Keep the old containers
running. Back up `/opt/neuroconic/deploy/scanprocessprint.conf`, update its
`spp_backend` to `scanprocessprint-<commit>:8080`, copy it into the edge container,
run `nginx -t`, then gracefully reload Nginx. On failure restore the saved config
and reload. Mirror the configuration into the edge image with
`docker compose build web` from `/opt/neuroconic` so future recreation retains it.

Monitor HTTPS before, during and after switching. Do not stop the old release
until in-flight requests have drained and the new release is verified.

Configure the Stripe destination as `https://scanprocessprint.com/api/stripe/webhook`.
Subscription licenses are reconciled from `invoice.paid`, persisted in Longo, and
notified through the configured mail provider. The authenticated recovery endpoint
`POST /api/stripe/recover` accepts an existing paid invoice event ID; it reads the
canonical Stripe invoice and reuses any stored entitlement on repeated requests.
Provider acceptance does not establish inbox delivery.

## Rollback across the legacy 8081 topology

Releases deployed before this Compose project used a loopback proxy
(`internal-proxy.conf`, listening on 8081, forwarding to `127.0.0.1:8080`). The
state file records `PREVIOUS_BACKEND_PORT` so `rollback` can point the edge at
`8081` again, and in that case the script also restarts the
`scanprocessprint-<release>-proxy-1` container.

That proxy container belongs to the older hand-deployed compose project - it is
**not** defined in `compose.yml` here, so a rollback that needs it requires that
project to still exist on `hel-core-01`. Rolling back to an 8081 release does not
recreate it. Once the former active release is a `compose.yml` release (port
8080), the 8081 path is never taken again and its proxy can be retired.

Rollback retains the shared Longo state; only application containers are swapped.

## Private workspace (models and tools)

`/workspace` and `/api/workflow` are off by default per release. In production
`Web/WorkflowApi.cs` answers `503 workflow_disabled` unless
`LSPP_WORKFLOW_ENABLED` is exactly `true`, and even then the signed-in account
must already be a **verified** account whose e-mail appears in
`LSPP_WORKFLOW_ALLOWED_EMAILS`. An unset or empty allowlist denies everybody in
production; `*` opens the workspace to every verified account. Enabling the flag
alone never exposes the workspace.

Model routes resolve in this order (`Workflow/ModelRoutes.cs`):

1. `LSPP_MODEL_ROUTES_FILE` - an absolute path to an operator-owned JSON array
   under 1 MiB. When it is set and valid it always wins.
2. Otherwise loopback discovery of a local Ollama runtime, using
   `LSPP_OLLAMA_ENDPOINT` (loopback only, no credentials/query/fragment),
   `LSPP_MODEL_PREFER` to pin a tag, and `LSPP_MODEL_AUTODISCOVER=off` to forbid
   discovery entirely.

With neither, the workspace reports "no model" (`modelSource: none`) instead of
inventing one. Remote model endpoints are only permitted when
`LSPP_WORKFLOW_ALLOW_REMOTE_MODELS=true`; printing through the workspace requires
`LSPP_WORKFLOW_ALLOW_PRINT=true`. `LSPP_WORKFLOW_WORKERS` bounds concurrent
workflow workers (1-32, default 2).

### enable-workspace.sh

```bash
sudo bash deploy/production/enable-workspace.sh discover
sudo bash deploy/production/enable-workspace.sh enable <verified-account-email> [ollama-base-url]
```

`discover` changes nothing: it names the active release container, prints its
networks, lists candidate Ollama containers and any host listener on 11434, then
probes each candidate from inside the app container. `enable` picks a reachable
endpoint (or the one you pass), reads that runtime's own `/api/tags` so only
models it actually reports end up in `model-routes.json`, writes that file into
`/opt/scanprocessprint/data` (readable by the app, mode 0640), backs up
`.env.production`, and sets:

- `LSPP_WORKFLOW_ENABLED=true`
- `LSPP_WORKFLOW_ALLOWED_EMAILS=<email>`
- `LSPP_MODEL_ROUTES_FILE=/home/app/.config/LightningSPP/model-routes.json`
- `LSPP_WORKFLOW_ALLOW_REMOTE_MODELS=false`
- `LSPP_WORKFLOW_ALLOW_PRINT=false`

Nothing takes effect until the next verified `deploy.sh` run, which re-reads
`.env.production` and is still rollback-by-inaction. The script only writes routes
marked `remote: false`, so the generated file never needs the remote permission.
Adding a second operator later means appending to the allowlist, not re-running
`enable`.

### workspace-preflight.sh

`bash deploy/production/workspace-preflight.sh` is read-only. It parses
`.env.production` without sourcing it and without printing values, and reports
`workflow_enabled`, `verified_account_allowlist_configured` and
`model_routes_configured` as PASS/FAIL, states that container paths need
in-container verification, and prints the running `scanprocessprint` /
`lightning` / `ollama` containers. It exits non-zero when a check fails.

Presence of configuration is not evidence of service reachability, model quality
or a successful workflow. Finish with a real end-to-end run: sign in as the
allowlisted account, open `/workspace`, and confirm an authenticated
`/api/workflow` job actually completes.

## Excel bridge runtime configuration

To enable Clisonix Excel tools in the private customer workspace, add actual HTTPS
service base URLs to `/opt/scanprocessprint/.env.production`:

- `SPP_EXCEL_CORE_URL` for the LAGTER workbook export service.
- `SPP_EXCEL_SERVICE_URL` for workbook parse and validation.
- `LSPP_WORKFLOW_ALLOW_REMOTE_SERVICES=true` only after both upstreams are behind
  an authenticated ingress and the permitted workspace accounts are configured.
- `SPP_EXCEL_CORE_API_KEY` and `SPP_EXCEL_SERVICE_API_KEY` only when that ingress
  actually validates bearer credentials; the Excel applications do not validate them themselves.

## Build secret and Longo persistence

`/opt/scanprocessprint/.env.build` (0600), containing `SIXLABORS_LICENSE`, is
loaded only by the build/deployment process. Docker receives it through an
environment-backed BuildKit secret; MSBuild reads `SixLaborsLicenseKey` only
during compilation. Never add `.env.build` to the application runtime `env_file`.
GitHub Actions uses the `SIXLABORS_LICENSE` repository secret. Neither the license
nor account passwords/cookie files belong in the image or source control.

Longo must be started with `persistence.yml` before the release; its host data
directory is `/opt/scanprocessprint/longo`, owned by UID 10001 with mode 0700.
Back up that directory along with application data before persistence changes.
The Longo project is brought up on every deploy with `--wait`, so a failing Longo
health probe stops the deploy before any application container starts.

## Known constraints

- The edge config, its directory (`/opt/neuroconic`), and the `spp_backend`
  directive shape are not in this repository. `deploy.sh` fails closed when the
  regex does not match, but those values must be confirmed by an operator. The
  same directory is spelled `neuroconic` here while the network and edge
  container are `neurosonic` - confirm the real path before trusting either.
- `deploy.sh` rewrites the edge config on the host and then rebuilds the edge
  image; a failed `docker compose build web` only logs a warning, so a later
  container recreation could revert a reload that was already live. Re-check the
  edge after recreating it.
- Verification curls run `curlimages/curl:8.10.1` on `neurosonic_default`, so the
  host must be able to pull that image.
- The public-domain check tests `/api/health` only; it does not replay the full
  static/license matrix through the edge.
- A deploy that fails after the edge swap rolls the edge back automatically, but
  the failed release's containers are left for inspection - remove that compose
  project by name before the next attempt if disk usage matters.
- A rollback target recorded on port 8081 needs the legacy proxy container to
  still exist; see "Rollback across the legacy 8081 topology".
