# Changelog

All notable changes to TimeReport are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/).

## [Unreleased]

### Security

- Encrypt pre-encryption migration backups with the database key.
- Fail closed when settings cannot be read or decrypted instead of generating
  replacement keys and overwriting recovery data.
- Do not change permissions on arbitrary existing database directories.
- Validate update release URLs and only open HTTPS GitHub release pages.
- Clean up stale plaintext sync snapshots on startup.
- Keep the single-instance lock file to avoid a re-acquisition race.
- Require confirmation before disabling Dropbox sync encryption.
- Remove the local database path from Excel exports.
- Apply the Dropbox custom CA only to Dropbox connections; update checks use the
  system certificate trust store.
- Version the Dropbox sync container with authenticated chunk ordering and a final
  marker while retaining read support for version 1 files.
- Reject unexpected views and triggers in restored databases and disable trusted
  schema during validation.
- Disable persisted checkout credentials in CI.

## [2.6.1] - 2026-11-01

### Added

- Main-window header shows the application version, build number, current date and
  live time in the configured time zone.
- CI publishes the GitHub run number and commit SHA as assembly metadata; local builds
  show `Build local`.
- About dialog includes the build number.

## [2.6.0] - 2026-11-01

### Added

- SQLCipher encryption at rest for the local database, with automatic migration of
  existing plaintext databases and encrypted backups.
- Per-installation database encryption keys protected by the existing token-protection
  layer.
- Client-side AES-GCM encryption for Dropbox uploads, with a portable sync passphrase.
- Dropbox proxy, custom CA certificate, retry/backoff for transient errors, and a
  conflict dialog showing local and remote modification times.
- In-client update checking against GitHub releases, with startup policy control.
- Time-zone support, per-entry time zones, and time-zone-aware "today" handling.
- Configurable daily and weekly overtime thresholds, totals cards, and Excel columns.
- Backup retention pruning and a backup verification action.
- Database integrity check, compaction and repair actions with automatic pre-repair
  backups.
- Central Settings dialog for time zone, theme, overtime, backup retention and updates.
- Terms/disclaimer documentation for free MIT use at the user's own risk.
- Additional tests for encryption, sync encryption, time zones, overtime, backup
  retention, maintenance and update parsing.

### Changed

- Schema version 3 adds entry time zones and performance indexes.
- SQLCipher export is used for backup, restore and plaintext sync snapshots.
- README and architecture documentation updated for encryption, time zones and
  enterprise operation.

## [2.5.0] - 2026-10-31

### Added

- Keyboard shortcuts: `Ctrl+N` new entry, `Ctrl+E` Excel export, `Ctrl+B` backup,
  `Ctrl+P` projects, `Ctrl+T` entry types, `Ctrl+Shift+S` sync, `F5` refresh,
  `F1` about, `Ctrl+Q` exit and `Ctrl+Left`/`Ctrl+Right` week navigation.
- Enter/Escape handling and default/cancel focus in dialogs.
- Inline date/time format hints and type-aware default times in the entry dialog.
- Rolling `timereport.log` file with startup and error diagnostics.
- About dialog shows the log path and can open the log file.
- `--help` and clearer missing-option errors for the command line.
- `global.json`, `.editorconfig`, central package versions, NuGet lock files,
  Dependabot, CodeQL, pinned GitHub Actions and CI coverage, vulnerability, cache
  and pull-request checks.
- CI artifact checksums, Linux `.tar.gz`/`.deb` and Windows `.zip` packaging.
- MIT license and third-party notices.
- Application icon, Linux desktop entry and packaging scripts.
- Single-instance guard and encrypted Dropbox token storage.
- Inline entry validation and undo for the last deleted entry.
- Light, dark, high-contrast and system theme variants, accessible names and
  live-region status text.

### Changed

- Excel Projects and Types sheets now deduct overlapping lunch from work hours,
  matching the Summary and Daily totals sheets.
- Built-in entry type names are displayed in the selected UI language.
- Totals tab keeps its last period instead of resetting on every visit.
- Switching databases disables Dropbox auto-sync for the new database, preventing
  one database from being uploaded over another.
- Dropbox uploads use revision-checked updates instead of unconditional overwrite and
  stream the database instead of loading it fully into memory.
- Backup/restore and export path checks detect symlinks and hard links.
- README clarifies that optional Dropbox sync is the only network feature.

### Fixed

- Excel export cannot overwrite the live SQLite database or its sidecar files.
- Restore can recover from a damaged live database because the safety backup no
  longer validates the database it is trying to preserve.
- Database opening and restore validation reject missing tables/columns and
  semantically invalid rows.
- Duplicate 31-character Excel sheet names no longer hang the exporter.
- XML-invalid control characters are sanitized in Excel cells and sheet names.
- Implicit entry type selection no longer falls back to archived types.
- Non-ASCII digits in time input produce a validation error instead of a crash.
- Backup/restore path checks resolve symlinks and hard links before comparing paths.
- Existing plaintext Dropbox tokens are migrated to encrypted storage on load.

## [2.4.0] - 2026-10-30

### Added

- Dynamic Excel `.xlsx` export with Summary, Entries, Daily totals, Monthly totals,
  Projects and Types sheets.
- Custom date ranges and presets for this/last month, this/last year and all time.
- English and Swedish export labels.

## [2.3.0] - 2026-10-30

### Added

- Manageable entry types with Work, Lunch, Away and X-Pause behavior categories.
- Migration from schema version 1 to version 2.

## [2.2.0] - 2026-10-29

### Added

- Optional Dropbox sync with OAuth refresh tokens and conflict detection.
- Configurable database location.
