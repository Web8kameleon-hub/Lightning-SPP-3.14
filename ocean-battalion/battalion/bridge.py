"""Credentials and URL construction for the repository's real services.

The contracts here are not invented; they mirror `Integrations/BridgeConnection.cs`:

* plain services take ``Authorization: Bearer <key>``;
* OS-CLX takes ``X-API-Key`` plus ``X-CLX-Role``, and signs writes with
  ``X-CLX-Timestamp`` / ``X-CLX-Signature`` over
  ``METHOD\\nPATH\\nTIMESTAMP\\nSHA256(body)`` using HMAC-SHA256.

The battalion only performs reads, so the signature path is implemented and
tested but never used for a crawl. A base URL is required to be HTTPS (or
loopback HTTP) and must not carry credentials, a query string or a fragment —
the same refusals the .NET transport makes.
"""

from __future__ import annotations

import hashlib
import hmac
import os
from dataclasses import dataclass
from urllib.parse import urljoin, urlparse

LOOPBACK_HOSTS = frozenset({"localhost", "127.0.0.1", "::1"})


class BridgeConfigError(RuntimeError):
    """A bridge source is not usable as configured; nothing was fetched."""


@dataclass(frozen=True)
class BridgeTarget:
    """A resolved service endpoint plus the headers its contract needs."""

    base_url: str
    headers: dict[str, str]

    @property
    def role(self) -> str:
        return self.headers.get("X-CLX-Role", "")


def resolve_base_url(environment_variable: str) -> str:
    """Read a base URL from the environment and validate it like the .NET side."""
    raw = os.environ.get(environment_variable, "").strip()
    if not raw:
        raise BridgeConfigError(
            f"{environment_variable} is not set; point it at the real service"
        )

    parsed = urlparse(raw)
    if not parsed.scheme or not parsed.netloc:
        raise BridgeConfigError(f"{environment_variable} is not an absolute URL")

    if parsed.scheme == "https":
        pass
    elif parsed.scheme == "http" and parsed.hostname in LOOPBACK_HOSTS:
        pass
    else:
        raise BridgeConfigError(
            f"{environment_variable} must use HTTPS (or HTTP on loopback)"
        )

    if parsed.username or parsed.password:
        raise BridgeConfigError(
            f"{environment_variable} must not embed credentials"
        )
    if parsed.query or parsed.fragment:
        raise BridgeConfigError(
            f"{environment_variable} must not contain a query or fragment"
        )

    return raw.rstrip("/")


def build_url(base_url: str, path: str) -> str:
    """Join a base URL and a relative path, refusing absolute overrides."""
    if "://" in path:
        raise BridgeConfigError("bridge path must be relative to the base URL")
    return urljoin(base_url.rstrip("/") + "/", path.lstrip("/"))


def bearer_headers(key_environment: str | None) -> dict[str, str]:
    """``Authorization: Bearer`` for services that use it (Excel, generic)."""
    if not key_environment:
        return {}
    key = os.environ.get(key_environment, "").strip()
    if not key:
        return {}
    _reject_control_characters(key, key_environment)
    return {"Authorization": f"Bearer {key}"}


def clx_headers(
    key_environment: str | None,
    role: str = "viewer",
) -> dict[str, str]:
    """OS-CLX read headers: ``X-API-Key`` and an explicit role.

    The role is ``viewer`` for reads and ``operator`` for writes; the battalion
    only reads, and never promotes a node's trust.
    """
    if role not in ("viewer", "operator"):
        raise BridgeConfigError(f"unsupported OS-CLX role: {role!r}")

    headers = {"X-CLX-Role": role}
    if not key_environment:
        return headers

    key = os.environ.get(key_environment, "").strip()
    if key:
        _reject_control_characters(key, key_environment)
        headers["X-API-Key"] = key
    return headers


def sign_clx_request(
    secret: str,
    method: str,
    path: str,
    body: bytes = b"",
    timestamp: str = "0",
) -> dict[str, str]:
    """Reproduce OS-CLX ``HMACGuard`` headers for a write request.

    Message format, byte for byte: ``METHOD\\nPATH\\nTIMESTAMP\\nSHA256(body)``,
    each hash lower-case hex, signature HMAC-SHA256 lower-case hex.
    """
    body_hash = hashlib.sha256(body).hexdigest()
    message = f"{method.upper()}\n{path}\n{timestamp}\n{body_hash}"
    signature = hmac.new(
        secret.encode("utf-8"),
        message.encode("utf-8"),
        hashlib.sha256,
    ).hexdigest()
    return {"X-CLX-Timestamp": timestamp, "X-CLX-Signature": signature}


def _reject_control_characters(value: str, description: str) -> None:
    if any(ord(character) < 32 for character in value):
        raise BridgeConfigError(f"{description} contains control characters")


def target_for(
    *,
    base_url_env: str,
    auth: str = "none",
    key_env: str | None = None,
) -> BridgeTarget:
    """Build the target for one source: URL plus the headers its contract wants."""
    base_url = resolve_base_url(base_url_env)

    if auth == "none":
        headers: dict[str, str] = {}
    elif auth == "bearer":
        headers = bearer_headers(key_env)
    elif auth == "clx":
        headers = clx_headers(key_env)
    else:
        raise BridgeConfigError(f"unknown auth mode: {auth!r}")

    return BridgeTarget(base_url=base_url, headers=headers)
