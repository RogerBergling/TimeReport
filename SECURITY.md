# Security policy

## Supported versions

Only the latest published TimeReport release receives security fixes.

## Reporting a vulnerability

Please do not open a public issue for a security problem. Use GitHub's
[private vulnerability reporting](https://github.com/RogerBergling/TimeReport/security/advisories/new)
and include:

- the affected version and platform,
- a description of the issue,
- steps to reproduce,
- any relevant logs with personal data removed.

You will receive an acknowledgement when the report has been reviewed. Please allow
time for a fix before publishing details.

## Data and network model

- The SQLite database is encrypted at rest with SQLCipher. The database key and
  Dropbox tokens are encrypted inside `settings.json`; `settings.json.key` contains
  the key that protects them. Keep the database, `settings.json` and
  `settings.json.key` together when moving or backing up data. If either settings
  file is lost, encrypted data cannot be recovered. TimeReport fails closed rather
  than generating replacement keys when settings cannot be read or decrypted.
- Optional Dropbox sync uploads a client-side AES-GCM encrypted database snapshot to
  the configured Dropbox account by default. New passphrase-based sync containers use
  a random per-file salt and PBKDF2-SHA256 (600,000 iterations); older version 1 and 2
  containers remain readable for migration. The sync passphrase is stored protected in
  the settings file, and the dialog rejects short or low-variety passphrases. When sync
  encryption is enabled, a plaintext Dropbox copy is rejected rather than silently
  imported; downloading legacy plaintext requires explicitly disabling sync encryption.
  If sync encryption is disabled after an explicit confirmation, TimeReport uploads a
  plaintext snapshot. Plaintext sync snapshots are kept only in an owner-only
  temporary folder and are cleaned up on startup. Update checks contact the GitHub
  releases API and use the system certificate trust store; the Dropbox custom CA
  setting is not applied to update checks. No other network access is performed.
- Proxy URLs containing user names or passwords are rejected when settings are saved
  and are never written verbatim to the log. Custom-CA validation requires a TLS server
  authentication EKU in addition to a chain to the selected root. When a custom CA is
  configured, only Dropbox requests use it.
- Dropbox tokens are encrypted with AES-GCM before they are written to the settings
  file. The encryption key is stored in a separate owner-only file. On Windows the key
  is additionally protected with the current user's DPAPI key. On Linux the key file is
  owner-only, so protect the user account and data folder. Back up the settings file and
  its key together; without the key the tokens cannot be decrypted.
- Settings, logs, sync snapshots, downloads and Excel temporary files are created with
  owner-only permissions on Unix before content is written. Stale temporary files are
  removed only from the default application directory or a directory explicitly marked
  as app-owned; an arbitrary `--data-dir` or database parent is not chmodded, emptied
  or treated as app-owned. `--database`/database-location aliases are compared by file
  identity so a symlink or hard link cannot reopen or replace the live database.
- Backup and restore validation checks that the source is a regular, reasonably sized
  file, opens the user-selected source read-only, runs both SQLite integrity checks and,
  for encrypted databases, SQLCipher's `cipher_integrity_check`. Restore works on a
  private copy so the original source is never written.
