"""Week, month, year and all-time totals view."""

from __future__ import annotations

import tkinter as tk
from datetime import date, timedelta
from tkinter import ttk

from ..models import Totals
from ..service import TimeReportService
from ..timeutil import (
    add_months,
    format_day_short,
    format_duration,
    format_month_name,
    format_week_range,
    month_bounds,
    week_number,
    week_start,
)
from .theme import COLORS
from .widgets import StatCard


class TotalsView(ttk.Frame):
    def __init__(self, parent: tk.Misc, service: TimeReportService) -> None:
        super().__init__(parent, style="App.TFrame")
        self.service = service
        self.anchor = date.today()
        self.period_kind = tk.StringVar(value="Week")

        self._build()
        self.refresh()

    def _build(self) -> None:
        controls = ttk.Frame(self, style="App.TFrame")
        controls.pack(fill="x", pady=(0, 10))

        ttk.Label(controls, text="Period").pack(side="left", padx=(0, 8))
        self.period_combo = ttk.Combobox(
            controls,
            textvariable=self.period_kind,
            values=("Week", "Month", "Year", "All time"),
            state="readonly",
            width=10,
        )
        self.period_combo.pack(side="left")
        self.period_combo.bind("<<ComboboxSelected>>", lambda _event: self.refresh())

        self.prev_button = ttk.Button(controls, text="◀", width=3, command=lambda: self._move(-1))
        self.prev_button.pack(side="left", padx=(14, 4))
        self.period_label = ttk.Label(controls, text="", style="Week.TLabel")
        self.period_label.pack(side="left", padx=6)
        self.next_button = ttk.Button(controls, text="▶", width=3, command=lambda: self._move(1))
        self.next_button.pack(side="left", padx=(4, 10))
        self.current_button = ttk.Button(controls, text="Current", command=self._current)
        self.current_button.pack(side="left")

        cards = ttk.Frame(self, style="App.TFrame")
        cards.pack(fill="x", pady=(0, 12))
        self.cards: dict[str, StatCard] = {}
        definitions = (
            ("worked", "Worked", False),
            ("lunch", "Lunch", False),
            ("away", "Away", False),
            ("xpause", "X-Pause", True),
            ("days_worked", "Days worked", False),
            ("days_away", "Days away", False),
        )
        for column, (key, label, accent) in enumerate(definitions):
            card = StatCard(
                cards,
                label,
                "0:00" if key not in {"days_worked", "days_away"} else "0",
                accent=accent,
            )
            card.grid(
                row=0,
                column=column,
                sticky="ew",
                padx=(0, 8 if column < len(definitions) - 1 else 0),
            )
            cards.columnconfigure(column, weight=1)
            self.cards[key] = card

        table_frame = ttk.Frame(self, style="App.TFrame")
        table_frame.pack(fill="both", expand=True)
        columns = (
            "period",
            "worked",
            "lunch",
            "away",
            "xpause",
            "days_worked",
            "days_away",
        )
        self.tree = ttk.Treeview(
            table_frame,
            columns=columns,
            show="headings",
            selectmode="browse",
            height=14,
        )
        headings = {
            "period": "Period",
            "worked": "Worked",
            "lunch": "Lunch",
            "away": "Away",
            "xpause": "X-Pause",
            "days_worked": "Days worked",
            "days_away": "Days away",
        }
        widths = {
            "period": 240,
            "worked": 90,
            "lunch": 80,
            "away": 80,
            "xpause": 90,
            "days_worked": 100,
            "days_away": 90,
        }
        for column in columns:
            self.tree.heading(column, text=headings[column])
            self.tree.column(
                column,
                width=widths[column],
                anchor="w" if column == "period" else "center",
                stretch=column == "period",
            )
        self.tree.tag_configure("zero", foreground=COLORS["muted"])
        self.tree.tag_configure("today", background=COLORS["accent_soft"])
        scrollbar = ttk.Scrollbar(table_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")

    def refresh(self) -> None:
        kind = self.period_kind.get()
        all_time = kind == "All time"
        state = "disabled" if all_time else "normal"
        self.prev_button.configure(state=state)
        self.next_button.configure(state=state)
        self.current_button.configure(state=state)

        today = date.today()
        rows: list[tuple[str, Totals, bool]] = []
        totals = Totals()

        if kind == "Week":
            start = week_start(self.anchor)
            end = start + timedelta(days=6)
            self.period_label.configure(
                text=f"Week {week_number(start)} · {format_week_range(start, end)}"
            )
            for day, day_totals in self.service.totals_by_day(start, end, include_empty=True):
                label = format_day_short(day)
                if day == today:
                    label += " · Today"
                rows.append((label, day_totals, day == today))
            totals = self.service.range_totals(start, end)
        elif kind == "Month":
            start = date(self.anchor.year, self.anchor.month, 1)
            end = month_bounds(start)[1]
            self.period_label.configure(text=f"{format_month_name(start)} {start.year}")
            for day, day_totals in self.service.totals_by_day(start, end, include_empty=True):
                label = format_day_short(day)
                if day == today:
                    label += " · Today"
                rows.append((label, day_totals, day == today))
            totals = self.service.range_totals(start, end)
        elif kind == "Year":
            start = date(self.anchor.year, 1, 1)
            end = date(self.anchor.year, 12, 31)
            self.period_label.configure(text=str(start.year))
            for month_start, month_totals in self.service.totals_by_month(start.year):
                rows.append((format_month_name(month_start), month_totals, False))
            totals = self.service.range_totals(start, end)
        else:
            bounds = self.service.all_time_bounds()
            if bounds is None:
                self.period_label.configure(text="All time")
                totals = Totals()
            else:
                start, end = bounds
                if start.year == end.year:
                    self.period_label.configure(text=f"All time · {start.year}")
                else:
                    self.period_label.configure(text=f"All time · {start.year}–{end.year}")
                for year_start, year_totals in self.service.totals_by_year(start.year, end.year):
                    rows.append((str(year_start.year), year_totals, False))
                totals = self.service.range_totals(start, end)

        self._update_cards(totals)
        self._populate(rows)

    def _update_cards(self, totals: Totals) -> None:
        self.cards["worked"].set_value(format_duration(totals.worked_minutes))
        self.cards["lunch"].set_value(format_duration(totals.lunch_minutes))
        self.cards["away"].set_value(format_duration(totals.away_minutes))
        self.cards["xpause"].set_value(
            format_duration(totals.xpause_minutes), color=COLORS["accent"]
        )
        self.cards["days_worked"].set_value(str(totals.days_worked))
        self.cards["days_away"].set_value(str(totals.days_away))

    def _populate(self, rows: list[tuple[str, Totals, bool]]) -> None:
        for item in self.tree.get_children():
            self.tree.delete(item)

        if not rows:
            self.tree.insert(
                "",
                "end",
                values=("No entries yet", "", "", "", "", "", ""),
                tags=("zero",),
            )
            return

        for label, totals, is_today in rows:
            tags: tuple[str, ...] = ()
            if is_today:
                tags = ("today",)
            elif (
                totals.worked_minutes == 0
                and totals.lunch_minutes == 0
                and totals.away_minutes == 0
                and totals.xpause_minutes == 0
            ):
                tags = ("zero",)
            self.tree.insert(
                "",
                "end",
                values=(
                    label,
                    format_duration(totals.worked_minutes),
                    format_duration(totals.lunch_minutes),
                    format_duration(totals.away_minutes),
                    format_duration(totals.xpause_minutes),
                    totals.days_worked,
                    totals.days_away,
                ),
                tags=tags,
            )

    def _move(self, direction: int) -> None:
        kind = self.period_kind.get()
        if kind == "Week":
            self.anchor += timedelta(days=7 * direction)
        elif kind == "Month":
            moved = add_months(self.anchor, direction)
            self.anchor = date(moved.year, moved.month, 1)
        elif kind == "Year":
            self.anchor = date(self.anchor.year + direction, 1, 1)
        else:
            return
        self.refresh()

    def _current(self) -> None:
        self.anchor = date.today()
        self.refresh()
