"""Test helpers."""

from __future__ import annotations

from datetime import date

from timereport.models import Entry, EntryKind
from timereport.timeutil import parse_time


def hm(hour: int, minute: int = 0) -> int:
    return hour * 60 + minute


def make_entry(
    day: date,
    kind: EntryKind | str,
    start: int | str,
    end: int | str,
    *,
    project_id: int | None = None,
    reason: str | None = None,
    entry_id: int | None = None,
) -> Entry:
    if isinstance(kind, str):
        kind = EntryKind(kind)
    if isinstance(start, str):
        start = parse_time(start)
    if isinstance(end, str):
        end = parse_time(end)
    return Entry(
        id=entry_id,
        entry_date=day,
        kind=kind,
        start_minute=start,
        end_minute=end,
        project_id=project_id,
        away_reason=reason,
    )
