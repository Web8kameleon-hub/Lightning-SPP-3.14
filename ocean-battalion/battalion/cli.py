"""Command line: run the battalion, inspect it, query what it collected."""

from __future__ import annotations

import argparse
import json
import sys

from battalion.config import ConfigError, load_config
from battalion.orchestrator import Orchestrator
from battalion.pipeline import locator
from battalion.pipeline.embed import default_embedder
from battalion.pipeline.ingest import ChunkOptions, Ingestor, offline_ingestor
from battalion.pipeline.tokens import ApproximateCounter
from battalion.storage import open_store

DEFAULT_SOURCES = "sources.yaml"


def main(argv: list[str] | None = None) -> int:
    parser = _build_parser()
    args = parser.parse_args(argv)

    try:
        if args.command == "run":
            return _run(args)
        if args.command == "doctor":
            return _doctor(args)
        if args.command == "query":
            return _query(args)
        if args.command == "stats":
            return _stats(args)
    except ConfigError as error:
        print(f"config: {error}", file=sys.stderr)
        return 2
    except (RuntimeError, ValueError) as error:
        print(f"error: {error}", file=sys.stderr)
        return 1

    parser.print_help()
    return 2


def _build_parser() -> argparse.ArgumentParser:
    parser = argparse.ArgumentParser(
        prog="ocean-battalion",
        description="Collect open sources and prepare them for Ocean Core.",
    )
    sub = parser.add_subparsers(dest="command")

    run = sub.add_parser("run", help="crawl the configured sources")
    run.add_argument("--sources", default=DEFAULT_SOURCES)
    run.add_argument("--store", default="jsonl", choices=("jsonl", "chroma"))
    run.add_argument("--data", default="./data/chunks.jsonl")
    run.add_argument("--offline", action="store_true",
                     help="use the hashing embedder (no model download)")
    run.add_argument("--only", default="", help="comma separated source ids")
    run.add_argument("--target", type=int, default=ChunkOptions().target)
    run.add_argument("--hard-max", type=int, default=ChunkOptions().hard_max)
    run.add_argument("--overlap", type=int, default=ChunkOptions().overlap)
    run.add_argument("--json", action="store_true", help="machine readable")

    doctor = sub.add_parser("doctor", help="report tools and configured sources")
    doctor.add_argument("--sources", default=DEFAULT_SOURCES)

    query = sub.add_parser("query", help="search what was collected")
    query.add_argument("text")
    query.add_argument("--store", default="jsonl", choices=("jsonl", "chroma"))
    query.add_argument("--data", default="./data/chunks.jsonl")
    query.add_argument("--top", type=int, default=5)
    query.add_argument("--offline", action="store_true")

    stats = sub.add_parser("stats", help="chunk counts per source")
    stats.add_argument("--store", default="jsonl", choices=("jsonl", "chroma"))
    stats.add_argument("--data", default="./data/chunks.jsonl")

    return parser


def _ingestor_for(args) -> Ingestor:
    store = open_store(args.store, path=args.data)
    options = ChunkOptions(
        target=args.target,
        hard_max=args.hard_max,
        overlap=args.overlap,
    )
    if args.offline:
        return offline_ingestor(store, options=options,
                                counter=ApproximateCounter())
    return Ingestor(default_embedder(), store, options=options)


def _run(args) -> int:
    config = load_config(args.sources)
    ingestor = _ingestor_for(args)
    orchestrator = Orchestrator(config, ingestor)
    only = [part.strip() for part in args.only.split(",") if part.strip()]
    report = orchestrator.run(only=only or None)

    if args.json:
        print(json.dumps(_as_dict(report), indent=2, ensure_ascii=False))
    else:
        print(report.render())
        if not report.semantic:
            print(
                "\nembedder is the offline hashing stub: vectors are useful for "
                "wiring, not for semantic search."
            )
    return 0


def _as_dict(report) -> dict:
    return {
        "embedder": report.embedder,
        "semantic": report.semantic,
        "counter": report.counter,
        "approximate": report.approximate,
        "documents": report.documents,
        "chunks": report.chunks,
        "sources": [
            {
                "id": result.source_id,
                "type": result.type,
                "fetched": result.fetched,
                "documents": result.documents,
                "chunks": result.chunks,
                "skipped": result.skipped,
                "refused": result.refused,
                "failed": result.failed,
                "notes": result.notes,
            }
            for result in report.results
        ],
    }


def _doctor(args) -> int:
    print("external tools")
    for name in ("pdftotext", "pdftoppm", "tesseract"):
        found = locator.try_locate(name)
        if found is None:
            print(f"  {name:<10} missing  (OCR/PDF text needs it)")
        else:
            print(f"  {name:<10} {found.path}  ({found.source})")

    try:
        config = load_config(args.sources)
    except ConfigError as error:
        print(f"\nconfig: {error}", file=sys.stderr)
        return 2

    print(f"\nsources in {args.sources}: {len(config.sources)}")
    for source in config.sources:
        print(
            f"  {source.id:<20} {source.type:<12} "
            f"{source.rate_limit:>5.2f}/s  refresh={source.refresh:<4} "
            f"license={source.license}"
        )
    return 0


def _query(args) -> int:
    store = open_store(args.store, path=args.data)
    embedder = (
        default_embedder(offline=True) if args.offline
        else default_embedder()
    )
    vector = embedder.embed([args.text])[0]
    hits = store.query(vector, top_k=args.top)

    print(f"store={args.data} chunks={store.count()} "
          f"embedder={embedder.name}")
    for hit in hits:
        preview = hit.text[:160].replace("\n", " ")
        print(f"  {hit.score:+.3f}  {hit.metadata.get('source', '?')}  "
              f"{hit.metadata.get('url', '')}")
        print(f"         {preview}...")
    return 0


def _stats(args) -> int:
    store = open_store(args.store, path=args.data)
    stats = store.stats()
    if not stats.per_source:
        print(f"{args.data}: no chunks yet")
        return 0

    print(f"{'SOURCE':<20} {'CHUNKS':>7}")
    print("-" * 29)
    for name in sorted(stats.per_source):
        print(f"{name:<20} {stats.per_source[name]:>7}")
    print("-" * 29)
    print(f"{'TOTAL':<20} {stats.total:>7}")
    print(f"\nmax chunk size: {stats.max_tokens} tokens")
    print("reader spread: " + ", ".join(
        f"{name}={count}" for name, count in sorted(stats.per_reader.items())
    ))
    return 0


if __name__ == "__main__":  # pragma: no cover
    raise SystemExit(main())
