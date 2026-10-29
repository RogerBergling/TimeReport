"""Totals calculations across days, weeks, months, years and all time."""

from __future__ import annotations

from datetime import date, timedelta

from tests.helpers import make_entry
from timereport.service import TimeReportService

MONDAY = date(2026, 10, 26)
TUESDAY = date(2026, 10, 27)


def test_work_lunch_and_xpause_totals(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "17:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))

    totals = service.daily_totals(MONDAY)
    assert totals.worked_minutes == 8 * 60 + 30
    assert totals.lunch_minutes == 30
    assert totals.xpause_minutes == 15
    assert totals.away_minutes == 0
    assert totals.days_worked == 1
    assert totals.days_away == 0


def test_xpause_is_not_deducted_from_work(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "17:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))
    assert service.daily_totals(MONDAY).worked_minutes == 9 * 60


def test_split_work_around_lunch(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "work", "12:30", "17:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    totals = service.daily_totals(MONDAY)
    assert totals.worked_minutes == 8 * 60 + 30
    assert totals.lunch_minutes == 30


def test_lunch_outside_work_does_not_reduce_work(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    totals = service.daily_totals(MONDAY)
    assert totals.worked_minutes == 4 * 60
    assert totals.lunch_minutes == 30


def test_lunch_can_cover_all_work(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "09:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "lunch", "08:00", "09:00"))
    totals = service.daily_totals(MONDAY)
    assert totals.worked_minutes == 0
    assert totals.lunch_minutes == 60
    assert totals.days_worked == 0


def test_away_and_work_on_same_day(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "away", "09:00", "12:00", reason="Sick"))
    service.save_entry(make_entry(MONDAY, "work", "13:00", "17:00", project_id=project.id))
    totals = service.daily_totals(MONDAY)
    assert totals.worked_minutes == 4 * 60
    assert totals.away_minutes == 3 * 60
    assert totals.days_worked == 1
    assert totals.days_away == 1


def test_days_worked_and_away_are_counted_once(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "work", "13:00", "17:00", project_id=project.id))
    service.save_entry(make_entry(TUESDAY, "work", "08:00", "16:00", project_id=project.id))
    service.save_entry(make_entry(TUESDAY, "away", "16:00", "17:00", reason="Appointment"))

    totals = service.range_totals(MONDAY, TUESDAY)
    assert totals.days_worked == 2
    assert totals.days_away == 1
    assert totals.worked_minutes == 4 * 60 + 4 * 60 + 8 * 60


def test_week_month_year_and_all_time(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    service.save_entry(make_entry(TUESDAY, "work", "08:00", "16:00", project_id=project.id))
    service.save_entry(
        make_entry(date(2026, 11, 2), "work", "08:00", "10:00", project_id=project.id)
    )
    service.save_entry(
        make_entry(date(2027, 1, 4), "work", "08:00", "11:00", project_id=project.id)
    )

    week = service.range_totals(MONDAY, MONDAY + timedelta(days=6))
    assert week.worked_minutes == 4 * 60 + 8 * 60

    october = service.range_totals(date(2026, 10, 1), date(2026, 10, 31))
    assert october.worked_minutes == 12 * 60

    year_2026 = service.range_totals(date(2026, 1, 1), date(2026, 12, 31))
    assert year_2026.worked_minutes == 14 * 60

    assert service.all_time_totals().worked_minutes == 17 * 60
    assert service.all_time_bounds() == (MONDAY, date(2027, 1, 4))

    months = service.totals_by_month(2026)
    assert len(months) == 12
    assert months[9][1].worked_minutes == 12 * 60
    assert months[10][1].worked_minutes == 2 * 60

    years = service.totals_by_year(2026, 2027)
    assert len(years) == 2
    assert years[0][1].worked_minutes == 14 * 60
    assert years[1][1].worked_minutes == 3 * 60


def test_week_includes_month_boundary(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    service.save_entry(
        make_entry(date(2026, 11, 1), "work", "08:00", "10:00", project_id=project.id)
    )
    week = service.range_totals(MONDAY, MONDAY + timedelta(days=6))
    assert week.worked_minutes == 6 * 60


def test_totals_by_day_includes_empty_days(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "12:00", project_id=project.id))
    rows = service.totals_by_day(MONDAY, MONDAY + timedelta(days=6))
    assert len(rows) == 7
    assert rows[0][0] == MONDAY
    assert rows[0][1].worked_minutes == 4 * 60
    assert rows[1][1].worked_minutes == 0
    assert len(service.totals_by_day(MONDAY, MONDAY + timedelta(days=6), include_empty=False)) == 1


def test_empty_database_totals(service: TimeReportService) -> None:
    assert service.all_time_bounds() is None
    assert service.all_time_totals().worked_minutes == 0
    assert service.range_totals(MONDAY, TUESDAY).worked_minutes == 0


def test_xpause_usage_and_remaining(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "work", "08:00", "17:00", project_id=project.id))
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))
    assert service.xpause_used_in_week(MONDAY) == 15
    assert service.xpause_remaining(MONDAY) == 45
