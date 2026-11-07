"""Embedders: a real multilingual model, and an offline stand-in.

``BgeM3Embedder`` wraps BAAI/bge-m3 — a multilingual model with an 8192-token
window, which is why the chunker aims at 4096. It needs the ``embed`` extra
(FlagEmbedding + torch) and downloads weights on first use.

``HashingEmbedder`` is deterministic, dependency-free and *not* a language
model. It exists so the surrounding machinery (chunking, storage, querying,
reporting) can be tested and run end to end without a 2.3 GB download, and the
CLI refuses to label it as semantic search.
"""

from __future__ import annotations

import hashlib
import math
import re
from typing import Protocol, Sequence, runtime_checkable

_WORD = re.compile(r"\w+", re.UNICODE)


@runtime_checkable
class Embedder(Protocol):
    name: str
    dimension: int
    semantic: bool

    def embed(self, texts: Sequence[str]) -> list[list[float]]: ...


class HashingEmbedder:
    """Hashed bag-of-words. Deterministic, no model, no download."""

    semantic = False
    name = "hashing(offline)"

    def __init__(self, dimension: int = 256) -> None:
        if dimension <= 0:
            raise ValueError("dimension must be positive")
        self.dimension = dimension

    def embed(self, texts: Sequence[str]) -> list[list[float]]:
        return [self._one(text) for text in texts]

    def _one(self, text: str) -> list[float]:
        vector = [0.0] * self.dimension
        for word in _WORD.findall(text.lower()):
            digest = hashlib.blake2b(
                word.encode("utf-8"), digest_size=8
            ).digest()
            bucket = int.from_bytes(digest, "big") % self.dimension
            vector[bucket] += 1.0
        norm = math.sqrt(sum(value * value for value in vector))
        if norm == 0:
            return vector
        return [value / norm for value in vector]


class BgeM3Embedder:
    """BAAI/bge-m3 dense embeddings (1024 dimensions)."""

    semantic = True
    dimension = 1024
    name = "BAAI/bge-m3"

    def __init__(self, model_name: str = "BAAI/bge-m3", use_fp16: bool = True) -> None:
        try:
            from FlagEmbedding import BGEM3FlagModel
        except ImportError as error:
            raise RuntimeError(
                "BGE-M3 needs the 'embed' extra: pip install "
                "'ocean-battalion[embed]'"
            ) from error

        self.name = model_name
        self._model = BGEM3FlagModel(model_name, use_fp16=use_fp16)

    def embed(self, texts: Sequence[str]) -> list[list[float]]:
        if not texts:
            return []
        result = self._model.encode(
            list(texts), return_dense=True, return_sparse=False
        )
        return [list(map(float, row)) for row in result["dense_vecs"]]


def default_embedder(offline: bool = False) -> Embedder:
    """Real embeddings when available; the offline stub only when asked."""
    if offline:
        return HashingEmbedder()
    try:
        return BgeM3Embedder()
    except Exception:
        return HashingEmbedder()
