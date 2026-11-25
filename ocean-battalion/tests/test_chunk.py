"""Chunking: the hard ceiling is a guarantee, the target is a preference."""

from __future__ import annotations

import pytest

from battalion.pipeline.chunk import chunk_text
from battalion.pipeline.tokens import ApproximateCounter


def _paragraphs(count: int, words_per_paragraph: int = 40) -> str:
    block = " ".join(["fjali"] * words_per_paragraph)
    return "\n\n".join(f"{block} {index}." for index in range(count))


def test_short_text_is_one_chunk_and_untouched():
    text = "Një dokument i shkurtër për faturën."
    plan = chunk_text(text)

    assert len(plan.chunks) == 1
    assert plan.chunks[0].text == text
    assert plan.counter_approximate is True


def test_empty_text_produces_no_chunks():
    plan = chunk_text("   \n\n  ")

    assert plan.chunks == []
    assert plan.max_tokens == 0


def test_no_chunk_exceeds_the_hard_ceiling():
    plan = chunk_text(_paragraphs(400), target=512, hard_max=1024, overlap=50)

    assert len(plan.chunks) > 1
    assert plan.over_limit == ()
    for chunk in plan.chunks:
        assert chunk.tokens <= 1024


def test_most_chunks_are_near_the_target():
    plan = chunk_text(_paragraphs(400), target=512, hard_max=1024, overlap=0)

    sizes = [chunk.tokens for chunk in plan.chunks]
    near_target = [size for size in sizes if size >= 512 * 0.5]
    assert len(near_target) >= len(sizes) - 1


def test_single_unbroken_run_is_cut_rather_than_dropped():
    # No separators at all: the only way out is a forced cut.
    blob = "a" * 40_000
    plan = chunk_text(blob, target=512, hard_max=1024, overlap=0)

    assert len(plan.chunks) > 1
    assert plan.over_limit == ()
    # Text must be preserved, not silently discarded.
    assert sum(len(chunk.text) for chunk in plan.chunks) >= len(blob) - 10


def test_overlap_repeats_the_previous_tail():
    plan = chunk_text(_paragraphs(200), target=256, hard_max=512, overlap=64)

    assert len(plan.chunks) >= 3
    assert plan.over_limit == ()
    for previous, current in zip(plan.chunks, plan.chunks[1:]):
        head = current.text.split("\n", 1)[0]
        assert head in previous.text


def test_invalid_configuration_is_refused():
    with pytest.raises(ValueError):
        chunk_text("text", target=0)
    with pytest.raises(ValueError):
        chunk_text("text", target=1024, hard_max=512)
    with pytest.raises(ValueError):
        chunk_text("text", target=1024, hard_max=2048, overlap=1024)


def test_chunk_indices_are_sequential():
    plan = chunk_text(_paragraphs(120), target=256, hard_max=512, overlap=0)

    assert [chunk.index for chunk in plan.chunks] == list(
        range(len(plan.chunks))
    )
    assert plan.counter_name == ApproximateCounter().name
