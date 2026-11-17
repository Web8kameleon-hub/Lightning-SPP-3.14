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
    """Real network access with per-I/O timeout and bounded, uncompressed response bytes."""

    def __init__(self, user_agent: str, timeout: float = 30.0, max_bytes: int = 8 * 1024 * 1024) -> None:
        import math
        if not math.isfinite(timeout) or timeout <= 0 or type(max_bytes) is not int or not 1 <= max_bytes <= 64 * 1024 * 1024:
            raise ValueError("Fetcher timeout and response budget must be positive and bounded")
        self.user_agent = user_agent
        self.timeout = timeout
        self.max_bytes = max_bytes

    def get(
        self,
        url: str,
        headers: Mapping[str, str] | None = None,
    ) -> Response:
        import httpx

        merged = {"User-Agent": self.user_agent, "Accept": "*/*"}
        if headers:
            merged.update(headers)
        merged["Accept-Encoding"] = "identity"
        try:
            with httpx.Client(
                follow_redirects=False,  # a redirect would escape the policy check
                timeout=self.timeout,
                headers=merged,
            ) as client:
                with client.stream("GET", url) as result:
                    if result.headers.get("content-encoding", "identity").lower() != "identity":
                        raise FetchError("Unsupported encoded HTTP body; no unbounded transparent decompression is allowed")
                    chunks = bytearray()
                    for chunk in result.iter_raw(chunk_size=8192):
                        if len(chunks) + len(chunk) > self.max_bytes:
                            raise FetchError("HTTP response exceeds its configured byte budget")
                        chunks.extend(chunk)
                    return Response(url=str(result.url), status=result.status_code, content=bytes(chunks),
                                    headers={k.lower(): v for k, v in result.headers.items()})
        except Exception as error:
            raise FetchError(f"{url}: {error}") from error
