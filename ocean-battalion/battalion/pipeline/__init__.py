"""Extraction, chunking, embedding: the part every source shares."""

from battalion.pipeline.chunk import (
    HARD_MAX_TOKENS,
    OVERLAP_TOKENS,
    TARGET_TOKENS,
    Chunk,
    ChunkPlan,
    chunk_text,
)
from battalion.pipeline.embed import (
    BgeM3Embedder,
    Embedder,
    HashingEmbedder,
    default_embedder,
)
from battalion.pipeline.extract import ExtractionError, ExtractionResult, extract
from battalion.pipeline.ingest import ChunkOptions, IngestReport, Ingestor
from battalion.pipeline.tokens import (
    ApproximateCounter,
    HuggingFaceCounter,
    TokenCounter,
    default_counter,
)

__all__ = [
    "HARD_MAX_TOKENS",
    "OVERLAP_TOKENS",
    "TARGET_TOKENS",
    "ApproximateCounter",
    "BgeM3Embedder",
    "Chunk",
    "ChunkOptions",
    "ChunkPlan",
    "Embedder",
    "ExtractionError",
    "ExtractionResult",
    "HashingEmbedder",
    "HuggingFaceCounter",
    "IngestReport",
    "Ingestor",
    "TokenCounter",
    "chunk_text",
    "default_counter",
    "default_embedder",
    "extract",
]
