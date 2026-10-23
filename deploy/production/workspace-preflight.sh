#!/usr/bin/env bash
# Read-only production preflight. Never print environment values, credentials or model prompts.
set -euo pipefail
STATE_DIR="${LSPP_STATE_DIR:-/opt/scanprocessprint}"
ENV_FILE="$STATE_DIR/.env.production"
DATA_DIR="$STATE_DIR/data"
fail=0
check() { local name="$1" result="$2"; printf '%s: %s\n' "$name" "$result"; [[ "$result" != FAIL ]] || fail=1; }
if [[ ! -r "$ENV_FILE" ]]; then check production_env FAIL; exit 1; fi
# Parse only key presence/boolean values; never source the file or echo secrets.
python3 - "$ENV_FILE" "$DATA_DIR" <<'PY'
import json, pathlib, sys
env, data = pathlib.Path(sys.argv[1]), pathlib.Path(sys.argv[2])
values = {}
for line in env.read_text().splitlines():
    if not line or line.lstrip().startswith("#") or "=" not in line: continue
    key, value = line.split("=", 1)
    values[key.strip()] = value.strip().strip('"').strip("'")
checks = {
    "workflow_enabled": values.get("LSPP_WORKFLOW_ENABLED") == "true",
    "verified_account_allowlist_configured": bool(values.get("LSPP_WORKFLOW_ALLOWED_EMAILS")),
    "model_routes_configured": bool(values.get("LSPP_MODEL_ROUTES_FILE")),
}
for name, ok in checks.items(): print(f"{name}: {'PASS' if ok else 'FAIL'}")
for key, label in (("LSPP_MODEL_ROUTES_FILE","model_routes_file"),("LSPP_TOOL_ADAPTERS_FILE","tool_adapters_file")):
    path = values.get(key)
    if not path: print(f"{label}: NOT_CONFIGURED"); continue
    # Paths may be container paths, so existence must be checked in the active container.
    print(f"{label}: CONFIGURED_CONTAINER_VERIFICATION_REQUIRED")
print("note: config presence is not proof of service reachability, model quality or successful workflows")
if not all(checks.values()): sys.exit(1)
PY
echo "active_release_file: $([[ -r "$STATE_DIR/active-release.env" ]] && echo PRESENT || echo MISSING)"
echo "docker_running_services:"
docker ps --format '{{.Names}} {{.Status}}' | grep -Ei 'scanprocessprint|lightning|ollama' || true
echo "next: verify /workspace and authenticated /api/workflow with a test account, then run actual E2E processing"
