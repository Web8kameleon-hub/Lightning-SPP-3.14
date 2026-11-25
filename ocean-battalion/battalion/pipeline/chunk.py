"""Chunking sized to the embedding model, not to a rule of thumb.

The brief is "as much text per chunk as possible, but never more than the
model accepts". That is expressed as three numbers:

* ``target``    — what we aim for (default 4096, half of BGE-M3's window, so
                  a chunk keeps context without saturating the model);
* ``hard_max``  — the model's ceiling (8192); nothing may exceed it;
* ``overlap``   — how much of the previous chunk is repeated (default 400,
                  ~10%) so a sentence on a boundary is not lost.

Splitting is structural first (paragraph, line, sentence, clause, word) and
destructive only as a last resort, when a single unbroken run of text is
already larger than ``hard_max``.
"""

from __future__ import annotations

from dataclasses import dataclass
from typing import Sequence

from battalion.pipeline.tokens import ApproximateCounter, TokenCounter

TARGET_TOKENS = 4096
HARD_MAX_TOKENS = 8192
OVERLAP_TOKENS = 400

# Coarsest boundary first: this order is what keeps paragraphs together.
SEPARATORS: tuple[str, ...] = (
    "\n\n",
    "\n",
    ". ",
    "! ",
    "? ",
    "; ",
    ", ",
    " ",
)


@dataclass(frozen=True)
class Chunk:
    index: int
    text: str
    tokens: int
    overlap_tokens: int = 0


@dataclass(frozen=True)
class ChunkPlan:
    """The chunks plus the facts a report must state about them."""

    chunks: Sequence[Chunk]
    target: int
    hard_max: int
    overlap: int
    counter_name: str
    counter_approximate: bool

    @property
    def max_tokens(self) -> int:
        return max((chunk.tokens for chunk in self.chunks), default=0)

    @property
    def min_tokens(self) -> int:
        return min((chunk.tokens for chunk in self.chunks), default=0)

    @property
    def over_limit(self) -> tuple[int, ...]:
        return tuple(c.tokens for c in self.chunks if c.tokens > self.hard_max)


def chunk_text(
    text: str,
    counter: TokenCounter | None = None,
    *,
    target: int = TARGET_TOKENS,
    hard_max: int = HARD_MAX_TOKENS,
    overlap: int = OVERLAP_TOKENS,
) -> ChunkPlan:
    """Split `text` into chunks of at most `hard_max` tokens."""
    _validate(target, hard_max, overlap)
    active = counter or ApproximateCounter()

    body = text.strip()
    if not body:
        return _plan([], active, target, hard_max, overlap)

    if active.count(body) <= target:
        return _plan([body], active, target, hard_max, overlap)

    atoms = _split(body, SEPARATORS, active, target)
    packed = _pack(atoms, active, target)
    bounded = _enforce_hard_max(packed, active, hard_max)
    pieces = _apply_overlap(bounded, active, overlap, hard_max)
    return _plan(pieces, active, target, hard_max, overlap)


def _validate(target: int, hard_max: int, overlap: int) -> None:
    if target <= 0:
        raise ValueError("target must be positive")
    if hard_max < target:
        raise ValueError("hard_max must be at least target")
    if overlap < 0:
        raise ValueError("overlap must not be negative")
    if overlap >= target:
        raise ValueError("overlap must be smaller than target")


def _plan(
    texts: Sequence[str],
    counter: TokenCounter,
    target: int,
    hard_max: int,
    overlap: int,
) -> ChunkPlan:
    chunks = [
        Chunk(index=i, text=text, tokens=counter.count(text))
        for i, text in enumerate(texts)
    ]
    return ChunkPlan(
        chunks=chunks,
        target=target,
        hard_max=hard_max,
        overlap=overlap,
        counter_name=counter.name,
        counter_approximate=counter.approximate,
    )


def _split(
    text: str,
    separators: Sequence[str],
    counter: TokenCounter,
    target: int,
) -> list[str]:
    """Break `text` down until every atom fits inside `target`."""
    if counter.count(text) <= target:
        return [text]
    if not separators:
        return [text]

    separator, rest = separators[0], separators[1:]
    parts = text.split(separator)
    if len(parts) == 1:
        return _split(text, rest, counter, target)

    atoms: list[str] = []
    last = len(parts) - 1
    for position, part in enumerate(parts):
        piece = part + separator if position < last else part
        if not piece:
            continue
        if counter.count(piece) <= target:
            atoms.append(piece)
        else:
            atoms.extend(_split(piece, rest, counter, target))
    return atoms


def _pack(
    atoms: Sequence[str],
    counter: TokenCounter,
    target: int,
) -> list[str]:
    """Greedily glue atoms together while the result stays under `target`."""
    packed: list[str] = []
    current = ""
    for atom in atoms:
        candidate = current + atom
        if not current or counter.count(candidate) <= target:
            current = candidate
            continue
        packed.append(current.strip())
        current = atom
    if current.strip():
        packed.append(current.strip())
    return [piece for piece in packed if piece]


def _enforce_hard_max(
    pieces: Sequence[str],
    counter: TokenCounter,
    hard_max: int,
) -> list[str]:
    """Cut anything still over the model ceiling — a last resort, logged."""
    bounded: list[str] = []
    for piece in pieces:
        bounded.extend(_cut(piece, counter, hard_max))
    return bounded


def _cut(piece: str, counter: TokenCounter, hard_max: int) -> list[str]:
    out: list[str] = []
    rest = piece
    while rest and counter.count(rest) > hard_max:
        head = counter.truncate(rest, hard_max)
        if not head or len(head) >= len(rest):
            head = rest[: max(1, len(rest) // 2)]
        out.append(head.strip())
        rest = rest[len(head):].lstrip()
    if rest:
        out.append(rest.strip())
    return [item for item in out if item]


def _apply_overlap(
    pieces: Sequence[str],
    counter: TokenCounter,
    overlap: int,
    hard_max: int,
) -> list[str]:
    """Repeat the tail of the previous chunk at the head of the next."""
    if overlap <= 0 or len(pieces) < 2:
        return list(pieces)

    result: list[str] = [pieces[0]]
    for index in range(1, len(pieces)):
        previous_tail = counter.tail(result[index - 1], overlap)
        merged = f"{previous_tail}\n{pieces[index]}" if previous_tail else pieces[index]
        if counter.count(merged) <= hard_max:
            result.append(merged)
        else:
            result.append(pieces[index])
    return result
