"""A store that needs nothing but the filesystem.

Every record is one JSON line, so the corpus survives a crash mid-batch, can be
diffed, grepped and backed up with `copy`. Search is a linear cosine scan: fine
for a first battalion, and honest about it — for millions of chunks this class
is replaced by the Chroma or Milvus adapter, not scaled up in place.
"""

from __future__ import annotations

import json
from pathlib import Path
from typing import Sequence

from battalion.storage.base import (
    QueryHit,
    StoreStats,
    VectorRecord,
    cosine_similarity,
)


class JsonlStore:
    """Append-only vector store with a linear-scan query."""

    def __init__(self, path: str | Path) -> None:
        self.path = Path(path)
        self.path.parent.mkdir(parents=True, exist_ok=True)
        self.path.touch(exist_ok=True)

    # -- writing -----------------------------------------------------------
    def upsert(self, records: Sequence[VectorRecord]) -> int:
        if not records:
            return 0

        with self.path.open("a", encoding="utf-8") as handle:
            for record in records:
                handle.write(json.dumps(_as_line(record), ensure_ascii=False))
                handle.write("\n")
        return len(records)

    # -- reading -----------------------------------------------------------
    def records(self) -> list[VectorRecord]:
        out: list[VectorRecord] = []
        for line in self.path.read_text(encoding="utf-8").splitlines():
            line = line.strip()
            if not line:
                continue
            payload = json.loads(line)
            out.append(
                VectorRecord(
                    id=payload["id"],
                    text=payload["text"],
                    vector=payload["vector"],
                    metadata=payload.get("metadata", {}),
                )
            )
        return out

    def count(self) -> int:
        return len(self.records())

    def query(self, vector: Sequence[float], top_k: int = 5) -> list[QueryHit]:
        scored: list[QueryHit] = []
        for record in self.records():
            try:
                score = cosine_similarity(vector, record.vector)
            except ValueError:
                # Dimension mismatch means the embedder changed underneath us.
                continue
            scored.append(
                QueryHit(
                    id=record.id,
                    text=record.text,
                    score=score,
                    metadata=record.metadata,
                )
            )
        scored.sort(key=lambda hit: hit.score, reverse=True)
        return scored[:top_k]

    def stats(self) -> StoreStats:
        per_source: dict[str, int] = {}
        per_reader: dict[str, int] = {}
        max_tokens = 0
        for record in self.records():
            per_source[record.source] = per_source.get(record.source, 0) + 1
            per_reader[record.reader] = per_reader.get(record.reader, 0) + 1
            max_tokens = max(max_tokens, record.tokens)
        return StoreStats(
            per_source=per_source,
            per_reader=per_reader,
            max_tokens=max_tokens,
        )


def _as_line(record: VectorRecord) -> dict:
    return {
        "id": record.id,
        "text": record.text,
        "vector": list(record.vector),
        "metadata": record.metadata,
    }
