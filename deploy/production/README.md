# Production release on hel-core-01

The running site uses the external Docker network `neurosonic_default` and the
existing `neurosonic-web-1` Nginx edge. Secrets stay exclusively in
`/opt/scanprocessprint/.env.production`; never commit this file.

## Automated zero-downtime deploy

[`deploy.sh`](./deploy.sh) automates the blue-green flow described below: it
builds the new release alongside the active one, runs internal health/static/
license/auth-rejection checks against it directly (bypassing the edge), only
then hot-swaps the edge's `spp_backend` upstream with a graceful `nginx -s
reload`, re-verifies over the public domain, and finally drains and stops the
old release. If any check fails, the script tears down the new release (or
swaps the edge back) and exits non-zero - the active release is never touched
until the replacement has proven itself, so a failed run leaves production
exactly as it was.

```bash
deploy/production/deploy.sh deploy <commit-sha>   # build, verify, promote
deploy/production/deploy.sh status                 # show active/previous release
deploy/production/deploy.sh rollback                # swap back to the previous release
```

One-time host bootstrap before the first automated run:

1. `/opt/scanprocessprint/.env.production` already exists (per above).
2. Create `/opt/scanprocessprint/active-release.env` describing the
   already-running hand-deployed container:
   ```
   ACTIVE_RELEASE_ID=<current commit sha>
   LICENSE_HOSTNAME=<current container hostname>
   PREVIOUS_RELEASE_ID=
   ```
3. Verify `BACKEND_LINE_REGEX` at the top of `deploy.sh` actually matches the
   `spp_backend` line in `/opt/neuroconic/deploy/scanprocessprint.conf` - that
   file belongs to a sibling ops repo this script does not control, so the
   default pattern is a best-effort assumption and must be confirmed once by
   an operator with shell access to hel-core-01.

The previous release is stopped (not removed) after a drain period, so
`rollback` is a fast restart rather than a rebuild. Releases older than
"previous" are pruned automatically on each successful deploy to bound disk
usage.

### Required GitHub Actions secrets

The `deploy-production` job in `.github/workflows/ci-cd.yml` runs on every
published release or an explicit CI/CD dispatch on `main` and needs:

- `PROD_SSH_HOST` - hel-core-01's address.
- `PROD_SSH_USER` - SSH user with Docker access on that host.
- `PROD_SSH_KEY` - private key for that user (deploy-only key, not a personal one).

For an application-only Docker deployment, use
`gh workflow run ci-cd.yml --ref main` after the required secrets are configured.
The dispatch runs the build, complete test suite, CLI benchmark, Docker build,
container checks and blue-green deployment. NuGet and Docker Hub publication
remain restricted to published releases. Validation logs are retained as a
commit-specific Actions artifact; benchmark targets are not performance guarantees.
Production deployments are serialized and are not cancelled midway by a newer run.

On the host itself, `/opt/scanprocessprint-src` must be a working clone of
this repository (`git clone` once; the CI job only runs `git fetch` +
`git checkout <sha>` there) so `deploy.sh` can build from source at the
released commit.

For CI, install `ci-entrypoint.sh` as root-owned `/usr/local/sbin/spp-ci-deploy`
with mode `0755`. Give the dedicated Actions public key this authorized_keys
prefix: `restrict,command="/usr/local/sbin/spp-ci-deploy"`. The key can only
request `deploy <40-character SHA>`; the entrypoint verifies a clean checkout,
requires the current `origin/main` commit, locks concurrent deployments and
invokes the existing application deployment script. Published releases must
therefore point at the current verified `main` commit.
For a private repository, the host uses a separate read-only repository deploy
key at `/root/.ssh/spp-repository-read` and GitHub's verified host key at
`/root/.ssh/spp-github-known-hosts`. This key cannot write to the repository.

## Manual flow (what the script automates)

Build each committed revision as `scanprocessprint:<commit>`. Set `RELEASE_ID`
and `LICENSE_HOSTNAME` when starting this Compose project. Preserve the hostname
of the currently licensed container because persisted license encryption uses it.
The application starts alongside the active release and the edge reaches it directly on port 8080. Configure LSPP_TRUSTED_PROXY_IPS with the current edge IP; keep Longo on the private scanprocessprint_state network.

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

The Docker build requires the private Six Labors license at
`/opt/scanprocessprint/.env.build` (0600), containing `SIXLABORS_LICENSE` and loaded only by the build/deployment process. Docker receives it through an environment-backed BuildKit secret; MSBuild reads `SixLaborsLicenseKey` only during compilation. Never add `.env.build` to the application runtime `env_file`.
GitHub Actions uses the `SIXLABORS_LICENSE` repository secret. Neither the license
nor account passwords/cookie files belong in the image or source control.
Longo must be started with `persistence.yml` before the release; its host data
directory is `/opt/scanprocessprint/longo`, owned by UID 10001 with mode 0700.
Back up that directory along with application data before persistence changes.
Rollback retains the shared Longo state and records each release's backend port
so the earlier 8081 proxy deployment can be restored if needed.
