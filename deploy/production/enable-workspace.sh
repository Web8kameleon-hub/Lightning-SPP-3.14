#!/usr/bin/env bash
# Operator script for hel-core-01: discover the server's Ollama, prove the active
# Lightning SPP container can reach it, then write the workspace configuration.
#
#   sudo bash deploy/production/enable-workspace.sh discover
#   sudo bash deploy/production/enable-workspace.sh enable <verified-account-email> [ollama-base-url]
#
# "discover" changes nothing. "enable" backs up .env.production, writes
# model-routes.json with only the models Ollama actually reports, and sets the
# workspace flag + allowlist. The new values take effect on the next
# deploy.sh run (blue-green, verified, rollback-by-inaction). Never prints secrets.
set -euo pipefail

STATE_DIR=/opt/scanprocessprint
ENV_FILE="$STATE_DIR/.env.production"
DATA_DIR="$STATE_DIR/data"
ROUTES_HOST="$DATA_DIR/model-routes.json"
ROUTES_CONTAINER=/home/app/.config/LightningSPP/model-routes.json

active_container() {
  # shellcheck disable=SC1091
  source "$STATE_DIR/active-release.env"
  docker ps --filter "label=com.docker.compose.project=scanprocessprint-${ACTIVE_RELEASE_ID}" \
    --filter "label=com.docker.compose.service=app" --format '{{.Names}}' | head -1
}

candidates() {
  docker ps --format '{{.Names}} {{.Image}} {{.Ports}}' | grep -i ollama | while read -r name _; do
    echo "http://$name:11434"
  done
  echo "http://host.docker.internal:11434"
}

probe() { # $1 container, $2 base url -> prints tags JSON or fails
  docker exec "$1" curl -fsS -m 8 "$2/api/tags"
}

cmd=${1:-}
app=$(active_container)
[[ -n "$app" ]] || { echo "No active Lightning SPP container found." >&2; exit 1; }

case "$cmd" in
  discover)
    echo "active container: $app"
    echo "app networks: $(docker inspect -f '{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}' "$app")"
    echo "--- ollama containers"
    docker ps --format '{{.Names}} | {{.Image}} | {{.Ports}}' | grep -i ollama || echo "(none by name/image)"
    for c in $(docker ps --format '{{.Names}}' | grep -i ollama); do
      echo "$c networks: $(docker inspect -f '{{range $k,$v := .NetworkSettings.Networks}}{{$k}} {{end}}' "$c")"
    done
    echo "--- host listener on 11434"
    (ss -ltnp 2>/dev/null | grep ':11434' ) || echo "(none)"
    echo "--- reachability from $app"
    for url in $(candidates); do
      if out=$(probe "$app" "$url" 2>/dev/null); then
        echo "REACHABLE $url models: $(echo "$out" | python3 -c 'import sys,json;print(" ".join(m["name"] for m in json.load(sys.stdin)["models"]))')"
      else
        echo "unreachable $url"
      fi
    done
    ;;
  enable)
    email=${2:?verified account email required}
    url=${3:-}
    if [[ -z "$url" ]]; then
      for c in $(candidates); do probe "$app" "$c" >/dev/null 2>&1 && { url=$c; break; }; done
    fi
    [[ -n "$url" ]] || { echo "No Ollama URL reachable from $app; run 'discover' and pass the URL." >&2; exit 1; }
    tags=$(probe "$app" "$url")
    TAGS="$tags" python3 - "$url" "$ROUTES_HOST" <<'PY'
import json, os, sys
url, target = sys.argv[1], sys.argv[2]
models = [m["name"] for m in json.loads(os.environ["TAGS"])["models"]]
if not models: sys.exit("Ollama reports no installed models.")
routes = [{"id": "ollama-" + n.replace(":", "-").replace("/", "-").replace(".", "-"), "endpoint": url,
           "model": n, "provider": "ollama", "remote": False} for n in models]
json.dump(routes, open(target, "w"), indent=2)
print("routes:", ", ".join(r["id"] + "=" + r["model"] for r in routes))
PY
    chown --reference="$DATA_DIR" "$ROUTES_HOST"; chmod 0640 "$ROUTES_HOST"
    cp -p "$ENV_FILE" "$ENV_FILE.bak-$(date -u +%Y%m%dT%H%M%SZ)"
    set_env() { if grep -q "^$1=" "$ENV_FILE"; then sed -i "s|^$1=.*|$1=$2|" "$ENV_FILE"; else echo "$1=$2" >> "$ENV_FILE"; fi; }
    set_env LSPP_WORKFLOW_ENABLED true
    set_env LSPP_WORKFLOW_ALLOWED_EMAILS "$email"
    set_env LSPP_MODEL_ROUTES_FILE "$ROUTES_CONTAINER"
    set_env LSPP_WORKFLOW_ALLOW_REMOTE_MODELS false
    set_env LSPP_WORKFLOW_ALLOW_PRINT false
    echo "Configured for $email via $url. Takes effect on the next deploy.sh run."
    ;;
  *) echo "usage: $0 discover | enable <email> [ollama-url]" >&2; exit 2 ;;
esac
