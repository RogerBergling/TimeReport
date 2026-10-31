# Contributing

Thanks for improving TimeReport.

## Prerequisites

- .NET 10 SDK. The exact SDK feature band is pinned in `global.json`.
- Linux GUI builds need the X11/fontconfig libraries listed in the CI workflow.
- Windows builds use the standard Windows 10+ desktop libraries.

## Build and test

```bash
dotnet restore TimeReport.slnx
dotnet build TimeReport.slnx -c Release --no-restore
dotnet test TimeReport.slnx -c Release --no-build
dotnet format TimeReport.slnx --verify-no-changes --no-restore
```

Linux GUI smoke test:

```bash
xvfb-run -a dotnet run --project src/TimeReport/TimeReport.csproj \
  -c Release --no-build -- --smoke-test --data-dir /tmp/timereport-test
```

## Guidelines

- Keep changes local-first and offline by default. Any network access must be
  explicit, optional and documented.
- Add or update tests for behaviour changes.
- Preserve existing database compatibility and add a schema migration for schema
  changes.
- Use English and Swedish translations for every new user-facing string.
- Keep the UI usable without a mouse: add keyboard handling and accessible names.
- Never commit a real database, settings file, Dropbox token or exported personal data.

## Release checklist

1. Update `CHANGELOG.md`, `README.md` and the version in `TimeReport.csproj`
   and `app.manifest`.
2. Run the full build, test, format and GUI smoke checks.
3. Publish Linux and Windows self-contained executables.
4. Replace the binaries in `release/`, regenerate `release/SHA256SUMS.txt` and
   verify the checksums.
5. Build the `.tar.gz`/`.deb` and Windows `.zip` with the scripts in `scripts/`.
6. Commit, push, wait for CI and publish a GitHub release with both binaries,
   packages, `LICENSE`, `THIRD-PARTY-NOTICES.md` and `SHA256SUMS.txt`.
