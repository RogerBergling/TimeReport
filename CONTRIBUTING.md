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

## Web build and tests

The web application shares `src/TimeReport.Core` with the desktop app.

```bash
dotnet build src/TimeReport.Web/TimeReport.Web.csproj -c Release
dotnet test tests/TimeReport.Web.Tests/TimeReport.Web.Tests.csproj -c Release
```

Run it locally with a temporary master key:

```bash
export TIMEREPORT_MASTER_KEY="$(head -c 32 /dev/urandom | base64 -w0)"
export TIMEREPORT_DATA_DIR="$PWD/data"
export TIMEREPORT_COOKIE_SECURE=false
export TIMEREPORT_SETUP_TOKEN="dev-setup-token-1234567890"
dotnet run --project src/TimeReport.Web/TimeReport.Web.csproj
```

The container build and deployment guide is in [`docs/WEB.md`](docs/WEB.md).

## Guidelines

- Keep changes local-first and offline by default. Any network access must be
  explicit, optional and documented.
- Add or update tests for behaviour changes.
- Preserve existing database compatibility and add a schema migration for schema
  changes. The web auth database has its own schema version and per-user time
  databases keep the shared desktop schema.
- The desktop app uses English and Swedish translations for every new
  user-facing string. The web app currently ships English-only and does not
  expose a language selector; add Swedish web resources before enabling one.
  Web requests set the language through `LocalizationService.UseLanguage`; do
  not mutate the process-wide default from a request.
- Keep the UI usable without a mouse: add keyboard handling and accessible names.
- Never add inline scripts or inline styles to the web UI. The Content Security
  Policy allows only same-origin `.js` and `.css` files.
- Never commit a real database, settings file, master key, setup token, Dropbox
  token or exported personal data. `secrets/` and `data/` are gitignored.

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
