"""The commander: builds an agent per source, runs them, sums the results.

Each source is independent. One government site timing out does not stop the
RSS feed, and a source that refuses everything is visible in the run report
instead of quietly contributing zero.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
from typing import Callable, Iterable

from battalion.config import Config, Source, load_config
from battalion.fetch import Fetcher, HttpFetcher
from battalion.pipeline.ingest import Ingestor
from battalion.policy import Policy, policy_for_source
from battalion.workers import REGISTRY
from battalion.workers.base import AgentResult


@dataclass
class RunReport:
    """Everything one run did, source by source."""

    results: list[AgentResult] = field(default_factory=list)
    embedder: str = ""
    semantic: bool = False
    counter: str = ""
    approximate: bool = True

    @property
    def documents(self) -> int:
        return sum(result.documents for result in self.results)

    @property
    def chunks(self) -> int:
        return sum(result.chunks for result in self.results)

    @property
    def failed(self) -> int:
        return sum(result.failed for result in self.results)

    @property
    def refused(self) -> int:
        return sum(result.refused for result in self.results)

    @property
    def skipped(self) -> int:
        return sum(result.skipped for result in self.results)

    def render(self) -> str:
        lines = [
            f"embedder={self.embedder} semantic={str(self.semantic).lower()}",
            f"chunk counter={self.counter} "
            f"approximate={str(self.approximate).lower()}",
            "",
            f"{'SOURCE':<20} {'TYPE':<12} {'DOC':>5} {'CHUNK':>6} "
            f"{'SKIP':>5} {'REF':>4} {'FAIL':>5}",
            "-" * 62,
        ]
        for result in self.results:
            lines.append(
                f"{result.source_id:<20} {result.type:<12} "
                f"{result.documents:>5} {result.chunks:>6} "
                f"{result.skipped:>5} {result.refused:>4} "
                f"{result.failed:>5}"
            )
        lines.append("-" * 62)
        lines.append(
            f"{'TOTAL':<20} {'':<12} {self.documents:>5} {self.chunks:>6} "
            f"{self.skipped:>5} {self.refused:>4} {self.failed:>5}"
        )
        return "\n".join(lines)


class Orchestrator:
    """Runs the configured sources against one ingestor."""

    def __init__(
        self,
        config: Config,
        ingestor: Ingestor,
        *,
        fetcher_factory: Callable[[Source], Fetcher] | None = None,
        policy_factory: Callable[[Source], Policy] | None = None,
        log: Callable[[str], None] = print,
    ) -> None:
        self.config = config
        self.ingestor = ingestor
        self._fetcher_factory = fetcher_factory or _http_fetcher
        self._policy_factory = policy_factory or policy_for_source
        self._log = log

    @classmethod
    def from_file(
        cls,
        sources_path: str | Path,
        ingestor: Ingestor,
        **kwargs,
    ) -> "Orchestrator":
        return cls(load_config(sources_path), ingestor, **kwargs)

    def run(self, only: Iterable[str] | None = None) -> RunReport:
        wanted = set(only) if only else None
        report = RunReport(
            embedder=getattr(self.ingestor.embedder, "name", "unknown"),
            semantic=bool(getattr(self.ingestor, "is_semantic", False)),
            counter=self.ingestor.counter.name,
            approximate=self.ingestor.counter.approximate,
        )

        for source in self.config.sources:
            if wanted is not None and source.id not in wanted:
                continue

            agent_class = REGISTRY.get(source.type)
            if agent_class is None:
                # Config validation rejects unknown types, so this is a guard
                # against a registry that drifted from the config module.
                report.results.append(
                    AgentResult(
                        source_id=source.id,
                        type=source.type,
                        notes=[f"no agent registered for type {source.type}"],
                    )
                )
                continue

            agent = agent_class(
                source,
                self._policy_factory(source),
                self.ingestor,
                self._fetcher_factory(source),
                log=self._log,
            )
            try:
                report.results.append(agent.run())
            except Exception as error:  # one source must not sink the run
                failed = AgentResult(
                    source_id=source.id,
                    type=source.type,
                    failed=1,
                    notes=[f"agent crashed: {type(error).__name__}: {error}"],
                )
                report.results.append(failed)
                self._log(f"[{source.id}] crashed: {error}")

        return report


def _http_fetcher(source: Source) -> Fetcher:
    user_agent = str(
        source.get(
            "user_agent",
            "OceanBattalion/0.1 (+https://example.invalid/contact)",
        )
    )
    return HttpFetcher(user_agent=user_agent)
