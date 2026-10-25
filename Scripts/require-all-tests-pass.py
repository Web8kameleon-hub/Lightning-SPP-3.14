#!/usr/bin/env python3
"""Require every discovered TRX test to pass before release. No silent exclusions."""
from pathlib import Path
import sys
import xml.etree.ElementTree as ET

files = sorted(Path("Tests/LightningSPP.Tests/TestResults").rglob("*.trx"))
if not files:
    sys.exit("FAIL: no TRX test result files found")
totals = dict(total=0, passed=0, failed=0, error=0, notExecuted=0)
for file in files:
    try:
        tree = ET.parse(file)
        counters = next((node for node in tree.iter() if node.tag.rsplit("}", 1)[-1] == "Counters"), None)
        if counters is None:
            raise ValueError("missing Counters element")
        for key in totals:
            totals[key] += int(counters.get(key, "0"))
    except (ValueError, ET.ParseError) as exc:
        sys.exit(f"FAIL: invalid test report {file}: {exc}")

print(f"Test evidence: {len(files)} TRX file(s), {totals}")

if totals["total"] == 0 or totals["passed"] != totals["total"] or any(totals[k] for k in ("failed", "error", "notExecuted")):
    sys.exit("FAIL: 100% PASS required; failed, error, skipped or ignored tests are prohibited")

print("PASS: all reported tests executed and passed")
