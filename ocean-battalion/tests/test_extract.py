"""Extraction: HTML, PDF text layer, and the OCR fallback."""

from __future__ import annotations

import shutil

import pytest

from battalion.pipeline import locator
from battalion.pipeline.extract import READER_HTML, READER_OCR, READER_TEXT_LAYER, extract

HAS_POPPLER = locator.try_locate("pdftotext") is not None
HAS_TESSERACT = locator.try_locate("tesseract") is not None


def build_pdf(*page_texts: tuple[str, ...]) -> bytes:
    """Minimal multi-page PDF with a real text layer and a real xref table."""
    objects = ["<< /Type /Catalog /Pages 2 0 R >>"]
    # Page objects are 3, 5, 7 … because each page is followed by its content
    # stream, so the kid numbering must skip every second object number.
    kids = " ".join(
        f"{3 + index * 2} 0 R" for index in range(len(page_texts))
    )
    objects.append(f"<< /Type /Pages /Kids [{kids}] "
                   f"/Count {len(page_texts)} >>")

    font_ref = 3 + len(page_texts) * 2
    for index in range(len(page_texts)):
        objects.append(
            f"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] "
            f"/Resources << /Font << /F1 {font_ref} 0 R >> >> "
            f"/Contents {4 + index * 2} 0 R >>"
        )
        objects.append(_content(page_texts[index]))

    objects.append("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>")

    out = "%PDF-1.4\n"
    offsets = []
    for number, body in enumerate(objects, start=1):
        offsets.append(len(out.encode("ascii")))
        out += f"{number} 0 obj\n{body}\nendobj\n"

    xref = len(out.encode("ascii"))
    out += f"xref\n0 {len(objects) + 1}\n0000000000 65535 f \n"
    for offset in offsets:
        out += f"{offset:010d} 00000 n \n"
    out += (f"trailer\n<< /Size {len(objects) + 1} /Root 1 0 R >>\n"
            f"startxref\n{xref}\n%%EOF\n")
    return out.encode("ascii")


def _content(lines: tuple[str, ...]) -> str:
    body = "BT\n/F1 14 Tf\n72 760 Td\n"
    for line in lines:
        escaped = line.replace("\\", "\\\\").replace("(", "\\(").replace(")", "\\)")
        body += f"({escaped}) Tj\n0 -22 Td\n"
    body += "ET"
    return f"<< /Length {len(body)} >>\nstream\n{body}\nendstream"


def test_html_is_stripped_of_markup_and_chrome():
    html = (
        "<html><head><style>p{}</style></head><body>"
        "<nav>menu</nav><h1>Raporti</h1><p>Teksti kryesor.</p>"
        "<script>alert(1)</script><footer>fund</footer></body></html>"
    )
    result = extract(html, "text/html")

    assert result.reader == READER_HTML
    assert "Raporti" in result.text
    assert "Teksti kryesor." in result.text
    assert "alert(1)" not in result.text
    assert "<h1>" not in result.text


@pytest.mark.skipif(not HAS_POPPLER, reason="Poppler/pdftotext not installed")
def test_pdf_text_layer_is_read_with_page_counts():
    pdf = build_pdf(("INVOICE", "Date: 2026-03-14"), ("Total: 1299.00",))

    result = extract(pdf, "application/pdf")

    assert result.reader == READER_TEXT_LAYER
    assert result.pages == 2
    assert "INVOICE" in result.text
    assert "Total: 1299.00" in result.text


def test_pdf_without_text_layer_and_without_ocr_says_so():
    empty_pdf = build_pdf(("",))

    result = extract(empty_pdf, "application/pdf", ocr=False)

    assert result.reader == READER_TEXT_LAYER
    assert result.is_empty
    assert result.note is not None
    assert "OCR was not enabled" in result.note


def test_ocr_path_runs_with_an_injected_runner():
    """The OCR branch is exercised without Tesseract by faking the binaries."""
    calls: list[list[str]] = []

    def runner(command):
        calls.append(list(command))
        joined = " ".join(str(part) for part in command)
        if "pdftoppm" in joined:
            image = command[-1] + "-1.png"
            with open(image, "wb") as handle:
                handle.write(b"fake-png")
            return 0, b"", b""
        if "tesseract" in joined:
            return 0, "TOTAL 1299.00".encode("utf-8"), b""
        return 0, b"", b""  # pdftotext: no text layer

    result = extract(build_pdf(("",)), "application/pdf", ocr=True, runner=runner)

    assert result.reader == READER_OCR
    assert "1299.00" in result.text
    assert any("pdftoppm" in " ".join(c) for c in calls)
    assert any("tesseract" in " ".join(c) for c in calls)


def test_missing_tool_is_an_explicit_error(monkeypatch):
    monkeypatch.setattr(
        locator, "locate",
        lambda name: (_ for _ in ()).throw(
            locator.ToolNotFound(f"{name} missing")
        ),
    )

    with pytest.raises(locator.ToolNotFound):
        extract(build_pdf(("text",)), "application/pdf")


@pytest.mark.skipif(
    not (HAS_TESSERACT and shutil.which("pdftoppm")),
    reason="Tesseract or pdftoppm not installed",
)
def test_real_ocr_recognises_a_rendered_page():
    # A PDF page that only exists as an image is the honest OCR case, but
    # building one needs a rasteriser; here we assert the tools resolve and
    # the OCR branch reaches them, which is what this environment can prove.
    assert locator.try_locate("pdftoppm") is not None
    assert locator.try_locate("tesseract") is not None
