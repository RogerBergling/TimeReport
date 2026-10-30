# Prebuilt TimeReport executables

These are self-contained single-file builds. No Python or separate .NET runtime is
required.

## Linux

```bash
chmod +x TimeReport-linux-x86_64
./TimeReport-linux-x86_64
```

## Windows

Run `TimeReport-windows-x86_64.exe`.

The Windows executable is unsigned, so Windows SmartScreen may warn on first launch.

## Data

The database is stored outside this folder:

- Linux: `~/.local/share/timereport/timereport.db`
- Windows: `%LOCALAPPDATA%\TimeReport\timereport.db`

Use **File → Database location…** to choose where the database file is stored.

Use **Data → Entry types…** or the **Types** button to add and manage custom entry types.

Use **File → Dropbox sync…** to configure optional Dropbox synchronization. You will
need a Dropbox app key and the redirect URI `http://127.0.0.1:53682/dropbox-callback`
registered in the Dropbox App Console.

Use **File → Backup database…** and **File → Restore database…** inside the app.

`SHA256SUMS.txt` contains checksums for both executables.
