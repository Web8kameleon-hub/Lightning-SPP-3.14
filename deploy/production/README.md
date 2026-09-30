# Production release on hel-core-01

The running site uses the external Docker network `neurosonic_default` and the
existing `neurosonic-web-1` Nginx edge. Secrets stay exclusively in
`/opt/scanprocessprint/.env.production`; never commit this file.

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
