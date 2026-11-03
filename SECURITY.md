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
  the configured Dropbox account by default. If sync encryption is disabled after an
  explicit confirmation, it sends a plaintext snapshot. Plaintext sync snapshots are
  kept only in a private temporary folder and are cleaned up on startup. Update checks
  contact the GitHub releases API and use the system certificate trust store; the
  Dropbox custom CA setting is not applied to update checks. No other network access
  is performed.
- Dropbox tokens are encrypted with AES-GCM before they are written to the settings
  file. The encryption key is stored in a separate owner-only file. On Windows the key
  is additionally protected with the current user's DPAPI key. On Linux the key file is
  owner-only, so protect the user account and data folder. Back up the settings file and
  its key together; without the key the tokens cannot be decrypted.
