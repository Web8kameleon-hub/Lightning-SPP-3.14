# Hetzner test VM provisioning checklist

The repository includes `Scripts/provision-hetzner-bridge-vm.sh` for creating a NEW, billable, dedicated Hetzner Cloud VM. It deliberately does not change existing servers. **No VM has been created by committing this script.**

Prerequisites: official `hcloud` CLI; an authorized Hetzner Cloud project token exported as `HCLOUD_TOKEN`; an existing SSH public key registered with Hetzner and its ID as `SPP_TEST_SSH_KEY_ID`. Never commit the token or private key. The operator must explicitly set `SPP_TEST_CREATE_CONFIRMED=yes` to authorize the billable create call. Review location, image, type and account quota first.

After provisioning, configure a dedicated test user and SSH server that allows only the intended local forwards, plus a firewall. Install the actual upstream Excel Core, Excel service (including its data-preservation fix) and reviewed OS-CLX runtime from their own repositories. Bind all upstream ports to loopback. Use separate test-only volumes and credentials. Do not clone the production environment or data.

The OS-CLX runtime's previously recorded security detection must be investigated before running it. Verify NodeDB read, signed write, incorrect-key and incorrect-HMAC behavior on isolated fixture nodes. Never expose its write API publicly.

Set GitHub Actions secrets listed in `Docs/HETZNER_ISOLATED_BRIDGE_CI.md`. Pin the test VM host SSH key after out-of-band fingerprint verification. Re-run CI. Only a real complete 100% PASS, benchmark evidence and downstream Docker/release gates can unblock deployment.

## Execution state
- Provisioning automation: committed, **not executed**
- Dedicated VM: **not verified**
- Upstream services: **not provisioned or verified**
- Secrets: **not configured**
- Full release gate: **not passed**
- Production: **unchanged**

If any item is missing, fail CI and do not deploy.
