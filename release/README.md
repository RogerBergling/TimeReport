# Prebuilt TimeReport executables

These are self-contained single-file builds. No Python or separate .NET runtime is
required.

## Linux

```bash
chmod +x TimeReport-linux-x86_64
./TimeReport-linux-x86_64
```

The Linux build expects the usual X11/fontconfig desktop libraries. On a minimal
server or container install the packages listed in `.github/workflows/build.yml`
(`libx11-6`, `libxrandr2`, `libxi6`, `libxcursor1`, `libxext6`, `libxrender1`,
`libfontconfig1`, `libice6`, `libsm6`, `libxfixes3`, `libxinerama1`,
`libxcomposite1`, `libxdamage1` and the XCB libraries).

## Windows

Run `TimeReport-windows-x86_64.exe`.

The Windows executable is unsigned, so Windows SmartScreen may warn on first launch.

## Data

The encrypted database is stored outside this folder:

- Linux: `~/.local/share/timereport/timereport.db`
- Windows: `%LOCALAPPDATA%\TimeReport\timereport.db`

Keep `settings.json.key` with the database; it contains the protected database and
sync keys. Without it, encrypted data cannot be recovered.

Use **File → Database location…** to choose where the database file is stored.

Use **Data → Entry types…** or the **Types** button to add and manage custom entry types.

Use **File → Export to Excel (.xlsx)…** or the **Export** button to export a dynamic date range.

Use **File → Dropbox sync…** to configure optional Dropbox synchronization. You will
need a Dropbox app key and the redirect URI `http://127.0.0.1:53682/dropbox-callback`
registered in the Dropbox App Console.

Use **File → Backup database…** and **File → Restore database…** inside the app.

Linux `.tar.gz`/`.deb` and Windows `.zip` packages can be produced with the scripts
in `scripts/`; see `packaging/README.md` in the repository.

`SHA256SUMS.txt` contains checksums for both executables. Verify a download with:

```bash
sha256sum -c SHA256SUMS.txt
```

```powershell
Get-FileHash .\TimeReport-windows-x86_64.exe -Algorithm SHA256
```

The Windows executable is not code-signed. The checksums prove that the file was
not modified after the release was published, but they do not provide publisher
identity.

TimeReport is released under the MIT License. See `LICENSE`, `TERMS.md` and
`THIRD-PARTY-NOTICES.md` for the license, disclaimer and dependency licenses.
