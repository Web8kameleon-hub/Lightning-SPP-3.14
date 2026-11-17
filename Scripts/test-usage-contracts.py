"""Test-only invented fixtures for contract validation, never production evidence/data."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("usage_contracts", Path(__file__).with_name("validate-usage-contracts.py"))
contracts = importlib.util.module_from_spec(spec)
spec.loader.exec_module(contracts)


def event():
    return {"schemaVersion": 1, "eventId": "00000000-0000-0000-0000-000000000001", "eventKind": "observed",
            "correctionOf": None, "occurredAt": "2026-11-16T12:00:00Z", "route": "/api/workflow/run", "method": "POST",
            "identity": {"kind": "anonymous"}, "httpStatus": 403, "outcome": "rejected", "elapsedMs": None,
            "inputBytes": None, "outputBytes": None, "plan": None, "planSource": "unavailable",
            "releaseCommit": None, "failureCategory": "unauthorized"}


def daily():
    return {"schemaVersion": 1, "utcDate": "2026-11-16", "windowStart": "2026-11-16T00:00:00Z",
            "windowEnd": "2026-11-17T00:00:00Z", "generatedAt": "2026-11-17T01:00:00Z", "revision": 1,
            "supersedesSha256": None, "evidenceState": "unavailable", "completeness": "unavailable",
            "coverageGaps": [{"start": "2026-11-16T00:00:00Z", "end": "2026-11-17T00:00:00Z", "reason": "not-instrumented"}],
            "coveredRoutes": [], "privacy": {"minimumContributors": 10, "cohortSatisfied": False},
            "suppressed": True, "totals": None, "releaseCommit": None}


class UsageContractTests(unittest.TestCase):
    def test_schemas_pass_draft_2020_12_meta_validation(self):
        for kind in contracts.FILES:
            contracts.schema(kind)

    def test_explicit_unknown_measurements_and_unavailable_day_are_valid(self):
        self.assertEqual([], contracts.validate("event", event()))
        self.assertEqual([], contracts.validate("daily", daily()))

    def test_no_ip_body_filename_cookie_or_secret_field_is_accepted(self):
        for name in ["ip", "body", "filename", "cookie", "licenseKey", "email"]:
            value = event(); value[name] = "test-only forbidden field"
            self.assertTrue(contracts.validate("event", value), name)

    def test_anonymous_identity_cannot_include_customer_identifier(self):
        value = event(); value["identity"]["customerId"] = "a" * 32
        self.assertTrue(contracts.validate("event", value))

    def test_measured_customer_requires_internal_identifier(self):
        value = event(); value["identity"] = {"kind": "customer", "customerId": "a" * 32}
        self.assertEqual([], contracts.validate("event", value))
        value["identity"]["customerId"] = "supplied@example.test"
        self.assertTrue(contracts.validate("event", value))

    def test_negative_bytes_and_unobserved_success_are_rejected(self):
        value = event(); value["inputBytes"] = -1
        self.assertTrue(contracts.validate("event", value))
        value = event(); value["httpStatus"] = None; value["outcome"] = "succeeded"
        self.assertTrue(contracts.validate("event", value))

    def test_raw_query_and_invalid_calendar_time_are_rejected(self):
        value = event(); value["route"] = "/api/workflow/report?path=private.txt"
        self.assertTrue(contracts.validate("event", value))
        value = event(); value["occurredAt"] = "2026-13-99T12:00:00Z"
        self.assertTrue(contracts.validate("event", value))

    def test_correction_requires_different_non_self_reference(self):
        value = event(); value["eventKind"] = "correction"
        self.assertTrue(contracts.validate("event", value))
        value["correctionOf"] = value["eventId"]
        self.assertTrue(contracts.validate("event", value))
        value["correctionOf"] = "00000000-0000-0000-0000-000000000002"
        self.assertEqual([], contracts.validate("event", value))

    def test_unavailable_day_cannot_publish_invented_totals(self):
        value = daily(); value["totals"] = {"requests": 0}
        self.assertTrue(contracts.validate("daily", value))

    def test_public_customer_rows_and_cohort_bypass_are_rejected(self):
        value = daily(); value["customerId"] = "a" * 32
        self.assertTrue(contracts.validate("daily", value))
        value = daily(); value["privacy"]["minimumContributors"] = 1
        self.assertTrue(contracts.validate("daily", value))

    def test_utc_day_gaps_and_future_generation_are_validated(self):
        value = daily(); value["windowEnd"] = "2026-11-18T00:00:00Z"
        self.assertTrue(contracts.validate("daily", value))
        value = daily(); value["coverageGaps"][0]["end"] = "2026-11-18T00:00:00Z"
        self.assertTrue(contracts.validate("daily", value))
        value = daily(); value["generatedAt"] = "2026-11-15T00:00:00Z"
        self.assertTrue(contracts.validate("daily", value))

    def test_revision_requires_previous_content_fingerprint(self):
        value = daily(); value["revision"] = 2
        self.assertTrue(contracts.validate("daily", value))
        value["supersedesSha256"] = "a" * 64
        self.assertEqual([], contracts.validate("daily", value))

    def test_actual_count_and_measurement_contracts_do_not_fill_unknowns_with_zero(self):
        value = daily(); value.update(evidenceState="measured", completeness="complete", coverageGaps=[],
                                     coveredRoutes=["/api/health"], suppressed=False)
        value["privacy"]["cohortSatisfied"] = True
        value["totals"] = {"requests": 10, "succeeded": 8, "rejected": 1, "failed": 1, "cancelled": 0,
            "durationMeasuredCount": 0, "elapsedTotalMs": None, "inputBytesMeasuredCount": 10,
            "inputBytesTotal": 1000, "outputBytesMeasuredCount": 0, "outputBytesTotal": None}
        self.assertEqual([], contracts.validate("daily", value))
        inconsistent = copy.deepcopy(value); inconsistent["totals"]["succeeded"] = 9
        self.assertTrue(contracts.validate("daily", inconsistent))
        inconsistent = copy.deepcopy(value); inconsistent["totals"]["elapsedTotalMs"] = 0
        self.assertTrue(contracts.validate("daily", inconsistent))
        inconsistent = copy.deepcopy(value); inconsistent["totals"]["inputBytesMeasuredCount"] = 11
        self.assertTrue(contracts.validate("daily", inconsistent))

    def test_nan_and_infinity_are_not_measurements(self):
        for number in [float("nan"), float("inf")]:
            value = event(); value["elapsedMs"] = number
            self.assertTrue(contracts.validate("event", value))


if __name__ == "__main__":
    unittest.main()
