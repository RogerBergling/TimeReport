# Architecture

TimeReport is a single-process Avalonia desktop application. The design keeps the
UI, domain rules, persistence and integrations in separate layers so they can be
tested without a window.

## Layers

```text
Views / ViewModels
        |
        v
Services (TimeReportService, ExcelExportService, DropboxSyncCoordinator)
        |
        v
Data (Database, migrations, backup/restore)
        |
        v
SQLite file
```

### Startup

`Program` parses `--database`, `--data-dir`, `--version` and `--smoke-test`.
`App` creates an `AppSession`, which loads settings, resolves the database path,
opens SQLite, applies migrations and exposes a `TimeReportService`. The main window
binds to `MainWindowViewModel` and `TotalsViewModel`.

### Domain rules

`TimeReportService` is the single source of truth for project/type management,
entry validation and totals. Important invariants:

- Work entries require an active project; away entries require a reason.
- Work cannot overlap work or away. Lunch may overlap work and is deducted.
- X-Pause is weekday-only, in 15-minute steps, at most one hour per ISO week, and
  strictly inside a work entry.
- Archived projects/types cannot be selected for new entries, but remain valid on
  historical entries.

Validation is transactional. A save that would orphan an X-Pause or create an
overlap is rolled back.

### Persistence

`Database` owns the SQLite connection and schema migrations. Schema version 1 stores
the original entries; version 2 adds managed entry types and backfills existing rows.
Backups use SQLite's online backup API, are validated, and are written to a temporary
file before an atomic replace. Restore validates the source, creates a raw safety
copy of the current database, replaces the live file and reopens it. Path checks
resolve symlinks and reject the live database and its sidecars.

### Excel export

`ExcelExportService` reads a date range and builds six sheets. `XlsxWriter` is a
dependency-free ZIP/XML writer for the modern `.xlsx` format. Work hours in the
Summary, Daily, Projects and Types sheets all deduct overlapping lunch.

### Dropbox sync

`DropboxSyncCoordinator` compares local modification time and remote metadata against
the last successful sync. Uploads are revision-checked (`update` with a rev) and a
409 is surfaced as a conflict rather than overwriting another device. Downloads are
validated and restored through the same safe restore path. Auto-sync is disabled when
the database location changes.

### Diagnostics and security

`AppLog` writes a rolling `timereport.log` in the data folder. Startup, shutdown,
unhandled exceptions and key operation failures are logged. Logging is best-effort
and never blocks the UI.

Dropbox secrets are encrypted with AES-GCM before being written to settings. The key
is kept in an owner-only file and is additionally DPAPI-protected on Windows. A
`SingleInstanceGuard` holds an exclusive lock in the settings folder so two instances
cannot edit the same database.

### UI themes

`AppStyles.axaml` provides Light, Dark and a custom HighContrast theme variant.
`AppThemes` applies the saved theme, and the theme can be changed from **Data → Theme**.

## Tests

`tests/TimeReport.Tests` contains xUnit tests for time parsing, validation, totals,
database migrations, backup/restore, path safety, Dropbox sync and Excel export.
Tests run serially because some services use static localization and logging state.
