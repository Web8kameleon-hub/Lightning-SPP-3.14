"""HTTP access behind one small interface.

Agents never call httpx directly. They ask a ``Fetcher``, which makes the whole
battalion testable offline: a test supplies canned responses and exercises the
real policy, extraction and chunking code without touching the network.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Mapping, Protocol, runtime_checkable


class FetchError(RuntimeError):
    """A request failed; the caller records it and moves on."""


@dataclass(frozen=True)
class Response:
    url: str
    status: int
    content: bytes
    headers: Mapping[str, str] = field(default_factory=dict)

    @property
    def ok(self) -> bool:
        return 200 <= self.status < 300

    @property
    def text(self) -> str:
        return self.content.decode(self.encoding, errors="replace")

    @property
    def encoding(self) -> str:
        content_type = self.mime
        marker = "charset="
        if marker in content_type:
            return content_type.split(marker, 1)[1].split(";")[0].strip() or "utf-8"
        return "utf-8"

    @property
    def mime(self) -> str:
        for name, value in self.headers.items():
            if name.lower() == "content-type":
                return value.lower()
        return ""

    @property
    def is_pdf(self) -> bool:
        return "pdf" in self.mime or self.url.lower().endswith(".pdf")

    @property
    def is_html(self) -> bool:
        if self.is_pdf:
            return False
        return "html" in self.mime or self.content[:512].lstrip()[:5] == b"<!doc"


@runtime_checkable
class Fetcher(Protocol):
    def get(
        self,
        url: str,
        headers: Mapping[str, str] | None = None,
    ) -> Response: ...


class HttpFetcher:
    """Real network access, with a declared identity and a hard timeout."""

    def __init__(self, user_agent: str, timeout: float = 30.0) -> None:
        self.user_agent = user_agent
        self.timeout = timeout

    def get(
        self,
        url: str,
        headers: Mapping[str, str] | None = None,
    ) -> Response:
        import httpx

        merged = {"User-Agent": self.user_agent, "Accept": "*/*"}
        if headers:
            merged.update(headers)
        try:
            with httpx.Client(
                follow_redirects=False,  # a redirect would escape the policy check
                timeout=self.timeout,
                headers=merged,
            ) as client:
                result = client.get(url)
        except Exception as error:
            raise FetchError(f"{url}: {error}") from error

        return Response(
            url=str(result.url),
            status=result.status_code,
            content=result.content,
            headers={k.lower(): v for k, v in result.headers.items()},
        )


class RecordedFetcher:
    """Serves canned responses; every request is recorded for assertions."""

    def __init__(self, responses: Mapping[str, Response | bytes | str]) -> None:
        self._responses = dict(responses)
        self.requested: list[str] = []
        self.sent_headers: list[Mapping[str, str]] = []

    def get(
        self,
        url: str,
        headers: Mapping[str, str] | None = None,
    ) -> Response:
        self.requested.append(url)
        self.sent_headers.append(dict(headers or {}))
        if url not in self._responses:
            raise FetchError(f"no recorded response for {url}")

        value = self._responses[url]
        if isinstance(value, Response):
            return value
        if isinstance(value, str):
            return Response(url, 200, value.encode("utf-8"),
                            {"content-type": "text/html; charset=utf-8"})
        return Response(url, 200, value, {"content-type": "application/pdf"})
