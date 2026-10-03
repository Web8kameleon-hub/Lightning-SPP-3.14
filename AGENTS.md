# Production rules — Lightning SPP

Lightning SPP has been in production since **2 October 2026 (2026-10-02)**. Treat every change in this repository as a change to a live production system.

## Mandatory acceptance gate: NO FAKE, PRODUCTION GRADE

- Every change must be production grade: complete, functional, secure, and verified for its intended use. This applies to code, configuration, deployment, documentation, SEO, demos, and public claims.
- No change with a known error, failing required check, unresolved regression, or unverified affected production behavior may pass review, merge, or deployment. Fix the problem and rerun the relevant checks first. If verification is blocked, report the blocker and keep the change unaccepted.
- NO FAKE: never substitute simulated processing, invented results, fabricated metrics, artificial delays, placeholder success responses, or unfinished implementations for real production behavior. Test fixtures and mocks belong only in tests and must never be presented as production evidence.
- Public descriptions, prices, feature claims, benchmarks, and demo results must match the actual implementation and verifiable evidence. Do not claim support, certification, performance, or successful checks without evidence.
- For code changes, run `dotnet build` and the relevant tests. Verify affected endpoints, workflows, and failure cases when applicable. For documentation and static content, validate the changed content, metadata, links, and formats as applicable. Record the actual checks and outcomes; never label an unrun check as passed.
- Preserve production data, secrets, authentication, authorization, and existing working behavior. Do not weaken protections to make a check pass. Deployment changes must include a recovery path and verification of service health.
- Passing checks is evidence for the cases exercised, not a guarantee that no unknown defect exists. Report any remaining uncertainty honestly; do not mark the change production ready while an acceptance requirement remains unmet.

**If it is not real, verified, and production grade, it does not pass.**
