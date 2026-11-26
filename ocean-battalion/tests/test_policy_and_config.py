"""Policy and registry: the two places where a battalion stays legal."""

from __future__ import annotations

import pytest

from battalion.config import ConfigError, load_config, parse_rate
from battalion.policy import Policy, RateLimiter


def test_rate_parsing_accepts_the_written_forms():
    assert parse_rate("2/s") == 2.0
    assert parse_rate("30/m") == pytest.approx(0.5)
    assert parse_rate(0.25) == 0.25


@pytest.mark.parametrize("bad", ["", "0/s", -1, "abc", None])
def test_rate_parsing_refuses_nonsense(bad):
    with pytest.raises(ConfigError):
        parse_rate(bad)


def test_registry_rejects_unknown_type(tmp_path):
    registry = tmp_path / "sources.yaml"
    registry.write_text(
        "sources:\n  - id: x\n    type: telepathy\n    seeds: [a]\n",
        encoding="utf-8",
    )

    with pytest.raises(ConfigError, match="unknown type"):
        load_config(registry)


def test_registry_rejects_duplicate_ids(tmp_path):
    registry = tmp_path / "sources.yaml"
    registry.write_text(
        "sources:\n"
        "  - id: same\n    type: rss\n    feeds: [a]\n"
        "  - id: same\n    type: rss\n    feeds: [b]\n",
        encoding="utf-8",
    )

    with pytest.raises(ConfigError, match="duplicate"):
        load_config(registry)


def test_registry_requires_the_key_the_agent_needs(tmp_path):
    registry = tmp_path / "sources.yaml"
    registry.write_text(
        "sources:\n  - id: x\n    type: opendata\n    max_datasets: 5\n",
        encoding="utf-8",
    )

    with pytest.raises(ConfigError, match="endpoint"):
        load_config(registry)


def test_defaults_flow_into_each_source(tmp_path):
    registry = tmp_path / "sources.yaml"
    registry.write_text(
        "defaults:\n"
        "  rate_limit: '1/s'\n"
        "  license: open\n"
        "sources:\n"
        "  - id: a\n    type: rss\n    feeds: [x]\n"
        "  - id: b\n    type: rss\n    feeds: [y]\n    rate_limit: '0.1/s'\n",
        encoding="utf-8",
    )

    config = load_config(registry)
    assert config.by_id("a").rate_limit == 1.0
    assert config.by_id("a").license == "open"
    assert config.by_id("b").rate_limit == 0.1


def _policy(robots: str | None, **kwargs) -> Policy:
    return Policy(robots_fetcher=lambda _url: robots, **kwargs)


def test_robots_disallow_blocks_the_url():
    policy = _policy("User-agent: *\nDisallow: /private\n")

    assert policy.allowed("https://example.test/public")
    assert not policy.allowed("https://example.test/private/page")
    assert policy.refused[0][1] == "disallowed by robots.txt"


def test_missing_robots_file_is_permissive():
    policy = _policy(None)

    assert policy.allowed("https://example.test/anything")


def test_unreachable_robots_file_fails_closed():
    def boom(_url: str) -> str | None:
        from battalion.policy import RobotsUnavailable

        raise RobotsUnavailable("robots.txt unreachable")

    policy = Policy(robots_fetcher=boom)

    assert not policy.allowed("https://example.test/page")


def test_non_http_scheme_is_refused():
    policy = _policy("")

    assert not policy.allowed("ftp://example.test/file")
    assert not policy.allowed("mailto:someone@example.test")


def test_rate_limiter_paces_without_sleeping_in_tests():
    clock = {"now": 100.0}
    limiter = RateLimiter(2.0, _clock=lambda: clock["now"])

    assert limiter.wait_seconds() == 0.0
    assert limiter.wait_seconds() == pytest.approx(0.5)

    clock["now"] += 1.0
    assert limiter.wait_seconds() == 0.0


def test_uncleared_licence_is_reported_not_assumed_open():
    policy = Policy(robots_fetcher=lambda _url: None, license="unknown")

    assert policy.licence_cleared is False

    cleared = Policy(robots_fetcher=lambda _url: None, license="open")
    assert cleared.licence_cleared is True
