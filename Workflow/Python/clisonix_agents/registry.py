"""Top-level agent registry and orchestrator.

The registry keys agents by a normalised name, so lookups tolerate the shapes
users and integrations actually send: full-width Unicode (``ＡＬＢＡ``),
separators/hyphens (``al-ba``) and diacritics (``álbà``) all resolve to the
same pool.
"""

from __future__ import annotations

import unicodedata
from typing import Iterable, Optional


def normalize_agent_name(name: str) -> str:
    """Fold a name to its canonical registry key.

    NFKD splits accented and full-width characters into a base character plus
    combining marks; the marks and every non-alphanumeric character are then
    dropped, leaving an uppercased ASCII key.
    """
    if not name:
        return ""
    decomposed = unicodedata.normalize("NFKD", name)
    stripped = "".join(char for char in decomposed if not unicodedata.combining(char))
    return "".join(char for char in stripped if char.isalnum()).upper()


class BaseAgent:
    """Minimal agent contract: a name and a health snapshot."""

    AGENT_NAME = "AGENT"

    def __init__(self) -> None:
        self.name = self.AGENT_NAME

    def health(self) -> dict[str, str]:
        return {"agent": self.name, "status": "ready"}

    def __repr__(self) -> str:  # pragma: no cover - debugging aid
        return f"<{type(self).__name__} name={self.AGENT_NAME!r}>"


class ALBAAgent(BaseAgent):
    """Collector agent (ALBA)."""

    AGENT_NAME = "ALBA"

    def collect(self) -> dict[str, str]:
        return {"agent": self.AGENT_NAME, "action": "collect"}


class ALBIAgent(BaseAgent):
    """Neural director agent (ALBI)."""

    AGENT_NAME = "ALBI"

    def grow(self) -> dict[str, str]:
        return {"agent": self.AGENT_NAME, "action": "grow"}


class JONAAgent(BaseAgent):
    """Harmoniser agent (JONA)."""

    AGENT_NAME = "JONA"

    def harmonize(self) -> dict[str, str]:
        return {"agent": self.AGENT_NAME, "action": "harmonize"}


CORE_AGENTS: tuple[type[BaseAgent], ...] = (ALBAAgent, ALBIAgent, JONAAgent)


class AgentRegistry:
    """Name → agents mapping with normalised lookups.

    Deliberately a plain class rather than a dataclass: this module is also
    loaded by file path (``spec_from_file_location``) without a ``sys.modules``
    entry, and ``@dataclass`` resolves annotations through that entry.
    """

    def __init__(self) -> None:
        self._pools: dict[str, list[type[BaseAgent]]] = {}

    def register(self, agent_class: type[BaseAgent], *, name: Optional[str] = None) -> str:
        """Add an agent class to its pool and return the normalised key."""
        key = normalize_agent_name(name or getattr(agent_class, "AGENT_NAME", agent_class.__name__))
        if not key:
            raise ValueError(f"agent {agent_class!r} has no usable name")
        self._pools.setdefault(key, []).append(agent_class)
        return key

    def register_many(self, agent_classes: Iterable[type[BaseAgent]]) -> list[str]:
        return [self.register(agent_class) for agent_class in agent_classes]

    def get_pool(self, name: str) -> Optional[list[type[BaseAgent]]]:
        """Return the agents registered under ``name``, or ``None``."""
        return self._pools.get(normalize_agent_name(name))

    def names(self) -> list[str]:
        return sorted(self._pools)

    def __len__(self) -> int:
        return sum(len(pool) for pool in self._pools.values())


class AgentOrchestrator:
    """Routes work to the pools held by an :class:`AgentRegistry`."""

    def __init__(self, auto_register_core: bool = True) -> None:
        self._registry = AgentRegistry()
        if auto_register_core:
            self._registry.register_many(CORE_AGENTS)

    @property
    def registry(self) -> AgentRegistry:
        return self._registry

    def register(self, agent_class: type[BaseAgent], *, name: Optional[str] = None) -> str:
        return self._registry.register(agent_class, name=name)

    def pool_for(self, name: str) -> list[type[BaseAgent]]:
        return self._registry.get_pool(name) or []

    def dispatch(self, name: str) -> list[dict[str, str]]:
        """Instantiate every agent in the pool and collect their health."""
        return [agent_class().health() for agent_class in self.pool_for(name)]


__all__ = [
    "ALBAAgent",
    "ALBIAgent",
    "JONAAgent",
    "AgentOrchestrator",
    "AgentRegistry",
    "BaseAgent",
    "CORE_AGENTS",
    "normalize_agent_name",
]
