"""Database schema, persistence and project tests."""

from __future__ import annotations

from datetime import date
from pathlib import Path

import pytest

from tests.helpers import make_entry
from timereport.db import SCHEMA_VERSION, Database, DatabaseError
from timereport.service import TimeReportService, ValidationError


def test_schema_is_created(db: Database) -> None:
    assert db.schema_version == SCHEMA_VERSION
    tables = {
        row[0]
        for row in db.conn.execute("SELECT name FROM sqlite_master WHERE type = 'table'").fetchall()
    }
    assert {"projects", "entries"} <= tables
    assert db.conn.execute("PRAGMA foreign_keys").fetchone()[0] == 1


def test_connect_is_idempotent(db: Database) -> None:
    db.connect()
    assert db.schema_version == SCHEMA_VERSION


def test_data_persists_after_reopen(tmp_path: Path) -> None:
    path = tmp_path / "persist.db"
    first = Database(path)
    first.connect()
    first_service = TimeReportService(first)
    project = first_service.create_project("Persisted")
    first_service.save_entry(
        make_entry(date(2026, 10, 26), "work", "08:00", "16:00", project_id=project.id)
    )
    first.close()

    second = Database(path)
    second.connect()
    second_service = TimeReportService(second)
    projects = second_service.list_projects()
    assert [item.name for item in projects] == ["Persisted"]
    assert len(second_service.list_entries(date(2026, 10, 26), date(2026, 10, 26))) == 1
    second.close()


def test_project_names_are_trimmed_and_unique(service: TimeReportService) -> None:
    project = service.create_project("  Acme   Corp  ")
    assert project.name == "Acme Corp"
    with pytest.raises(ValidationError):
        service.create_project("acme corp")


def test_project_rename_archive_and_delete(service: TimeReportService) -> None:
    project = service.create_project("Old name")
    renamed = service.rename_project(project.id, "New name")
    assert renamed.name == "New name"
    assert service.get_project(project.id).name == "New name"

    service.set_project_archived(project.id, True)
    assert service.list_projects() == []
    assert [item.name for item in service.list_projects(include_archived=True)] == ["New name"]
    service.set_project_archived(project.id, False)
    assert [item.name for item in service.list_projects()] == ["New name"]

    service.delete_project(project.id)
    assert service.get_project(project.id) is None


def test_used_project_cannot_be_deleted(service: TimeReportService, project) -> None:
    service.save_entry(
        make_entry(date(2026, 10, 26), "work", "08:00", "16:00", project_id=project.id)
    )
    assert service.project_entry_count(project.id) == 1
    with pytest.raises(ValidationError):
        service.delete_project(project.id)


def test_entry_crud_and_project_name(service: TimeReportService, project) -> None:
    day = date(2026, 10, 26)
    saved = service.save_entry(make_entry(day, "work", "08:00", "16:00", project_id=project.id))
    assert saved.id is not None
    assert saved.project_name == "Acme"
    assert saved.duration_minutes == 480

    fetched = service.get_entry(saved.id)
    assert fetched == saved

    updated = service.save_entry(
        make_entry(day, "work", "09:00", "17:00", project_id=project.id, entry_id=saved.id)
    )
    assert updated.id == saved.id
    assert updated.start_minute == 540

    service.rename_project(project.id, "Renamed")
    fetched = service.get_entry(saved.id)
    assert fetched.project_name == "Renamed"

    service.delete_entry(saved.id)
    assert service.get_entry(saved.id) is None


def test_backup_file_is_a_valid_database(db: Database, tmp_path: Path) -> None:
    backup = db.backup_to(tmp_path / "backup.db")
    assert backup.is_file()
    assert db.validate_file(backup) == SCHEMA_VERSION


def test_backup_to_live_path_is_rejected(db: Database) -> None:
    with pytest.raises(DatabaseError):
        db.backup_to(db.path)
