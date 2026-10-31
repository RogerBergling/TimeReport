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

- The SQLite database is local and unencrypted. Protect the data folder and backups.
- Optional Dropbox sync uploads an unencrypted database copy to the configured
  Dropbox account. No other network access is performed.
- Dropbox tokens are encrypted with AES-GCM before they are written to the settings
  file. The encryption key is stored in a separate owner-only file. On Windows the key
  is additionally protected with the current user's DPAPI key. On Linux the key file is
  owner-only, so protect the user account and data folder. Back up the settings file and
  its key together; without the key the tokens cannot be decrypted.
