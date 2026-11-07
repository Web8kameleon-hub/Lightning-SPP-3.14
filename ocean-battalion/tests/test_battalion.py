"""End to end: agents → extract → chunk → embed → store, with no network."""

from __future__ import annotations

from battalion.config import load_config
from battalion.fetch import RecordedFetcher, Response
from battalion.orchestrator import Orchestrator
from battalion.pipeline.ingest import ChunkOptions, offline_ingestor
from battalion.storage.jsonl_store import JsonlStore

FEED = """<?xml version="1.0"?>
<rss version="2.0"><channel>
  <item>
    <title>Raporti vjetor</title>
    <link>https://example.test/a</link>
    <description>Totali i shpenzimeve ishte 1299.00 lekë.</description>
  </item>
  <item>
    <title>Bosh</title>
    <link>https://example.test/b</link>
    <description>   </description>
  </item>
</channel></rss>
"""

PAGE = (
    "<html><body><h1>Dokumente</h1>"
    "<p>Ky dokument përshkruan procedurat e brendshme.</p>"
    "<a href='/files/raport.pdf'>raport</a></body></html>"
)

CKAN = (
    '{"success": true, "result": {"results": ['
    '{"name": "test", "resources": ['
    '{"url": "https://example.test/data.csv", "format": "csv"},'
    '{"url": "https://example.test/ignored.bin", "format": "BIN"}'
    "]}]}}"
)


def _registry(tmp_path):
    path = tmp_path / "sources.yaml"
    path.write_text(
        "defaults:\n"
        "  rate_limit: '100/s'\n"
        "  license: open\n"
        "sources:\n"
        "  - id: feed\n    type: rss\n    feeds: [https://example.test/feed]\n"
        "  - id: site\n    type: web\n    seeds: [https://example.test/]\n"
        "    max_pages: 3\n"
        "  - id: data\n    type: opendata\n"
        "    endpoint: https://example.test\n",
        encoding="utf-8",
    )
    return load_config(path)


def _fetcher():
    return RecordedFetcher(
        {
            "https://example.test/feed": FEED,
            "https://example.test/": PAGE,
            "https://example.test/api/3/action/package_search?rows=20": CKAN,
            "https://example.test/data.csv": "a,b\n1,2\n",
        }
    )


def _orchestrator(tmp_path):
    config = _registry(tmp_path)
    store = JsonlStore(tmp_path / "chunks.jsonl")
    ingestor = offline_ingestor(
        store,
        options=ChunkOptions(target=64, hard_max=128, overlap=8),
    )
    fetcher = _fetcher()
    orchestrator = Orchestrator(
        config,
        ingestor,
        fetcher_factory=lambda _source: fetcher,
        policy_factory=lambda _source: _permissive(),
        log=lambda _message: None,
    )
    return orchestrator, store, fetcher


def _permissive():
    from battalion.policy import Policy

    return Policy(robots_fetcher=lambda _url: None, rate_limit=100.0)


def test_run_collects_chunks_from_every_source(tmp_path):
    orchestrator, store, _ = _orchestrator(tmp_path)

    report = orchestrator.run()

    assert report.chunks > 0
    assert store.count() == report.chunks
    assert report.semantic is False
    sources = {result.source_id: result for result in report.results}
    assert sources["feed"].documents == 1
    assert sources["feed"].skipped == 1  # the empty entry is reported
    assert sources["site"].documents >= 1
    assert sources["data"].documents == 1  # only the CSV is supported


def test_stored_records_keep_provenance(tmp_path):
    orchestrator, store, _ = _orchestrator(tmp_path)
    orchestrator.run()

    records = store.records()
    assert records
    for record in records:
        assert record.metadata["source"]
        assert record.metadata["reader"] in {"html", "text", "text-layer", "ocr"}
        assert record.tokens <= 128
        assert record.metadata["license"] == "open"


def test_stats_and_query_work_on_what_was_stored(tmp_path):
    orchestrator, store, _ = _orchestrator(tmp_path)
    orchestrator.run()

    stats = store.stats()
    assert stats.total == store.count()
    assert stats.max_tokens <= 128
    assert set(stats.per_source) == {"feed", "site", "data"}

    hits = store.query([0.0] * 256, top_k=3)
    assert len(hits) == 3


def test_refused_urls_are_counted_not_hidden(tmp_path):
    from battalion.policy import Policy

    config = _registry(tmp_path)
    store = JsonlStore(tmp_path / "chunks.jsonl")
    ingestor = offline_ingestor(store)

    def deny_all(_source):
        return Policy(
            robots_fetcher=lambda _url: "User-agent: *\nDisallow: /\n",
            rate_limit=100.0,
        )

    orchestrator = Orchestrator(
        config,
        ingestor,
        fetcher_factory=lambda _source: _fetcher(),
        policy_factory=deny_all,
        log=lambda _message: None,
    )
    report = orchestrator.run()

    assert report.chunks == 0
    assert report.refused > 0


def test_source_without_credentials_fails_loudly(tmp_path):
    path = tmp_path / "sources.yaml"
    path.write_text(
        "sources:\n"
        "  - id: g\n    type: api_google\n    queries: [test]\n",
        encoding="utf-8",
    )
    store = JsonlStore(tmp_path / "chunks.jsonl")
    orchestrator = Orchestrator(
        load_config(path),
        offline_ingestor(store),
        fetcher_factory=lambda _source: _fetcher(),
        policy_factory=lambda _source: _permissive(),
        log=lambda _message: None,
    )

    report = orchestrator.run()

    assert report.failed == 1
    assert "GOOGLE_CSE_KEY" in report.results[0].notes[0]


def test_pdf_response_without_text_layer_is_skipped_with_a_reason(tmp_path):
    from battalion.pipeline.ingest import offline_ingestor as offline

    pdf = _empty_pdf()
    store = JsonlStore(tmp_path / "chunks.jsonl")
    ingestor = offline(store)

    report = ingestor.ingest(
        pdf,
        "application/pdf",
        source_id="gov",
        url="https://example.test/scan.pdf",
        ocr=False,
    )

    assert report.skipped
    assert "OCR was not enabled" in report.skipped_reason
    assert store.count() == 0


def _empty_pdf() -> bytes:
    from test_extract import build_pdf

    return build_pdf(("",))


def test_html_response_is_ingested_and_traceable(tmp_path):
    store = JsonlStore(tmp_path / "chunks.jsonl")
    ingestor = offline_ingestor(store)

    report = ingestor.ingest(
        Response(
            url="https://example.test/page",
            status=200,
            content=PAGE.encode("utf-8"),
            headers={"content-type": "text/html"},
        ).content,
        "text/html",
        source_id="site",
        url="https://example.test/page",
    )

    assert report.chunks >= 1
    assert report.reader == "html"
    assert store.records()[0].metadata["url"] == "https://example.test/page"
