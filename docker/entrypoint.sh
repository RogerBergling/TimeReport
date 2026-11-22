#!/bin/sh
set -eu

# SQLCipher sidecars and application temp files inherit the process umask.
# Keep new files owner-only even if a future code path does not set a mode.
umask 077

exec "$@"
