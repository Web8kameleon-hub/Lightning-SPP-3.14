#!/usr/bin/env python3
"""Enforce the dynamic Usage Telemetry Planlog schedule and evidence contract."""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
from datetime import datetime, timezone
from pathlib import Path
from typing import Any
from zoneinfo import ZoneInfo, ZoneInfoNotFoundError

PLANLOG = "Docs/USAGE_TELEMETRY_PLANLOG.md"
BEGIN = "<!-- PLANLOG_CI_BEGIN -->"
END = "<!-- PLANLOG_CI_END -->"
REQUIRED_PHASES = ("P0", "P1", "P2", "P3", "P4", "P5", "P6", "P7")
VALID_STATES = {"planned", "in_progress", "blocked", "verified"}
AMENDABLE_FIELDS = ("target_local", "state", "deliverable", "acceptance", "evidence", "owner")


def load_contract_text(text: str) -> tuple[dict[str, Any] | None, list[str]]:
    if text.count(BEGIN) != 1 or text.count(END) != 1:
        return None, [f"planlog must contain exactly one {BEGIN}/{END} JSON block"]
    block = text.split(BEGIN, 1)[1].split(END, 1)[0].strip()
    try:
        value = json.loads(block)
    except json.JSONDecodeError as exc:
        return None, [f"invalid planlog JSON at line {exc.lineno}: {exc.msg}"]
    if not isinstance(value, dict):
        return None, ["planlog contract must be a JSON object"]
    return value, []


def load_contract(root: Path) -> tuple[dict[str, Any] | None, list[str]]:
    path = root / PLANLOG
    if not path.is_file():
        return None, [f"required planlog is missing: {PLANLOG}"]
    return load_contract_text(path.read_text(encoding="utf-8"))


def baseline_contract(root: Path) -> tuple[dict[str, Any] | None, list[str]]:
    """Read the planlog from the PR base or previous commit for amendment checks."""
    event = os.environ.get("GITHUB_EVENT_NAME", "")
    base_ref = "HEAD^1" if event == "pull_request" else "HEAD^"
    try:
        exists = subprocess.run(
            ["git", "cat-file", "-e", f"{base_ref}^{{commit}}"],
            cwd=root, capture_output=True, text=True, timeout=30,
        )
    except (OSError, subprocess.SubprocessError) as exc:
        return None, [f"cannot inspect Planlog baseline: {exc}"]
    if exists.returncode != 0:
        return None, ["Planlog amendment check requires full git history; configure checkout fetch-depth: 0"]

    path_exists = subprocess.run(
        ["git", "cat-file", "-e", f"{base_ref}:{PLANLOG}"],
        cwd=root, capture_output=True, text=True, timeout=30,
    )
    if path_exists.returncode != 0:
        # First introduction of the planlog has no earlier contract to amend.
        return None, []

    result = subprocess.run(
        ["git", "show", f"{base_ref}:{PLANLOG}"],
        cwd=root, capture_output=True, text=True, timeout=30,
    )
    if result.returncode != 0:
        return None, ["could not read the Planlog from its base revision"]
    return load_contract_text(result.stdout)


def phase_map(contract: dict[str, Any]) -> dict[str, dict[str, Any]]:
    return {
        phase["id"]: phase
        for phase in contract.get("phases", [])
        if isinstance(phase, dict) and isinstance(phase.get("id"), str)
    }


def validate_amendments(
    current: dict[str, Any], baseline: dict[str, Any] | None
) -> list[str]:
    if baseline is None:
        return []
    amendments = current.get("amendments")
    if not isinstance(amendments, list):
        return ["amendments must be an array"]

    old_phases = phase_map(baseline)
    new_phases = phase_map(current)
    errors: list[str] = []
    for phase_id in REQUIRED_PHASES:
        old_phase = old_phases.get(phase_id)
        new_phase = new_phases.get(phase_id)
        if old_phase is None or new_phase is None:
            continue
        for field in AMENDABLE_FIELDS:
            old_value = old_phase.get(field)
            new_value = new_phase.get(field)
            if old_value == new_value:
                continue
            matching = [
                item for item in amendments
                if isinstance(item, dict)
                and item.get("phase") == phase_id
                and item.get("field") == field
                and item.get("from") == old_value
                and item.get("to") == new_value
                and isinstance(item.get("reason"), str)
                and item["reason"].strip()
                and isinstance(item.get("decision_owner"), str)
                and item["decision_owner"].strip()
                and isinstance(item.get("evidence"), list)
                and item["evidence"]
            ]
            if not matching:
                errors.append(
                    f"{phase_id}: {field} changed without a dated amendment "
                    "record containing prior/new values, reason, decision owner and evidence"
                )
            else:
                for item in matching:
                    try:
                        stamp = datetime.fromisoformat(item.get("date_time", ""))
                        if stamp.tzinfo is None or stamp.utcoffset() is None:
                            raise ValueError("timezone offset required")
                    except (TypeError, ValueError):
                        errors.append(f"{phase_id}: amendment date_time must be ISO-8601 with timezone")
                    for evidence_path in item.get("evidence", []):
                        if not isinstance(evidence_path, str) or not evidence_matches(root, evidence_path):
                            errors.append(f"{phase_id}: amendment evidence is missing or empty: {evidence_path}")
    return errors


