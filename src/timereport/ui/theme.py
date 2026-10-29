"""Shared visual theme for the Tkinter UI."""

from __future__ import annotations

import tkinter as tk
import tkinter.font as tkfont
from tkinter import ttk

COLORS = {
    "bg": "#f3f5f9",
    "card": "#ffffff",
    "text": "#1f2937",
    "muted": "#667085",
    "border": "#d9dee8",
    "accent": "#2563eb",
    "accent_dark": "#1d4ed8",
    "accent_soft": "#eff6ff",
    "success": "#047857",
    "warning": "#b45309",
    "danger": "#b91c1c",
    "selection": "#2563eb",
    "row_alt": "#f8fafc",
}


def configure_theme(root: tk.Tk) -> None:
    style = ttk.Style(root)
    try:
        style.theme_use("clam")
    except tk.TclError:
        pass

    root.configure(bg=COLORS["bg"])
    family = tkfont.nametofont("TkDefaultFont").actual("family")
    for font_name in ("TkDefaultFont", "TkTextFont", "TkMenuFont", "TkHeadingFont"):
        try:
            tkfont.nametofont(font_name).configure(family=family, size=10)
        except tk.TclError:
            pass

    style.configure("App.TFrame", background=COLORS["bg"])
    style.configure("Card.TFrame", background=COLORS["card"])
    style.configure("TFrame", background=COLORS["bg"])
    style.configure("TLabel", background=COLORS["bg"], foreground=COLORS["text"])
    style.configure(
        "Title.TLabel",
        background=COLORS["bg"],
        foreground=COLORS["text"],
        font=(family, 20, "bold"),
    )
    style.configure(
        "Week.TLabel",
        background=COLORS["bg"],
        foreground=COLORS["text"],
        font=(family, 14, "bold"),
    )
    style.configure("Muted.TLabel", background=COLORS["bg"], foreground=COLORS["muted"])
    style.configure("Card.TLabel", background=COLORS["card"], foreground=COLORS["text"])
    style.configure("CardMuted.TLabel", background=COLORS["card"], foreground=COLORS["muted"])
    style.configure(
        "CardValue.TLabel",
        background=COLORS["card"],
        foreground=COLORS["text"],
        font=(family, 16, "bold"),
    )
    style.configure(
        "CardValueAccent.TLabel",
        background=COLORS["card"],
        foreground=COLORS["accent"],
        font=(family, 16, "bold"),
    )
    style.configure("TButton", padding=(10, 5))
    style.configure(
        "Accent.TButton",
        background=COLORS["accent"],
        foreground="white",
        padding=(12, 5),
    )
    style.map(
        "Accent.TButton",
        background=[("active", COLORS["accent_dark"]), ("pressed", COLORS["accent_dark"])],
        foreground=[("active", "white"), ("pressed", "white")],
    )
    style.configure("Small.TButton", padding=(7, 3))
    style.configure("Danger.TButton", foreground=COLORS["danger"], padding=(7, 3))
    style.configure("TNotebook", background=COLORS["bg"], borderwidth=0)
    style.configure(
        "TNotebook.Tab",
        padding=(18, 8),
        font=(family, 10, "bold"),
    )
    style.map(
        "TNotebook.Tab",
        background=[("selected", COLORS["card"]), ("active", COLORS["accent_soft"])],
        foreground=[("selected", COLORS["accent"]), ("active", COLORS["text"])],
    )
    style.configure("Treeview", rowheight=26, font=(family, 10))
    style.configure(
        "Treeview.Heading",
        font=(family, 9, "bold"),
        padding=(4, 4),
    )
    style.configure("TEntry", padding=4)
    style.configure("TCombobox", padding=4)
    style.configure("TSeparator", background=COLORS["border"])
    style.configure("Status.TLabel", background=COLORS["bg"], foreground=COLORS["muted"])
