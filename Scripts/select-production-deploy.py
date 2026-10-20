#!/usr/bin/env python3
"""Fail-closed production selection against the last successful deployment."""
import json
import os
import subprocess
import urllib.request
from pathlib import PurePosixPath


def classify(paths):
    runtime = []
    review = []
    for path in paths:
        p = PurePosixPath(path)
        # Only known, non-runtime documentation/test trees are ignored.
        if path.startswith(('Tests/', 'Docs/', 'Wiki/', 'evidence/')) or (
            len(p.parts) == 1 and p.suffix == '.md'
        ):
            continue
        # Static public UI and processing code may deploy after all CI gates.
        if path.startswith(('Services/', 'PublicAPI/', 'SDK/', 'Web/wwwroot/')):
            runtime.append(path)
        else:
            # Includes auth/account server code, persistence, dependencies,
            # workflows, Docker, deploy scripts, and all unknown paths.
            review.append(path)
    return bool(runtime) and not review, runtime, review


def last_successful_deployment(api):
    # Bounded lookup: absence of evidence blocks automatic deployment.
    deployments = api('/deployments?environment=production&per_page=100')
    for deployment in deployments:
        statuses = api(f"/deployments/{deployment['id']}/statuses?per_page=1")
        if statuses and statuses[0]['state'] == 'success':
            return deployment['sha']
    raise RuntimeError('No successful production deployment found in the last 100 records')


def main():
    event = json.loads(open(os.environ['GITHUB_EVENT_PATH'], encoding='utf-8').read())
    event_name = os.environ['GITHUB_EVENT_NAME']
    deploy = False
    reason = 'This event does not deploy production'
    if event_name == 'workflow_dispatch':
        deploy = event.get('inputs', {}).get('deploy_production') in (True, 'true')
        reason = 'Explicit production deployment requested' if deploy else 'Validation/publication only'
    elif event_name == 'push' and os.environ['GITHUB_REF'] == 'refs/heads/main':
        try:
            repo = os.environ['GITHUB_REPOSITORY']
            def api(path):
                req = urllib.request.Request(
                    f"{os.environ['GITHUB_API_URL']}/repos/{repo}{path}",
                    headers={'Authorization': f"Bearer {os.environ['GITHUB_TOKEN']}",
                             'Accept': 'application/vnd.github+json'})
                with urllib.request.urlopen(req, timeout=30) as response:
                    return json.load(response)
            base = last_successful_deployment(api)
            head = os.environ['GITHUB_SHA']
            subprocess.run(['git', 'merge-base', '--is-ancestor', base, head], check=True)
            changed = subprocess.check_output(
                ['git', 'diff', '--no-renames', '--name-only', '-z', base, head]
            ).decode('utf-8').split('\0')
            deploy, runtime, review = classify([p for p in changed if p])
            reason = f'Baseline {base}; runtime paths: {len(runtime)}; paths requiring explicit deploy: {len(review)}'
        except Exception as exc:
            # Do not print API response bodies or credentials.
            reason = f'Automatic deployment blocked: {type(exc).__name__}; baseline could not be verified'
    with open(os.environ['GITHUB_OUTPUT'], 'a', encoding='utf-8') as output:
        output.write(f'deploy={str(deploy).lower()}\n')
    with open(os.environ['GITHUB_STEP_SUMMARY'], 'a', encoding='utf-8') as summary:
        summary.write(f'Production selection: **{str(deploy).lower()}**\n\n{reason}\n')
    print(reason)


if __name__ == '__main__':
    main()
