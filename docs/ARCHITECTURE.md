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
binds to `MainWindowViewModel` and `TotalsViewModel`. The header displays the
version/build number and a live date/time clock driven by a `DispatcherTimer` in the
configured time zone.

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
time zones and performance indexes. The live database, automatic backups and
pre-encryption backups are encrypted at rest. The key is protected by the same
token-protection layer as Dropbox secrets. Existing plaintext databases are exported
into an encrypted file with an encrypted pre-encryption backup. Settings loading fails
closed when the settings file or key file is missing or cannot be decrypted; it never
generates replacement keys silently.

Backups use SQLCipher's `sqlcipher_export`, are validated, and are written to a
private temporary file before an atomic replace. Restore accepts plaintext or
encrypted sources and re-encrypts the result into the live database. The selected
source is validated read-only with both SQLite and SQLCipher integrity checks, then
staged to a private copy so the original is never written. Restore validation runs
with `trusted_schema = OFF` and rejects unexpected tables, views and triggers. Path
checks resolve symlinks and hard links and reject the live database and its sidecars.
Automatic backups are pruned to the configured retention count. Plaintext sync
snapshots are kept in an owner-only temporary folder and cleaned up on startup only
from app-owned directories.

### Excel export

`ExcelExportService` reads a date range and builds six sheets. `XlsxWriter` is a
dependency-free ZIP/XML writer for the modern `.xlsx` format. It writes an owner-only
temporary file and atomically renames it into place. Work hours in the Summary, Daily,
Projects and Types sheets all deduct overlapping lunch.

### Dropbox sync

`DropboxSyncCoordinator` compares local modification time and remote metadata against
the last successful sync. Uploads are revision-checked (`update` with a rev) and a
409 is surfaced as a conflict rather than overwriting another device. Uploads are
created from a plaintext snapshot and encrypted with a portable sync key using a
versioned chunked AES-GCM container. Version 3 derives a per-file key from the sync
passphrase with PBKDF2-SHA256 and a random salt stored in the container. Version 2
authenticates a per-file identifier, chunk index, chunk length and final marker;
version 1 and 2 files remain readable. A plaintext remote copy is rejected while sync
encryption is enabled. Downloads are decrypted into a private temporary directory and
then re-encrypted into the local SQLCipher database. The OAuth loopback listener
validates the callback path, state and request size, and ignores stray connections.
The client supports proxy and custom CA settings (without embedded proxy credentials),
retries transient 429/5xx responses, and shows a conflict dialog with local/remote
timestamps.
The custom CA is applied only to Dropbox connections; update checks use the system
trust store. Auto-sync is disabled when the database location changes.

### Diagnostics and security

`AppLog` writes a rolling `timereport.log` in the data folder. Startup, shutdown,
unhandled exceptions and key operation failures are logged. Logging is best-effort
and never blocks the UI.

Dropbox secrets are encrypted with AES-GCM before being written to settings. The key
is kept in an owner-only file and is additionally DPAPI-protected on Windows. A
`SingleInstanceGuard` holds an exclusive lock in the settings folder so two instances
cannot edit the same database. The lock file is intentionally left in place after
shutdown; deleting it would create a re-acquisition race.

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
newer version is available. Release URLs are restricted to HTTPS GitHub release pages
for this repository; update checks use the system certificate trust store. Database
maintenance supports integrity checks, compaction and repair with a pre-repair backup.

## Tests

`tests/TimeReport.Tests` contains xUnit tests for time parsing, validation, totals,
database migrations, backup/restore, path safety, settings/key failure handling,
single-instance locking, versioned sync encryption, Dropbox sync and Excel export.
Tests run serially because some services use static localization and logging state.
