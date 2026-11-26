"""Unit failure fixtures: no claim of real model inference or embedding quality."""
import pytest
from battalion.pipeline import embed


def test_required_model_failure_never_silently_substitutes_hashing(monkeypatch):
    def unavailable():
        raise RuntimeError("test-only unavailable model fixture")
    monkeypatch.setattr(embed, "BgeM3Embedder", unavailable)
    with pytest.raises(RuntimeError, match="unavailable model"):
        embed.default_embedder()


def test_hashing_requires_explicit_nonsemantic_selection():
    selected = embed.default_embedder(offline=True)
    assert selected.semantic is False
    assert selected.name == "hashing(offline)"
