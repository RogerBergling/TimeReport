$ErrorActionPreference = "Stop"
Set-Location (Join-Path $PSScriptRoot "..")
python -m PyInstaller --clean --noconfirm packaging/timereport.spec
