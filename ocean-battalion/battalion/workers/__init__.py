"""The agent registry: source type → agent class.

Adding a source type is a two-line change here plus the class itself. The
registry is what the orchestrator consults, so an unknown type fails at load
time (config validation) instead of at crawl time.
"""

from battalion.workers.api_agent import GoogleCseAgent, MicrosoftGraphAgent
from battalion.workers.base import AgentResult, BaseAgent
from battalion.workers.bridge_agent import BridgeAgent, ClxNodeAgent, ExcelAgent
from battalion.workers.opendata_agent import OpenDataAgent
from battalion.workers.rss_agent import RssAgent
from battalion.workers.web_crawler import WebCrawler

REGISTRY: dict[str, type[BaseAgent]] = {
    "rss": RssAgent,
    "web": WebCrawler,
    "opendata": OpenDataAgent,
    "api_google": GoogleCseAgent,
    "api_graph": MicrosoftGraphAgent,
    # Services that actually exist in this repository.
    "bridge": BridgeAgent,
    "clx_node": ClxNodeAgent,
    "excel": ExcelAgent,
}

__all__ = [
    "REGISTRY",
    "AgentResult",
    "BaseAgent",
    "BridgeAgent",
    "ClxNodeAgent",
    "ExcelAgent",
    "GoogleCseAgent",
    "MicrosoftGraphAgent",
    "OpenDataAgent",
    "RssAgent",
    "WebCrawler",
]
