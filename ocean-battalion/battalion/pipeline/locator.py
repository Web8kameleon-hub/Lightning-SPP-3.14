"""Locating the external tools the pipeline needs.

Poppler (`pdftotext`, `pdftoppm`) and Tesseract are not Python packages, so the
battalion has to find them the same way the .NET side does: a configured path
first, then PATH, then a well-known install location. A configured path that
does not exist is an error — never a quiet fallback to a different binary.
"""

from __future__ import annotations

import os
import shutil
from dataclasses import dataclass
from pathlib import Path


class ToolNotFound(RuntimeError):
    """The requested external tool could not be located."""


@dataclass(frozen=True)
class ToolLocation:
    name: str
    path: str
    source: str


_WINDOWS_INSTALLS = {
    "tesseract": (r"C:\Program Files\Tesseract-OCR\tesseract.exe",),
    "pdftotext": (),
    "pdftoppm": (),
}


def _executable(name: str) -> str:
    return f"{name}.exe" if os.name == "nt" else name


def locate(name: str) -> ToolLocation:
    """Find an external tool, or raise :class:`ToolNotFound`."""
    env_var = f"OCEAN_{name.upper()}_PATH"
    configured = os.environ.get(env_var)
    if configured:
        if not Path(configured).is_file():
            raise ToolNotFound(f"{env_var} points to a missing file: {configured}")
        return ToolLocation(name, configured, "environment")

    on_path = shutil.which(name)
    if on_path:
        return ToolLocation(name, on_path, "path")

    for candidate in _WINDOWS_INSTALLS.get(name, ()):
        if Path(candidate).is_file():
            return ToolLocation(name, candidate, "install")

    repository = _search_repository(name)
    if repository:
        return ToolLocation(name, repository, "repository")

    raise ToolNotFound(
        f"{name} was not found. Install it or set {env_var} to the executable."
    )


def try_locate(name: str) -> ToolLocation | None:
    try:
        return locate(name)
    except ToolNotFound:
        return None


def _search_repository(name: str) -> str | None:
    """Look for a bundled copy under `artifacts/tooling`, like the .NET locator."""
    probe = Path(__file__).resolve()
    for parent in probe.parents:
        tooling = parent / "artifacts" / "tooling"
        if not tooling.is_dir():
            continue
        for found in tooling.rglob(_executable(name)):
            return str(found)
    return None


def sibling(tool: ToolLocation, name: str) -> ToolLocation | None:
    """Find `name` next to an already located tool (Poppler ships them together)."""
    candidate = Path(tool.path).parent / _executable(name)
    if candidate.is_file():
        return ToolLocation(name, str(candidate), f"{tool.source}(sibling)")
    return try_locate(name)
