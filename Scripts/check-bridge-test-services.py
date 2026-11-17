#!/usr/bin/env python3
"""Fail fast when real isolated bridge integrations are unavailable; never fake upstreams."""
import os
import sys
from urllib.parse import urlsplit

required = ("SPP_EXCEL_SERVICE_URL", "SPP_EXCEL_CORE_URL", "SPP_OSCLX_URL",
            "SPP_OSCLX_API_KEY", "SPP_OSCLX_HMAC_SECRET")
missing = [key for key in required if not os.environ.get(key)]
errors = []
if missing:
    errors.append("Missing isolated bridge test configuration: " + ", ".join(missing))
for key in required[:3]:
    value = os.environ.get(key)
    if not value:
        continue
    try:
        uri = urlsplit(value)
        if uri.scheme != "http" or uri.hostname not in ("127.0.0.1", "::1", "localhost") or not uri.port:
            errors.append(f"{key} must target a dedicated loopback HTTP service with an explicit port")
    except ValueError:
        errors.append(f"{key} is not a valid loopback URL")
if errors:
    print("BRIDGE PREFLIGHT FAILED: actual isolated Excel, Excel Core and OS-CLX services must be started before tests.", file=sys.stderr)
    for error in errors:
        print(error, file=sys.stderr)
    sys.exit(1)
print("Bridge configuration present. This does NOT prove upstream readiness; integration tests must execute.")
