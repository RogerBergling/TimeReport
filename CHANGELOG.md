# Changelog

All notable changes to TimeReport are documented here.

The format follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and the project uses [Semantic Versioning](https://semver.org/).

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
