"""One day card in the weekly timesheet."""

from __future__ import annotations

import tkinter as tk
import tkinter.font as tkfont
from collections.abc import Callable
from datetime import date
from tkinter import ttk

from ..models import ENTRY_KIND_LABELS, Entry
from ..service import TimeReportService
from ..timeutil import format_date_long, format_duration, format_time
from .theme import COLORS

EntryCallback = Callable[[Entry], None]
AddCallback = Callable[[date], None]


class DayCard(tk.Frame):
    def __init__(
        self,
        parent: tk.Misc,
        service: TimeReportService,
        day: date,
        entries: list[Entry],
        *,
        on_add: AddCallback,
        on_edit: EntryCallback,
        on_delete: EntryCallback,
    ) -> None:
        super().__init__(
            parent,
            bg=COLORS["card"],
            highlightbackground=COLORS["border"],
            highlightthickness=1,
            bd=0,
        )
        self.service = service
        self.day = day
        self.entries = entries
        self.entries_by_id = {entry.id: entry for entry in entries if entry.id is not None}
        self.on_add = on_add
        self.on_edit = on_edit
        self.on_delete = on_delete

        self._build()
        self._populate()

    def _build(self) -> None:
        family = tkfont.nametofont("TkDefaultFont").actual("family")
        totals = self.service.daily_totals(self.day, self.entries)

        header = tk.Frame(self, bg=COLORS["card"])
        header.pack(fill="x", padx=12, pady=(10, 4))

        left = tk.Frame(header, bg=COLORS["card"])
        left.pack(side="left", fill="x", expand=True)

        title = format_date_long(self.day)
        if self.day == date.today():
            title += " · Today"
        tk.Label(
            left,
            text=title,
            bg=COLORS["card"],
            fg=COLORS["text"],
            font=(family, 11, "bold"),
            anchor="w",
        ).pack(anchor="w")

        summary = tk.Frame(left, bg=COLORS["card"])
        summary.pack(anchor="w", pady=(2, 0))
        for label, value, color in (
            ("Worked", format_duration(totals.worked_minutes), COLORS["text"]),
            ("Lunch", format_duration(totals.lunch_minutes), COLORS["muted"]),
            ("Away", format_duration(totals.away_minutes), COLORS["warning"]),
            ("X-Pause", format_duration(totals.xpause_minutes), COLORS["accent"]),
        ):
            tk.Label(
                summary,
                text=f"{label} {value}",
                bg=COLORS["card"],
                fg=color,
                font=(family, 9),
                anchor="w",
            ).pack(side="left", padx=(0, 14))

        buttons = tk.Frame(header, bg=COLORS["card"])
        buttons.pack(side="right", anchor="n")
        ttk.Button(
            buttons,
            text="+ Add",
            style="Accent.TButton",
            command=lambda: self.on_add(self.day),
        ).pack(side="left")
        self.edit_button = ttk.Button(
            buttons,
            text="Edit",
            style="Small.TButton",
            command=self._edit_selected,
            state="disabled",
        )
        self.edit_button.pack(side="left", padx=(6, 0))
        self.delete_button = ttk.Button(
            buttons,
            text="Delete",
            style="Danger.TButton",
            command=self._delete_selected,
            state="disabled",
        )
        self.delete_button.pack(side="left", padx=(6, 0))

        self.tree = ttk.Treeview(
            self,
            columns=("kind", "detail", "start", "end", "duration"),
            show="headings",
            selectmode="browse",
            height=max(1, len(self.entries)),
        )
        headings = {
            "kind": "Type",
            "detail": "Project / reason",
            "start": "Start",
            "end": "End",
            "duration": "Duration",
        }
        widths = {"kind": 90, "detail": 360, "start": 70, "end": 70, "duration": 80}
        for column, heading in headings.items():
            self.tree.heading(column, text=heading)
            self.tree.column(
                column,
                width=widths[column],
                anchor="w" if column in {"kind", "detail"} else "center",
                stretch=column == "detail",
            )
        self.tree.tag_configure("work", foreground=COLORS["text"])
        self.tree.tag_configure("lunch", foreground=COLORS["muted"])
        self.tree.tag_configure("away", foreground=COLORS["warning"])
        self.tree.tag_configure("xpause", foreground=COLORS["accent"])
        self.tree.tag_configure("empty", foreground=COLORS["muted"])
        self.tree.bind("<<TreeviewSelect>>", lambda _event: self._update_buttons())
        self.tree.bind("<Double-1>", lambda _event: self._edit_selected())
        self.tree.bind("<Delete>", lambda _event: self._delete_selected())
        self.tree.pack(fill="x", padx=12, pady=(0, 10))

    def _populate(self) -> None:
        if not self.entries:
            self.tree.insert(
                "",
                "end",
                iid="empty",
                values=("", "No entries yet", "", "", ""),
                tags=("empty",),
            )
            return

        for entry in self.entries:
            detail = entry.project_name or entry.away_reason or "—"
            values = (
                ENTRY_KIND_LABELS[entry.kind],
                detail,
                format_time(entry.start_minute),
                format_time(entry.end_minute),
                format_duration(entry.duration_minutes),
            )
            if entry.id is None:
                self.tree.insert("", "end", values=values, tags=(entry.kind.value,))
            else:
                self.tree.insert(
                    "",
                    "end",
                    iid=str(entry.id),
                    values=values,
                    tags=(entry.kind.value,),
                )

    def _selected_entry(self) -> Entry | None:
        selection = self.tree.selection()
        if not selection or selection[0] == "empty":
            return None
        try:
            return self.entries_by_id.get(int(selection[0]))
        except ValueError:
            return None

    def _update_buttons(self) -> None:
        state = "normal" if self._selected_entry() is not None else "disabled"
        self.edit_button.configure(state=state)
        self.delete_button.configure(state=state)

    def _edit_selected(self) -> None:
        entry = self._selected_entry()
        if entry is not None:
            self.on_edit(entry)

    def _delete_selected(self) -> None:
        entry = self._selected_entry()
        if entry is not None:
            self.on_delete(entry)
