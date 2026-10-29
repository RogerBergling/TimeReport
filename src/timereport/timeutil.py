"""Time, date and interval helpers used by the UI and the service layer."""

from __future__ import annotations

import calendar
import re
from collections.abc import Iterable
from datetime import date, timedelta

MINUTES_PER_DAY = 24 * 60
XPAUSE_WEEKLY_LIMIT = 60
XPAUSE_STEP = 15

WEEKDAY_NAMES = (
    "Monday",
    "Tuesday",
    "Wednesday",
    "Thursday",
    "Friday",
    "Saturday",
    "Sunday",
)
WEEKDAY_SHORT = ("Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun")
MONTH_NAMES = (
    "January",
    "February",
    "March",
    "April",
    "May",
    "June",
    "July",
    "August",
    "September",
    "October",
    "November",
    "December",
)

_TIME_RE = re.compile(r"^(\d{1,2}):(\d{2})$")


def parse_time(value: str, *, allow_end_of_day: bool = True) -> int:
    """Parse a 24-hour HH:MM value into minutes since midnight.

    ``24:00`` is accepted for an end time and represented as 1440.
    """

    text = value.strip()
    match = _TIME_RE.fullmatch(text)
    if not match:
        raise ValueError("Enter a time as HH:MM using the 24-hour clock.")

    hour = int(match.group(1))
    minute = int(match.group(2))

    if hour == 24 and minute == 0:
        if not allow_end_of_day:
            raise ValueError("24:00 can only be used as an end time.")
        return MINUTES_PER_DAY

    if hour > 23 or minute > 59:
        raise ValueError("Enter a time as HH:MM using the 24-hour clock.")

    return hour * 60 + minute


def format_time(minutes: int) -> str:
    if minutes == MINUTES_PER_DAY:
        return "24:00"
    if not 0 <= minutes < MINUTES_PER_DAY:
        raise ValueError("Time must be between 00:00 and 24:00.")
    return f"{minutes // 60:02d}:{minutes % 60:02d}"


def format_duration(minutes: int) -> str:
    if minutes < 0:
        raise ValueError("Duration cannot be negative.")
    return f"{minutes // 60}:{minutes % 60:02d}"


def parse_iso_date(value: str) -> date:
    try:
        return date.fromisoformat(value.strip())
    except ValueError as exc:
        raise ValueError("Enter a date as YYYY-MM-DD.") from exc


def week_start(day: date) -> date:
    """Return the Monday of the ISO week containing ``day``."""

    return day - timedelta(days=day.weekday())


def week_end(day: date) -> date:
    return week_start(day) + timedelta(days=6)


def week_number(day: date) -> int:
    return day.isocalendar().week


def month_bounds(day: date) -> tuple[date, date]:
    last_day = calendar.monthrange(day.year, day.month)[1]
    return date(day.year, day.month, 1), date(day.year, day.month, last_day)


def year_bounds(day: date) -> tuple[date, date]:
    return date(day.year, 1, 1), date(day.year, 12, 31)


def add_months(day: date, months: int) -> date:
    month_index = day.year * 12 + (day.month - 1) + months
    year, month_zero = divmod(month_index, 12)
    month = month_zero + 1
    day_number = min(day.day, calendar.monthrange(year, month)[1])
    return date(year, month, day_number)


def date_range_days(start: date, end: date) -> list[date]:
    if end < start:
        return []
    return [start + timedelta(days=offset) for offset in range((end - start).days + 1)]


def format_date_long(day: date) -> str:
    return f"{WEEKDAY_NAMES[day.weekday()]} {day.day} {MONTH_NAMES[day.month - 1]} {day.year}"


def format_day_short(day: date) -> str:
    return f"{WEEKDAY_SHORT[day.weekday()]} {day.day} {MONTH_NAMES[day.month - 1][:3]}"


def format_month_name(day: date) -> str:
    return MONTH_NAMES[day.month - 1]


def format_week_range(start: date, end: date) -> str:
    return (
        f"{start.day} {MONTH_NAMES[start.month - 1][:3]} – "
        f"{end.day} {MONTH_NAMES[end.month - 1][:3]} {end.year}"
    )


def merge_intervals(intervals: Iterable[tuple[int, int]]) -> list[tuple[int, int]]:
    """Merge overlapping or adjacent (start, end) intervals."""

    ordered = sorted((start, end) for start, end in intervals if end > start)
    if not ordered:
        return []

    merged: list[tuple[int, int]] = [ordered[0]]
    for start, end in ordered[1:]:
        current_start, current_end = merged[-1]
        if start <= current_end:
            merged[-1] = (current_start, max(current_end, end))
        else:
            merged.append((start, end))
    return merged


def subtract_intervals(
    base: Iterable[tuple[int, int]], cuts: Iterable[tuple[int, int]]
) -> list[tuple[int, int]]:
    """Return the parts of ``base`` that are not covered by ``cuts``."""

    remaining = merge_intervals(base)
    for cut_start, cut_end in merge_intervals(cuts):
        next_remaining: list[tuple[int, int]] = []
        for start, end in remaining:
            if cut_end <= start or cut_start >= end:
                next_remaining.append((start, end))
                continue
            if start < cut_start:
                next_remaining.append((start, cut_start))
            if cut_end < end:
                next_remaining.append((cut_end, end))
        remaining = next_remaining
        if not remaining:
            break
    return remaining


def total_covered(intervals: Iterable[tuple[int, int]]) -> int:
    return sum(end - start for start, end in merge_intervals(intervals))


def intervals_overlap(a_start: int, a_end: int, b_start: int, b_end: int) -> bool:
    return a_start < b_end and b_start < a_end


def strictly_inside(inner_start: int, inner_end: int, outer_start: int, outer_end: int) -> bool:
    return outer_start < inner_start and inner_end < outer_end
