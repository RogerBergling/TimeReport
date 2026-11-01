# Packaging

The packaging scripts turn the published single-file binaries into small
distributable archives. They do not rebuild the application.

## Linux

```bash
dotnet publish src/TimeReport/TimeReport.csproj \
  -c Release -r linux-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -o dist/linux

./scripts/package-linux.sh 2.6.1
```

This creates:

- `dist/packages/TimeReport-<version>-linux-x86_64.tar.gz`
- `dist/packages/TimeReport-<version>-linux-x86_64.deb` when `dpkg-deb` is available
- `.sha256` files for both

The tarball contains the executable, desktop entry, icon, README, license and
third-party notices. The `.deb` installs to `/usr/bin`, `/usr/share/applications`
and the hicolor icon theme.

## Windows

```powershell
dotnet publish src/TimeReport/TimeReport.csproj `
  -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -p:EnableCompressionInSingleFile=true `
  -o dist/windows

./scripts/package-windows.ps1 2.6.1
```

This creates `dist/packages/TimeReport-<version>-windows-x86_64.zip` plus a
SHA-256 file. The zip contains the executable, README, license and third-party
notices. It is intentionally not code-signed; see `SECURITY.md`.
