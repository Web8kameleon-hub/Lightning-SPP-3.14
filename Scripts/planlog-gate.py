#!/usr/bin/env python3
"""Validate the dynamic planlog schedule and block overdue, unverified milestones."""

from __future__ import annotations

import argparse
import json
import re
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any

PLANLOG = "Docs/USAGE_TELEMETRY_PLANLOG.md"
BEGIN = "<!-- PLANLOG_CI_BEGIN -->"
END = "<!-- PLANLOG_CI_END -->"
REQUIRED_PHASES = ("P0", "P1", "P2", "P3", "P4", "P5", "P6", "P7")
VALID_STATES = {"planned", "in_progress", "blocked", "verified"}


def fail(message: str) -> None:
    print(f"FAIL planlog gate: {message}")


def load_contract(root: Path) -> tuple[dict[str, Any] | None, list[str]]:
    path = root / PLANLOG
    if not path.is_file():
        return None, [f"required planlog is missing: {PLANLOG}"]

    text = path.read_text(encoding="utf-8")
    if text.count(BEGIN) != 1 or text.count(END) != 1:
        return None, [f"{PLANLOG} must contain one {BEGIN}/{END} JSON block"]

    block = text.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    try:
        contract = json.loads(block)
    except json.JSONDecodeError as exc:
        return None, [f"invalid planlog JSON at line {exc.lineno}: {exc.msg}"]

    if not isinstance(contract, dict):
        return None, ["planlog contract must be a JSON object"]
    return contract, []


def validate_contract(root: Path, contract: dict[str, Any], now: datetime) -> list[str]:
    errors: list[str] = []
    if contract.get("version") != 1:
        errors.append("contract version must be 1")
    if contract.get("timezone") != "Europe/Athens":
        errors.append("contract timezone must be Europe/Athens")

    phases = contract.get("phases")
    if not isinstance(phases, list):
        return errors + ["phases must be a JSON array"]

    by_id: dict[str, dict[str, Any]] = {}
    for phase in phases:
        if not isinstance(phase, dict):
            errors.append("each phase must be an object")
            continue
        phase_id = phase.get("id")
        if not isinstance(phase_id, str) or phase_id in by_id:
            errors.append(f"invalid or duplicate phase id: {phase_id!r}")
            continue
        by_id[phase_id] = phase

    if tuple(by_id) != REQUIRED_PHASES:
        errors.append(f"phase order must be exactly {', '.join(REQUIRED_PHASES)}")

    for phase_id in REQUIRED_PHASES:
        phase = by_id.get(phase_id)
        if phase is None:
            continue
        for field in ("target_local", "state", "deliverable", "acceptance", "evidence"):
            if field not in phase:
                errors.append(f"{phase_id}: missing {field}")
        state = phase.get("state")
        if state not in VALID_STATES:
            errors.append(f"{phase_id}: state must be one of {', '.join(sorted(VALID_STATES))}")
            continue
        for field in ("deliverable", "acceptance", "evidence"):
            if not isinstance(phase.get(field), list) or not phase[field]:
                errors.append(f"{phase_id}: {field} must be a non-empty array")

        target = phase.get("target_local")
        try:
            due = datetime.fromisoformat(target)
            if due.tzinfo is None or due.utcoffset() is None:
                raise ValueError("timezone offset required")
            if due.tzname() is None:
                raise ValueError("timezone information required")
        except (TypeError, ValueError) as exc:
            errors.append(f"{phase_id}: target_local must be an ISO-8601 datetime with offset ({exc})")
            continue

        if state == "verified":
            evidence = phase.get("evidence")
            if isinstance(evidence, list):
                if not evidence:
                    errors.append(f"{phase_id}: verified requires evidence paths")
                for item in evidence:
                    if not isinstance(item, str) or not item.strip():
                        errors.append(f"{phase_id}: evidence entries must be non-empty repository paths")
                        continue
                    candidate = (root / item).resolve()
                    try:
                        candidate.relative_to(root.resolve())
                    except ValueError:
                        errors.append(f"{phase_id}: evidence path escapes repository: {item}")
                        continue
                    if candidate == (root / PLANLOG).resolve():
                        errors.append(f"{phase_id}: planlog cannot be its own completion evidence")
                    elif not candidate.is_file() or candidate.stat().st_size == 0:
                        errors.append(f"{phase_id}: evidence file is missing or empty: {item}")

        if now > due.astimezone(timezone.utc) and state != "verified":
            errors.append(
                f"{phase_id}: deadline {target} passed with state '{state}'; "
                "attach required evidence and update the planlog state"
            )

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(description="Enforce Usage Telemetry Planlog deadlines and evidence.")
    parser.add_argument("--root", default=".", help="repository root")
    parser.add_argument("--now", help="ISO-8601 time override for controlled policy review")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    if args.now:
        try:
            now = datetime.fromisoformat(args.now)
            if now.tzinfo is None:
                raise ValueError("timezone offset required")
            now = now.astimezone(timezone.utc)
        except ValueError as exc:
            fail(f"--now must be an ISO-8601 datetime with timezone offset ({exc})")
            return 1
    else:
        now = datetime.now(timezone.utc)

    contract, errors = load_contract(root)
    if contract is not None:
        errors.extend(validate_contract(root, contract, now))

    if errors:
        print("PLANLOG GATE: BLOCKED")
        for error in errors:
            print(f"- {error}")
        return 1

    print(f"PLANLOG GATE: PASS ({PLANLOG}; evaluated at {now.isoformat()})")
    for phase in contract["phases"]:
        print(f"- {phase['id']}: {phase['state']} | due {phase['target_local']}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
