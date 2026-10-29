"""Application rules, persistence operations and totals."""

from __future__ import annotations

import sqlite3
from collections import defaultdict
from dataclasses import replace
from datetime import UTC, date, datetime, timedelta
from pathlib import Path

from .db import Database, DatabaseError
from .models import ENTRY_KIND_LABELS, Entry, EntryKind, Project, Totals
from .timeutil import (
    XPAUSE_STEP,
    XPAUSE_WEEKLY_LIMIT,
    date_range_days,
    format_date_long,
    format_duration,
    format_time,
    intervals_overlap,
    month_bounds,
    strictly_inside,
    subtract_intervals,
    total_covered,
    week_start,
)


class ValidationError(ValueError):
    """Raised when an entry or project change would break a rule."""


def _utc_now() -> str:
    return datetime.now(UTC).strftime("%Y-%m-%dT%H:%M:%SZ")


class TimeReportService:
    def __init__(self, database: Database):
        self.db = database

    # Projects -----------------------------------------------------------------

    def list_projects(self, *, include_archived: bool = False) -> list[Project]:
        query = "SELECT id, name, archived FROM projects"
        if not include_archived:
            query += " WHERE archived = 0"
        query += " ORDER BY name COLLATE NOCASE"
        return [
            Project(id=row["id"], name=row["name"], archived=bool(row["archived"]))
            for row in self.db.conn.execute(query).fetchall()
        ]

    def get_project(self, project_id: int) -> Project | None:
        row = self.db.conn.execute(
            "SELECT id, name, archived FROM projects WHERE id = ?", (project_id,)
        ).fetchone()
        if row is None:
            return None
        return Project(id=row["id"], name=row["name"], archived=bool(row["archived"]))

    def create_project(self, name: str) -> Project:
        cleaned = self._clean_project_name(name)
        try:
            with self.db.transaction() as connection:
                cursor = connection.execute("INSERT INTO projects (name) VALUES (?)", (cleaned,))
                project_id = int(cursor.lastrowid)
        except sqlite3.IntegrityError as exc:
            raise ValidationError(f'Project "{cleaned}" already exists.') from exc
        return Project(id=project_id, name=cleaned, archived=False)

    def rename_project(self, project_id: int, name: str) -> Project:
        cleaned = self._clean_project_name(name)
        try:
            with self.db.transaction() as connection:
                cursor = connection.execute(
                    "UPDATE projects SET name = ? WHERE id = ?", (cleaned, project_id)
                )
                if cursor.rowcount == 0:
                    raise ValidationError("Project not found.")
                row = connection.execute(
                    "SELECT archived FROM projects WHERE id = ?", (project_id,)
                ).fetchone()
        except sqlite3.IntegrityError as exc:
            raise ValidationError(f'Project "{cleaned}" already exists.') from exc
        return Project(id=project_id, name=cleaned, archived=bool(row["archived"]))

    def set_project_archived(self, project_id: int, archived: bool) -> None:
        with self.db.transaction() as connection:
            cursor = connection.execute(
                "UPDATE projects SET archived = ? WHERE id = ?",
                (1 if archived else 0, project_id),
            )
            if cursor.rowcount == 0:
                raise ValidationError("Project not found.")

    def delete_project(self, project_id: int) -> None:
        if self.project_entry_count(project_id) > 0:
            raise ValidationError(
                "This project is used by time entries. Archive it instead of deleting it."
            )
        with self.db.transaction() as connection:
            cursor = connection.execute("DELETE FROM projects WHERE id = ?", (project_id,))
            if cursor.rowcount == 0:
                raise ValidationError("Project not found.")

    def project_entry_count(self, project_id: int) -> int:
        row = self.db.conn.execute(
            "SELECT COUNT(*) AS count FROM entries WHERE project_id = ?", (project_id,)
        ).fetchone()
        return int(row["count"])

    @staticmethod
    def _clean_project_name(name: str) -> str:
        cleaned = " ".join(name.split())
        if not cleaned:
            raise ValidationError("Enter a project name.")
        if len(cleaned) > 80:
            raise ValidationError("Project names must be 80 characters or fewer.")
        return cleaned

    # Entries ------------------------------------------------------------------

    def list_entries(self, start: date, end: date) -> list[Entry]:
        return self._fetch_entries(self.db.conn, start, end)

    def get_entry(self, entry_id: int) -> Entry | None:
        return self._get_entry(self.db.conn, entry_id)

    def save_entry(self, entry: Entry) -> Entry:
        entry = self._normalise_entry(entry)
        original = self.get_entry(entry.id) if entry.id is not None else None
        if entry.id is not None and original is None:
            raise ValidationError("Entry not found.")

        first_day = week_start(entry.entry_date)
        last_day = first_day + timedelta(days=6)

        with self.db.transaction() as connection:
            if original is not None:
                old_first_day = week_start(original.entry_date)
                if old_first_day != first_day:
                    old_week_entries = self._fetch_entries(
                        connection, old_first_day, old_first_day + timedelta(days=6)
                    )
                    old_proposed = [item for item in old_week_entries if item.id != entry.id]
                    self._validate_week_xpause(old_proposed)

            week_entries = self._fetch_entries(connection, first_day, last_day)
            existing = [item for item in week_entries if item.id != entry.id]
            self._validate_candidate(entry, existing)
            self._validate_week_xpause([*existing, entry])
            self._validate_project(entry, original, connection)

            now = _utc_now()
            if entry.id is None:
                cursor = connection.execute(
                    """
                    INSERT INTO entries (
                        entry_date, kind, project_id, label,
                        start_minute, end_minute, created_at, updated_at
                    ) VALUES (?, ?, ?, ?, ?, ?, ?, ?)
                    """,
                    (
                        entry.entry_date.isoformat(),
                        entry.kind.value,
                        entry.project_id,
                        entry.away_reason,
                        entry.start_minute,
                        entry.end_minute,
                        now,
                        now,
                    ),
                )
                entry_id = int(cursor.lastrowid)
            else:
                cursor = connection.execute(
                    """
                    UPDATE entries SET
                        entry_date = ?, kind = ?, project_id = ?, label = ?,
                        start_minute = ?, end_minute = ?, updated_at = ?
                    WHERE id = ?
                    """,
                    (
                        entry.entry_date.isoformat(),
                        entry.kind.value,
                        entry.project_id,
                        entry.away_reason,
                        entry.start_minute,
                        entry.end_minute,
                        now,
                        entry.id,
                    ),
                )
                if cursor.rowcount == 0:
                    raise ValidationError("Entry not found.")
                entry_id = entry.id

        saved = self.get_entry(entry_id)
        if saved is None:
            raise DatabaseError("The saved entry could not be read back.")
        return saved

    def delete_entry(self, entry_id: int) -> None:
        with self.db.transaction() as connection:
            entry = self._get_entry(connection, entry_id)
            if entry is None:
                raise ValidationError("Entry not found.")
            first_day = week_start(entry.entry_date)
            last_day = first_day + timedelta(days=6)
            week_entries = self._fetch_entries(connection, first_day, last_day)
            proposed = [item for item in week_entries if item.id != entry_id]
            self._validate_week_xpause(proposed)
            connection.execute("DELETE FROM entries WHERE id = ?", (entry_id,))

    def _fetch_entries(self, connection: sqlite3.Connection, start: date, end: date) -> list[Entry]:
        rows = connection.execute(
            """
            SELECT
                e.id, e.entry_date, e.kind, e.project_id, e.label,
                e.start_minute, e.end_minute, p.name AS project_name
            FROM entries AS e
            LEFT JOIN projects AS p ON p.id = e.project_id
            WHERE e.entry_date BETWEEN ? AND ?
            ORDER BY e.entry_date, e.start_minute, e.id
            """,
            (start.isoformat(), end.isoformat()),
        ).fetchall()
        return [self._row_to_entry(row) for row in rows]

    def _get_entry(self, connection: sqlite3.Connection, entry_id: int) -> Entry | None:
        row = connection.execute(
            """
            SELECT
                e.id, e.entry_date, e.kind, e.project_id, e.label,
                e.start_minute, e.end_minute, p.name AS project_name
            FROM entries AS e
            LEFT JOIN projects AS p ON p.id = e.project_id
            WHERE e.id = ?
            """,
            (entry_id,),
        ).fetchone()
        return self._row_to_entry(row) if row is not None else None

    @staticmethod
    def _row_to_entry(row: sqlite3.Row) -> Entry:
        keys = row.keys()
        return Entry(
            id=row["id"],
            entry_date=date.fromisoformat(row["entry_date"]),
            kind=EntryKind(row["kind"]),
            start_minute=row["start_minute"],
            end_minute=row["end_minute"],
            project_id=row["project_id"],
            project_name=row["project_name"] if "project_name" in keys else None,
            away_reason=row["label"],
        )

    def _normalise_entry(self, entry: Entry) -> Entry:
        try:
            kind = EntryKind(entry.kind)
        except ValueError as exc:
            raise ValidationError("Unknown entry type.") from exc

        if entry.start_minute < 0 or entry.end_minute > 1440:
            raise ValidationError("Times must be between 00:00 and 24:00.")
        if entry.end_minute <= entry.start_minute:
            raise ValidationError("End time must be later than start time.")

        reason = entry.away_reason.strip() if entry.away_reason else None
        project_id = entry.project_id

        if kind is EntryKind.WORK:
            if project_id is None:
                raise ValidationError("Choose a project for a work entry.")
            reason = None
        elif kind is EntryKind.AWAY:
            if not reason:
                raise ValidationError("Enter a reason for the away time.")
            if len(reason) > 80:
                raise ValidationError("Away reason must be 80 characters or fewer.")
            if project_id is not None:
                raise ValidationError("Away entries cannot have a project.")
            project_id = None
        else:
            if project_id is not None:
                raise ValidationError(f"{ENTRY_KIND_LABELS[kind]} entries cannot have a project.")
            reason = None

        return replace(
            entry, kind=kind, project_id=project_id, away_reason=reason, project_name=None
        )

    def _validate_candidate(self, candidate: Entry, existing: list[Entry]) -> None:
        same_day = [item for item in existing if item.entry_date == candidate.entry_date]

        if candidate.kind is EntryKind.WORK:
            other = self._find_overlap(candidate, same_day, {EntryKind.WORK, EntryKind.AWAY})
            if other is not None:
                raise ValidationError(self._overlap_message("work", other))
            return

        if candidate.kind is EntryKind.LUNCH:
            other = self._find_overlap(
                candidate, same_day, {EntryKind.LUNCH, EntryKind.AWAY, EntryKind.XPAUSE}
            )
            if other is not None:
                raise ValidationError(self._overlap_message("lunch", other))
            return

        if candidate.kind is EntryKind.AWAY:
            other = self._find_overlap(
                candidate,
                same_day,
                {EntryKind.WORK, EntryKind.LUNCH, EntryKind.AWAY, EntryKind.XPAUSE},
            )
            if other is not None:
                raise ValidationError(self._overlap_message("away", other))
            return

        if candidate.kind is EntryKind.XPAUSE:
            if candidate.entry_date.weekday() >= 5:
                raise ValidationError("X-Pause can only be used Monday to Friday.")

            duration = candidate.duration_minutes
            if duration % XPAUSE_STEP != 0:
                raise ValidationError("X-Pause must be entered in 15-minute steps.")
            if duration > XPAUSE_WEEKLY_LIMIT:
                raise ValidationError("X-Pause cannot be longer than 1:00 in one day.")

            used = sum(
                item.duration_minutes
                for item in existing
                if item.kind is EntryKind.XPAUSE
                and week_start(item.entry_date) == week_start(candidate.entry_date)
            )
            remaining = XPAUSE_WEEKLY_LIMIT - used
            if duration > remaining:
                raise ValidationError(
                    "X-Pause is limited to 1:00 per week. "
                    f"You have {format_duration(max(0, remaining))} remaining this week."
                )

            containing_work = [
                item
                for item in same_day
                if item.kind is EntryKind.WORK
                and strictly_inside(
                    candidate.start_minute,
                    candidate.end_minute,
                    item.start_minute,
                    item.end_minute,
                )
            ]
            if not containing_work:
                raise ValidationError(
                    "X-Pause must be inside a work entry. It cannot start at the beginning "
                    "or end at the end of the workday."
                )

            other = self._find_overlap(
                candidate, same_day, {EntryKind.LUNCH, EntryKind.AWAY, EntryKind.XPAUSE}
            )
            if other is not None:
                raise ValidationError(self._overlap_message("X-Pause", other))

    def _validate_week_xpause(self, entries: list[Entry]) -> None:
        xpauses = [item for item in entries if item.kind is EntryKind.XPAUSE]
        total = sum(item.duration_minutes for item in xpauses)
        if total > XPAUSE_WEEKLY_LIMIT:
            raise ValidationError("X-Pause is limited to 1:00 per week.")
        for xpause in xpauses:
            others = [item for item in entries if item is not xpause]
            self._validate_xpause_entry(xpause, others)

    def _validate_xpause_entry(self, xpause: Entry, entries: list[Entry]) -> None:
        if xpause.entry_date.weekday() >= 5:
            raise ValidationError("X-Pause can only be used Monday to Friday.")
        if xpause.duration_minutes % XPAUSE_STEP != 0:
            raise ValidationError("X-Pause must be entered in 15-minute steps.")
        if xpause.duration_minutes > XPAUSE_WEEKLY_LIMIT:
            raise ValidationError("X-Pause cannot be longer than 1:00 in one day.")

        same_day = [item for item in entries if item.entry_date == xpause.entry_date]
        containing_work = [
            item
            for item in same_day
            if item.kind is EntryKind.WORK
            and strictly_inside(
                xpause.start_minute,
                xpause.end_minute,
                item.start_minute,
                item.end_minute,
            )
        ]
        if not containing_work:
            raise ValidationError(
                f"X-Pause on {format_date_long(xpause.entry_date)} is no longer inside a work "
                "entry. Adjust the X-Pause or the work entry first."
            )

        other = self._find_overlap(
            xpause,
            same_day,
            {EntryKind.LUNCH, EntryKind.AWAY, EntryKind.XPAUSE},
            exclude_id=xpause.id,
        )
        if other is not None:
            raise ValidationError(self._overlap_message("X-Pause", other))

    def _validate_project(
        self, entry: Entry, original: Entry | None, connection: sqlite3.Connection
    ) -> None:
        if entry.kind is not EntryKind.WORK:
            return
        row = connection.execute(
            "SELECT id, archived FROM projects WHERE id = ?", (entry.project_id,)
        ).fetchone()
        if row is None:
            raise ValidationError("The selected project no longer exists.")
        if row["archived"] and (original is None or original.project_id != entry.project_id):
            raise ValidationError("The selected project is archived. Choose an active project.")

    @staticmethod
    def _find_overlap(
        candidate: Entry,
        entries: list[Entry],
        kinds: set[EntryKind],
        *,
        exclude_id: int | None = None,
    ) -> Entry | None:
        for item in entries:
            if item.kind not in kinds:
                continue
            if exclude_id is not None and item.id == exclude_id:
                continue
            if intervals_overlap(
                candidate.start_minute,
                candidate.end_minute,
                item.start_minute,
                item.end_minute,
            ):
                return item
        return None

    @staticmethod
    def _overlap_message(candidate_label: str, other: Entry) -> str:
        other_label = ENTRY_KIND_LABELS[other.kind]
        detail = other.project_name or other.away_reason
        suffix = f" · {detail}" if detail else ""
        return (
            f"This {candidate_label} entry overlaps {other_label.lower()} time "
            f"({format_time(other.start_minute)}–{format_time(other.end_minute)}{suffix})."
        )

    # Totals -------------------------------------------------------------------

    def daily_totals(self, day: date, entries: list[Entry] | None = None) -> Totals:
        if entries is None:
            entries = self.list_entries(day, day)
        return self._calculate_totals(entries)

    def totals_by_day(
        self, start: date, end: date, *, include_empty: bool = True
    ) -> list[tuple[date, Totals]]:
        entries = self.list_entries(start, end)
        by_date: defaultdict[date, list[Entry]] = defaultdict(list)
        for entry in entries:
            by_date[entry.entry_date].append(entry)

        if include_empty:
            days = date_range_days(start, end)
        else:
            days = sorted(by_date)
        return [(day, self._calculate_totals(by_date.get(day, []))) for day in days]

    def range_totals(self, start: date, end: date) -> Totals:
        total = Totals()
        for _, day_total in self.totals_by_day(start, end, include_empty=False):
            total += day_total
        return total

    def totals_by_month(self, year: int) -> list[tuple[date, Totals]]:
        results: list[tuple[date, Totals]] = []
        for month in range(1, 13):
            start = date(year, month, 1)
            _, end = month_bounds(start)
            results.append((start, self.range_totals(start, end)))
        return results

    def totals_by_year(self, start_year: int, end_year: int) -> list[tuple[date, Totals]]:
        return [
            (date(year, 1, 1), self.range_totals(date(year, 1, 1), date(year, 12, 31)))
            for year in range(start_year, end_year + 1)
        ]

    def all_time_bounds(self) -> tuple[date, date] | None:
        row = self.db.conn.execute(
            "SELECT MIN(entry_date) AS first_day, MAX(entry_date) AS last_day FROM entries"
        ).fetchone()
        if row is None or row["first_day"] is None:
            return None
        return date.fromisoformat(row["first_day"]), date.fromisoformat(row["last_day"])

    def all_time_totals(self) -> Totals:
        bounds = self.all_time_bounds()
        if bounds is None:
            return Totals()
        return self.range_totals(*bounds)

    def xpause_used_in_week(self, first_day: date, *, exclude_entry_id: int | None = None) -> int:
        week_entries = self.list_entries(first_day, first_day + timedelta(days=6))
        return sum(
            entry.duration_minutes
            for entry in week_entries
            if entry.kind is EntryKind.XPAUSE and entry.id != exclude_entry_id
        )

    def xpause_remaining(self, day: date, *, exclude_entry_id: int | None = None) -> int:
        used = self.xpause_used_in_week(week_start(day), exclude_entry_id=exclude_entry_id)
        return max(0, XPAUSE_WEEKLY_LIMIT - used)

    @staticmethod
    def _calculate_totals(entries: list[Entry]) -> Totals:
        work = [
            (entry.start_minute, entry.end_minute)
            for entry in entries
            if entry.kind is EntryKind.WORK
        ]
        lunch = [
            (entry.start_minute, entry.end_minute)
            for entry in entries
            if entry.kind is EntryKind.LUNCH
        ]
        away = [
            (entry.start_minute, entry.end_minute)
            for entry in entries
            if entry.kind is EntryKind.AWAY
        ]
        xpause = [
            (entry.start_minute, entry.end_minute)
            for entry in entries
            if entry.kind is EntryKind.XPAUSE
        ]

        worked_minutes = total_covered(subtract_intervals(work, lunch))
        lunch_minutes = total_covered(lunch)
        away_minutes = total_covered(away)
        xpause_minutes = total_covered(xpause)
        return Totals(
            worked_minutes=worked_minutes,
            lunch_minutes=lunch_minutes,
            away_minutes=away_minutes,
            xpause_minutes=xpause_minutes,
            days_worked=1 if worked_minutes > 0 else 0,
            days_away=1 if away_minutes > 0 else 0,
        )

    # Paths --------------------------------------------------------------------

    def database_path(self) -> Path:
        return self.db.path
