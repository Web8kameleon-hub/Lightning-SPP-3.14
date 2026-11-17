"""Run the battalion with no network, no model download and no database.

Useful for two things: proving the wiring works on a fresh machine, and
showing exactly what a run report looks like. Every source below is served
from a canned response, so the output is deterministic.
"""

from __future__ import annotations

import sys
import tempfile
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
if str(ROOT) not in sys.path:
    sys.path.insert(0, str(ROOT))

from battalion.config import load_config
from battalion_test_fetcher import RecordedFetcher
from battalion.orchestrator import Orchestrator
from battalion.pipeline.ingest import ChunkOptions, offline_ingestor
from battalion.policy import Policy
from battalion.storage.jsonl_store import JsonlStore

FEED = """<?xml version="1.0"?>
<rss version="2.0"><channel>
  <item>
    <title>Raporti vjetor</title>
    <link>https://demo.test/raporti</link>
    <description>Totali i shpenzimeve ishte 1299.00 leke.</description>
  </item>
</channel></rss>
"""

PAGE = (
    "<html><body><h1>Dokumente zyrtare</h1>"
    "<p>Ky seksion permban dokumentet e publikuara.</p>"
    "<a href='/files/udhezimi.pdf'>udhezimi</a></body></html>"
)

CKAN = (
    '{"success": true, "result": {"results": ['
    '{"name": "buxheti", "resources": ['
    '{"url": "https://demo.test/buxheti.csv", "format": "CSV"}'
    "]}]}}"
)


def main() -> int:
    with tempfile.TemporaryDirectory(prefix="ocean-demo-") as workdir:
        root = Path(workdir)
        registry = root / "sources.yaml"
        registry.write_text(
            "defaults:\n"
            "  rate_limit: '100/s'\n"
            "  license: open\n"
            "sources:\n"
            "  - id: demo-feed\n    type: rss\n"
            "    feeds: [https://demo.test/feed]\n"
            "  - id: demo-site\n    type: web\n"
            "    seeds: [https://demo.test/]\n"
            "    max_pages: 2\n"
            "  - id: demo-opendata\n    type: opendata\n"
            "    endpoint: https://demo.test\n",
            encoding="utf-8",
        )

        fetcher = RecordedFetcher(
            {
                "https://demo.test/feed": FEED,
                "https://demo.test/": PAGE,
                "https://demo.test/api/3/action/package_search?rows=20": CKAN,
                "https://demo.test/buxheti.csv": "kategoria,shuma\narsimi,1299.00\n",
            }
        )

        store = JsonlStore(root / "chunks.jsonl")
        ingestor = offline_ingestor(
            store,
            options=ChunkOptions(target=256, hard_max=512, overlap=32),
        )
        orchestrator = Orchestrator(
            load_config(registry),
            ingestor,
            fetcher_factory=lambda _source: fetcher,
            policy_factory=lambda _source: Policy(
                robots_fetcher=lambda _url: None,
                rate_limit=100.0,
            ),
            log=lambda _message: None,
        )

        report = orchestrator.run()
        print(report.render())
        print()

        for result in report.results:
            for note in result.notes:
                print(f"  {result.source_id}: {note}")
        print()

        stats = store.stats()
        print(f"stored chunks: {stats.total}")
        for name in sorted(stats.per_source):
            print(f"  {name:<16} {stats.per_source[name]:>4}")
        print(f"reader spread: {stats.per_reader}")
        print(f"largest chunk: {stats.max_tokens} tokens")

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
