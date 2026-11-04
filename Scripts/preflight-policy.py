#!/usr/bin/env python3
"""Repository policy gate.

Runs before any deployment and fails closed. Every check maps to a claim this
repository makes; if the claim cannot be verified, the gate blocks.

Usage:
    python Scripts/preflight-policy.py [--root .]

Exit codes: 0 all checks passed, 1 at least one check failed.
"""

from __future__ import annotations

import argparse
import re
import subprocess
import sys
from pathlib import Path

REQUIRED_FILES = [
    "README.md",
    "MANUAL.md",
    "SECURITY.md",
    "COMPLIANCE.md",
    "Docs/MANUAL/README.md",
    "Docs/COMPLIANCE/README.md",
    "Docs/COMPLIANCE/GAP_ANALYSIS.md",
    "Docs/COMPLIANCE/RISK_REGISTER.md",
    "Docs/COMPLIANCE/SOUP_INVENTORY.md",
    "Docs/COMPLIANCE/SECURITY_CONTROLS.md",
    "Docs/COMPLIANCE/TRACEABILITY.md",
    "Licensing/LicenseSignature.cs",
    "Tests/LightningSPP.Tests/LicenseAsymmetricTests.cs",
    "Tests/LightningSPP.Tests/FlowExtractionTests.cs",
    "Tests/LightningSPP.Tests/FlowBatchAndPackageTests.cs",
]

# Phrases that must never appear as a product claim. Each is a real claim the
# repository cannot support; the gate keeps them from creeping back.
BANNED_CLAIMS = [
    "iso 27001 certified",
    "iso 13485 certified",
    "soc 2 certified",
    "hipaa compliant",
    "ce marked",
    "fda cleared",
    "fda 510(k) cleared",
]

PUBLIC_DOC_GLOBS = ["*.md", "Docs/**/*.md", "Wiki/*.md"]

# Code must not reference the removed simulated path.
CODE_ROOTS = ["Services", "PublicAPI", "SDK", "Web", "Documents", "Configuration", "Licensing", "Flow", "Batch", "Extraction", "Intake", "Packaging", "Provenance"]


def check_required_files(root: Path) -> list[str]:
    return [f"missing required file: {name}" for name in REQUIRED_FILES if not (root / name).is_file()]


def check_banned_claims(root: Path) -> list[str]:
    problems: list[str] = []
    seen: set[Path] = set()
    for pattern in PUBLIC_DOC_GLOBS:
        for path in root.glob(pattern):
            if path in seen or not path.is_file():
                continue
            seen.add(path)
            text = path.read_text(encoding="utf-8", errors="replace").lower()
            for banned in BANNED_CLAIMS:
                if banned in text:
                    problems.append(f"banned claim '{banned}' in {path.relative_to(root)}")
    return problems


def check_removed_api(root: Path) -> list[str]:
    problems: list[str] = []
    for directory in CODE_ROOTS:
        base = root / directory
        if not base.is_dir():
            continue
        for path in base.rglob("*.cs"):
            text = path.read_text(encoding="utf-8", errors="replace")
            if "UseSimulatedServices" in text or "FastSimulated" in text:
                problems.append(
                    f"removed simulated API referenced in " f"{path.relative_to(root)}"
                )
    return problems


def check_manifest_links(root: Path) -> list[str]:
    security = root / "SECURITY.md"
    if not security.is_file():
        return ["SECURITY.md is missing"]
    text = security.read_text(encoding="utf-8", errors="replace")
    problems: list[str] = []
    for match in re.finditer(r"\]\((Docs/COMPLIANCE/[^)]+)\)", text):
        target = root / match.group(1)
        if not target.is_file():
            problems.append(f"SECURITY.md links a missing document: {match.group(1)}")
    return problems


def check_whitespace(root: Path) -> list[str]:
    try:
        result = subprocess.run(
            ["git", "diff", "--check"],
            cwd=root,
            capture_output=True,
            text=True,
            timeout=120,
        )
    except (OSError, subprocess.SubprocessError):
        return ["git diff --check could not run; whitespace policy unverified"]

    if result.returncode == 0:
        return []
    lines = [line for line in result.stdout.splitlines() if line.strip()]
    return [f"git diff --check: {line}" for line in lines[:20]]


CHECKS = [
    ("required documents", check_required_files),
    ("banned certification claims", check_banned_claims),
    ("removed simulated API", check_removed_api),
    ("compliance links", check_manifest_links),
    ("whitespace", check_whitespace),
]


def main() -> int:
    parser = argparse.ArgumentParser(description="Repository policy gate")
    parser.add_argument("--root", default=".", help="repository root")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    failures = 0

    for name, check in CHECKS:
        problems = check(root)
        if problems:
            failures += 1
            print(f"FAIL {name}")
            for problem in problems:
                print(f"     {problem}")
        else:
            print(f"PASS {name}")

    print()
    if failures:
        print(f"GATE: BLOCKED ({failures} failing check(s))")
        return 1

    print("GATE: PASS — policy checks passed")
    print("      build, test and device acceptance are still required")
    return 0


if __name__ == "__main__":
    sys.exit(main())
