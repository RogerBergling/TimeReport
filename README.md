# TimeReport

A local-first desktop time report for Linux and Windows, written in C#/.NET 10 with
Avalonia. The published executables are self-contained single-file binaries: they do
not require Python or a separate .NET installation.

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
- Backup and restore from the GUI, with automatic safety backups.
- 24-hour clock and ISO dates (`YYYY-MM-DD`).
- No account, cloud service, telemetry or network access.

## Prebuilt executables

Ready-to-run self-contained binaries are included in [`release/`](release/):

- Linux: `release/TimeReport-linux-x86_64`
- Windows: `release/TimeReport-windows-x86_64.exe`
- Checksums: `release/SHA256SUMS.txt`

They are also attached to the [v2.0.0 release](https://github.com/RogerBergling/TimeReport/releases/tag/v2.0.0).

## Data storage

The database is deliberately stored outside the application folder. Replacing or
upgrading the executable does not remove your data.

| Platform | Default location |
|---|---|
| Linux | `$XDG_DATA_HOME/timereport/timereport.db`, normally `~/.local/share/timereport/timereport.db` |
| Windows | `%LOCALAPPDATA%\TimeReport\timereport.db` |

The database schema is unchanged from the earlier Python version, so an existing
`timereport.db` continues to work.

The app also writes a rotating log file, `timereport.log`, in the same data folder.
Backups made automatically before a restore or schema migration are placed in the
`backups/` subfolder.

You can override the data folder for testing or portable use:

```bash
TimeReport --data-dir /path/to/data
```

```powershell
TimeReport.exe --data-dir C:\path\to\data
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

## Building from source

Requirements:

- .NET 10 SDK.
- Linux GUI builds need the usual X11/fontconfig desktop libraries. Windows needs the
  standard Windows desktop runtime libraries included with Windows 10+.

```bash
dotnet restore TimeReport.slnx
dotnet build TimeReport.slnx -c Release
dotnet run --project src/TimeReport/TimeReport.csproj -c Release
```

## Publishing self-contained executables

The project is configured for self-contained single-file publishing. No .NET runtime is
required on the target machine.

### Linux

```bash
dotnet publish src/TimeReport/TimeReport.csproj \
  -c Release \
  -r linux-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o dist/linux

# Result: dist/linux/TimeReport
```

### Windows

```powershell
dotnet publish src/TimeReport/TimeReport.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o dist/windows

# Result: dist\windows\TimeReport.exe
```

The GitHub Actions workflow in `.github/workflows/build.yml` runs tests on Linux and
Windows, builds both executables, and uploads them as build artifacts.

## Testing

```bash
dotnet test TimeReport.slnx -c Release
```

The Linux GUI smoke test requires Xvfb:

```bash
xvfb-run -a dotnet run --project src/TimeReport/TimeReport.csproj -c Release -- \
  --smoke-test --data-dir /tmp/timereport-test
```

## Project layout

```text
src/TimeReport/
  App.axaml                 Application theme and startup
  Program.cs                CLI and Avalonia bootstrap
  Data/Database.cs          SQLite schema, migrations, backup and restore
  Models/                   Projects, entries, totals
  Services/                 Validation, totals, paths, time utilities
  ViewModels/               Main timesheet and totals view models
  Views/                    Main window and dialogs
  Styles/AppStyles.axaml    Shared visual theme
tests/TimeReport.Tests/     xUnit tests for time, database, validation, totals, backup
```

## Rules and assumptions

- Week starts Monday and includes Saturday/Sunday.
- Work entries cannot overlap other work or away entries.
- Lunch may overlap work and is deducted from worked time. Lunch cannot overlap another
  lunch, away time or X-Pause.
- Away entries cannot overlap work, lunch, X-Pause or another away entry.
- X-Pause is not deducted from worked time; it is displayed separately.
- A work entry must end on the same date. Cross-midnight shifts are not supported in
  version 2.
- The database is single-user and unencrypted. It is intended for local use.
- Dates are ISO dates and times use the 24-hour clock.

## Version

`2.0.0`
