#!/usr/bin/env bash
# Generate the 32-byte deployment master key used to derive the auth database,
# every user database and the TOTP secret protection key.
set -euo pipefail

output="${1:-secrets/timereport_master_key}"

if [ -e "$output" ]; then
    echo "Refusing to overwrite existing key: $output" >&2
    exit 1
fi

umask 077
mkdir -p "$(dirname "$output")"
head -c 32 /dev/urandom | base64 | tr -d '\n' > "$output"
chmod 0600 "$output"

echo "Wrote a new master key to $output"
echo
echo "Back this file up with the database volume. Losing it makes every"
echo "encrypted database unrecoverable. Never commit it to Git."
