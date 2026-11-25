# Usage telemetry Planlog CI failure

## Observed failure

The [Build & Test job in run 45050518082](https://github.com/Web8kameleon-hub/Lightning-SPP-3.14/actions/runs/45050518082/job/137935790787) failed on 23 November 2026 when `Scripts/planlog-gate.py` reported that P0 had passed its 23 November 17:00 Athens deadline while still `planned`. The job stopped at the gate before build and tests.

## Schedule decision

On 25 November the repository owner requested changing the dates. The P0–P7 targets were moved to 30 November through 18 December 2026; individual old and new dates are recorded in the Planlog amendments. This changes the schedule only. The phases remain planned and their deliverables and acceptance criteria have not been verified by this amendment. The gate will block again if a revised deadline passes without verified evidence.
