"""Reusable UI widgets."""

from __future__ import annotations

import tkinter as tk
import tkinter.font as tkfont
from tkinter import ttk

from .theme import COLORS


class StatCard(tk.Frame):
    """A compact summary card used for daily, weekly and period totals."""

    def __init__(
        self,
        parent: tk.Misc,
        label: str,
        value: str = "0:00",
        *,
        accent: bool = False,
    ) -> None:
        super().__init__(
            parent,
            bg=COLORS["card"],
            highlightbackground=COLORS["border"],
            highlightthickness=1,
            bd=0,
        )
        family = tkfont.nametofont("TkDefaultFont").actual("family")
        self._label = tk.Label(
            self,
            text=label,
            bg=COLORS["card"],
            fg=COLORS["muted"],
            font=(family, 9),
            anchor="w",
        )
        self._value = tk.Label(
            self,
            text=value,
            bg=COLORS["card"],
            fg=COLORS["accent"] if accent else COLORS["text"],
            font=(family, 16, "bold"),
            anchor="w",
        )
        self._label.pack(fill="x", padx=12, pady=(8, 0))
        self._value.pack(fill="x", padx=12, pady=(0, 8))

    def set_value(self, value: str, color: str | None = None) -> None:
        self._value.configure(text=value, fg=color or COLORS["text"])


class ScrollableFrame(ttk.Frame):
    """A vertically scrollable frame that works on Linux and Windows."""

    def __init__(self, parent: tk.Misc, *, background: str = COLORS["bg"]) -> None:
        super().__init__(parent, style="App.TFrame")
        self.canvas = tk.Canvas(
            self,
            bg=background,
            highlightthickness=0,
            bd=0,
            takefocus=False,
        )
        self.scrollbar = ttk.Scrollbar(self, orient="vertical", command=self.canvas.yview)
        self.inner = tk.Frame(self.canvas, bg=background)
        self._window_id = self.canvas.create_window((0, 0), window=self.inner, anchor="nw")

        self.canvas.configure(yscrollcommand=self.scrollbar.set)
        self.canvas.pack(side="left", fill="both", expand=True)
        self.scrollbar.pack(side="right", fill="y")

        self.inner.bind("<Configure>", self._on_inner_configure)
        self.canvas.bind("<Configure>", self._on_canvas_configure)

        self.canvas.bind_all("<MouseWheel>", self._on_mousewheel)
        self.canvas.bind_all("<Button-4>", self._on_mousewheel)
        self.canvas.bind_all("<Button-5>", self._on_mousewheel)

    def _on_inner_configure(self, _event: tk.Event) -> None:
        self.canvas.configure(scrollregion=self.canvas.bbox("all"))

    def _on_canvas_configure(self, event: tk.Event) -> None:
        self.canvas.itemconfigure(self._window_id, width=event.width)

    def _pointer_inside(self, event: tk.Event) -> bool:
        widget = self.winfo_containing(event.x_root, event.y_root)
        while widget is not None:
            if widget is self or widget is self.canvas or widget is self.inner:
                return True
            widget = widget.master
        return False

    def _on_mousewheel(self, event: tk.Event) -> str | None:
        if not self._pointer_inside(event):
            return None

        if getattr(event, "num", None) == 4:
            units = -1
        elif getattr(event, "num", None) == 5:
            units = 1
        else:
            delta = getattr(event, "delta", 0)
            if delta == 0:
                return None
            units = -1 if delta > 0 else 1
        self.canvas.yview_scroll(units, "units")
        return "break"

    def scroll_fraction(self) -> float:
        return float(self.canvas.yview()[0])

    def scroll_to_fraction(self, fraction: float) -> None:
        self.canvas.yview_moveto(max(0.0, min(1.0, fraction)))

    def scroll_to_top(self) -> None:
        self.canvas.yview_moveto(0.0)
