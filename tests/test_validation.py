"""Validation rules for work, lunch, away and X-Pause entries."""

from __future__ import annotations

from dataclasses import replace
from datetime import date

import pytest

from tests.helpers import make_entry
from timereport.service import TimeReportService, ValidationError

MONDAY = date(2026, 10, 26)
TUESDAY = date(2026, 10, 27)
SATURDAY = date(2026, 10, 31)


def work(service: TimeReportService, project, day=MONDAY, start="08:00", end="17:00"):
    return service.save_entry(make_entry(day, "work", start, end, project_id=project.id))


def test_work_requires_project(service: TimeReportService) -> None:
    with pytest.raises(ValidationError, match="Choose a project"):
        service.save_entry(make_entry(MONDAY, "work", "08:00", "16:00"))


def test_end_must_be_after_start(service: TimeReportService, project) -> None:
    with pytest.raises(ValidationError, match="End time"):
        service.save_entry(make_entry(MONDAY, "work", "16:00", "08:00", project_id=project.id))


def test_work_cannot_overlap_work(service: TimeReportService, project) -> None:
    work(service, project, start="08:00", end="12:00")
    with pytest.raises(ValidationError, match="overlaps work"):
        work(service, project, start="11:00", end="13:00")


def test_work_cannot_overlap_away(service: TimeReportService, project) -> None:
    service.save_entry(make_entry(MONDAY, "away", "09:00", "10:00", reason="Sick"))
    with pytest.raises(ValidationError, match="overlaps away"):
        work(service, project, start="08:00", end="12:00")


def test_lunch_may_overlap_work(service: TimeReportService, project) -> None:
    work(service, project, start="08:00", end="17:00")
    lunch = service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    assert lunch.duration_minutes == 30


def test_lunch_cannot_overlap_lunch(service: TimeReportService) -> None:
    service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    with pytest.raises(ValidationError, match="overlaps lunch"):
        service.save_entry(make_entry(MONDAY, "lunch", "12:15", "12:45"))


def test_away_requires_reason(service: TimeReportService) -> None:
    with pytest.raises(ValidationError, match="reason"):
        service.save_entry(make_entry(MONDAY, "away", "09:00", "10:00"))


def test_away_cannot_overlap_work(service: TimeReportService, project) -> None:
    work(service, project, start="08:00", end="17:00")
    with pytest.raises(ValidationError, match="overlaps work"):
        service.save_entry(make_entry(MONDAY, "away", "09:00", "10:00", reason="Sick"))


def test_away_cannot_overlap_lunch(service: TimeReportService) -> None:
    service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30"))
    with pytest.raises(ValidationError, match="overlaps lunch"):
        service.save_entry(make_entry(MONDAY, "away", "12:15", "13:00", reason="Sick"))


def test_away_reason_is_trimmed(service: TimeReportService) -> None:
    away = service.save_entry(make_entry(MONDAY, "away", "09:00", "10:00", reason="  Sick  "))
    assert away.away_reason == "Sick"


def test_non_work_entries_cannot_have_project(service: TimeReportService, project) -> None:
    with pytest.raises(ValidationError, match="cannot have a project"):
        service.save_entry(make_entry(MONDAY, "lunch", "12:00", "12:30", project_id=project.id))
    with pytest.raises(ValidationError, match="cannot have a project"):
        service.save_entry(
            make_entry(
                MONDAY,
                "away",
                "12:00",
                "13:00",
                project_id=project.id,
                reason="Sick",
            )
        )


def test_xpause_only_on_weekdays(service: TimeReportService, project) -> None:
    work(service, project, day=SATURDAY, start="08:00", end="17:00")
    with pytest.raises(ValidationError, match="Monday to Friday"):
        service.save_entry(make_entry(SATURDAY, "xpause", "10:00", "10:15"))


def test_xpause_requires_15_minute_steps(service: TimeReportService, project) -> None:
    work(service, project)
    with pytest.raises(ValidationError, match="15-minute"):
        service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:10"))
    with pytest.raises(ValidationError, match="15-minute"):
        service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:20"))


