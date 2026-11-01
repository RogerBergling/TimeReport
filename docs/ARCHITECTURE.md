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

`Database` owns the SQLCipher connection and schema migrations. Schema version 1 stores
the original entries; version 2 adds managed entry types; version 3 adds per-entry
time zones and performance indexes. The live database and automatic backups are
encrypted at rest. The key is protected by the same token-protection layer as Dropbox
secrets. Existing plaintext databases are exported into an encrypted file with a
pre-encryption backup.

Backups use SQLCipher's `sqlcipher_export`, are validated, and are written to a
temporary file before an atomic replace. Restore accepts plaintext or encrypted
sources and re-encrypts the result into the live database. Path checks resolve symlinks
and hard links and reject the live database and its sidecars. Automatic backups are
pruned to the configured retention count.

### Excel export

`ExcelExportService` reads a date range and builds six sheets. `XlsxWriter` is a
dependency-free ZIP/XML writer for the modern `.xlsx` format. Work hours in the
Summary, Daily, Projects and Types sheets all deduct overlapping lunch.

### Dropbox sync

`DropboxSyncCoordinator` compares local modification time and remote metadata against
the last successful sync. Uploads are revision-checked (`update` with a rev) and a
409 is surfaced as a conflict rather than overwriting another device. Uploads are
created from a plaintext snapshot and encrypted with a portable sync key using
chunked AES-GCM; downloads are decrypted into a temporary plaintext database and then
re-encrypted into the local SQLCipher database. The client supports proxy and custom
CA settings, retries transient 429/5xx responses, and shows a conflict dialog with
local/remote timestamps. Auto-sync is disabled when the database location changes.

### Diagnostics and security

`AppLog` writes a rolling `timereport.log` in the data folder. Startup, shutdown,
unhandled exceptions and key operation failures are logged. Logging is best-effort
and never blocks the UI.

Dropbox secrets are encrypted with AES-GCM before being written to settings. The key
is kept in an owner-only file and is additionally DPAPI-protected on Windows. A
`SingleInstanceGuard` holds an exclusive lock in the settings folder so two instances
cannot edit the same database.

### Time zones, overtime and settings

Entries store an optional IANA/Windows-compatible time-zone identifier. `TimeZoneService`
resolves IDs across platforms, and the selected time zone controls "today" and new
entry defaults. `TimeReportService` applies configurable daily/weekly overtime
thresholds to totals. `SettingsDialog` configures time zone, theme, overtime, backup
retention and update checking.

### UI themes

`AppStyles.axaml` provides Light, Dark and a custom HighContrast theme variant.
`AppThemes` applies the saved theme, and the theme can be changed from **Data → Theme**
or Settings.

### Updates and maintenance

`UpdateService` checks the latest GitHub release and opens the release page when a
newer version is available. Database maintenance supports integrity checks, compaction
and repair with a pre-repair backup.

## Tests

`tests/TimeReport.Tests` contains xUnit tests for time parsing, validation, totals,
database migrations, backup/restore, path safety, Dropbox sync and Excel export.
Tests run serially because some services use static localization and logging state.
