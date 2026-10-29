"""Tests for time, date and interval helpers."""

from __future__ import annotations

from datetime import date

import pytest

from timereport.timeutil import (
    add_months,
    date_range_days,
    format_date_long,
    format_day_short,
    format_duration,
    format_time,
    format_week_range,
    merge_intervals,
    month_bounds,
    parse_iso_date,
    parse_time,
    subtract_intervals,
    total_covered,
    week_end,
    week_number,
    week_start,
    year_bounds,
)


@pytest.mark.parametrize(
    ("text", "expected"),
    [
        ("08:00", 480),
        ("8:00", 480),
        ("00:00", 0),
        ("23:59", 1439),
        ("24:00", 1440),
    ],
)
def test_parse_time_valid(text: str, expected: int) -> None:
    assert parse_time(text) == expected


@pytest.mark.parametrize("text", ["", "8", "25:00", "24:01", "12:60", "12:0a", "-1:00"])
def test_parse_time_invalid(text: str) -> None:
    with pytest.raises(ValueError):
        parse_time(text)


def test_parse_time_rejects_end_of_day_for_start() -> None:
    with pytest.raises(ValueError):
        parse_time("24:00", allow_end_of_day=False)


def test_format_time() -> None:
    assert format_time(0) == "00:00"
    assert format_time(480) == "08:00"
    assert format_time(1439) == "23:59"
    assert format_time(1440) == "24:00"
    with pytest.raises(ValueError):
        format_time(1441)


def test_format_duration() -> None:
    assert format_duration(0) == "0:00"
    assert format_duration(65) == "1:05"
    assert format_duration(600) == "10:00"
    assert format_duration(1500) == "25:00"
    with pytest.raises(ValueError):
        format_duration(-1)


def test_parse_iso_date() -> None:
    assert parse_iso_date("2026-10-26") == date(2026, 10, 26)
    with pytest.raises(ValueError):
        parse_iso_date("26/10/2026")


def test_week_boundaries() -> None:
    monday = date(2026, 10, 26)
    sunday = date(2026, 11, 1)
    assert week_start(monday) == monday
    assert week_start(sunday) == monday
    assert week_end(monday) == sunday
    assert week_number(monday) == 44


def test_month_and_year_bounds() -> None:
    assert month_bounds(date(2024, 2, 10)) == (date(2024, 2, 1), date(2024, 2, 29))
    assert month_bounds(date(2025, 2, 10)) == (date(2025, 2, 1), date(2025, 2, 28))
    assert year_bounds(date(2026, 10, 29)) == (date(2026, 1, 1), date(2026, 12, 31))


def test_add_months() -> None:
    assert add_months(date(2026, 1, 31), 1) == date(2026, 2, 28)
    assert add_months(date(2024, 1, 31), 1) == date(2024, 2, 29)
    assert add_months(date(2026, 1, 15), -1) == date(2025, 12, 15)


def test_date_range_days() -> None:
    assert date_range_days(date(2026, 10, 26), date(2026, 10, 28)) == [
        date(2026, 10, 26),
        date(2026, 10, 27),
        date(2026, 10, 28),
    ]
    assert date_range_days(date(2026, 10, 28), date(2026, 10, 26)) == []


def test_formatting() -> None:
    day = date(2026, 10, 26)
    assert format_date_long(day) == "Monday 26 October 2026"
    assert format_day_short(day) == "Mon 26 Oct"
    assert format_week_range(day, date(2026, 11, 1)) == "26 Oct – 1 Nov 2026"


def test_merge_intervals() -> None:
    assert merge_intervals([(480, 720), (600, 780)]) == [(480, 780)]
    assert merge_intervals([(480, 600), (600, 720)]) == [(480, 720)]
    assert merge_intervals([(480, 600), (700, 800)]) == [(480, 600), (700, 800)]
    assert merge_intervals([]) == []


def test_subtract_intervals() -> None:
    assert subtract_intervals([(480, 1020)], [(720, 750)]) == [
        (480, 720),
        (750, 1020),
    ]
    assert subtract_intervals([(480, 1020)], [(400, 500), (1000, 1100)]) == [(500, 1000)]
    assert subtract_intervals([(480, 600)], [(480, 600)]) == []
    assert subtract_intervals([(480, 600)], [(700, 800)]) == [(480, 600)]


def test_total_covered() -> None:
    assert total_covered([(480, 600), (600, 720)]) == 240
    assert total_covered([]) == 0
