"""Cross-platform locations for application data."""

from __future__ import annotations

import os
import sys
from pathlib import Path

APP_NAME = "TimeReport"
APP_DIR_NAME = "timereport"
DATABASE_FILENAME = "timereport.db"
BACKUPS_DIRNAME = "backups"


def default_data_dir() -> Path:
    """Return the per-user data directory for TimeReport.

    The database is deliberately kept outside the installation directory so
    replacing or upgrading the executable never removes user data.
    """

    override = os.environ.get("TIMEREPORT_DATA_DIR")
    if override:
        return Path(override).expanduser().resolve()

    if sys.platform.startswith("win"):
        base = os.environ.get("LOCALAPPDATA") or os.environ.get("APPDATA")
        if base:
            return (Path(base) / APP_NAME).resolve()
        return (Path.home() / "AppData" / "Local" / APP_NAME).resolve()

    if sys.platform == "darwin":
        return (Path.home() / "Library" / "Application Support" / APP_NAME).resolve()

    xdg_data_home = os.environ.get("XDG_DATA_HOME")
    base = Path(xdg_data_home).expanduser() if xdg_data_home else Path.home() / ".local" / "share"
    return (base / APP_DIR_NAME).resolve()


def database_path(data_dir: Path | None = None) -> Path:
    return (data_dir or default_data_dir()) / DATABASE_FILENAME


def backups_dir(data_dir: Path | None = None) -> Path:
    return (data_dir or default_data_dir()) / BACKUPS_DIRNAME


def ensure_data_dir(data_dir: Path) -> Path:
    data_dir.mkdir(parents=True, exist_ok=True)
    if os.name != "nt":
        try:
            data_dir.chmod(0o700)
        except OSError:
            pass
    return data_dir
