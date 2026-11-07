"""Loading and validation of the source registry (`sources.yaml`).

The registry is the heart of the battalion: every agent, its rate limit, its
refresh interval and its licensing status come from here. Configuration that
cannot be understood is rejected at load time — a crawler that silently skips a
source it was told to watch is worse than one that refuses to start.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Any, Mapping

import yaml


class ConfigError(ValueError):
    """Raised when the source registry is missing, malformed or inconsistent."""


KNOWN_TYPES = frozenset(
    {"rss", "web", "opendata", "api_google", "api_graph",
     "bridge", "clx_node", "excel"}
)

# Which keys each agent type refuses to start without.
REQUIRED_BY_TYPE: Mapping[str, tuple[str, ...]] = {
    "rss": ("feeds",),
    "web": ("seeds",),
    "opendata": ("endpoint",),
    "api_google": ("queries",),
    "api_graph": ("site",),
    # The repository's own services. `bridge` needs an endpoint to read;
    # `clx_node` needs node ids; `excel` needs a URL to name in its refusal.
    "bridge": ("base_url_env", "paths"),
    "clx_node": ("nodes",),
    "excel": ("base_url_env",),
}


def parse_rate(value: Any) -> float:
    """Turn `2/s`, `0.5/s`, `30/m` or a plain number into requests per second.

    Anything indeterminate is refused instead of defaulting to "no limit": an
    accidental unlimited crawler is exactly how a battalion gets an IP banned.
    """
    if isinstance(value, bool):
        raise ConfigError(f"rate_limit must not be a boolean: {value!r}")
    if isinstance(value, (int, float)):
        rate = float(value)
    elif isinstance(value, str):
        text = value.strip().lower()
        if not text:
            raise ConfigError("rate_limit must not be empty")
        try:
            if text.endswith("/s"):
                rate = float(text[:-2])
            elif text.endswith("/m"):
                rate = float(text[:-2]) / 60.0
            else:
                rate = float(text)
        except ValueError as error:
            raise ConfigError(f"rate_limit is not a number: {value!r}") from error
    else:
        raise ConfigError(f"rate_limit has unsupported type: {type(value).__name__}")

    if rate <= 0:
        raise ConfigError(f"rate_limit must be positive, got {value!r}")
    return rate


@dataclass(frozen=True)
class Defaults:
    respect_robots: bool = True
    rate_limit: float = 0.5
    refresh: str = "7d"
    license: str = "unknown"
    user_agent: str = "OceanBattalion/0.1 (+https://example.invalid/contact)"


@dataclass(frozen=True)
class Source:
    """One configured source, with the defaults already resolved into it."""

    id: str
    type: str
    options: Mapping[str, Any] = field(default_factory=dict)

    def get(self, key: str, default: Any = None) -> Any:
        return self.options.get(key, default)

    def list_of(self, key: str) -> list[str]:
        value = self.options.get(key, [])
        if isinstance(value, str):
            return [value]
        return [str(item) for item in value]

    @property
    def rate_limit(self) -> float:
        return float(self.options["rate_limit"])

    @property
    def refresh(self) -> str:
        return str(self.options["refresh"])

    @property
    def requires_ocr(self) -> bool:
        return bool(self.options.get("ocr", False))

    @property
    def license(self) -> str:
        return str(self.options["license"])


@dataclass(frozen=True)
class Config:
    defaults: Defaults
    sources: tuple[Source, ...]

    def by_id(self, source_id: str) -> Source:
        for source in self.sources:
            if source.id == source_id:
                return source
        raise KeyError(source_id)


def load_config(path: str | Path) -> Config:
    """Read `sources.yaml` and refuse anything that would crawl by accident."""
    file = Path(path)
    if not file.is_file():
        raise ConfigError(f"source registry not found: {file}")

    try:
        raw = yaml.safe_load(file.read_text(encoding="utf-8"))
    except yaml.YAMLError as error:  # pragma: no cover - depends on input file
        raise ConfigError(f"{file} is not valid YAML: {error}") from error

    if not isinstance(raw, dict):
        raise ConfigError(f"{file} must contain a mapping at the top level")

    defaults = _parse_defaults(raw.get("defaults") or {})
    entries = raw.get("sources")
    if not isinstance(entries, list) or not entries:
        raise ConfigError(f"{file} must define a non-empty 'sources' list")

    seen: set[str] = set()
    sources: list[Source] = []
    for index, entry in enumerate(entries):
        source = _parse_source(entry, index, defaults)
        if source.id in seen:
            raise ConfigError(f"duplicate source id: {source.id}")
        seen.add(source.id)
        sources.append(source)

    return Config(defaults=defaults, sources=tuple(sources))


def _parse_defaults(raw: Any) -> Defaults:
    if not isinstance(raw, dict):
        raise ConfigError("'defaults' must be a mapping")

    return Defaults(
        respect_robots=bool(raw.get("respect_robots", True)),
        rate_limit=parse_rate(raw.get("rate_limit", "0.5/s")),
        refresh=str(raw.get("refresh", "7d")),
        license=str(raw.get("license", "unknown")),
        user_agent=str(raw.get("user_agent", Defaults.user_agent)),
    )


def _parse_source(entry: Any, index: int, defaults: Defaults) -> Source:
    if not isinstance(entry, dict):
        raise ConfigError(f"sources[{index}] must be a mapping")

    source_id = entry.get("id")
    if not isinstance(source_id, str) or not source_id.strip():
        raise ConfigError(f"sources[{index}] has no usable 'id'")

    source_type = entry.get("type")
    if source_type not in KNOWN_TYPES:
        raise ConfigError(
            f"source '{source_id}' has unknown type {source_type!r}; "
            f"known types: {', '.join(sorted(KNOWN_TYPES))}"
        )

    for required in REQUIRED_BY_TYPE[source_type]:
        value = entry.get(required)
        if value is None or (isinstance(value, (list, tuple)) and not value):
            raise ConfigError(f"source '{source_id}' ({source_type}) is missing '{required}'")

    options: dict[str, Any] = {
        "rate_limit": parse_rate(entry.get("rate_limit", defaults.rate_limit)),
        "refresh": str(entry.get("refresh", defaults.refresh)),
        "license": str(entry.get("license", defaults.license)),
        "respect_robots": bool(entry.get("respect_robots", defaults.respect_robots)),
        "user_agent": str(entry.get("user_agent", defaults.user_agent)),
    }
    options.update({key: value for key, value in entry.items() if key not in options})

    return Source(id=source_id, type=source_type, options=options)
