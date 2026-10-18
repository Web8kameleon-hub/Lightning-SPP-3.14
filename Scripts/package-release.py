#!/usr/bin/env python3
"""Create a self-contained runtime archive; never copy private build inputs."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import platform
import shutil
import subprocess
import tarfile
import xml.etree.ElementTree as ET
import zipfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--runtime", required=True, choices=["win-x64", "linux-x64"])
    parser.add_argument("--output", type=Path, default=Path("artifacts/distribution"))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[1]
    version = ET.parse(root / "wwwmmm.csproj").findtext("PropertyGroup/Version")
    name = f"lightning-spp-{version}-{args.runtime}"
    output = args.output.resolve()
    stage = output / name
    # Refuse reuse: stale or private files must never enter a later release.
    stage.mkdir(parents=True, exist_ok=False)
    subprocess.run([
        "dotnet", "publish", str(root / "wwwmmm.csproj"), "-c", "Release",
        "-r", args.runtime, "--self-contained", "true", "-p:PublishSingleFile=false",
        "--artifacts-path", str(root / "artifacts/package-build" / args.runtime),
        "-o", str(stage),
    ], check=True, cwd=root)
    for notice in ["LICENSE", "LICENSE-MIT.txt", "LICENSE-COMMERCIAL.txt", "SECURITY.md", "COMPLIANCE.md"]:
        shutil.copyfile(root / notice, stage / notice)
    shutil.copyfile(root / "Docs/INSTALLATION.md", stage / "INSTALLATION.md")
    files = sorted(p for p in stage.rglob("*") if p.is_file())
    for path in files:
        if path.name.startswith(".env") or path.name.lower() in {"sixlabors.lic", "cookies.txt", "register-body.json"}:
            raise RuntimeError("Private input found in package; publication refused")
    for variable in ["SIXLABORS_LICENSE", "SixLaborsLicenseKey"]:
        value = os.environ.get(variable)
        if value:
            for path in files:
                data = path.read_bytes()
                if value.encode() in data or value.encode("utf-16le") in data:
                    raise RuntimeError("Build secret found in package; publication refused")
    inventory = {
        "version": version, "runtime": args.runtime, "selfContained": True,
        "sourceCommit": subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=root, text=True).strip(),
        "workingTreeDirty": bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=root, text=True).strip()),
        "nativePdfDependency": "Poppler (pdfinfo and pdftoppm); install separately",
        "files": {str(p.relative_to(stage)).replace("\\", "/"): hashlib.sha256(p.read_bytes()).hexdigest() for p in files},
    }
    (stage / "package-manifest.json").write_text(json.dumps(inventory, indent=2) + "\n", encoding="utf-8")
    files.append(stage / "package-manifest.json")
    if args.runtime == "win-x64":
        archive = output / (name + ".zip")
        with zipfile.ZipFile(archive, "x", compression=zipfile.ZIP_DEFLATED) as bundle:
            for path in files:
                bundle.write(path, arcname=path.relative_to(output))
    else:
        archive = output / (name + ".tar.gz")
        with tarfile.open(archive, "x:gz") as bundle:
            for path in files:
                info = bundle.gettarinfo(str(path), arcname=str(path.relative_to(output)))
                # Cross-publishing on Windows must retain the Linux apphost execute bit.
                info.mode = 0o755 if path.name == "wwwmmm" else 0o644
                with path.open("rb") as content:
                    bundle.addfile(info, content)
    digest = hashlib.sha256(archive.read_bytes()).hexdigest()
    (output / (archive.name + ".sha256")).write_text(f"{digest}  {archive.name}\n", encoding="ascii")
    print(json.dumps({"archive": str(archive), "sha256": digest, "builtOn": platform.platform()}))


if __name__ == "__main__":
    main()
