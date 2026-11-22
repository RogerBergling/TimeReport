# Architecture

TimeReport has two front ends over one shared domain layer:

- a single-process Avalonia desktop application for Linux and Windows;
- a server-rendered ASP.NET Core web application for multiuser self-hosting.

`TimeReport.Core` owns the domain rules, validation, totals, SQLCipher persistence
and Excel export. The desktop and web projects add their own UI, settings and
deployment concerns.

## Layers

```text
Desktop Views / ViewModels        Web Razor Pages / auth
              \                    /
               v                  v
        TimeReport.Core (TimeReportService, ExcelExportService,
                         validation, totals, Database, migrations)
                         |
                         v
                    SQLite/SQLCipher
```

### Desktop startup

The desktop `Program` parses `--database`, `--data-dir`, `--version` and `--smoke-test`.
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

## Web application

```text
Razor Pages / wwwroot
        |
        v
ASPNET Core auth, antiforgery, security headers, TOTP gate
        |
        +--> AuthDatabase (SQLCipher)        users, MFA, audit
        |
        +--> UserDataStore (SQLCipher)       one database per account
                  |
                  v
             TimeReportService
                  |
                  v
             ExcelExportService
```

The master key is never stored by the application. HKDF-SHA256 derives:

- the authentication database key;
- a per-account time database key, keyed by the account GUID;
- an AES-GCM key for TOTP secrets;
- an HMAC key for recovery-code hashes and pending-MFA tokens.

`AuthDatabase` uses short-lived SQLCipher connections so ASP.NET Core requests can
run concurrently. `UserDataStore` opens one private database per request and uses
SQLite WAL mode for short overlapping reads and writes. The account database and
the time databases are separate, so admin account operations never require
opening a user's time data.

Authentication is a two-stage flow. Password verification creates a short-lived,
single-use pending-MFA record if TOTP is confirmed. A successful TOTP or recovery
code consumes the record and issues the encrypted authentication cookie. The
cookie contains a security stamp; password changes, MFA resets, role changes and
disable operations rotate the stamp and invalidate existing sessions.

TOTP is mandatory. Middleware keeps authenticated users inside the setup flow:
temporary password change, TOTP enrollment, then recovery-code generation. The
same middleware prevents a half-configured account from opening the timesheet.

The web app deliberately does not include Dropbox sync, update checks or desktop
database import. It has no public JSON API; all state changes are Razor Page form
posts protected by antiforgery tokens. The CSP blocks inline scripts, inline
styles and third-party requests.

### Demo mode

`TIMEREPORT_DEMO_MODE=true` is a disposable deployment mode. Startup removes
TimeReport database files, creates a random in-memory master key, seeds a public
`guest` administrator with fake data, and opens a public `/demo/otp` endpoint so
the sign-in page can display a current TOTP code. A hosted service stops the
process after the configured interval; the container restarts, tmpfs data is
lost and the seeder rebuilds the demo. Guest credential mutations are blocked so
visitors cannot lock out the shared account. The demo image is built separately
and must never be pointed at a production volume.

## Tests

`tests/TimeReport.Tests` contains xUnit tests for time parsing, validation, totals,
database migrations, backup/restore, path safety, settings/key failure handling,
single-instance locking, versioned sync encryption, Dropbox sync and Excel export.
`tests/TimeReport.Web.Tests` covers TOTP vectors, key derivation, recovery codes,
encrypted auth/user databases, first-run setup, login with TOTP and recovery
codes, admin pages and per-user data isolation. Tests run serially because some
services use static localization and logging state.
