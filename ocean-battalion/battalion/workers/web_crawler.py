"""Web agent: one site, breadth-first, polite.

It stays on the seed host (a crawler that wanders across the internet is not a
source, it is an incident), respects robots.txt for every URL it touches, and
hands both HTML pages and PDFs to the ingestor — the PDF path is where OCR
earns its place, because older government documents are scans.
"""

from __future__ import annotations

from urllib.parse import urljoin, urlparse

from battalion.workers.base import AgentResult, BaseAgent

# Never worth fetching for text extraction.
_SKIP_SUFFIXES = (
    ".jpg", ".jpeg", ".png", ".gif", ".svg", ".webp",
    ".zip", ".gz", ".rar", ".7z",
    ".mp3", ".mp4", ".avi", ".mov",
    ".exe", ".msi", ".dmg",
    ".css", ".js", ".ico", ".woff", ".woff2",
)


class WebCrawler(BaseAgent):
    """Breadth-first within the seed host, with a hard page budget."""

    def run(self) -> AgentResult:
        max_pages = int(self.source.get("max_pages", 50))
        follow = bool(self.source.get("follow_links", True))
        allow_pdf = bool(self.source.get("allow_pdf", True))

        queue = list(self.source.list_of("seeds"))
        visited: set[str] = set()

        while queue and self.result.fetched < max_pages:
            url = queue.pop(0)
            if url in visited:
                continue
            visited.add(url)

            response = self.fetch(url)
            if response is None:
                continue

            if response.is_pdf:
                if not allow_pdf:
                    self.result.skipped += 1
                    self.result.note(f"PDF ignored (allow_pdf=false): {url}")
                    continue
                self.ingest(response, ocr=self.source.requires_ocr)
                continue

            self.ingest(response)

            if follow:
                queue.extend(self._links(response, url, visited, allow_pdf))

        self.result.note(
            f"visited {len(visited)} URLs, fetched {self.result.fetched}"
        )
        return self.result

    def _links(
        self,
        response,
        page_url: str,
        visited: set[str],
        allow_pdf: bool,
    ) -> list[str]:
        from bs4 import BeautifulSoup

        host = urlparse(page_url).netloc
        try:
            soup = BeautifulSoup(response.text, "html.parser")
        except Exception:  # malformed HTML must not end the run
            return []

        found: list[str] = []
        for anchor in soup.find_all("a", href=True):
            link = urljoin(page_url, anchor["href"]).split("#")[0]
            parsed = urlparse(link)
            if parsed.scheme not in ("http", "https"):
                continue
            if parsed.netloc != host:
                continue
            if link in visited:
                continue
            lowered = parsed.path.lower()
            if lowered.endswith(_SKIP_SUFFIXES):
                continue
            if lowered.endswith(".pdf") and not allow_pdf:
                continue
            found.append(link)
        return found
