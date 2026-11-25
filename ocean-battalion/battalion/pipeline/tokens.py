"""Token counting for chunking.

Chunk size is measured in tokens, because that is what an embedding model
actually accepts. Two counters exist:

* :class:`HuggingFaceCounter` — exact, uses the model's own tokenizer.
* :class:`ApproximateCounter` — no dependencies, estimates 4 characters per
  token. It is honest about being an estimate (:attr:`approximate`), so a
  report built on it can say so instead of implying tokenizer precision.
"""

from __future__ import annotations

import math
from typing import Protocol, runtime_checkable

CHARS_PER_TOKEN = 4


@runtime_checkable
class TokenCounter(Protocol):
    name: str
    approximate: bool

    def count(self, text: str) -> int: ...
    def truncate(self, text: str, max_tokens: int) -> str: ...
    def tail(self, text: str, tokens: int) -> str: ...


class ApproximateCounter:
    """Dependency-free estimate. Never reports fewer tokens than `len/4`."""

    name = "approximate(4 chars/token)"
    approximate = True

    def count(self, text: str) -> int:
        if not text:
            return 0
        return math.ceil(len(text) / CHARS_PER_TOKEN)

    def truncate(self, text: str, max_tokens: int) -> str:
        if max_tokens <= 0:
            return ""
        return text[: max_tokens * CHARS_PER_TOKEN]

    def tail(self, text: str, tokens: int) -> str:
        if tokens <= 0 or not text:
            return ""
        return text[-(tokens * CHARS_PER_TOKEN) :]


class HuggingFaceCounter:
    """Exact token counts from a Hugging Face tokenizer (e.g. BAAI/bge-m3)."""

    approximate = False

    def __init__(self, model_name: str = "BAAI/bge-m3") -> None:
        try:
            from transformers import AutoTokenizer
        except ImportError as error:  # pragma: no cover - optional dependency
            raise RuntimeError(
                "exact token counting needs `transformers`; "
                "install the 'embed' extra or use ApproximateCounter"
            ) from error

        self.name = f"hf:{model_name}"
        self._tokenizer = AutoTokenizer.from_pretrained(model_name)

    def count(self, text: str) -> int:
        if not text:
            return 0
        return len(self._tokenizer.encode(text, add_special_tokens=False))

    def truncate(self, text: str, max_tokens: int) -> str:
        if max_tokens <= 0:
            return ""
        ids = self._tokenizer.encode(text, add_special_tokens=False)
        return self._tokenizer.decode(ids[:max_tokens])

    def tail(self, text: str, tokens: int) -> str:
        if tokens <= 0 or not text:
            return ""
        ids = self._tokenizer.encode(text, add_special_tokens=False)
        return self._tokenizer.decode(ids[-tokens:])


def default_counter(model_name: str = "BAAI/bge-m3") -> TokenCounter:
    """Exact counting when the tokenizer is installed, otherwise an estimate."""
    try:
        return HuggingFaceCounter(model_name)
    except Exception:  # missing package, no network, no cache
        return ApproximateCounter()
