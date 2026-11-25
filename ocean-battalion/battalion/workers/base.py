"""What every agent shares: policy checks, pacing, and the ingest call.

An agent only knows how to *find* documents (a feed, a page, a catalogue, an
API). Everything after the bytes arrive is the ingestor's job, so a bug in
chunking or embedding cannot differ between agents.
"""

from __future__ import annotations

import time
from dataclasses import dataclass, field
from typing import Callable, Mapping

from battalion.config import Source
from battalion.fetch import Fetcher, FetchError, Response
from battalion.pipeline.ingest import IngestReport
from battalion.policy import Policy


@dataclass
class AgentResult:
    """Per-source tally. Every counter has to add up to something visible."""

    source_id: str
    type: str
    fetched: int = 0
    documents: int = 0
    chunks: int = 0
    skipped: int = 0
    refused: int = 0
    failed: int = 0
    notes: list[str] = field(default_factory=list)

    def note(self, message: str) -> None:
        self.notes.append(message)


class BaseAgent:
    """Shared plumbing. Subclasses implement :meth:`run`."""

    def __init__(
        self,
        source: Source,
        policy: Policy,
        ingestor,
        fetcher: Fetcher,
        *,
        sleep: Callable[[float], None] = time.sleep,
        log: Callable[[str], None] = print,
    ) -> None:
        self.source = source
        self.policy = policy
        self.ingestor = ingestor
        self.fetcher = fetcher
        self._sleep = sleep
        self._log = log
        self.result = AgentResult(source_id=source.id, type=source.type)

    # -- subclass contract -------------------------------------------------
    def run(self) -> AgentResult:
        raise NotImplementedError

    # -- shared steps ------------------------------------------------------
    def fetch(
        self,
        url: str,
        *,
        headers: Mapping[str, str] | None = None,
    ) -> Response | None:
        """Fetch `url` if policy allows it, honouring this source's pace.

        `headers` carries service credentials for the bridge agents. Policy is
        checked first: a credential never turns a refusal into a fetch.
        """
        reason = self.policy.refusal_reason(url)
        if reason is not None:
            self.policy.refused.append((url, reason))
            self.result.refused += 1
            self.result.note(f"refused {url}: {reason}")
            self._log(f"[{self.source.id}] refused {url}: {reason}")
            return None

        delay = self.policy.limiter.wait_seconds()
        if delay > 0:
            self._sleep(delay)

        try:
            response = self.fetcher.get(url, headers)
        except FetchError as error:
            self.result.failed += 1
            self.result.note(f"fetch failed {url}: {error}")
            self._log(f"[{self.source.id}] fetch failed {url}: {error}")
            return None

        if not response.ok:
            self.result.failed += 1
            self.result.note(f"HTTP {response.status} for {url}")
            self._log(f"[{self.source.id}] HTTP {response.status} {url}")
            return None

        self.result.fetched += 1
        return response

    def ingest(self, response: Response, *, ocr: bool | None = None) -> IngestReport | None:
        """Send a fetched document through extract → chunk → embed."""
        use_ocr = self.source.requires_ocr if ocr is None else ocr
        mime = _mime_for(response)

        try:
            report = self.ingestor.ingest(
                response.content,
                mime,
                source_id=self.source.id,
                url=response.url,
                ocr=use_ocr,
                license=self.source.license,
                pages_limit=int(self.source.get("max_ocr_pages", 32)),
            )
        except Exception as error:  # extraction/embedding failure is per-document
            self.result.failed += 1
            self.result.note(f"ingest failed {response.url}: {error}")
            self._log(f"[{self.source.id}] ingest failed {response.url}: {error}")
            return None

        if report.skipped:
            self.result.skipped += 1
            self.result.note(f"skipped {response.url}: {report.skipped_reason}")
            self._log(
                f"[{self.source.id}] skipped {response.url}: "
                f"{report.skipped_reason}"
            )
            return report

        self.result.documents += 1
        self.result.chunks += report.chunks
        self._log(
            f"[{self.source.id}] {report.chunks} chunks from {response.url} "
            f"(reader={report.reader})"
        )
        return report


def _mime_for(response: Response) -> str:
    """Use the declared content type, falling back to a structural guess.

    Forcing text/html for everything non-PDF silently mangles CSV and JSON
    resources, which are common in open-data catalogues.
    """
    declared = response.mime.split(";")[0].strip()
    if declared and declared != "application/octet-stream":
        return declared
    return "application/pdf" if response.is_pdf else "text/html"
