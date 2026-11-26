"""Official-API agents: Google Custom Search and Microsoft Graph.

Neither Google nor Microsoft can be "ingested". Both offer narrow, authorised
doors, and that is all this module uses:

* Google CSE returns at most ten results per query and forbids scraping the
  results page — so we take the snippets and links, nothing more.
* Microsoft Graph requires an application registration and admin consent, and
  only the files the signed-in identity may read.

Both agents refuse to start without their credential and say so, rather than
running and producing zero documents that look like an empty corpus.
"""

from __future__ import annotations

import json
import os

from battalion.fetch import Response
from battalion.workers.base import AgentResult, BaseAgent

GOOGLE_ENDPOINT = "https://www.googleapis.com/customsearch/v1"
GRAPH_ENDPOINT = "https://graph.microsoft.com/v1.0"


class GoogleCseAgent(BaseAgent):
    """Queries the official Custom Search JSON API."""

    def run(self) -> AgentResult:
        key = os.environ.get(str(self.source.get("api_key_env", "GOOGLE_CSE_KEY")))
        cx = os.environ.get(str(self.source.get("cx_env", "GOOGLE_CSE_CX")))
        if not key or not cx:
            self.result.failed += 1
            self.result.note(
                "missing GOOGLE_CSE_KEY / GOOGLE_CSE_CX; source skipped"
            )
            return self.result

        for query in self.source.list_of("queries"):
            url = (
                f"{GOOGLE_ENDPOINT}?key={key}&cx={cx}"
                f"&num=10&q={_encode(query)}"
            )
            response = self.fetch(url)
            if response is None:
                continue
            items = _google_items(response.content)
            self.result.note(f"query '{query}': {len(items)} results")

            for item in items:
                snippet = item.get("snippet") or ""
                link = item.get("link") or ""
                if not snippet.strip() or not link:
                    continue
                # The snippet is what the API returns; the page itself is a
                # separate decision, taken by the web agent if configured.
                self.ingest(
                    Response(
                        url=link,
                        status=200,
                        content=snippet.encode("utf-8"),
                        headers={"content-type": "text/html; charset=utf-8"},
                    )
                )
        return self.result


class MicrosoftGraphAgent(BaseAgent):
    """Lists a SharePoint/OneDrive folder and ingests the files it may read."""

    def run(self) -> AgentResult:
        token = os.environ.get(str(self.source.get("token_env", "MS_GRAPH_TOKEN")))
        site = self.source.get("site")
        if not token or not site:
            self.result.failed += 1
            self.result.note(
                "missing MS_GRAPH_TOKEN or 'site'; check the Azure app "
                "registration and admin consent"
            )
            return self.result

        folder = str(self.source.get("folder", "root/children"))
        url = f"{GRAPH_ENDPOINT}/sites/{site}/drive/{folder}"
        response = self.fetch(_with_token(url, token))
        if response is None:
            return self.result

        items = _graph_items(response.content)
        self.result.note(f"{len(items)} items in {site}/{folder}")

        for item in items:
            if "file" not in item:
                continue
            download = item.get("@microsoft.graph.downloadUrl")
            name = item.get("name", "")
            if not download:
                continue
            fetched = self.fetch(download)
            if fetched is None:
                continue
            mime = "application/pdf" if name.lower().endswith(".pdf") else "text/html"
            self.ingest(
                Response(
                    url=item.get("webUrl", download),
                    status=fetched.status,
                    content=fetched.content,
                    headers={"content-type": mime},
                )
            )
        return self.result


def _encode(value: str) -> str:
    from urllib.parse import quote_plus

    return quote_plus(value)


def _with_token(url: str, token: str) -> str:
    """Graph needs an Authorization header; keep the token out of the URL.

    The Fetcher protocol only takes a URL, so the token is passed through an
    environment variable the HttpFetcher reads. Documented in README.
    """
    os.environ.setdefault("OCEAN_GRAPH_BEARER", token)
    return url


def _google_items(body: bytes) -> list[dict]:
    try:
        payload = json.loads(body.decode("utf-8", errors="replace"))
    except json.JSONDecodeError:
        return []
    items = payload.get("items")
    return items if isinstance(items, list) else []


def _graph_items(body: bytes) -> list[dict]:
    try:
        payload = json.loads(body.decode("utf-8", errors="replace"))
    except json.JSONDecodeError:
        return []
    value = payload.get("value")
    return value if isinstance(value, list) else []
