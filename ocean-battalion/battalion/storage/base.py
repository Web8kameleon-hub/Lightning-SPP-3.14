"""The vector store contract the battalion writes to.

Ocean Core reads from a store; it should not care whether that store is Chroma,
Milvus or a JSONL file on disk. Only two operations matter here: write a chunk
with its vector and metadata, and read back statistics per source.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from typing import Any, Protocol, Sequence, runtime_checkable


@dataclass(frozen=True)
class VectorRecord:
    """One embedded chunk, with everything a report needs to trace it."""

    id: str
    text: str
    vector: Sequence[float]
    metadata: dict[str, Any] = field(default_factory=dict)

    @property
    def source(self) -> str:
        return str(self.metadata.get("source", "unknown"))

    @property
    def tokens(self) -> int:
        return int(self.metadata.get("tokens", 0))

    @property
    def reader(self) -> str:
        return str(self.metadata.get("reader", "unknown"))


@dataclass(frozen=True)
class QueryHit:
    id: str
    text: str
    score: float
    metadata: dict[str, Any] = field(default_factory=dict)


@dataclass(frozen=True)
class StoreStats:
    """What the dashboard prints. One entry per source id."""

    per_source: dict[str, int]
    per_reader: dict[str, int]
    max_tokens: int

    @property
    def total(self) -> int:
        return sum(self.per_source.values())


@runtime_checkable
class VectorStore(Protocol):
    def upsert(self, records: Sequence[VectorRecord]) -> int: ...
    def query(self, vector: Sequence[float], top_k: int = 5) -> list[QueryHit]: ...
    def stats(self) -> StoreStats: ...
    def count(self) -> int: ...


def cosine_similarity(left: Sequence[float], right: Sequence[float]) -> float:
    """Plain cosine. Used by the JSONL store, so search works with no extras."""
    if len(left) != len(right):
        raise ValueError("vectors have different dimensions")
    dot = sum(a * b for a, b in zip(left, right))
    norm_left = sum(a * a for a in left) ** 0.5
    norm_right = sum(b * b for b in right) ** 0.5
    if norm_left == 0 or norm_right == 0:
        return 0.0
    return dot / (norm_left * norm_right)
