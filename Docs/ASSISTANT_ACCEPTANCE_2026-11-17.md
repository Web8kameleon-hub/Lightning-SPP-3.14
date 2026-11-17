# Public assistant investigation — 17 November 2026

Status: local real-model conversation verified; public service remains unaccepted.

## Observed failures

- `https://scanprocessprint.com/api/assistant` returned HTTP 503 on 17 November
  at 17:58 UTC. Evidence: `artifacts/matia-public-before.json`.
- A real POST to `https://www.clisonix.com/api/ocean` returned HTTP 503 with
  `Curiosity Ocean is temporarily unavailable`. Enabling the guest switch alone
  cannot establish that the remote bridge works.
- The executable built from this branch rejected `locale: "en"` with HTTP 400,
  `Locale must be a valid language tag`. The executable enabled invariant
  globalization; the test host did not share that setting. Disabling invariant
  globalization in the application restores the culture lookup it requires.
- An unbounded direct Ollama request to the installed thinking-capable model
  exceeded a 145-second client timeout. Public local inference now requests a
  bounded 8192-token context, at most 512 generated tokens, and disables thinking
  when the discovered model reports that capability. Truncated responses still
  fail; token limits are not evidence of latency or accuracy guarantees.
- Both configured SSH aliases (`hetzner-new`, `hetzner-old`) timed out on port
  22. No production environment values were changed.
- GitHub rejected `continuous-release-evidence.yml` because its required-test
  step had neither `run` nor `uses`. The step now executes the unfiltered .NET
  suite and the existing zero-skip report gate. That workflow still requires
  real upstream bridge dependencies; this investigation does not establish
  their availability or claim that its full gate passed. Other workflows'
  `BridgeUpstreamTests` exclusions must not be interpreted as complete upstream
  acceptance.

## Executed candidate checks

- Release application build: zero warnings and errors after the final changes.
- Relevant language, guest safety and model tests: 29 executed, 29 passed,
  zero failed or skipped. TRX: `artifacts/matia-tests/matia-safety.trx`.
  This includes a transport-fixture regression asserting bounded interactive
  generation and rejection of `done_reason: "length"`. That fixture is a unit
  test, not evidence of actual inference.
- Real executable on loopback port 5189, isolated data directory, opt-in guest
  access and installed Ollama model `qwen3.5:0.8b`: full manifest/cookie/CSRF/
  conversation exchange returned `42` for 17 + 25. Recorded duration: 29.279
  seconds for that single exchange, not a benchmark aggregate.
  Evidence: `artifacts/matia-local-bounded-ready.json`.
- A subsequent real service question returned a complete answer in 57.933
  seconds, but manual review rejected its unsupported hardware advice (including
  an empty paper tray recommendation). HTTP success was not accepted as answer
  quality. Evidence: `artifacts/matia-local-service.json`. The guest instruction
  now supplies the documented product scope and explicitly requires manufacturer
  instructions, actual device evidence and uncertainty instead of invented
  hardware procedures. The repeated real question returned a complete answer in
  32.623 seconds: it explicitly denied access to devices/files/logs, required
  actual output compared with a known source and manufacturer self-tests, and
  avoided claiming completed operations. This limited case passed manual review;
  it is not a general accuracy evaluation. Evidence:
  `artifacts/matia-local-service-grounded-ready.json`. The final relevant test
  run again executed and passed all 29 cases with zero skips.
- Repository policy checks passed. This does not replace deployment acceptance.

The reusable `Scripts/verify-public-assistant.py` command records failures and
returns nonzero without substituting a backend or accepting an empty answer.
It deliberately does not claim that answer quality has been automatically
validated. Evidence files remain ignored artifacts; do not publish private
questions, cookies, CSRF tokens or environment secrets.

## Remaining acceptance

Obtain working access to the production host, confirm its active revision and
private opt-in setting, and provide a reachable real model under the existing
network/permission boundary. Validate the candidate and rollback path before
promotion. Repeat the real conversation through the public origin and review
answers against the service's documented capabilities. Until those checks pass,
the public assistant is not fixed or accepted for production.
