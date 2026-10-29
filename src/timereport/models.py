"""Application data models."""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date
from enum import StrEnum


class EntryKind(StrEnum):
    WORK = "work"
    LUNCH = "lunch"
    AWAY = "away"
    XPAUSE = "xpause"


ENTRY_KIND_LABELS: dict[EntryKind, str] = {
    EntryKind.WORK: "Work",
    EntryKind.LUNCH: "Lunch",
    EntryKind.AWAY: "Away",
    EntryKind.XPAUSE: "X-Pause",
}

LABEL_TO_ENTRY_KIND: dict[str, EntryKind] = {
    label: kind for kind, label in ENTRY_KIND_LABELS.items()
}


@dataclass(frozen=True, slots=True)
class Project:
    id: int
    name: str
    archived: bool = False


@dataclass(frozen=True, slots=True)
class Entry:
    id: int | None
    entry_date: date
    kind: EntryKind
    start_minute: int
    end_minute: int
    project_id: int | None = None
    project_name: str | None = None
    away_reason: str | None = None

    @property
    def duration_minutes(self) -> int:
        return self.end_minute - self.start_minute


@dataclass(frozen=True, slots=True)
class Totals:
    worked_minutes: int = 0
    lunch_minutes: int = 0
    away_minutes: int = 0
    xpause_minutes: int = 0
    days_worked: int = 0
    days_away: int = 0

    def __add__(self, other: Totals) -> Totals:
        return Totals(
            worked_minutes=self.worked_minutes + other.worked_minutes,
            lunch_minutes=self.lunch_minutes + other.lunch_minutes,
            away_minutes=self.away_minutes + other.away_minutes,
            xpause_minutes=self.xpause_minutes + other.xpause_minutes,
            days_worked=self.days_worked + other.days_worked,
            days_away=self.days_away + other.days_away,
        )
