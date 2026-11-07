"""Storage adapters.

`JsonlStore` needs nothing beyond Python and is the default. `open_store`
picks Chroma when it is installed and asked for, otherwise JSONL, and never
pretends the substitution did not happen.
"""

from battalion.storage.base import (
    QueryHit,
    StoreStats,
    VectorRecord,
    VectorStore,
    cosine_similarity,
)
from battalion.storage.jsonl_store import JsonlStore

__all__ = [
    "JsonlStore",
    "QueryHit",
    "StoreStats",
    "VectorRecord",
    "VectorStore",
    "cosine_similarity",
    "open_store",
]


def open_store(kind: str = "jsonl", **kwargs):
    """Return a store by name: ``jsonl`` (default) or ``chroma``."""
    normalised = (kind or "jsonl").strip().lower()
    if normalised == "jsonl":
        return JsonlStore(kwargs.get("path", "./data/chunks.jsonl"))

    if normalised == "chroma":
        from battalion.storage.chroma_store import ChromaStore

        return ChromaStore(
            path=kwargs.get("path", "./data/chroma"),
            collection=kwargs.get("collection", "ocean"),
        )

    raise ValueError(f"unknown store kind: {kind!r} (use 'jsonl' or 'chroma')")
