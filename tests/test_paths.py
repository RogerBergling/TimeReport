"""Cross-platform data directory tests."""

from __future__ import annotations

import sys
from pathlib import Path

from timereport.paths import backups_dir, database_path, default_data_dir, ensure_data_dir


def test_linux_xdg_data_home(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "linux")
    monkeypatch.delenv("TIMEREPORT_DATA_DIR", raising=False)
    monkeypatch.setenv("XDG_DATA_HOME", str(tmp_path / "xdg"))
    assert default_data_dir() == (tmp_path / "xdg" / "timereport").resolve()


def test_linux_default_home(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "linux")
    monkeypatch.delenv("TIMEREPORT_DATA_DIR", raising=False)
    monkeypatch.delenv("XDG_DATA_HOME", raising=False)
    monkeypatch.setenv("HOME", str(tmp_path / "home"))
    monkeypatch.setenv("USERPROFILE", str(tmp_path / "home"))
    assert default_data_dir() == (tmp_path / "home" / ".local" / "share" / "timereport").resolve()


def test_windows_localappdata(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "win32")
    monkeypatch.delenv("TIMEREPORT_DATA_DIR", raising=False)
    monkeypatch.setenv("LOCALAPPDATA", str(tmp_path / "local"))
    monkeypatch.setenv("APPDATA", str(tmp_path / "roaming"))
    assert default_data_dir() == (tmp_path / "local" / "TimeReport").resolve()


def test_windows_appdata_fallback(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "win32")
    monkeypatch.delenv("TIMEREPORT_DATA_DIR", raising=False)
    monkeypatch.delenv("LOCALAPPDATA", raising=False)
    monkeypatch.setenv("APPDATA", str(tmp_path / "roaming"))
    assert default_data_dir() == (tmp_path / "roaming" / "TimeReport").resolve()


def test_macos(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "darwin")
    monkeypatch.delenv("TIMEREPORT_DATA_DIR", raising=False)
    monkeypatch.setenv("HOME", str(tmp_path / "home"))
    monkeypatch.setenv("USERPROFILE", str(tmp_path / "home"))
    assert (
        default_data_dir()
        == (tmp_path / "home" / "Library" / "Application Support" / "TimeReport").resolve()
    )


def test_override_wins(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setattr(sys, "platform", "win32")
    monkeypatch.setenv("TIMEREPORT_DATA_DIR", str(tmp_path / "custom"))
    assert default_data_dir() == (tmp_path / "custom").resolve()


def test_database_and_backup_paths(monkeypatch, tmp_path: Path) -> None:
    monkeypatch.setenv("TIMEREPORT_DATA_DIR", str(tmp_path / "data"))
    assert database_path() == (tmp_path / "data" / "timereport.db").resolve()
    assert backups_dir() == (tmp_path / "data" / "backups").resolve()
    assert database_path(tmp_path / "other") == tmp_path / "other" / "timereport.db"
    assert backups_dir(tmp_path / "other") == tmp_path / "other" / "backups"


def test_ensure_data_dir(tmp_path: Path) -> None:
    target = tmp_path / "nested" / "data"
    assert ensure_data_dir(target) == target
    assert target.is_dir()
    assert ensure_data_dir(target) == target