def evidence_matches(root: Path, pattern: str) -> list[Path]:
    if not pattern or Path(pattern).is_absolute() or ".." in Path(pattern).parts:
        return []
    return [
        p for p in root.glob(pattern)
        if p.is_file() and p.stat().st_size > 0
        and p.resolve().is_relative_to(root.resolve())
        and p.resolve() != (root / PLANLOG).resolve()
    ]


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

    try:
        zone = ZoneInfo(contract.get("timezone", ""))
    except (ZoneInfoNotFoundError, TypeError):
        return errors + ["contract timezone is not available in this Python runtime"]

    previous_due: datetime | None = None
    for phase_id in REQUIRED_PHASES:
        phase = by_id.get(phase_id)
        if phase is None:
            continue
        for field in ("target_local", "state", "deliverable", "acceptance", "evidence", "owner"):
            if field not in phase:
                errors.append(f"{phase_id}: missing {field}")

        state = phase.get("state")
        if state not in VALID_STATES:
            errors.append(f"{phase_id}: state must be one of {', '.join(sorted(VALID_STATES))}")
            continue

        for field in ("deliverable", "acceptance", "evidence"):
            values = phase.get(field)
            if not isinstance(values, list) or not values or not all(
                isinstance(value, str) and value.strip() for value in values
            ):
                errors.append(f"{phase_id}: {field} must be a non-empty string array")
        if not isinstance(phase.get("owner"), str) or not phase["owner"].strip():
            errors.append(f"{phase_id}: owner must be a non-empty string")

        target = phase.get("target_local")
        try:
            due_local = datetime.fromisoformat(target)
            if due_local.tzinfo is None or due_local.utcoffset() is None:
                raise ValueError("timezone offset required")
            local_wall_time = due_local.replace(tzinfo=None)
            if local_wall_time.replace(tzinfo=zone).utcoffset() != due_local.utcoffset():
                raise ValueError(f"offset does not match {contract['timezone']}")
        except (TypeError, ValueError) as exc:
            errors.append(f"{phase_id}: target_local must be an ISO-8601 datetime in {contract['timezone']} ({exc})")
            continue

        due_utc = due_local.astimezone(timezone.utc)
        if previous_due is not None and due_utc <= previous_due:
            errors.append(f"{phase_id}: deadlines must be strictly increasing")
        previous_due = due_utc

        if state == "verified":
            for pattern in phase.get("evidence", []):
                matches = evidence_matches(root, pattern)
                if not matches:
                    errors.append(f"{phase_id}: verified evidence is missing or empty: {pattern}")

        if now > due_utc and state != "verified":
            errors.append(
                f"{phase_id}: deadline {target} passed with state '{state}'; "
                "attach non-empty evidence and update the Planlog state"
            )

    return errors


def main() -> int:
    parser = argparse.ArgumentParser(
        description="Enforce dynamic Usage Telemetry Planlog deadlines, evidence and amendments."
    )
    parser.add_argument("--root", default=".", help="repository root")
    parser.add_argument("--now", help="ISO-8601 override for controlled policy review")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    try:
        if args.now:
            parsed_now = datetime.fromisoformat(args.now)
            if parsed_now.tzinfo is None or parsed_now.utcoffset() is None:
                raise ValueError("timezone offset required")
            now = parsed_now.astimezone(timezone.utc)
        else:
            now = datetime.now(timezone.utc)
    except ValueError as exc:
        print(f"PLANLOG GATE: BLOCKED\n- invalid evaluation time: {exc}")
        return 1

    contract, errors = load_contract(root)
    baseline, baseline_errors = baseline_contract(root)
    errors.extend(baseline_errors)
    if contract is not None:
        errors.extend(validate_contract(root, contract, now))
        errors.extend(validate_amendments(contract, baseline))

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
