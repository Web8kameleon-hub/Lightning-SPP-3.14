#!/usr/bin/env python3
"""Fail-closed release evidence gate for the current CI run; no synthetic results."""
import hashlib
import json
import os
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

root = Path("artifacts")
root.mkdir(exist_ok=True)
trx = sorted(Path("Tests/LightningSPP.Tests/TestResults").glob("*.trx"))
benchmark = root / "speed-benchmark.txt"
environment = root / "benchmark-environment.txt"
failures = []
if not trx:
    failures.append("No TRX test results")
total = passed = failed = skipped = 0
for file in trx:
    try:
        tree = ET.parse(file)
        counters = next((e for e in tree.iter() if e.tag.rsplit("}", 1)[-1] == "Counters"), None)
        if counters is None:
            raise ValueError("missing Counters")
        total += int(counters.get("total", "0"))
        passed += int(counters.get("passed", "0"))
        failed += int(counters.get("failed", "0")) + int(counters.get("error", "0"))
        skipped += int(counters.get("notExecuted", "0"))
    except (ET.ParseError, ValueError) as exc:
        failures.append(f"Invalid TRX {file.name}: {exc}")
if total == 0 or passed == 0 or failed or passed + skipped != total:
    failures.append(f"Test counts unacceptable: total={total} passed={passed} failed={failed} skipped={skipped}")
for file in (benchmark, environment):
    if not file.is_file() or not file.read_bytes().strip():
        failures.append(f"Missing or empty {file}")
sha = os.environ.get("GITHUB_SHA", "")
if len(sha) != 40 or any(c not in "0123456789abcdef" for c in sha.lower()):
    failures.append("Invalid commit SHA")
if environment.exists() and sha not in environment.read_text(errors="replace"):
    failures.append("Benchmark environment does not identify current commit")
files = [*trx, benchmark, environment]
hashes = {str(p): hashlib.sha256(p.read_bytes()).hexdigest() for p in files if p.is_file()}
report = {
    "commit": sha,
    "tests": {"total": total, "passed": passed, "failed": failed, "skipped": skipped},
    "evidence_sha256": hashes,
    "gate": "FAIL" if failures else "PASS",
    "errors": failures,
    "deployment_verified": False,
}
(root / "analysis-report.json").write_text(json.dumps(report, indent=2) + "\n")
(root / "analysis-report.md").write_text(
    f"# Release analysis — {sha}\n\n"
    f"Gate: **{report['gate']}**\n\n"
    f"Tests: {total} total, {passed} passed, {failed} failed, {skipped} skipped.\n\n"
    "Skipped tests are disclosed, never counted as passed.\n\n"
    "Benchmark is a raw observation, not a certified performance claim.\n\n"
    "Deployment is not verified by this report.\n\n"
    + ("\n".join(f"- ERROR: {e}" for e in failures) + "\n" if failures else "")
)
print(json.dumps(report, indent=2))
if failures:
    sys.exit(1)