def test_xpause_must_be_inside_work(service: TimeReportService, project) -> None:
    work(service, project)
    with pytest.raises(ValidationError, match="inside a work entry"):
        service.save_entry(make_entry(MONDAY, "xpause", "07:45", "08:00"))


def test_xpause_cannot_start_the_day(service: TimeReportService, project) -> None:
    work(service, project)
    with pytest.raises(ValidationError, match="inside a work entry"):
        service.save_entry(make_entry(MONDAY, "xpause", "08:00", "08:15"))


def test_xpause_cannot_end_the_day(service: TimeReportService, project) -> None:
    work(service, project)
    with pytest.raises(ValidationError, match="inside a work entry"):
        service.save_entry(make_entry(MONDAY, "xpause", "16:45", "17:00"))


def test_xpause_cannot_span_a_work_gap(service: TimeReportService, project) -> None:
    work(service, project, start="08:00", end="12:00")
    work(service, project, start="13:00", end="17:00")
    with pytest.raises(ValidationError, match="inside a work entry"):
        service.save_entry(make_entry(MONDAY, "xpause", "11:45", "12:15"))


def test_xpause_cannot_overlap_lunch(service: TimeReportService, project) -> None:
    work(service, project)
    service.save_entry(make_entry(MONDAY, "lunch", "10:00", "10:30"))
    with pytest.raises(ValidationError, match="overlaps lunch"):
        service.save_entry(make_entry(MONDAY, "xpause", "10:15", "10:30"))


def test_xpause_weekly_limit(service: TimeReportService, project) -> None:
    work(service, project, day=MONDAY)
    work(service, project, day=TUESDAY)
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:30"))
    service.save_entry(make_entry(TUESDAY, "xpause", "10:00", "10:30"))
    with pytest.raises(ValidationError, match="1:00 per week"):
        service.save_entry(make_entry(TUESDAY, "xpause", "11:00", "11:15"))


def test_editing_xpause_uses_updated_duration_for_weekly_limit(
    service: TimeReportService, project
) -> None:
    work(service, project, day=MONDAY)
    work(service, project, day=TUESDAY)
    monday_xpause = service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:30"))
    tuesday_xpause = service.save_entry(make_entry(TUESDAY, "xpause", "10:00", "10:15"))

    tuesday_xpause = service.save_entry(replace(tuesday_xpause, end_minute=10 * 60 + 30))
    assert tuesday_xpause.duration_minutes == 30

    with pytest.raises(ValidationError, match="1:00 per week"):
        service.save_entry(replace(monday_xpause, end_minute=10 * 60 + 45))


def test_editing_work_can_invalidate_xpause(service: TimeReportService, project) -> None:
    saved_work = work(service, project)
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))
    with pytest.raises(ValidationError, match="no longer inside"):
        service.save_entry(replace(saved_work, end_minute=10 * 60 + 10))


def test_deleting_work_with_xpause_is_rejected(service: TimeReportService, project) -> None:
    saved_work = work(service, project)
    xpause = service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))
    with pytest.raises(ValidationError, match="no longer inside"):
        service.delete_entry(saved_work.id)
    service.delete_entry(xpause.id)
    service.delete_entry(saved_work.id)


def test_moving_work_to_another_week_cannot_orphan_xpause(
    service: TimeReportService, project
) -> None:
    saved_work = work(service, project)
    service.save_entry(make_entry(MONDAY, "xpause", "10:00", "10:15"))
    with pytest.raises(ValidationError, match="no longer inside"):
        service.save_entry(replace(saved_work, entry_date=date(2026, 11, 2)))


def test_archived_project_cannot_be_used_for_new_work(service: TimeReportService, project) -> None:
    saved_work = work(service, project)
    service.set_project_archived(project.id, True)
    with pytest.raises(ValidationError, match="archived"):
        work(service, project, day=TUESDAY)
    # Editing the existing entry with the same archived project remains possible.
    service.save_entry(replace(saved_work, end_minute=16 * 60))


def test_xpause_sixty_minutes_in_one_day_is_allowed(service: TimeReportService, project) -> None:
    work(service, project)
    xpause = service.save_entry(make_entry(MONDAY, "xpause", "09:00", "10:00"))
    assert xpause.duration_minutes == 60
