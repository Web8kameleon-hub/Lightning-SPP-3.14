from typing import Mapping
from battalion.fetch import FetchError, Response

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
