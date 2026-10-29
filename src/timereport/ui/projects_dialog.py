"""Project management dialog."""

from __future__ import annotations

import tkinter as tk
from tkinter import messagebox, simpledialog, ttk

from ..models import Project
from ..service import TimeReportService, ValidationError
from .theme import COLORS


class ProjectsDialog(tk.Toplevel):
    def __init__(self, parent: tk.Misc, service: TimeReportService) -> None:
        super().__init__(parent)
        self.service = service
        self._projects_by_id: dict[int, Project] = {}

        self.title("Projects")
        self.transient(parent)
        self.resizable(True, True)
        self.minsize(520, 380)
        self.configure(bg=COLORS["bg"])
        self.protocol("WM_DELETE_WINDOW", self._close)
        self.bind("<Escape>", lambda _event: self._close())

        container = ttk.Frame(self, padding=16, style="App.TFrame")
        container.pack(fill="both", expand=True)

        heading = ttk.Label(container, text="Projects", style="Week.TLabel")
        heading.pack(anchor="w", pady=(0, 8))

        tree_frame = ttk.Frame(container, style="App.TFrame")
        tree_frame.pack(fill="both", expand=True)

        self.tree = ttk.Treeview(
            tree_frame,
            columns=("name", "status", "entries"),
            show="headings",
            selectmode="browse",
            height=12,
        )
        self.tree.heading("name", text="Project")
        self.tree.heading("status", text="Status")
        self.tree.heading("entries", text="Entries")
        self.tree.column("name", width=280, anchor="w", stretch=True)
        self.tree.column("status", width=90, anchor="center", stretch=False)
        self.tree.column("entries", width=80, anchor="center", stretch=False)
        scrollbar = ttk.Scrollbar(tree_frame, orient="vertical", command=self.tree.yview)
        self.tree.configure(yscrollcommand=scrollbar.set)
        self.tree.pack(side="left", fill="both", expand=True)
        scrollbar.pack(side="right", fill="y")
        self.tree.bind("<<TreeviewSelect>>", lambda _event: self._update_buttons())
        self.tree.bind("<Double-1>", lambda _event: self._rename())

        buttons = ttk.Frame(container, style="App.TFrame")
        buttons.pack(fill="x", pady=(12, 0))
        ttk.Button(buttons, text="Add", style="Accent.TButton", command=self._add).pack(side="left")
        ttk.Button(buttons, text="Rename", command=self._rename).pack(side="left", padx=(6, 0))
        self.archive_button = ttk.Button(buttons, text="Archive", command=self._toggle_archive)
        self.archive_button.pack(side="left", padx=(6, 0))
        ttk.Button(buttons, text="Delete", style="Danger.TButton", command=self._delete).pack(
            side="left", padx=(6, 0)
        )
        ttk.Button(buttons, text="Close", command=self._close).pack(side="right")

        self._refresh()
        self.update_idletasks()
        self._center(parent)
        self.deiconify()
        self.wait_visibility()
        self.grab_set()
        self.focus_set()

    def _refresh(self) -> None:
        selected_id = self._selected_id()
        for item in self.tree.get_children():
            self.tree.delete(item)

        projects = self.service.list_projects(include_archived=True)
        self._projects_by_id = {project.id: project for project in projects}
        for project in projects:
            self.tree.insert(
                "",
                "end",
                iid=str(project.id),
                values=(
                    project.name,
                    "Archived" if project.archived else "Active",
                    self.service.project_entry_count(project.id),
                ),
            )
        if selected_id is not None and str(selected_id) in self.tree.get_children():
            self.tree.selection_set(str(selected_id))
        elif self.tree.get_children():
            first = self.tree.get_children()[0]
            self.tree.selection_set(first)
            self.tree.focus(first)
        self._update_buttons()

    def _selected_id(self) -> int | None:
        selection = self.tree.selection()
        if not selection:
            return None
        try:
            return int(selection[0])
        except ValueError:
            return None

    def _selected_project(self) -> Project | None:
        selected_id = self._selected_id()
        if selected_id is None:
            return None
        return self._projects_by_id.get(selected_id)

    def _update_buttons(self) -> None:
        project = self._selected_project()
        if project is None:
            self.archive_button.configure(text="Archive")
        else:
            self.archive_button.configure(text="Unarchive" if project.archived else "Archive")

    def _add(self) -> None:
        name = simpledialog.askstring("New project", "Project name:", parent=self)
        if name is None:
            return
        try:
            project = self.service.create_project(name)
        except ValidationError as exc:
            messagebox.showerror("Cannot create project", str(exc), parent=self)
            return
        self._refresh()
        if str(project.id) in self.tree.get_children():
            self.tree.selection_set(str(project.id))
            self.tree.focus(str(project.id))

    def _rename(self) -> None:
        project = self._selected_project()
        if project is None:
            return
        name = simpledialog.askstring(
            "Rename project", "Project name:", initialvalue=project.name, parent=self
        )
        if name is None:
            return
        try:
            self.service.rename_project(project.id, name)
        except ValidationError as exc:
            messagebox.showerror("Cannot rename project", str(exc), parent=self)
            return
        self._refresh()

    def _toggle_archive(self) -> None:
        project = self._selected_project()
        if project is None:
            return
        try:
            self.service.set_project_archived(project.id, not project.archived)
        except ValidationError as exc:
            messagebox.showerror("Cannot change project", str(exc), parent=self)
            return
        self._refresh()

    def _delete(self) -> None:
        project = self._selected_project()
        if project is None:
            return
        if not messagebox.askyesno(
            "Delete project",
            f'Delete "{project.name}"?\n\nProjects with time entries can only be archived.',
            parent=self,
        ):
            return
        try:
            self.service.delete_project(project.id)
        except ValidationError as exc:
            messagebox.showerror("Cannot delete project", str(exc), parent=self)
            return
        self._refresh()

    def _center(self, parent: tk.Misc) -> None:
        width = self.winfo_width()
        height = self.winfo_height()
        x = parent.winfo_rootx() + max(0, (parent.winfo_width() - width) // 2)
        y = parent.winfo_rooty() + max(0, (parent.winfo_height() - height) // 2)
        self.geometry(f"+{x}+{y}")

    def _close(self) -> None:
        self.destroy()

    def show(self) -> None:
        self.wait_window()
