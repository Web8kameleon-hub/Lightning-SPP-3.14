#!/usr/bin/env python3
"""Extract a new package, verify its file manifest and exercise the installed runtime."""
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
import tarfile
import zipfile


def main():
    archive, destination = map(lambda value: Path(value).resolve(), sys.argv[1:])
    expected = Path(str(archive) + ".sha256").read_text(encoding="ascii").split()[0]
    if hashlib.sha256(archive.read_bytes()).hexdigest() != expected:
        raise RuntimeError("Archive checksum mismatch")
    destination.mkdir(parents=True, exist_ok=False)
    if archive.name.endswith(".zip"):
        with zipfile.ZipFile(archive) as bundle:
            for name in bundle.namelist():
                if not (destination / name).resolve().is_relative_to(destination):
                    raise RuntimeError("Archive path escapes extraction directory")
            bundle.extractall(destination)
    else:
        with tarfile.open(archive) as bundle:
            bundle.extractall(destination, filter="data")
    roots = list(destination.iterdir())
    if len(roots) != 1 or not roots[0].is_dir():
        raise RuntimeError("Expected one runtime directory")
    runtime = roots[0]
    manifest = json.loads((runtime / "package-manifest.json").read_text(encoding="utf-8"))
    for name, digest in manifest["files"].items():
        path = (runtime / name).resolve()
        if not path.is_relative_to(runtime) or hashlib.sha256(path.read_bytes()).hexdigest() != digest:
            raise RuntimeError("Packaged file integrity check failed")
    executable = runtime / ("wwwmmm.exe" if manifest["runtime"] == "win-x64" else "wwwmmm")
    work = destination / "smoke-evidence"
    work.mkdir()
    for command in [["--help"], ["demo"], ["document", "pdf-create", str(work / "created.pdf"), str(runtime / "Web/wwwroot/favicon-32x32.png")]]:
        subprocess.run([str(executable), *command], cwd=work, check=True, timeout=120)
    if not (work / "created.pdf").read_bytes().startswith(b"%PDF-"):
        raise RuntimeError("Installed PDF writer did not create a PDF")
    corpus = work / "corpus"
    corpus.mkdir()
    shutil.copyfile(runtime / "Web/wwwroot/favicon-32x32.png", corpus / "image.png")
    if shutil.which("pdfinfo") and shutil.which("pdftoppm"):
        shutil.copyfile(work / "created.pdf", corpus / "document.pdf")
    report = work / "measurements.json"
    subprocess.run([str(executable), "benchmark-documents", str(corpus), str(report), "3"], cwd=work, check=True, timeout=180)
    data = json.loads(report.read_text(encoding="utf-8"))
    if data["failures"] or data["documentCount"] != len(list(corpus.iterdir())):
        raise RuntimeError("Installed document benchmark failed")
    original = report.read_bytes()
    rejected = subprocess.run([str(executable), "benchmark-documents", str(corpus), str(report), "1"], cwd=work, timeout=30)
    if rejected.returncode == 0 or report.read_bytes() != original:
        raise RuntimeError("Existing report overwrite protection failed")
    print(json.dumps({"runtime": manifest["runtime"], "archiveVerified": True, "documentsExercised": data["documentCount"], "customerAcceptance": False}))


if __name__ == "__main__":
    main()
