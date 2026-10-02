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
published release and needs:

- `PROD_SSH_HOST` - hel-core-01's address.
- `PROD_SSH_USER` - SSH user with Docker access on that host.
- `PROD_SSH_KEY` - private key for that user (deploy-only key, not a personal one).

On the host itself, `/opt/scanprocessprint-src` must be a working clone of
this repository (`git clone` once; the CI job only runs `git fetch` +
`git checkout <sha>` there) so `deploy.sh` can build from source at the
released commit.

## Manual flow (what the script automates)

Build each committed revision as `scanprocessprint:<commit>`. Set `RELEASE_ID`
and `LICENSE_HOSTNAME` when starting this Compose project. Preserve the hostname
of the currently licensed container because persisted license encryption uses it.
The application and its loopback proxy start alongside the active release.

Before routing traffic, verify health, all static assets, license validity and
unauthenticated rejection of license issue/activation. Keep the old containers
running. Back up `/opt/neuroconic/deploy/scanprocessprint.conf`, update its
`spp_backend` to `scanprocessprint-<commit>:8081`, copy it into the edge container,
run `nginx -t`, then gracefully reload Nginx. On failure restore the saved config
and reload. Mirror the configuration into the edge image with
`docker compose build web` from `/opt/neuroconic` so future recreation retains it.

Monitor HTTPS before, during and after switching. Do not stop the old release
until in-flight requests have drained and the new release is verified.
Stripe API keys are configured, but automated checkout and fulfillment remain
unfinished; key presence alone does not establish working payments.
