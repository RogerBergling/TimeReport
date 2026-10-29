"""Headless GUI smoke tests (run under Xvfb on Linux)."""

# ruff: noqa: E402

from __future__ import annotations

import os
import sys
from datetime import date

import pytest

tk = pytest.importorskip("tkinter")

from tests.helpers import make_entry
from timereport.db import Database
from timereport.service import TimeReportService
from timereport.ui.entry_dialog import EntryDialog
from timereport.ui.main_window import MainWindow
from timereport.ui.projects_dialog import ProjectsDialog

pytestmark = pytest.mark.skipif(
    not (sys.platform.startswith("linux") and os.environ.get("DISPLAY")),
    reason="GUI smoke tests require Linux with Xvfb",
)


def _service(tmp_path):
    database = Database(tmp_path / "gui.db")
    database.connect()
    service = TimeReportService(database)
    project = service.create_project("GUI project")
    service.save_entry(make_entry(date.today(), "work", "08:00", "16:00", project_id=project.id))
    return database, service


def test_main_window_builds_and_refreshes(tmp_path) -> None:
    database, service = _service(tmp_path)
    root = tk.Tk()
    root.withdraw()
    try:
        window = MainWindow(root, service, tmp_path)
        root.update_idletasks()
        assert "Week" in window.week_label.cget("text")
        assert len(window._day_cards) == 7
        assert window.week_cards["worked"]._value.cget("text") == "8:00"

        window.totals_view.period_kind.set("Month")
        window.totals_view.refresh()
        assert window.totals_view.period_label.cget("text")
    finally:
        root.destroy()
        database.close()


def test_entry_and_projects_dialogs_build(tmp_path) -> None:
    database, service = _service(tmp_path)
    root = tk.Tk()
    root.geometry("900x700")
    root.deiconify()
    root.update()
    try:
        entry_dialog = EntryDialog(root, service, date.today())
        root.update()
        assert entry_dialog.kind_var.get() == "Work"
        entry_dialog.destroy()

        projects_dialog = ProjectsDialog(root, service)
        root.update()
        assert projects_dialog.tree.get_children()
        projects_dialog.destroy()
    finally:
        root.destroy()
        database.close()
