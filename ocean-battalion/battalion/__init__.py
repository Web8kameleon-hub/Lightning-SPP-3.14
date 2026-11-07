"""Ocean battalion: a polite, auditable information-intake layer.

Pipeline: source registry → agent → extract → chunk → embed → store.

Nothing here claims to read "everything on the internet". Each source is
declared, rate limited, checked against robots.txt, and each document records
which reader produced its text and whether that reader was OCR.
"""

from battalion.config import Config, ConfigError, Source, load_config

__all__ = ["Config", "ConfigError", "Source", "load_config"]
__version__ = "0.1.0"
