"""Dialog for adding and editing time entries."""

from __future__ import annotations

import tkinter as tk
import tkinter.font as tkfont
from datetime import date
from tkinter import messagebox, simpledialog, ttk

from ..db import DatabaseError
from ..models import ENTRY_KIND_LABELS, LABEL_TO_ENTRY_KIND, Entry, EntryKind, Project
from ..service import TimeReportService, ValidationError
from ..timeutil import (
    XPAUSE_WEEKLY_LIMIT,
    format_duration,
    format_time,
    parse_iso_date,
    parse_time,
)
from .theme import COLORS


class EntryDialog(tk.Toplevel):
    def __init__(
        self,
        parent: tk.Misc,
        service: TimeReportService,
        default_date: date,
        entry: Entry | None = None,
    ) -> None:
        super().__init__(parent)
        self.service = service
        self.entry = entry
        self.result: Entry | None = None
        self._projects: list[Project] = []
        self._project_by_name: dict[str, Project] = {}

        self.title("Edit entry" if entry is not None else "Add entry")
        self.transient(parent)
        self.resizable(False, False)
        self.configure(bg=COLORS["bg"])
        self.protocol("WM_DELETE_WINDOW", self._cancel)

        self.date_var = tk.StringVar(value=default_date.isoformat())
        self.kind_var = tk.StringVar(value=ENTRY_KIND_LABELS[EntryKind.WORK])
        self.start_var = tk.StringVar(value="08:00")
        self.end_var = tk.StringVar(value="16:00")
        self.project_var = tk.StringVar()
        self.away_var = tk.StringVar()
        self.info_var = tk.StringVar()
        self.xpause_hint_var = tk.StringVar()

        self._build()
        self._load_projects()
        self._populate()
        self._on_kind_change()

        self.date_var.trace_add("write", lambda *_args: self._refresh_xpause_hint())
        self.kind_var.trace_add("write", lambda *_args: self._on_kind_change())

        self.bind("<Escape>", lambda _event: self._cancel())
        self.bind("<Control-Return>", lambda _event: self._save())
        self.update_idletasks()
        self._center(parent)
        self.deiconify()
        self.wait_visibility()
        self.grab_set()
        self.focus_set()

    def _build(self) -> None:
        container = ttk.Frame(self, padding=18, style="App.TFrame")
        container.pack(fill="both", expand=True)
        container.columnconfigure(1, weight=1)

        row = 0
        ttk.Label(container, text="Date").grid(row=row, column=0, sticky="w", padx=(0, 14), pady=4)
        ttk.Entry(container, textvariable=self.date_var, width=16).grid(
            row=row, column=1, sticky="w", pady=4
        )

        row += 1
        ttk.Label(container, text="Type").grid(row=row, column=0, sticky="w", padx=(0, 14), pady=4)
        self.kind_combo = ttk.Combobox(
            container,
            textvariable=self.kind_var,
            state="readonly",
            values=[ENTRY_KIND_LABELS[kind] for kind in EntryKind],
            width=18,
        )
        self.kind_combo.grid(row=row, column=1, sticky="w", pady=4)

        row += 1
        self.project_label = ttk.Label(container, text="Project")
        self.project_label.grid(row=row, column=0, sticky="w", padx=(0, 14), pady=4)
        self.project_frame = ttk.Frame(container, style="App.TFrame")
        self.project_frame.grid(row=row, column=1, sticky="ew", pady=4)
        self.project_frame.columnconfigure(0, weight=1)
        self.project_combo = ttk.Combobox(
            self.project_frame,
            textvariable=self.project_var,
            state="readonly",
            width=30,
        )
        self.project_combo.grid(row=0, column=0, sticky="ew")
        self.new_project_button = ttk.Button(
            self.project_frame,
            text="New…",
            style="Small.TButton",
            command=self._new_project,
        )
        self.new_project_button.grid(row=0, column=1, padx=(8, 0))

        row += 1
        self.away_label = ttk.Label(container, text="Away reason")
        self.away_label.grid(row=row, column=0, sticky="w", padx=(0, 14), pady=4)
        self.away_frame = ttk.Frame(container, style="App.TFrame")
        self.away_frame.grid(row=row, column=1, sticky="ew", pady=4)
        self.away_frame.columnconfigure(0, weight=1)
        self.away_combo = ttk.Combobox(
            self.away_frame,
            textvariable=self.away_var,
            width=30,
            values=("Sick", "Vacation", "VAB", "Parental leave", "Training"),
        )
        self.away_combo.grid(row=0, column=0, sticky="ew")

        row += 1
        ttk.Label(container, text="Time").grid(row=row, column=0, sticky="nw", padx=(0, 14), pady=4)
        time_frame = ttk.Frame(container, style="App.TFrame")
        time_frame.grid(row=row, column=1, sticky="w", pady=4)
        ttk.Label(time_frame, text="Start").pack(side="left")
        ttk.Entry(time_frame, textvariable=self.start_var, width=8).pack(side="left", padx=(6, 14))
        ttk.Label(time_frame, text="End").pack(side="left")
        ttk.Entry(time_frame, textvariable=self.end_var, width=8).pack(side="left", padx=(6, 12))
        ttk.Label(time_frame, text="24-hour clock", style="Muted.TLabel").pack(side="left")

        row += 1
        self.info_label = ttk.Label(
            container,
            textvariable=self.info_var,
            style="Muted.TLabel",
            wraplength=500,
            justify="left",
        )
        self.info_label.grid(row=row, column=0, columnspan=2, sticky="w", pady=(10, 2))

        row += 1
        self.xpause_hint = tk.Label(
            container,
            textvariable=self.xpause_hint_var,
            bg=COLORS["bg"],
            fg=COLORS["accent"],
            anchor="w",
            justify="left",
            wraplength=500,
            font=(tkfont.nametofont("TkDefaultFont").actual("family"), 9),
        )
        self.xpause_hint.grid(row=row, column=0, columnspan=2, sticky="w", pady=(0, 4))

        row += 1
        buttons = ttk.Frame(container, style="App.TFrame")
        buttons.grid(row=row, column=0, columnspan=2, sticky="e", pady=(14, 0))
        ttk.Button(buttons, text="Cancel", command=self._cancel).pack(side="right", padx=(8, 0))
        self.save_button = ttk.Button(
            buttons, text="Save", style="Accent.TButton", command=self._save
        )
        self.save_button.pack(side="right")

    def _load_projects(self) -> None:
        include_archived = self.entry is not None
        self._projects = self.service.list_projects(include_archived=include_archived)
        self._project_by_name = {project.name: project for project in self._projects}
        names = [project.name for project in self._projects]
        self.project_combo.configure(values=names)
        if names and not self.project_var.get():
            self.project_var.set(names[0])

    def _populate(self) -> None:
        if self.entry is None:
            return
        self.date_var.set(self.entry.entry_date.isoformat())
        self.kind_var.set(ENTRY_KIND_LABELS[self.entry.kind])
        self.start_var.set(format_time(self.entry.start_minute))
        self.end_var.set(format_time(self.entry.end_minute))
        if self.entry.kind is EntryKind.WORK:
            project = self.entry.project_name
            if project is None and self.entry.project_id is not None:
                found = self.service.get_project(self.entry.project_id)
                project = found.name if found else None
            if project:
                self.project_var.set(project)
        if self.entry.away_reason:
            self.away_var.set(self.entry.away_reason)

    def _on_kind_change(self) -> None:
        kind = LABEL_TO_ENTRY_KIND.get(self.kind_var.get(), EntryKind.WORK)
        if kind is EntryKind.WORK:
            self.project_label.grid()
            self.project_frame.grid()
        else:
            self.project_label.grid_remove()
            self.project_frame.grid_remove()

        if kind is EntryKind.AWAY:
            self.away_label.grid()
            self.away_frame.grid()
        else:
            self.away_label.grid_remove()
            self.away_frame.grid_remove()

        info = {
            EntryKind.WORK: (
                "Work time is counted. Lunch that overlaps work is deducted; "
                "X-Pause is tracked separately."
            ),
            EntryKind.LUNCH: "Lunch is deducted from work time when it overlaps a work entry.",
            EntryKind.AWAY: "Away time is tracked separately and is not counted as work.",
            EntryKind.XPAUSE: (
                "X-Pause is a separate weekly benefit. It must be inside work hours, "
                "in 15-minute steps, max 1:00 per week, and is not deducted from worked time."
            ),
        }
        self.info_var.set(info[kind])
        self._refresh_xpause_hint()

    def _refresh_xpause_hint(self) -> None:
        kind = LABEL_TO_ENTRY_KIND.get(self.kind_var.get(), EntryKind.WORK)
        if kind is not EntryKind.XPAUSE:
            self.xpause_hint_var.set("")
            return

        try:
            day = parse_iso_date(self.date_var.get())
        except ValueError:
            self.xpause_hint_var.set("Enter a valid date to see the weekly X-Pause balance.")
            return

        remaining = self.service.xpause_remaining(
            day, exclude_entry_id=self.entry.id if self.entry is not None else None
        )
        used = XPAUSE_WEEKLY_LIMIT - remaining
        weekday_note = "" if day.weekday() < 5 else " X-Pause is only available Monday–Friday."
        self.xpause_hint_var.set(
            f"X-Pause this week: {format_duration(used)} used of 1:00, "
            f"{format_duration(remaining)} remaining.{weekday_note}"
        )

    def _new_project(self) -> None:
        name = simpledialog.askstring("New project", "Project name:", parent=self)
        if name is None:
            return
        try:
            project = self.service.create_project(name)
        except ValidationError as exc:
            messagebox.showerror("Cannot create project", str(exc), parent=self)
            return
        self._load_projects()
        self.project_var.set(project.name)

    def _save(self) -> None:
        try:
            day = parse_iso_date(self.date_var.get())
            start_minute = parse_time(self.start_var.get())
            end_minute = parse_time(self.end_var.get(), allow_end_of_day=True)
        except ValueError as exc:
            messagebox.showerror("Cannot save entry", str(exc), parent=self)
            return

        kind = LABEL_TO_ENTRY_KIND.get(self.kind_var.get(), EntryKind.WORK)
        project_id: int | None = None
        away_reason: str | None = None

        if kind is EntryKind.WORK:
            project_name = self.project_var.get().strip()
            project = self._project_by_name.get(project_name)
            if project is None:
                messagebox.showerror(
                    "Cannot save entry",
                    "Choose a project. Use “New…” to create one if needed.",
                    parent=self,
                )
                return
            project_id = project.id
        elif kind is EntryKind.AWAY:
            away_reason = self.away_var.get().strip()

        candidate = Entry(
            id=self.entry.id if self.entry is not None else None,
            entry_date=day,
            kind=kind,
            start_minute=start_minute,
            end_minute=end_minute,
            project_id=project_id,
            away_reason=away_reason,
        )

        try:
            saved = self.service.save_entry(candidate)
        except (ValidationError, DatabaseError) as exc:
            messagebox.showerror("Cannot save entry", str(exc), parent=self)
            return

        self.result = saved
        self.destroy()

    def _cancel(self) -> None:
        self.result = None
        self.destroy()

    def _center(self, parent: tk.Misc) -> None:
        width = self.winfo_width()
        height = self.winfo_height()
        x = parent.winfo_rootx() + max(0, (parent.winfo_width() - width) // 2)
        y = parent.winfo_rooty() + max(0, (parent.winfo_height() - height) // 2)
        self.geometry(f"+{x}+{y}")

    def show(self) -> Entry | None:
        self.wait_window()
        return self.result
