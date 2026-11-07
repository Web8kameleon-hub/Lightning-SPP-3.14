"""Turning bytes into text, and saying which reader produced it.

Three readers exist and the caller is always told which one ran:

* ``html``       — HTML page, tags stripped.
* ``text-layer`` — the PDF's own embedded text, via Poppler's ``pdftotext``.
* ``ocr``        — the PDF had no usable text layer, so pages were rendered
                   (``pdftoppm``) and recognised (Tesseract).

The distinction matters: text-layer output is deterministic, OCR output is a
guess. A record that says ``reader=ocr`` can be trusted to be honest about its
own unreliability, and ``reader=text-layer`` can be re-derived byte for byte.
"""

from __future__ import annotations

import subprocess
import tempfile
from dataclasses import dataclass
from pathlib import Path
from typing import Callable, Sequence

from battalion.pipeline import locator

READER_HTML = "html"
READER_TEXT = "text"
READER_TEXT_LAYER = "text-layer"
READER_OCR = "ocr"

DEFAULT_MAX_OCR_PAGES = 32
DEFAULT_OCR_DPI = 300

Runner = Callable[[Sequence[str]], tuple[int, bytes, bytes]]


class ExtractionError(RuntimeError):
    """Extraction failed for a reason the caller must not paper over."""


@dataclass(frozen=True)
class ExtractionResult:
    text: str
    reader: str
    pages: int = 1
    note: str | None = None

    @property
    def is_empty(self) -> bool:
        return not self.text.strip()


def extract(
    content: bytes | str,
    mime: str,
    *,
    ocr: bool = False,
    ocr_lang: str = "sqi",
    dpi: int = DEFAULT_OCR_DPI,
    max_ocr_pages: int = DEFAULT_MAX_OCR_PAGES,
    runner: Runner | None = None,
) -> ExtractionResult:
    """Extract text from HTML, PDF or plain text."""
    normalised = (mime or "").lower()
    run = runner or _run

    if isinstance(content, str):
        body = content
        raw = content.encode("utf-8", errors="replace")
    else:
        raw = content
        body = ""

    if "html" in normalised or (not raw[:4] == b"%PDF" and _looks_like_html(raw, body)):
        return ExtractionResult(_html_to_text(body or _decode(raw)), READER_HTML)

    if "pdf" in normalised or raw[:4] == b"%PDF":
        return _extract_pdf(
            raw, ocr=ocr, ocr_lang=ocr_lang, dpi=dpi,
            max_pages=max_ocr_pages, run=run,
        )

    return ExtractionResult(body or _decode(raw), READER_TEXT)


def _extract_pdf(
    raw: bytes,
    *,
    ocr: bool,
    ocr_lang: str,
    dpi: int,
    max_pages: int,
    run: Runner,
) -> ExtractionResult:
    with tempfile.TemporaryDirectory(prefix="ocean-pdf-") as workdir:
        pdf_path = Path(workdir) / "input.pdf"
        pdf_path.write_bytes(raw)

        text, pages = _text_layer(pdf_path, run)
        if text.strip():
            return ExtractionResult(text, READER_TEXT_LAYER, pages=pages)

        if not ocr:
            return ExtractionResult(
                text,
                READER_TEXT_LAYER,
                pages=pages,
                note=(
                    "PDF has no usable text layer. OCR was not enabled for this "
                    "source, so no text was produced."
                ),
            )

        return _ocr_pdf(pdf_path, workdir, ocr_lang=ocr_lang, dpi=dpi,
                        max_pages=max_pages, pages=pages, run=run)


def _text_layer(pdf_path: Path, run: Runner) -> tuple[str, int]:
    tool = locator.locate("pdftotext")
    code, out, err = run([tool.path, "-layout", "-enc", "UTF-8", str(pdf_path), "-"])
    if code != 0:
        raise ExtractionError(
            f"pdftotext failed with {code}: {err.decode('utf-8', 'replace').strip()}"
        )
    text = out.decode("utf-8", "replace")
    # pdftotext terminates the last page with a form feed, so the split leaves
    # a trailing empty element that is not a page.
    raw_pages = text.split("\f")
    if raw_pages and not raw_pages[-1].strip():
        raw_pages = raw_pages[:-1]
    return text.replace("\f", "\n"), max(1, len(raw_pages))


def _ocr_pdf(
    pdf_path: Path,
    workdir: str,
    *,
    ocr_lang: str,
    dpi: int,
    max_pages: int,
    pages: int,
    run: Runner,
) -> ExtractionResult:
    tesseract = locator.locate("tesseract")
    pdftoppm_tool = locator.sibling(locator.locate("pdftotext"), "pdftoppm")
    if pdftoppm_tool is None:
        raise ExtractionError(
            "OCR needs pdftoppm (Poppler) to render pages; it was not found."
        )

    prefix = Path(workdir) / "page"
    code, _, err = run([
        pdftoppm_tool.path, "-r", str(dpi), "-png",
        "-f", "1", "-l", str(max_pages), str(pdf_path), str(prefix),
    ])
    if code != 0:
        raise ExtractionError(
            f"pdftoppm failed with {code}: {err.decode('utf-8', 'replace').strip()}"
        )

    rendered = sorted(Path(workdir).glob("page*.png"))
    if not rendered:
        raise ExtractionError("pdftoppm rendered no pages; OCR cannot run.")

    chunks: list[str] = []
    for image in rendered:
        code, out, err = run([
            tesseract.path, str(image), "stdout", "-l", ocr_lang, "--dpi", str(dpi),
        ])
        if code != 0:
            raise ExtractionError(
                f"tesseract failed with {code}: {err.decode('utf-8', 'replace').strip()}"
            )
        chunks.append(out.decode("utf-8", "replace"))

    note = None
    if pages > len(rendered):
        note = (
            f"OCR covered the first {len(rendered)} of {pages} pages "
            f"(max_ocr_pages={max_pages})."
        )

    return ExtractionResult("\n\n".join(chunks), READER_OCR, pages=len(rendered), note=note)


def _run(command: Sequence[str]) -> tuple[int, bytes, bytes]:
    completed = subprocess.run(  # noqa: S603 - fixed argv, no shell
        list(command),
        capture_output=True,
        check=False,
    )
    return completed.returncode, completed.stdout, completed.stderr


def _decode(raw: bytes) -> str:
    return raw.decode("utf-8", errors="replace")


def _looks_like_html(raw: bytes, body: str) -> bool:
    head = (body or raw[:512].decode("utf-8", errors="replace")).lstrip().lower()
    return head.startswith("<!doctype html") or head.startswith("<html") or "<body" in head


def _html_to_text(body: str) -> str:
    try:
        from bs4 import BeautifulSoup
    except ImportError:  # pragma: no cover - bs4 is a declared dependency
        import re

        without_tags = re.sub(r"<[^>]+>", " ", body)
        return "\n".join(line.strip() for line in without_tags.splitlines() if line.strip())

    soup = BeautifulSoup(body, "html.parser")
    for tag in soup(["script", "style", "nav", "footer", "header", "aside", "form"]):
        tag.decompose()
    lines = [line.strip() for line in soup.get_text(separator="\n").splitlines()]
    return "\n\n".join(line for line in lines if line)
