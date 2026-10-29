#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."
python -m PyInstaller --clean --noconfirm packaging/timereport.spec
