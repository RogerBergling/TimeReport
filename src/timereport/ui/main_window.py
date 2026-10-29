"""Main application window and weekly timesheet."""

from __future__ import annotations

import logging
import os
import subprocess
import sys
import tkinter as tk
from collections import defaultdict
from datetime import date, datetime, timedelta
from pathlib import Path
from tkinter import filedialog, messagebox, ttk

from .. import __version__
from ..db import DatabaseError, RestoreError
from ..models import ENTRY_KIND_LABELS, Entry
from ..service import TimeReportService, ValidationError
from ..timeutil import (
    format_duration,
    format_week_range,
    week_number,
    week_start,
)
from .day_card import DayCard
from .entry_dialog import EntryDialog
from .projects_dialog import ProjectsDialog
from .theme import COLORS, configure_theme
from .totals_view import TotalsView
from .widgets import ScrollableFrame, StatCard

logger = logging.getLogger(__name__)


class MainWindow:
    def __init__(self, root: tk.Tk, service: TimeReportService, data_dir: Path) -> None:
        self.root = root
        self.service = service
        self.data_dir = Path(data_dir)
        self.current_week_start = week_start(date.today())
        self._day_cards: list[DayCard] = []

        configure_theme(root)
        root.title(f"TimeReport {__version__}")
        root.geometry("1180x800")
        root.minsize(980, 640)
        root.option_add("*tearOff", False)
        root.protocol("WM_DELETE_WINDOW", self._on_close)

        self._build_menu()
        self._build_layout()
        self._bind_shortcuts()
        self.refresh()

    def _build_menu(self) -> None:
        menubar = tk.Menu(self.root)

        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(
            label="Backup database…",
            accelerator="Ctrl+B",
            command=self.backup_database,
        )
        file_menu.add_command(label="Restore database…", command=self.restore_database)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self._on_close)
        menubar.add_cascade(label="File", menu=file_menu)

        data_menu = tk.Menu(menubar, tearoff=0)
        data_menu.add_command(label="Projects…", accelerator="Ctrl+P", command=self.open_projects)
        data_menu.add_command(label="Refresh", accelerator="F5", command=self.refresh)
        menubar.add_cascade(label="Data", menu=data_menu)

        help_menu = tk.Menu(menubar, tearoff=0)
        help_menu.add_command(label="About TimeReport", command=self.show_about)
        menubar.add_cascade(label="Help", menu=help_menu)

        self.root.configure(menu=menubar)

    def _build_layout(self) -> None:
        header = tk.Frame(self.root, bg=COLORS["bg"])
        header.pack(fill="x", padx=18, pady=(14, 6))
        ttk.Label(header, text="TimeReport", style="Title.TLabel").pack(side="left")
        ttk.Label(header, text="Local time reporting", style="Muted.TLabel").pack(
            side="left", padx=(12, 0), pady=(8, 0)
        )

        header_buttons = ttk.Frame(header, style="App.TFrame")
        header_buttons.pack(side="right")
        ttk.Button(header_buttons, text="Projects", command=self.open_projects).pack(
            side="left", padx=(0, 4)
        )
        ttk.Button(header_buttons, text="Backup", command=self.backup_database).pack(
            side="left", padx=(0, 4)
        )

        status = tk.Frame(self.root, bg=COLORS["bg"])
        status.pack(side="bottom", fill="x", padx=18, pady=(0, 8))
        self.status_var = tk.StringVar(value=f"Database: {self.service.db.path}")
        ttk.Label(status, textvariable=self.status_var, style="Status.TLabel").pack(side="left")
        ttk.Label(
            status,
            text="All data stays on this computer",
            style="Status.TLabel",
        ).pack(side="right")

        self.notebook = ttk.Notebook(self.root)
        self.notebook.pack(fill="both", expand=True, padx=18, pady=(0, 6))

        self.timesheet_tab = ttk.Frame(self.notebook, style="App.TFrame")
        self.notebook.add(self.timesheet_tab, text="Timesheet")
        self.totals_view = TotalsView(self.notebook, self.service)
        self.notebook.add(self.totals_view, text="Totals")
        self.notebook.bind("<<NotebookTabChanged>>", self._on_tab_changed)

        self._build_timesheet(self.timesheet_tab)

    def _build_timesheet(self, parent: ttk.Frame) -> None:
        navigation = ttk.Frame(parent, style="App.TFrame")
        navigation.pack(fill="x", pady=(8, 6))

        self.prev_week_button = ttk.Button(
            navigation, text="◀", width=3, command=lambda: self.change_week(-1)
        )
        self.prev_week_button.pack(side="left")
        self.week_label = ttk.Label(navigation, text="", style="Week.TLabel")
        self.week_label.pack(side="left", padx=10)
        self.next_week_button = ttk.Button(
            navigation, text="▶", width=3, command=lambda: self.change_week(1)
        )
        self.next_week_button.pack(side="left")
        ttk.Button(navigation, text="This week", command=self.go_today).pack(
            side="left", padx=(10, 0)
        )
        ttk.Button(
            navigation,
            text="+ Add entry",
            style="Accent.TButton",
            command=lambda: self.add_entry(self.default_entry_date()),
        ).pack(side="right")

        cards = ttk.Frame(parent, style="App.TFrame")
        cards.pack(fill="x", pady=(0, 10))
        self.week_cards: dict[str, StatCard] = {}
        definitions = (
            ("worked", "Worked", False),
            ("lunch", "Lunch", False),
            ("away", "Away", False),
            ("xpause", "X-Pause", True),
            ("days_worked", "Days worked", False),
        )
        for column, (key, label, accent) in enumerate(definitions):
            card = StatCard(cards, label, "0", accent=accent)
            card.grid(
                row=0,
                column=column,
                sticky="ew",
                padx=(0, 8 if column < len(definitions) - 1 else 0),
            )
            cards.columnconfigure(column, weight=1)
            self.week_cards[key] = card

        self.week_scroll = ScrollableFrame(parent, background=COLORS["bg"])
        self.week_scroll.pack(fill="both", expand=True)

    def _bind_shortcuts(self) -> None:
        self.root.bind_all("<Control-b>", lambda _event: self._run_shortcut(self.backup_database))
        self.root.bind_all("<Control-p>", lambda _event: self._run_shortcut(self.open_projects))
        self.root.bind_all(
            "<Control-n>",
            lambda _event: self._run_shortcut(lambda: self.add_entry(self.default_entry_date())),
        )
        self.root.bind_all("<F5>", lambda _event: self._run_shortcut(self.refresh))

    def _run_shortcut(self, callback) -> str:
        if self.root.grab_current() is not None:
            return "break"
        callback()
        return "break"

    def refresh(self) -> None:
        try:
            entries = self.service.list_entries(
                self.current_week_start, self.current_week_start + timedelta(days=6)
            )
        except DatabaseError as exc:
            messagebox.showerror("Cannot load entries", str(exc), parent=self.root)
            return

        entries_by_day: defaultdict[date, list[Entry]] = defaultdict(list)
        for entry in entries:
            entries_by_day[entry.entry_date].append(entry)

        last_day = self.current_week_start + timedelta(days=6)
        self.week_label.configure(
            text=(
                f"Week {week_number(self.current_week_start)} · "
                f"{format_week_range(self.current_week_start, last_day)}"
            )
        )

        totals = self.service.range_totals(self.current_week_start, last_day)
        self.week_cards["worked"].set_value(format_duration(totals.worked_minutes))
        self.week_cards["lunch"].set_value(format_duration(totals.lunch_minutes))
        self.week_cards["away"].set_value(format_duration(totals.away_minutes))
        xpause_color = COLORS["danger"] if totals.xpause_minutes > 60 else COLORS["accent"]
        self.week_cards["xpause"].set_value(
            f"{format_duration(totals.xpause_minutes)} / 1:00", color=xpause_color
        )
        self.week_cards["days_worked"].set_value(str(totals.days_worked))

        self._rebuild_day_cards(entries_by_day)
        if self.notebook.index("current") == 1:
            self.totals_view.refresh()

    def _rebuild_day_cards(self, entries_by_day: dict[date, list[Entry]]) -> None:
        fraction = self.week_scroll.scroll_fraction()
        for child in self.week_scroll.inner.winfo_children():
            child.destroy()
        self._day_cards = []

        for offset in range(7):
            day = self.current_week_start + timedelta(days=offset)
            card = DayCard(
                self.week_scroll.inner,
                self.service,
                day,
                entries_by_day.get(day, []),
                on_add=self.add_entry,
                on_edit=self.edit_entry,
                on_delete=self.delete_entry,
            )
            card.pack(fill="x", padx=2, pady=5)
            self._day_cards.append(card)

        self.week_scroll.inner.update_idletasks()
        self.week_scroll.scroll_to_fraction(fraction)

    def _on_tab_changed(self, _event: tk.Event) -> None:
        if self.notebook.index("current") == 1:
            if self.totals_view.period_kind.get() != "All time":
                self.totals_view.anchor = self.current_week_start
            self.totals_view.refresh()

    def change_week(self, weeks: int) -> None:
        self.current_week_start += timedelta(days=7 * weeks)
        self.refresh()

    def go_today(self) -> None:
        self.current_week_start = week_start(date.today())
        self.refresh()

    def default_entry_date(self) -> date:
        today = date.today()
        if self.current_week_start <= today <= self.current_week_start + timedelta(days=6):
            return today
        return self.current_week_start

    def add_entry(self, day: date) -> None:
        dialog = EntryDialog(self.root, self.service, day)
        saved = dialog.show()
        if saved is not None:
            self.current_week_start = week_start(saved.entry_date)
            self.refresh()

    def edit_entry(self, entry: Entry) -> None:
        dialog = EntryDialog(self.root, self.service, entry.entry_date, entry=entry)
        saved = dialog.show()
        if saved is not None:
            self.current_week_start = week_start(saved.entry_date)
            self.refresh()

    def delete_entry(self, entry: Entry) -> None:
        if entry.id is None:
            return
        detail = entry.project_name or entry.away_reason
        description = ENTRY_KIND_LABELS[entry.kind].lower()
        if detail:
            description += f" · {detail}"
        if not messagebox.askyesno(
            "Delete entry",
            f"Delete this {description} entry?",
            parent=self.root,
        ):
            return
        try:
            self.service.delete_entry(entry.id)
        except (ValidationError, DatabaseError) as exc:
            messagebox.showerror("Cannot delete entry", str(exc), parent=self.root)
            return
        self.refresh()

    def open_projects(self) -> None:
        dialog = ProjectsDialog(self.root, self.service)
        dialog.show()
        self.refresh()

    def backup_database(self) -> None:
        timestamp = datetime.now().strftime("%Y%m%d-%H%M%S")
        path = filedialog.asksaveasfilename(
            parent=self.root,
            title="Backup TimeReport database",
            defaultextension=".db",
            initialfile=f"TimeReport-backup-{timestamp}.db",
            filetypes=(("SQLite database", "*.db"), ("All files", "*.*")),
        )
        if not path:
            return
        try:
            saved = self.service.db.backup_to(Path(path))
        except DatabaseError as exc:
            messagebox.showerror("Backup failed", str(exc), parent=self.root)
            return
        messagebox.showinfo(
            "Backup complete",
            f"Backup saved to:\n{saved}",
            parent=self.root,
        )

    def restore_database(self) -> None:
        path = filedialog.askopenfilename(
            parent=self.root,
            title="Restore TimeReport database",
            filetypes=(
                ("SQLite database", "*.db"),
                ("SQLite database", "*.sqlite"),
                ("SQLite database", "*.sqlite3"),
                ("All files", "*.*"),
            ),
        )
        if not path:
            return
        if not messagebox.askyesno(
            "Restore database",
            "Restoring replaces all current data with the selected backup.\n\n"
            "A backup of the current database will be created automatically first.\n\n"
            "Continue?",
            icon="warning",
            parent=self.root,
        ):
            return
        try:
            auto_backup = self.service.db.restore_from(Path(path))
        except (RestoreError, DatabaseError) as exc:
            messagebox.showerror("Restore failed", str(exc), parent=self.root)
            return
        self.refresh()
        self.totals_view.refresh()
        messagebox.showinfo(
            "Restore complete",
            f"Data restored.\n\nThe previous database was backed up to:\n{auto_backup}",
            parent=self.root,
        )

    def show_about(self) -> None:
        about = tk.Toplevel(self.root)
        about.title("About TimeReport")
        about.transient(self.root)
        about.resizable(False, False)
        about.configure(bg=COLORS["bg"])
        about.protocol("WM_DELETE_WINDOW", about.destroy)
        about.bind("<Escape>", lambda _event: about.destroy())

        container = ttk.Frame(about, padding=18, style="App.TFrame")
        container.pack(fill="both", expand=True)

        ttk.Label(container, text="TimeReport", style="Title.TLabel").pack(anchor="w")
        ttk.Label(
            container,
            text=f"Version {__version__}",
            style="Muted.TLabel",
        ).pack(anchor="w", pady=(0, 12))
        ttk.Label(
            container,
            text="A local-first time report. No account, cloud or telemetry.",
            style="Muted.TLabel",
            wraplength=520,
            justify="left",
        ).pack(anchor="w", pady=(0, 12))

        info = (
            f"Database:\n{self.service.db.path}\n\n"
            f"Data folder:\n{self.data_dir}\n\n"
            f"Schema version: {self.service.db.schema_version}"
        )
        ttk.Label(
            container,
            text=info,
            style="Card.TLabel",
            wraplength=520,
            justify="left",
        ).pack(anchor="w")

        buttons = ttk.Frame(container, style="App.TFrame")
        buttons.pack(fill="x", pady=(16, 0))
        ttk.Button(buttons, text="Open data folder", command=self._open_data_folder).pack(
            side="left"
        )
        ttk.Button(buttons, text="Close", command=about.destroy).pack(side="right")

        about.update_idletasks()
        width = about.winfo_width()
        height = about.winfo_height()
        x = self.root.winfo_rootx() + max(0, (self.root.winfo_width() - width) // 2)
        y = self.root.winfo_rooty() + max(0, (self.root.winfo_height() - height) // 2)
        about.geometry(f"+{x}+{y}")
        about.deiconify()
        about.wait_visibility()
        about.grab_set()
        about.focus_set()

    def _open_data_folder(self) -> None:
        try:
            if sys.platform.startswith("win"):
                os.startfile(self.data_dir)  # type: ignore[attr-defined]
            elif sys.platform == "darwin":
                subprocess.Popen(["open", str(self.data_dir)])
            else:
                subprocess.Popen(["xdg-open", str(self.data_dir)])
        except Exception as exc:
            messagebox.showerror("Cannot open data folder", str(exc), parent=self.root)

    def _on_close(self) -> None:
        try:
            self.service.db.close()
        except Exception:
            logger.exception("Could not close the database cleanly")
        finally:
            self.root.destroy()
