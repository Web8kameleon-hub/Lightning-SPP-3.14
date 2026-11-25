"""Open-data agent for CKAN portals (`/api/3/action/package_search`).

This is the cleanest government path there is: a published API, an explicit
licence per dataset, and files listed with their format. The agent reads the
catalogue once, then fetches only formats the extractor can actually turn into
text — plus PDFs, which is where Albanian public bodies still publish most.
"""

from __future__ import annotations

import json

from battalion.fetch import Response
from battalion.workers.base import AgentResult, BaseAgent

SUPPORTED_FORMATS = frozenset(
    {"PDF", "HTML", "HTM", "TXT", "JSON", "CSV", "XML", "RDF"}
)

_TEXT_FORMATS = frozenset({"TXT", "JSON", "CSV", "XML", "RDF"})


class OpenDataAgent(BaseAgent):
    """Walks a CKAN catalogue and ingests the resources it can read."""

    def run(self) -> AgentResult:
        endpoint = str(self.source.get("endpoint", "")).rstrip("/")
        if not endpoint:
            self.result.failed += 1
            self.result.note("no endpoint configured")
            return self.result

        query_url = (
            f"{endpoint}/api/3/action/package_search"
            f"?rows={int(self.source.get('max_datasets', 20))}"
        )
        response = self.fetch(query_url)
        if response is None:
            return self.result

        datasets, problem = _datasets(response.content)
        if datasets is None:
            self.result.failed += 1
            self.result.note(f"catalogue at {endpoint} is not CKAN JSON: {problem}")
            return self.result

        self.result.note(f"{endpoint}: {len(datasets)} datasets")

        for dataset in datasets:
            for resource in dataset.get("resources", []) or []:
                url = resource.get("url")
                if not url:
                    continue
                fmt = (resource.get("format") or "").upper()
                if fmt not in SUPPORTED_FORMATS:
                    self.result.skipped += 1
                    continue

                fetched = self.fetch(url)
                if fetched is None:
                    continue

                if fmt == "PDF":
                    self.ingest(fetched, ocr=self.source.requires_ocr)
                elif fmt in _TEXT_FORMATS:
                    self._ingest_plain_text(fetched, resource)
                else:
                    self.ingest(fetched)

        return self.result

    def _ingest_plain_text(self, response: Response, resource: dict) -> None:
        """CSV/JSON/XML have no markup to strip; pass them through as-is."""
        self.ingest(
            Response(
                url=response.url,
                status=response.status,
                content=response.content,
                headers={"content-type": "text/plain; charset=utf-8"},
            )
        )


def _datasets(body: bytes) -> tuple[list[dict] | None, str]:
    """Return the dataset list, plus the reason when it is not CKAN.

    The reason is carried into the run report: "not CKAN JSON" on its own sends
    an operator to the wrong place when the real cause is a login page.
    """
    try:
        payload = json.loads(body.decode("utf-8", errors="replace"))
    except json.JSONDecodeError as error:
        return None, f"not JSON ({error.msg} at {error.pos})"

    if not isinstance(payload, dict):
        return None, f"JSON is a {type(payload).__name__}, not an object"
    if not payload.get("success"):
        return None, "response has no 'success': true"
    result = payload.get("result") or {}
    datasets = result.get("results")
    if not isinstance(datasets, list):
        return None, "'result.results' is not a list"
    return datasets, ""
