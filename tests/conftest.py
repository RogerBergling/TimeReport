"""Shared pytest fixtures."""

from __future__ import annotations

from pathlib import Path

import pytest

from timereport.db import Database
from timereport.service import TimeReportService


@pytest.fixture
def db(tmp_path: Path):
    database = Database(tmp_path / "timereport.db")
    database.connect()
    yield database
    database.close()


@pytest.fixture
def service(db: Database) -> TimeReportService:
    return TimeReportService(db)


@pytest.fixture
def project(service: TimeReportService):
    return service.create_project("Acme")
