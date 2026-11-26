#!/usr/bin/env python3
"""Exercise the real guest chat, including cookie, CSRF and model response.

No substitute backend, retries or generated success responses are used.
Only HTTPS or loopback HTTP targets are accepted. Cookies and CSRF tokens
remain in memory and are never included in the evidence report.
"""

import argparse
import http.cookiejar
import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timezone
from pathlib import Path


class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self, req, fp, code, msg, headers, newurl):
        return None


def verify(origin, question):
    url = urllib.parse.urlsplit(origin)
    if (url.scheme not in ('http', 'https') or not url.hostname
            or url.username or url.password or url.query or url.fragment
            or url.path not in ('', '/')
            or (url.scheme == 'http' and url.hostname not in ('127.0.0.1', '::1', 'localhost'))):
        raise ValueError('Use an HTTPS origin or loopback HTTP origin without credentials or path.')
    origin = origin.rstrip('/')
    opener = urllib.request.build_opener(
        urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()), NoRedirect())

    def request(path, payload=None, token=None):
        headers = {'User-Agent': 'Mozilla/5.0 LightningSPP-AssistantVerification',
                   'Accept': 'application/json', 'Origin': origin}
        data = None
        if payload is not None:
            data = json.dumps(payload).encode('utf-8')
            headers['Content-Type'] = 'application/json'
            headers['X-CSRF-Token'] = token
        req = urllib.request.Request(origin + path, data=data, headers=headers)
        with opener.open(req, timeout=150) as response:
            if response.headers.get_content_type() != 'application/json':
                raise ValueError('Endpoint did not return JSON.')
            body = response.read(262145)
            if len(body) > 262144:
                raise ValueError('Response exceeds budget.')
            return json.loads(body)

    manifest = request('/api/assistant')
    token = manifest.get('csrfToken')
    if not isinstance(token, str) or not token:
        raise ValueError('Assistant did not supply a CSRF token.')
    backend = manifest.get('channel')
    if backend not in ('local', 'clisonix'):
        raise ValueError('Assistant backend was not identified; deployment may be outdated.')
    result = request('/api/assistant/conversation',
                     {'goal': question, 'backend': backend, 'locale': 'en'}, token)
    text = result.get('text')
    identity = result.get('identity')
    if not isinstance(text, str) or not text.strip():
        raise ValueError('No usable model response.')
    if not isinstance(identity, dict) or not identity.get('provider') or not identity.get('model'):
        raise ValueError('Response did not identify its provider and model label.')
    return {'backend': backend, 'identity': identity, 'answer': text,
            'scope': 'One real HTTP conversation; answer accuracy requires human review.'}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--origin', required=True)
    parser.add_argument('--question', default='What does Lightning SPP do? State any uncertainty briefly.')
    parser.add_argument('--report', type=Path, required=True)
    args = parser.parse_args()
    started = time.monotonic()
    report = {'origin': args.origin, 'timestampUtc': datetime.now(timezone.utc).isoformat(),
              'passed': False}
    try:
        report.update(verify(args.origin, args.question))
        report['passed'] = True
    except urllib.error.HTTPError as exc:
        report['error'] = f'Assistant HTTP request failed with status {exc.code}.'
    except (ValueError, OSError, urllib.error.URLError) as exc:
        report['error'] = str(exc)
    report['elapsedSeconds'] = round(time.monotonic() - started, 3)
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, indent=2))
    return 0 if report['passed'] else 1


if __name__ == '__main__':
    sys.exit(main())
