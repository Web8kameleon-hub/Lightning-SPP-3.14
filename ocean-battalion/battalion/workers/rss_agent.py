"""RSS/Atom agent — the safest first source in any battalion.

Feeds are published for machine consumption, so there is no scraping decision
to make. Each entry's content (or summary) goes through the same ingest path as
any other document, tagged with the entry link so a chunk can be traced back.
"""

from __future__ import annotations

from xml.etree import ElementTree

from battalion.fetch import Response
from battalion.workers.base import AgentResult, BaseAgent

_ATOM = "{http://www.w3.org/2005/Atom}"


def parse_feed(body: bytes | str) -> list[dict]:
    """Parse RSS 2.0 or Atom into `[{title, link, content}]`.

    feedparser is used when installed because it tolerates broken feeds; the
    standard library parser is the fallback so the agent still works without it.
    """
    try:
        import feedparser

        parsed = feedparser.parse(body)
        if parsed.entries:
            return [
                {
                    "title": entry.get("title", ""),
                    "link": entry.get("link", ""),
                    "content": _entry_content(entry),
                }
                for entry in parsed.entries
            ]
    except ImportError:
        pass

    return _parse_with_stdlib(body)


def _entry_content(entry) -> str:
    blocks = entry.get("content") or []
    if blocks:
        return "\n".join(block.get("value", "") for block in blocks)
    return entry.get("summary", "") or entry.get("description", "")


def _parse_with_stdlib(body: bytes | str) -> list[dict]:
    text = body if isinstance(body, str) else body.decode("utf-8", "replace")
    try:
        root = ElementTree.fromstring(text)
    except ElementTree.ParseError:
        return []

    entries: list[dict] = []
    for item in root.iter():
        tag = item.tag
        if tag in ("item", f"{_ATOM}entry"):
            entries.append(_stdlib_entry(item))
    return entries


def _stdlib_entry(item) -> dict:
    title = _child_text(item, ("title", f"{_ATOM}title"))
    link = _child_text(item, ("link", f"{_ATOM}link"))
    if not link:
        for child in item:
            if child.tag.endswith("link") and child.get("href"):
                link = child.get("href", "")
                break
    content = _child_text(
        item,
        ("encoded", "description", "summary", f"{_ATOM}content", f"{_ATOM}summary"),
    )
    return {"title": title, "link": link, "content": content}


def _child_text(item, wanted: tuple[str, ...]) -> str:
    for child in item:
        for name in wanted:
            if child.tag == name or child.tag.endswith(name):
                return (child.text or "").strip()
    return ""


class RssAgent(BaseAgent):
    """Reads each configured feed and ingests its entries."""

    def run(self) -> AgentResult:
        for feed_url in self.source.list_of("feeds"):
            response = self.fetch(feed_url)
            if response is None:
                continue
            entries = parse_feed(response.content)
            self.result.note(f"{feed_url}: {len(entries)} entries")
            self._log(f"[{self.source.id}] {feed_url}: {len(entries)} entries")

            for entry in entries:
                # A title alone is not a document. Entries without content are
                # counted as skipped so the run report shows them.
                body = entry.get("content") or ""
                if not body.strip():
                    self.result.skipped += 1
                    self.result.note(
                        f"entry without content in {feed_url}: "
                        f"{entry.get('link', '?')}"
                    )
                    continue
                target = entry.get("link") or feed_url
                self.ingest(
                    Response(
                        url=target,
                        status=200,
                        content=body.encode("utf-8"),
                        headers={"content-type": "text/html; charset=utf-8"},
                    )
                )
        return self.result
