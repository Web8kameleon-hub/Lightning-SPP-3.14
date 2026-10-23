# Workspace production recovery and module mapping

## 1. Identify the deployed version (read only)
Run `bash deploy/production/deploy.sh status` and `bash deploy/production/workspace-preflight.sh` on the production host. Compare active release SHA to GitHub main. Do not assume a merged PR was deployed.

## 2. Restore without disabling security
The production workflow API intentionally returns 503 if `LSPP_WORKFLOW_ENABLED` is not true. A verified account must also be in `LSPP_WORKFLOW_ALLOWED_EMAILS`. The browser page may exist while the authenticated API is unavailable. Check `/workspace` separately from `/api/workflow`; anonymous API 401/403 is not a failure. Use `enable-workspace.sh discover` to test Ollama reachability without changes. Do not run `enable` or deploy until a verified account and active release are confirmed. Preserve server private environment, model routes, account state and Six Labors license.

## 3. Module/tool mapping contract
The source of truth is `WorkflowApi.Registry` (FileTools, TableTools, DomainTools, ModelGateway, DeviceTools, StripeWorkspaceTools and operator-configured ServiceAdapters). Each capability must have a stable tool ID, input contract, permission, execution implementation, and verifiable result. The assistant prompt is NOT a tool registry or authorization layer. Discover contracts from the authenticated workflow API; map the user goal to available tools, validate the plan, require review, execute and record evidence. Unknown/unreachable modules are unavailable, never simulated.

For Clisonix services: use `LSPP_TOOL_ADAPTERS_FILE` only for proven read-only endpoints. Register exact allowed hosts/paths; verify network reachability from the application container, upstream auth, response schema and semantics. Mutating endpoints require separate permissioned tools and explicit confirmation; do not expose them through the read-only adapter.

## 4. Model quality and routing
Use operator-controlled `LSPP_MODEL_ROUTES_FILE`. Discover installed models and actual capabilities through provider APIs; do not assume a model exists based on its name. Evaluate candidate models on representative Albanian/German/English document tasks, extraction accuracy, tool argument correctness, evidence fidelity, latency, memory and hardware limits. Keep local/remote consent boundaries. Prefer measured task quality to size; fail visibly if no qualified model is available. No silent fallback to a remote provider.

## 5. Release acceptance
A green /health or HTTP 200 alone is insufficient. Block promotion unless a real authenticated session can: open workspace; enumerate authorized tool contracts and models; import a sample PDF; execute a reviewed plan with a registered tool; verify source hashes and output; export a valid result; and recover from a deliberate failed step. Check unauthorized access and wrong-user isolation. Capture commit SHA, tested image digest, TRX/browser results, benchmark, changelog, deployment SHA and rollback reference. An unexecuted test must not be reported as PASS.

This document describes acceptance gates; it does not assert the live server passed them.
