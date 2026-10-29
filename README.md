# TimeReport

A local-first desktop time report for Linux and Windows. It stores everything in a
SQLite database on your own computer, keeps working across application updates, and
includes backup/restore in the GUI.

## Features

- Weekly timesheet showing all seven days, Monday–Sunday.
- Multiple entries per day: work, lunch, away and X-Pause.
- Work entries have a project, start time and end time.
- Projects are stored in the database. Add, rename, archive and delete unused projects
  from the GUI.
- Lunch is deducted from work time when it overlaps a work entry.
- Away entries have a free-text reason and a start/end time. They are tracked separately
  from work.
- X-Pause is tracked as a separate weekly benefit:
  - Monday–Friday only.
  - 15-minute steps: 15, 30, 45 or 60 minutes.
  - Maximum 1 hour per ISO week.
  - Must be strictly inside a work entry. It cannot start at the beginning or end at the
    end of the workday.
  - It does not reduce worked hours.
- Daily totals, weekly totals, monthly totals, yearly totals and an all-time total.
- Backup and restore from the File menu, with automatic safety backups.
- 24-hour clock and ISO dates (`YYYY-MM-DD`).
- No account, cloud service, telemetry or network access.

## Data storage

The database is deliberately stored outside the application folder. Replacing or
upgrading the executable does not remove your data.

| Platform | Default location |
|---|---|
| Linux | `$XDG_DATA_HOME/timereport/timereport.db`, normally `~/.local/share/timereport/timereport.db` |
| Windows | `%LOCALAPPDATA%\TimeReport\timereport.db` |
| macOS (not an official target) | `~/Library/Application Support/TimeReport/timereport.db` |

The app also writes a rotating log file, `timereport.log`, in the same data folder.
Backups made automatically before a restore or schema migration are placed in the
`backups/` subfolder.

You can override the data folder for testing or portable use:

```bash
python -m timereport --data-dir /path/to/data
```

```powershell
python -m timereport --data-dir C:\path\to\data
```

## Backup and restore

Use **File → Backup database…** to write a consistent SQLite backup to any location.

Use **File → Restore database…** to replace the current database. Restore:

1. Validates the selected file with SQLite's integrity check.
2. Checks that it has the expected TimeReport schema.
3. Creates an automatic backup of the current database in `backups/`.
4. Replaces the database atomically.
5. Refuses databases from a newer, unsupported schema version.

Backup files are normal SQLite databases and can be moved between Linux and Windows.
They contain personal time data, so store them securely.

## Running from source

### Linux

```bash
sudo apt-get install python3-tk python3-venv
python3 -m venv .venv
source .venv/bin/activate
python -m pip install -e ".[dev]"
python -m timereport
```

### Windows

Install Python 3.11 or newer from python.org, then:

```powershell
py -3.12 -m venv .venv
.venv\Scripts\Activate.ps1
python -m pip install -e ".[dev]"
python -m timereport
```

## Building executables

PyInstaller is included in the development dependencies.

### Linux

```bash
source .venv/bin/activate
python -m PyInstaller --clean --noconfirm packaging/timereport.spec
# Result: dist/TimeReport
```

Or use `packaging/build_linux.sh`.

### Windows

```powershell
.venv\Scripts\Activate.ps1
python -m PyInstaller --clean --noconfirm packaging/timereport.spec
# Result: dist\TimeReport.exe
```

Or use `packaging\build_windows.ps1`.

The GitHub Actions workflow in `.github/workflows/build.yml` runs tests on Linux and
Windows and uploads `TimeReport` / `TimeReport.exe` as build artifacts. PyInstaller
cannot reliably cross-compile a Windows executable from Linux, so the Windows build is
produced on a Windows runner.

## Testing

```bash
source .venv/bin/activate
python -m pytest -q
ruff check .
ruff format --check .
python -m compileall -q src
```

The GUI smoke test requires a display. On Linux, run it under Xvfb:

```bash
xvfb-run -a python -m timereport --smoke-test --data-dir /tmp/timereport-test
```

## Project layout

```text
src/timereport/
  app.py             Application bootstrap and CLI
  db.py              SQLite schema, migrations, backup and restore
  models.py          Dataclasses and entry types
  paths.py           Cross-platform data folder resolution
  service.py         Validation, CRUD and totals calculations
  timeutil.py        24-hour time parsing, periods and interval math
  ui/
    main_window.py   Main window, menus and weekly timesheet
    day_card.py      One day card in the week view
    entry_dialog.py  Add/edit entry dialog
    projects_dialog.py
    totals_view.py   Week/month/year/all-time totals
    theme.py
    widgets.py
tests/               Unit, integration and GUI smoke tests
packaging/           PyInstaller spec and build scripts
```

## Rules and assumptions

- Week starts Monday and includes Saturday/Sunday.
- Work entries cannot overlap other work or away entries.
- Lunch may overlap work and is deducted from worked time. Lunch cannot overlap another
  lunch, away time or X-Pause.
- Away entries cannot overlap work, lunch, X-Pause or another away entry.
- X-Pause is not deducted from worked time; it is displayed separately.
- A work entry must end on the same date. Cross-midnight shifts are not supported in
  version 1.
- The database is single-user and unencrypted. It is intended for local use.
- Dates are ISO dates and times use the 24-hour clock.

## Version

`1.0.0`
