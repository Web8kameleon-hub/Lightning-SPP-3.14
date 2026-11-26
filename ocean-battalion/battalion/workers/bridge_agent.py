"""Workers for the services this repository actually runs.

Three upstreams exist, with contracts already fixed by the .NET bridges:

* ``clx_node``   — OS-CLX NodeDB, ``GET /v1/ndb/nodes/{nodeId}``
                   (`Integrations/OsClxNodeDbBridge.cs`).
* ``excel``      — Clisonix Excel service, ``POST /api/excel/parse``. Read-only
                   crawling cannot upload a workbook, so this agent records that
                   the contract needs a multipart write and refuses to fake it.
* ``bridge``     — any read-only GET declared in the registry, with bearer or
                   OS-CLX auth. This is how Excel Core
                   (``GET /api/lagter/v1/export``) or a new read endpoint is
                   attached without writing an agent.

Every one of them is read-only. None of them invents a response: a missing URL,
missing credential or rejected upstream produces a recorded failure with the
reason, never an empty success.
"""

from __future__ import annotations

from battalion import bridge
from battalion.fetch import Response
from battalion.workers.base import AgentResult, BaseAgent

NODE_ID_CHARACTERS = frozenset(
    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_"
)


class BridgeAgent(BaseAgent):
    """Declared read-only GETs against a real service."""

    def run(self) -> AgentResult:
        try:
            target = bridge.target_for(
                base_url_env=str(self.source.get("base_url_env", "")),
                auth=str(self.source.get("auth", "none")),
                key_env=self.source.get("key_env"),
            )
        except bridge.BridgeConfigError as error:
            self.result.failed += 1
            self.result.note(str(error))
            return self.result

        for path in self.source.list_of("paths"):
            try:
                url = bridge.build_url(target.base_url, path)
            except bridge.BridgeConfigError as error:
                self.result.failed += 1
                self.result.note(str(error))
                continue

            response = self.fetch(url, headers=target.headers)
            if response is None:
                continue
            self.ingest(response)

        return self.result


class ClxNodeAgent(BaseAgent):
    """Reads OS-CLX NodeDB nodes and ingests their telemetry as documents."""

    def run(self) -> AgentResult:
        try:
            target = bridge.target_for(
                base_url_env=str(self.source.get("base_url_env", "SPP_OSCLX_URL")),
                auth="clx",
                key_env=self.source.get("key_env", "SPP_OSCLX_API_KEY"),
            )
        except bridge.BridgeConfigError as error:
            self.result.failed += 1
            self.result.note(str(error))
            return self.result

        for node_id in self.source.list_of("nodes"):
            if not _valid_node_id(node_id):
                self.result.failed += 1
                self.result.note(
                    f"invalid node id '{node_id}': 1-128 letters, digits, "
                    f"hyphens or underscores"
                )
                continue

            url = bridge.build_url(
                target.base_url, f"v1/ndb/nodes/{node_id}"
            )
            response = self.fetch(url, headers=target.headers)
            if response is None:
                continue
            self.ingest(response)

        return self.result


class ExcelAgent(BaseAgent):
    """Excel parse/validate is a multipart write; a crawler must not pretend."""

    def run(self) -> AgentResult:
        self.result.failed += 1
        self.result.note(
            "Excel parse/validate requires uploading a workbook "
            "(POST /api/excel/parse, multipart). A read-only crawl cannot "
            "supply one, so no request was made. Use `bridge excel-parse` in the "
            ".NET CLI for a local workbook, or declare a read endpoint as a "
            "'bridge' source."
        )
        return self.result


def _valid_node_id(node_id: str) -> bool:
    return 0 < len(node_id) <= 128 and all(
        character in NODE_ID_CHARACTERS for character in node_id
    )
