"""Crawl discipline: robots.txt, rate limiting and licence gating.

Everything in the battalion asks this module before it fetches anything. The
rules are deliberately conservative and fail closed:

* if robots.txt cannot be read, the URL is not fetched;
* if a source declares no licence, it is recorded as `unknown` rather than
  silently promoted to `open`;
* pacing is per source, not global, so one slow government site cannot stall
  the rest of the battalion.
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Callable, Mapping
from urllib.parse import urlparse

ROBOTS_AGENT = "*"

# Licences we are willing to crawl without a human looking at them first.
CLEARED_LICENCES = frozenset({"open", "public-domain", "cc-by", "cc-by-sa", "rss"})


class RobotsUnavailable(RuntimeError):
    """robots.txt could not be read, so the crawler refuses to proceed."""


@dataclass
class RateLimiter:
    """Paces requests for one source. Injected clock keeps tests instant."""

    requests_per_second: float
    _clock: Callable[[], float] = field(default=time.monotonic, repr=False)
    _last: float | None = field(default=None, init=False, repr=False)

    def __post_init__(self) -> None:
        if self.requests_per_second <= 0:
            raise ValueError("requests_per_second must be positive")

    @property
    def min_interval(self) -> float:
        return 1.0 / self.requests_per_second

    def wait_seconds(self) -> float:
        """How long the caller must wait before the next request."""
        now = self._clock()
        if self._last is None:
            self._last = now
            return 0.0
        elapsed = now - self._last
        remaining = self.min_interval - elapsed
        if remaining <= 0:
            self._last = now
            return 0.0
        self._last = now + remaining
        return remaining


class Policy:
    """Answers 'may I fetch this?' for one source."""

    def __init__(
        self,
        *,
        respect_robots: bool = True,
        license: str = "unknown",
        rate_limit: float = 0.5,
        robots_fetcher: Callable[[str], str | None] | None = None,
    ) -> None:
        self.respect_robots = respect_robots
        self.license = (license or "unknown").strip().lower()
        self.limiter = RateLimiter(rate_limit)
        self._robots_fetcher = robots_fetcher or _fetch_robots
        self._robots: dict[str, object | None] = {}
        self.refused: list[tuple[str, str]] = []

    # -- licence -----------------------------------------------------------
    @property
    def licence_cleared(self) -> bool:
        return self.license in CLEARED_LICENCES

    def refusal_reason(self, url: str) -> str | None:
        """Return why `url` may not be fetched, or None when it may."""
        if not self.respect_robots:
            return "robots.txt checking disabled by configuration"
        parsed = urlparse(url)
        if parsed.scheme not in ("http", "https"):
            return f"unsupported scheme: {parsed.scheme or 'none'}"
        if not parsed.netloc:
            return "URL has no host"
        try:
            parser = self._parser_for(parsed.netloc, parsed.scheme)
        except RobotsUnavailable as error:
            return str(error)
        if parser is None:
            return None
        if not parser.can_fetch(ROBOTS_AGENT, url):  # type: ignore[attr-defined]
            return "disallowed by robots.txt"
        return None

    def allowed(self, url: str) -> bool:
        reason = self.refusal_reason(url)
        if reason is None:
            return True
        self.refused.append((url, reason))
        return False

    def _parser_for(self, netloc: str, scheme: str):
        if netloc in self._robots:
            return self._robots[netloc]

        import urllib.robotparser as robotparser

        robots_url = f"{scheme}://{netloc}/robots.txt"
        body = self._robots_fetcher(robots_url)
        if body is None:
            self._robots[netloc] = None
            return None

        parser = robotparser.RobotFileParser()
        parser.parse(body.splitlines())
        self._robots[netloc] = parser
        return parser


def _fetch_robots(url: str) -> str | None:
    """Fetch robots.txt. A missing file means 'no restrictions'; a failure does not."""
    import httpx

    try:
        response = httpx.get(url, timeout=10.0, follow_redirects=True)
    except Exception as error:  # network error: fail closed
        raise RobotsUnavailable(f"robots.txt unreachable ({url}): {error}") from error

    if response.status_code >= 400:
        # 4xx means no robots file exists, which is a permissive answer.
        return None
    return response.text


def policy_for_source(source, robots_fetcher=None) -> Policy:
    """Build the policy that belongs to one configured source."""
    return Policy(
        respect_robots=bool(source.get("respect_robots", True)),
        license=source.license,
        rate_limit=source.rate_limit,
        robots_fetcher=robots_fetcher,
    )


def summarise(licences: Mapping[str, str]) -> str:
    """Human-readable licence spread, used by the dashboard header."""
    counts: dict[str, int] = {}
    for value in licences.values():
        counts[value] = counts.get(value, 0) + 1
    parts = [f"{name}={count}" for name, count in sorted(counts.items())]
    return " ".join(parts) if parts else "no sources"
