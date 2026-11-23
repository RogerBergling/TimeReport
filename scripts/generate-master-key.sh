#!/usr/bin/env bash
# Generate the 32-byte deployment master key used to derive the auth database,
# every user database and the TOTP secret protection key.
set -euo pipefail

output="${1:-secrets/timereport_master_key}"
directory="$(dirname "$output")"

umask 077
mkdir -p "$directory"

# Docker Compose file secrets are bind-mounted into the container with the
# host file's ownership and mode. The app runs as UID 10001, so the mounted key
# must be readable by that UID. Keep the secrets directory private to the host
# account (0700) so a 0644 key is still not readable by other host users.
if [ "$(basename "$directory")" = "secrets" ]; then
    chmod 0700 "$directory"
else
    echo "Warning: $directory is not the default 'secrets' directory." >&2
    echo "Ensure its permissions prevent other host users from reading the key." >&2
fi

if [ -e "$output" ]; then
    chmod 0644 "$output"
    echo "Existing master key kept; permissions updated for the container: $output"
    echo
    echo "Back this file up with the database volume. Losing it makes every"
    echo "encrypted database unrecoverable. Never commit it to Git."
    exit 0
fi

head -c 32 /dev/urandom | base64 | tr -d '\n' > "$output"
chmod 0644 "$output"

echo "Wrote a new master key to $output"
echo
echo "Back this file up with the database volume. Losing it makes every"
echo "encrypted database unrecoverable. Never commit it to Git."
