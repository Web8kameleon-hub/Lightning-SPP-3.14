"""Validate declared P1 contracts; this does not emit or reconstruct production usage."""
import argparse
from datetime import datetime, timedelta, timezone
import json
import math
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[1]
FILES = {"event": "usage-event", "daily": "daily-usage"}


def schema(kind):
    value = json.loads((ROOT / "Docs/schemas" / (FILES[kind] + ".schema.json")).read_text(encoding="utf-8"))
    Draft202012Validator.check_schema(value)
    return value


def validate(kind, value):
    def nonfinite(item):
        if isinstance(item, float):
            return not math.isfinite(item)
        if isinstance(item, dict):
            return any(nonfinite(v) for v in item.values())
        if isinstance(item, list):
            return any(nonfinite(v) for v in item)
        return False
    if nonfinite(value):
        return ["non-finite numbers are not JSON measurements"]
    errors = ["schema violation at /" + "/".join(map(str, error.path)) + ": " + str(error.validator)
              for error in Draft202012Validator(schema(kind)).iter_errors(value)]
    if errors:
        return errors  # Do not print payload values, identities or arbitrary error text.
    try:
        if kind == "event":
            datetime.fromisoformat(value["occurredAt"])
            if value["eventId"] == value["correctionOf"]:
                errors.append("correction cannot refer to itself")
            status = value["httpStatus"]
            if value["outcome"] == "succeeded" and (status is None or not 200 <= status < 400):
                errors.append("HTTP success requires an observed 2xx/3xx status")
            if status is None and value["failureCategory"] is None:
                errors.append("unavailable HTTP status requires an actual failure category")
        else:
            start = datetime.fromisoformat(value["windowStart"])
            end = datetime.fromisoformat(value["windowEnd"])
            expected = datetime.strptime(value["utcDate"], "%Y-%m-%d").replace(tzinfo=timezone.utc)
            generated = datetime.fromisoformat(value["generatedAt"])
            if start != expected or end != start + timedelta(days=1):
                errors.append("daily window must be exactly its labelled UTC calendar day")
            if generated < start or (value["completeness"] == "complete" and generated < end):
                errors.append("generation time cannot establish future coverage")
            for gap in value["coverageGaps"]:
                gap_start, gap_end = datetime.fromisoformat(gap["start"]), datetime.fromisoformat(gap["end"])
                if not start <= gap_start < gap_end <= end:
                    errors.append("coverage gap must be ordered and contained in the UTC window")
            totals = value["totals"]
            if totals is not None:
                requests = totals["requests"]
                if sum(totals[x] for x in ("succeeded", "rejected", "failed", "cancelled")) != requests:
                    errors.append("outcome counts must sum to observed requests")
                for count, total in [("durationMeasuredCount", "elapsedTotalMs"),
                                     ("inputBytesMeasuredCount", "inputBytesTotal"),
                                     ("outputBytesMeasuredCount", "outputBytesTotal")]:
                    if totals[count] > requests or (totals[count] == 0) != (totals[total] is None):
                        errors.append("measurement count and availability are inconsistent")
            if value["completeness"] != "unavailable" and not value["coveredRoutes"]:
                errors.append("measured coverage must name its canonical routes")
    except (ValueError, OverflowError):
        errors.append("invalid calendar time")
    return errors


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--kind", choices=FILES)
    parser.add_argument("file", nargs="?")
    args = parser.parse_args()
    if bool(args.kind) != bool(args.file):
        parser.error("--kind and file must be supplied together")
    if args.file:
        path = Path(args.file)
        if path.stat().st_size > 1024 * 1024:
            raise SystemExit("Contract input exceeds 1 MiB")
        errors = validate(args.kind, json.loads(path.read_text(encoding="utf-8")))
        for error in errors:
            print(error)
        return 1 if errors else 0
    for kind in FILES:
        schema(kind)
        print("PASS schema structure:", kind)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
