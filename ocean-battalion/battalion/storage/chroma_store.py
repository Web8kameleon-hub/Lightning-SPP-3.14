"""Chroma adapter, for when the corpus outgrows a text file.

Chroma is an optional extra: nothing else in the battalion imports it, and the
JSONL store remains the default so a first run needs no database at all.
"""

from __future__ import annotations

from pathlib import Path
from typing import Sequence

from battalion.storage.base import QueryHit, StoreStats, VectorRecord


class ChromaStore:
    """Persistent Chroma collection with cosine distance."""

    def __init__(self, path: str | Path = "./data/chroma", collection: str = "ocean"):
        try:
            import chromadb
        except ImportError as error:
            raise RuntimeError(
                "Chroma needs the 'chroma' extra: pip install "
                "'ocean-battalion[chroma]'"
            ) from error

        self.path = str(path)
        self.collection_name = collection
        self._client = chromadb.PersistentClient(path=self.path)
        self._collection = self._client.get_or_create_collection(
            name=collection,
            metadata={"hnsw:space": "cosine"},
        )

    def upsert(self, records: Sequence[VectorRecord]) -> int:
        if not records:
            return 0
        self._collection.upsert(
            ids=[record.id for record in records],
            embeddings=[list(record.vector) for record in records],
            documents=[record.text for record in records],
            metadatas=[dict(record.metadata) for record in records],
        )
        return len(records)

    def query(self, vector: Sequence[float], top_k: int = 5) -> list[QueryHit]:
        result = self._collection.query(
            query_embeddings=[list(vector)],
            n_results=top_k,
        )
        ids = (result.get("ids") or [[]])[0]
        documents = (result.get("documents") or [[]])[0]
        metadatas = (result.get("metadatas") or [[]])[0]
        distances = (result.get("distances") or [[]])[0]

        hits: list[QueryHit] = []
        for position, identifier in enumerate(ids):
            distance = distances[position] if position < len(distances) else 1.0
            hits.append(
                QueryHit(
                    id=identifier,
                    text=documents[position] if position < len(documents) else "",
                    score=1.0 - float(distance),
                    metadata=metadatas[position] if position < len(metadatas) else {},
                )
            )
        return hits

    def count(self) -> int:
        return int(self._collection.count())

    def stats(self) -> StoreStats:
        payload = self._collection.get(include=["metadatas"])
        per_source: dict[str, int] = {}
        per_reader: dict[str, int] = {}
        max_tokens = 0
        for metadata in payload.get("metadatas") or []:
            meta = metadata or {}
            source = str(meta.get("source", "unknown"))
            reader = str(meta.get("reader", "unknown"))
            per_source[source] = per_source.get(source, 0) + 1
            per_reader[reader] = per_reader.get(reader, 0) + 1
            max_tokens = max(max_tokens, int(meta.get("tokens", 0)))
        return StoreStats(
            per_source=per_source,
            per_reader=per_reader,
            max_tokens=max_tokens,
        )
