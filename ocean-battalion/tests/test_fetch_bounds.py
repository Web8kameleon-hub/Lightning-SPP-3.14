"""Real loopback HTTP with test-only payloads; no live-source or model-quality claims."""
import gzip
from contextlib import contextmanager
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import threading
import pytest
from battalion.fetch import FetchError, HttpFetcher


@contextmanager
def server(encoded=False):
    class Handler(BaseHTTPRequestHandler):
        def do_GET(self):
            data = gzip.compress(b"actual test bytes") if encoded else b"actual test bytes"
            self.send_response(200)
            self.send_header("Content-Length", str(len(data)))
            if encoded:
                self.send_header("Content-Encoding", "gzip")
            self.end_headers(); self.wfile.write(data)

        def log_message(self, *_):
            pass

    http = ThreadingHTTPServer(("127.0.0.1", 0), Handler)
    worker = threading.Thread(target=http.serve_forever, kwargs={"poll_interval": 0.01}, daemon=True)
    worker.start()
    try:
        yield "http://127.0.0.1:" + str(http.server_port)
    finally:
        http.shutdown(); http.server_close(); worker.join()


def test_real_fetch_is_bounded_and_never_replaces_an_oversize_body():
    with server() as url:
        assert HttpFetcher("test-only-agent").get(url).content == b"actual test bytes"
        with pytest.raises(FetchError, match="budget"):
            HttpFetcher("test-only-agent", max_bytes=4).get(url)


def test_unrequested_encoded_transport_fails_before_decompression():
    with server(encoded=True) as url:
        with pytest.raises(FetchError, match="encoded HTTP body"):
            HttpFetcher("test-only-agent").get(url)
