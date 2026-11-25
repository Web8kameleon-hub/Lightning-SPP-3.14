"""The one path every collected document travels: extract → chunk → embed.

Agents never touch the store directly. They hand bytes to an ``Ingestor`` and
get back a report saying what happened — including "nothing, and here is why".
A source that yields no text produces an explicit skip record, never silence.
"""

from __future__ import annotations

import hashlib
from dataclasses import dataclass, field
from typing import Sequence

from battalion.pipeline.chunk import (
    HARD_MAX_TOKENS,
    OVERLAP_TOKENS,
    TARGET_TOKENS,
    ChunkPlan,
    chunk_text,
)
from battalion.pipeline.embed import Embedder, HashingEmbedder
from battalion.pipeline.extract import (
    ExtractionError,
    ExtractionResult,
    extract,
)
from battalion.pipeline.tokens import ApproximateCounter, TokenCounter
from battalion.storage.base import VectorRecord


@dataclass(frozen=True)
class ChunkOptions:
    target: int = TARGET_TOKENS
    hard_max: int = HARD_MAX_TOKENS
    overlap: int = OVERLAP_TOKENS


@dataclass
class IngestReport:
    """What one document produced. Always populated, even when skipped."""

    source_id: str
    url: str
    reader: str
    pages: int
    chunks: int = 0
    max_tokens: int = 0
    min_tokens: int = 0
    counter: str = ""
    approximate: bool = True
    note: str | None = None
    skipped_reason: str | None = None
    over_limit: tuple[int, ...] = field(default_factory=tuple)

    @property
    def skipped(self) -> bool:
        return self.skipped_reason is not None

    @property
    def ocr_used(self) -> bool:
        return self.reader == "ocr"


class Ingestor:
    """Glues extraction, chunking, embedding and storage together."""

    def __init__(
        self,
        embedder: Embedder,
        store,
        *,
        counter: TokenCounter | None = None,
        options: ChunkOptions | None = None,
        batch_size: int = 8,
    ) -> None:
        self.embedder = embedder
        self.store = store
        self.counter = counter or ApproximateCounter()
        self.options = options or ChunkOptions()
        self.batch_size = max(1, batch_size)

    @property
    def is_semantic(self) -> bool:
        return bool(getattr(self.embedder, "semantic", False))

    def ingest(
        self,
        content: bytes | str,
        mime: str,
        *,
        source_id: str,
        url: str,
        ocr: bool = False,
        ocr_lang: str = "sqi",
        license: str = "unknown",
        pages_limit: int = 32,
        runner=None,
    ) -> IngestReport:
        result = extract(
            content,
            mime,
            ocr=ocr,
            ocr_lang=ocr_lang,
            max_ocr_pages=pages_limit,
            runner=runner,
        )
        report = IngestReport(
            source_id=source_id,
            url=url,
            reader=result.reader,
            pages=result.pages,
            counter=self.counter.name,
            approximate=self.counter.approximate,
            note=result.note,
        )

        if result.is_empty:
            report.skipped_reason = _empty_reason(result)
            return report

        plan = chunk_text(
            result.text,
            self.counter,
            target=self.options.target,
            hard_max=self.options.hard_max,
            overlap=self.options.overlap,
        )
        if not plan.chunks:
            report.skipped_reason = "chunker produced no chunks"
            return report

        written = self._embed_and_store(
            plan,
            source_id=source_id,
            url=url,
            reader=result.reader,
            license=license,
        )

        report.chunks = written
        report.max_tokens = plan.max_tokens
        report.min_tokens = plan.min_tokens
        report.over_limit = plan.over_limit
        return report

    def _embed_and_store(
        self,
        plan: ChunkPlan,
        *,
        source_id: str,
        url: str,
        reader: str,
        license: str,
    ) -> int:
        texts = [chunk.text for chunk in plan.chunks]
        written = 0
        for start in range(0, len(texts), self.batch_size):
            batch = texts[start : start + self.batch_size]
            vectors = self.embedder.embed(batch)
            if len(vectors) != len(batch):
                raise RuntimeError(
                    f"embedder returned {len(vectors)} vectors for "
                    f"{len(batch)} texts"
                )
            records = [
                VectorRecord(
                    id=_record_id(url, plan.chunks[start + offset].index, text),
                    text=text,
                    vector=vector,
                    metadata={
                        "source": source_id,
                        "url": url,
                        "reader": reader,
                        "license": license,
                        "chunk_index": plan.chunks[start + offset].index,
                        "tokens": plan.chunks[start + offset].tokens,
                        "embedder": self.embedder.name,
                        "semantic": self.is_semantic,
                    },
                )
                for offset, (text, vector) in enumerate(zip(batch, vectors))
            ]
            written += self.store.upsert(records)
        return written


def _empty_reason(result: ExtractionResult) -> str:
    if result.reader == "text-layer":
        return (
            "no text layer and OCR was not enabled for this source; "
            "the document is an image, not text"
        )
    return f"reader '{result.reader}' produced no text"


def _record_id(url: str, index: int, text: str) -> str:
    digest = hashlib.sha256(
        f"{url}|{index}|{text}".encode("utf-8", errors="replace")
    ).hexdigest()
    return digest[:32]


def offline_ingestor(store, **kwargs) -> Ingestor:
    """Ingestor that needs no model download; used by tests and demo runs."""
    return Ingestor(HashingEmbedder(), store, **kwargs)


__all__: Sequence[str] = (
    "ChunkOptions",
    "IngestReport",
    "Ingestor",
    "ExtractionError",
    "offline_ingestor",
)
