import importlib.util
from pathlib import Path
import unittest
import json
import os
import subprocess
import sys
import tempfile

spec = importlib.util.spec_from_file_location('selector', Path(__file__).with_name('select-production-deploy.py'))
selector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(selector)


class SelectionTests(unittest.TestCase):
    def test_docs_and_tests_do_not_deploy(self):
        self.assertFalse(selector.classify(['README.md', 'Tests/Test.cs', 'Docs/INSTALLATION.md'])[0])

    def test_runtime_with_docs_deploys(self):
        self.assertTrue(selector.classify(['Services/LightningPipeline.cs', 'Web/wwwroot/site.css', 'CHANGELOG.md'])[0])

    def test_sensitive_or_unknown_changes_block_even_with_ui(self):
        for path in ['Persistence/Longo/Main.java', 'Licensing/LicenseManager.cs', 'Configuration/config.cs',
                     'Web/CustomerAccounts.cs', 'Web/LicenseApiServer.cs', 'wwwmmm.csproj', 'Dockerfile',
                     '.github/workflows/ci-cd.yml', 'deploy/production/deploy.sh', '.env.production',
                     'unknown/file', 'Scripts/select-production-deploy.py']:
            with self.subTest(path=path):
                self.assertFalse(selector.classify(['Web/wwwroot/site.css', path])[0])

    def test_runtime_docs_are_not_ignored(self):
        self.assertTrue(selector.classify(['Web/wwwroot/help.md'])[0])

    def test_empty_diff_does_not_deploy(self):
        self.assertFalse(selector.classify([])[0])

    def test_baseline_skips_failed_and_in_progress_deployments(self):
        responses = {
            '/deployments?environment=production&per_page=100': [{'id': 3, 'sha': 'new'}, {'id': 2, 'sha': 'bad'}, {'id': 1, 'sha': 'good'}],
            '/deployments/3/statuses?per_page=1': [{'state': 'in_progress'}],
            '/deployments/2/statuses?per_page=1': [{'state': 'failure'}],
            '/deployments/1/statuses?per_page=1': [{'state': 'success'}],
        }
        self.assertEqual(selector.last_successful_deployment(responses.__getitem__), 'good')

    def test_event_selection(self):
        cases = [
            ('pull_request', {}, False),
            ('release', {}, False),
            ('workflow_dispatch', {'inputs': {}}, False),
            ('workflow_dispatch', {'inputs': {'deploy_production': 'true'}}, True),
            ('workflow_dispatch', {'inputs': {'deploy_production': 'false'}}, False),
        ]
        for event_name, event, expected in cases:
            with self.subTest(event=event_name, payload=event), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                (root / 'event.json').write_text(json.dumps(event))
                env = dict(os.environ, GITHUB_EVENT_NAME=event_name,
                           GITHUB_EVENT_PATH=str(root / 'event.json'),
                           GITHUB_OUTPUT=str(root / 'output'),
                           GITHUB_STEP_SUMMARY=str(root / 'summary'))
                subprocess.run([sys.executable, str(Path(__file__).with_name('select-production-deploy.py'))],
                               env=env, check=True, capture_output=True)
                self.assertEqual((root / 'output').read_text(), f'deploy={str(expected).lower()}\n')

    def test_missing_baseline_fails_closed(self):
        with self.assertRaises(RuntimeError):
            selector.last_successful_deployment(lambda _: [])


if __name__ == '__main__':
    unittest.main()
