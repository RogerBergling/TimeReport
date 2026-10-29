"""Backup and restore tests."""

from __future__ import annotations

import sqlite3
from datetime import date
from pathlib import Path

import pytest

from tests.helpers import make_entry
from timereport.db import SCHEMA_VERSION, Database, DatabaseError, RestoreError
from timereport.service import TimeReportService

MONDAY = date(2026, 10, 26)


def test_backup_and_restore_round_trip(
    db: Database, service: TimeReportService, project, tmp_path: Path
) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "16:00", project_id=project.id))
    backup = db.backup_to(tmp_path / "backup.db")
    assert db.validate_file(backup) == SCHEMA_VERSION

    second = service.create_project("Second")
    service.save_entry(make_entry(MONDAY, "work", "16:00", "17:00", project_id=second.id))
    assert len(service.list_entries(MONDAY, MONDAY)) == 2

    auto_backup = db.restore_from(backup)
    assert auto_backup.is_file()
    assert auto_backup.parent == tmp_path / "backups"
    assert [item.name for item in service.list_projects()] == ["Acme"]
    assert len(service.list_entries(MONDAY, MONDAY)) == 1


def test_backup_can_be_opened_as_a_database(
    db: Database, service: TimeReportService, project, tmp_path: Path
) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "16:00", project_id=project.id))
    backup = db.backup_to(tmp_path / "backup.db")
    db.close()

    reopened = Database(backup)
    reopened.connect()
    try:
        assert [item.name for item in TimeReportService(reopened).list_projects()] == ["Acme"]
    finally:
        reopened.close()


def test_invalid_restore_file_is_rejected(
    db: Database, service: TimeReportService, project, tmp_path: Path
) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "16:00", project_id=project.id))
    invalid = tmp_path / "invalid.db"
    invalid.write_text("this is not a sqlite database", encoding="utf-8")
    with pytest.raises(RestoreError):
        db.restore_from(invalid)
    assert len(service.list_entries(MONDAY, MONDAY)) == 1


def test_wrong_schema_is_rejected(db: Database, tmp_path: Path) -> None:
    wrong = tmp_path / "wrong.db"
    connection = sqlite3.connect(wrong)
    connection.execute("CREATE TABLE unrelated (id INTEGER)")
    connection.execute("PRAGMA user_version = 1")
    connection.commit()
    connection.close()
    with pytest.raises(RestoreError, match="not a TimeReport database"):
        db.restore_from(wrong)


def test_newer_schema_is_rejected(
    db: Database, service: TimeReportService, project, tmp_path: Path
) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "16:00", project_id=project.id))
    backup = db.backup_to(tmp_path / "backup.db")
    connection = sqlite3.connect(backup)
    connection.execute("PRAGMA user_version = 999")
    connection.commit()
    connection.close()
    with pytest.raises(RestoreError, match="schema version 999"):
        db.restore_from(backup)


def test_restore_from_live_database_is_rejected(db: Database) -> None:
    with pytest.raises(RestoreError):
        db.restore_from(db.path)


def test_backup_to_live_database_is_rejected(db: Database) -> None:
    with pytest.raises(DatabaseError):
        db.backup_to(db.path)
