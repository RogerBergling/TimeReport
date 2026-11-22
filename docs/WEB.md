# TimeReport Web

TimeReport Web is the multiuser web application in this repository. It reuses the
desktop domain rules, validation and Excel export through `TimeReport.Core`, but
stores each account's time data in its own SQLCipher database.

The web app is **server-rendered** (ASP.NET Core Razor Pages). There is no SPA,
no public API, no telemetry, no CDN and no third-party browser request.

## Features

- Weekly timesheet with work, lunch, away and X-Pause.
- Projects and custom entry types, private per account.
- Daily, weekly, monthly, yearly and all-time totals.
- Overtime thresholds and time zone, configured per account.
- Excel `.xlsx` export with the same six sheets as the desktop application.
  Web exports are capped at a 10-year range to bound work and memory.
- Mandatory TOTP with QR enrollment and single-use recovery codes.
- Admin-created accounts. No self-registration.
- Administrator role for account management and audit, without a direct timesheet
  view of other users. Administrators can perform audited credential resets, so
  choose administrators you trust with account recovery.
- Per-user encrypted SQLite databases and an encrypted account database.
- SQLCipher at rest, TOTP secrets encrypted with a separate derived key.
- CSRF protection, strict security headers, secure cookies and login throttling.

Not included in the web build: Dropbox sync, desktop update checks, desktop
database import, email delivery and password reset by email. Those are
desktop-only or intentionally out of scope. The web UI is English in this
release; the desktop app keeps its English/Swedish interface.

## Architecture

```text
Browser
  │  HTTPS (your reverse proxy terminates TLS)
  ▼
TimeReport.Web (ASP.NET Core, 127.0.0.1:8080)
  ├── AuthDatabase        /data/auth.db       users, MFA, audit, pending MFA
  ├── UserDataStore       /data/users/<id>/timereport.db
  │     └── TimeReportService / SQLCipher
  ├── ExcelExportService  in-memory download
  └── MasterKey           env/file, never stored by the app
```

The master key is not the database key. It is a 32-byte root secret. HKDF-SHA256
derives:

- the authentication database key;
- a different key for every user database;
- the AES-GCM key used to protect TOTP secrets;
- the HMAC key used to hash recovery codes and pending-MFA tokens.

If the master key is lost, the authentication database and every user database
are unrecoverable. Back it up separately from the data volume, but keep both
available for a restore.

## Quick start with Docker Compose

1. Create the master key:

   ```bash
   ./scripts/generate-master-key.sh
   ```

   This writes `secrets/timereport_master_key` with mode `0600`. Back it up now.

2. Copy and edit the environment file:

   ```bash
   cp .env.example .env
   ```

   Set at least:

   ```dotenv
   TIMEREPORT_ALLOWED_HOSTS=time.example.com
   TIMEREPORT_TRUSTED_PROXIES=172.16.0.0/12
   ```

   Keep `TIMEREPORT_HTTP_BIND=127.0.0.1` when your TLS proxy runs on the same
   host. Leave `TIMEREPORT_SETUP_TOKEN` empty to generate a one-time token in
   the container log, or set your own strong token.

3. If the GHCR package is private, sign in first (or make the package public):

   ```bash
   echo "$GHCR_TOKEN" | docker login ghcr.io -u YOUR_GITHUB_USER --password-stdin
   ```

4. Start the service:

   ```bash
   docker compose pull
   docker compose up -d
   docker compose logs -f timereport
   ```

   `:latest` is published from `v*` tags. Before the first release, change the
   image tag to `:main`, use an immutable version tag, or build locally with
   `docker compose build`.

5. Find the first-run token if you did not set one:

   ```bash
   docker compose logs timereport | grep "First-run setup token"
   ```

6. Open `https://time.example.com/setup` through your TLS reverse proxy.

7. Create the first administrator, scan the QR code with an authenticator app,
   verify the code, and save the ten recovery codes. The application will not
   open the timesheet until TOTP and recovery codes are complete.

8. Sign in as the administrator and create accounts under **Users**. New users
   receive a temporary password, must change it, then enroll TOTP and save
   recovery codes.

## Reverse proxy

Do not publish port 8080 directly to the internet. Bind it to loopback (the
compose default) or a private interface and terminate TLS in a reverse proxy.
Forward the original `Host` header and send `X-Forwarded-Proto: https`.
`TIMEREPORT_TRUSTED_PROXIES` must contain the proxy address or CIDR as seen from
inside the container, otherwise the app ignores forwarded headers.

### Caddy

```caddyfile
time.example.com {
    reverse_proxy 127.0.0.1:8080
}
```

### HAProxy

```haproxy
frontend timereport_https
    mode http
    bind *:443 ssl crt /etc/haproxy/certs/time.example.com.pem
    option forwardfor
    http-request set-header X-Forwarded-Proto https
    default_backend timereport

backend timereport
    mode http
    server timereport 127.0.0.1:8080
```

### nginx

```nginx
server {
    listen 443 ssl http2;
    server_name time.example.com;

    ssl_certificate     /etc/letsencrypt/live/time.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/time.example.com/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:8080;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    }
}
```

The cookies use the `__Host-` prefix, `Secure`, `HttpOnly` and `SameSite=Strict`.
That requires HTTPS from the browser's point of view. If login redirects back to
the sign-in page, check that the proxy sends `X-Forwarded-Proto: https` and that
the proxy address is in `TIMEREPORT_TRUSTED_PROXIES`.

## Public demo deployment

The repository also publishes a disposable public demo image:

```text
ghcr.io/rogerbergling/timereport-web-demo
```

It is built from the same source with `DEMO_MODE=true`. Use a separate Compose
file and a separate hostname:

```bash
cp .env.demo.example .env.demo
docker compose -f compose.demo.yaml --env-file .env.demo pull
docker compose -f compose.demo.yaml --env-file .env.demo up -d
```

The demo has no persistent volume. `/data` is tmpfs, and the container is
restarted after `TIMEREPORT_DEMO_RESET_HOURS` (default `2`) by the application
itself. On startup the demo image:

- removes any old TimeReport database state;
- creates a random in-memory master key;
- seeds a `guest` administrator with fake timesheet data;
- sets `guest` / `guest` with a fixed TOTP secret and recovery code.

The sign-in page displays the guest username, password, TOTP QR/secret, the
current TOTP code (refreshed in the browser) and a one-time recovery code. The
guest is an administrator so the full application can be explored, but the guest
credentials cannot be changed from the UI. Visitors can create another user to
test password resets, MFA resets and multiuser isolation.

**Never deploy the demo image with real data.** The guest password is public,
the guest is an administrator, and the application deliberately deletes its
database state on every restart. Put the demo behind the same TLS reverse proxy
as production and set `TIMEREPORT_DEMO_ALLOWED_HOSTS` / the trusted proxy CIDR.

## Environment variables

| Variable | Default | Purpose |
|---|---|---|
| `TIMEREPORT_DATA_DIR` | `/data` in a container, `./data` otherwise | Persistent data directory. |
| `TIMEREPORT_MASTER_KEY` | unset | Base64 32-byte master key. Prefer the file variable. |
| `TIMEREPORT_MASTER_KEY_FILE` | unset | File containing the master key. |
| `TIMEREPORT_SETUP_TOKEN` | generated | Optional first-run setup token. |
| `TIMEREPORT_SETUP_TOKEN_FILE` | unset | File containing the setup token. |
| `TIMEREPORT_COOKIE_SECURE` | `true` | Set to `false` only for local HTTP development. |
| `TIMEREPORT_TRUSTED_PROXIES` | `127.0.0.1/32,::1/128` | Comma-separated proxy IPs/CIDRs. |
| `TIMEREPORT_ALLOWED_HOSTS` | `*` | Accepted `Host` header values. Set your domain in production. |
| `TIMEREPORT_SESSION_MINUTES` | `30` | Sliding idle session lifetime, 5–1440. |
| `TIMEREPORT_SESSION_ABSOLUTE_HOURS` | `12` | Absolute session lifetime, 1–168. |
| `TIMEREPORT_MFA_MINUTES` | `5` | Pending MFA cookie lifetime, 1–15. |
| `TIMEREPORT_LOGIN_WINDOW_MINUTES` | `15` | Login throttle window. |
| `TIMEREPORT_MAX_LOGIN_ATTEMPTS_PER_IP` | `20` | Failed attempts per IP per window. |
| `TIMEREPORT_MAX_LOGIN_ATTEMPTS_PER_USER` | `8` | Failed attempts per user key per window. |
| `TIMEREPORT_MAX_MFA_ATTEMPTS` | `5` | Wrong TOTP/recovery attempts per pending login. |
| `TIMEREPORT_MAX_SETUP_ATTEMPTS` | `10` | Setup-token attempts per IP. |
| `TIMEREPORT_ISSUER` | `TimeReport` | Issuer label in the authenticator QR code. |
| `TIMEREPORT_DEMO_MODE` | `false` | Throw-away guest demo. Never enable in production. |
| `TIMEREPORT_DEMO_RESET_HOURS` | `2` | Demo restart/reseed interval, 1–24. |

The container also sets `ASPNETCORE_URLS=http://0.0.0.0:8080` and
`ASPNETCORE_ENVIRONMENT=Production`. Do not override the URL binding unless you
also update the proxy and compose port mapping.

## Data and backups

Inside the volume:

```text
/data/auth.db                          encrypted account/MFA/audit database
/data/users/<guid>/timereport.db       encrypted private database per account
/data/users/<guid>/timereport.db-wal   transient SQLite WAL (when active)
```

The application stores Data Protection keys only in `/tmp`, so a container
restart signs everyone out. It never writes those keys to the data volume.

To back up, stop the service and copy the volume **and** the master key:

```bash
docker compose stop timereport
docker run --rm \
  -v timereport_timereport-data:/data:ro \
  -v "$PWD/backups":/backup \
  alpine sh -c 'tar czf /backup/timereport-data.tar.gz -C /data .'
cp secrets/timereport_master_key backups/timereport_master_key
docker compose start timereport
```

Restore by stopping the service, extracting the archive into the volume and
restoring the same master key. A backup of the volume without the master key
cannot be decrypted.

Do not copy a live database with ordinary file-copy tools. The application opens
SQLCipher with WAL enabled; use a stopped-service snapshot or the same backup
discipline you use for other SQLite services.

Per-user files are private to the account in the application, but the server
operator with root access to the volume and the master key can decrypt them.
If you need separation from the operator, run each account in a separate
deployment.

Run exactly one web container per data volume. The session throttle and
last-administrator protection are process-local, and sharing a SQLCipher file
between replicas is not supported.

## Security model

- **Passwords:** PBKDF2-HMAC-SHA256, 210,000 iterations, random per-user salt,
  minimum 12 characters, no composition rules. Unknown usernames run a dummy
  hash to reduce timing differences.
- **TOTP:** RFC 6238, SHA-1, 6 digits, 30-second period, one step of clock skew.
  The last accepted step is stored so a code cannot be replayed. TOTP is
  mandatory before the application opens. Enrollment requires the current
  password, not just an authenticated session.
- **Recovery codes:** ten single-use codes per enrollment, shown once, stored as
  HMAC-SHA256 keyed hashes. They are required before application access.
- **Sessions:** encrypted authentication cookie, `HttpOnly`, `SameSite=Strict`,
  `Secure` in production, `__Host-` prefix. Idle timeout defaults to 30 minutes.
  A password change, MFA reset, role change or disable operation rotates the
  security stamp and revokes existing sessions.
- **CSRF:** Razor Pages antiforgery tokens on every state-changing form.
- **Headers:** strict Content Security Policy (no inline scripts/styles, no
  third-party requests), `X-Content-Type-Options`, `X-Frame-Options`,
  `Referrer-Policy`, `Permissions-Policy`, COOP/CORP and HSTS on HTTPS.
- **Rate limiting:** in-memory per-IP and per-user sliding windows for password,
  MFA, recovery-code, re-authentication and setup attempts.
- **Audit:** login, MFA, recovery, setup, password, account and role changes are
  written to the encrypted auth database. Passwords, TOTP codes, recovery codes
  and secrets are never logged.
- **Encryption:** SQLCipher for auth and user databases; AES-GCM for TOTP
  secrets and setup/pending tokens; HKDF-SHA256 for key separation.
- **Isolation:** every user has a separate database file and key. Admin pages do
  not query another user's timesheet. Administrators can reset a user's password
  and MFA, so an administrator can take over an account if they choose; those
  actions are audited. Restrict the administrator role to people you trust with
  that recovery power.

### Break-glass administrator MFA reset

If an administrator loses both the authenticator and all recovery codes, stop
the service and run the built-in reset. It requires the master key and the data
volume:

```bash
docker compose stop timereport
docker compose run --rm timereport --reset-admin-mfa admin
docker compose start timereport
```

The administrator signs in with their password and enrolls TOTP again. This
revokes existing sessions and clears the old recovery codes. The event is
audited as actor `cli`.

There is no web-based password reset by email. If a user loses their password,
an administrator resets it from **Users** and gives them the one-time temporary
password. If the last administrator loses the password, recovery requires a
manual database/volume operation; keep an offline credential and recovery-code
record.

## Master key rotation

There is no online key rotation. The auth database and every user database are
encrypted with keys derived from the current master key. Changing the master key
without a migration makes the data unreadable. Plan rotation as an export/import
to a new deployment, and keep the old deployment read-only until the migration
is verified.

## Image tags and GHCR

The workflow `.github/workflows/web-image.yml` publishes:

```text
ghcr.io/rogerbergling/timereport-web:main
ghcr.io/rogerbergling/timereport-web:edge
ghcr.io/rogerbergling/timereport-web:sha-<commit>
ghcr.io/rogerbergling/timereport-web:<version>
ghcr.io/rogerbergling/timereport-web:<major>.<minor>
ghcr.io/rogerbergling/timereport-web:latest
```

`latest` is only published for `v*` tags. Use an immutable version tag or digest
in production. Images are built for `linux/amd64` and `linux/arm64`. New GHCR
packages are private by default; either make the package public in GitHub package
settings or run `docker login ghcr.io` with a token that has `read:packages`.

The demo image is published separately as
`ghcr.io/rogerbergling/timereport-web-demo`; production and demo tags are built
from the same commit by `.github/workflows/web-image.yml`.

The image runs as UID/GID `10001`, with a read-only root filesystem in the
example compose file. The only writable paths are the `/data` volume and a
`/tmp` tmpfs. The Data Protection key ring is written under `/tmp` and is lost
on restart by design.

## Local development

```bash
export TIMEREPORT_MASTER_KEY="$(head -c 32 /dev/urandom | base64 -w0)"
export TIMEREPORT_DATA_DIR="$PWD/data"
export TIMEREPORT_COOKIE_SECURE=false
export TIMEREPORT_SETUP_TOKEN="dev-setup-token-1234567890"
export TIMEREPORT_TRUSTED_PROXIES="127.0.0.1/32"
dotnet run --project src/TimeReport.Web/TimeReport.Web.csproj
```

Open `http://127.0.0.1:5000` (or the URL printed by Kestrel) and use the setup
token. Never use `TIMEREPORT_COOKIE_SECURE=false` on a network-accessible host.

Build and test:

```bash
dotnet restore TimeReport.slnx --locked-mode
dotnet build TimeReport.slnx -c Release --no-restore
dotnet test tests/TimeReport.Web.Tests/TimeReport.Web.Tests.csproj -c Release
dotnet format TimeReport.slnx --verify-no-changes --no-restore
```

Docker:

```bash
docker build -t timereport-web:local .
docker run --rm -p 127.0.0.1:8080:8080 \
  -e TIMEREPORT_MASTER_KEY="$(head -c 32 /dev/urandom | base64 -w0)" \
  -e TIMEREPORT_COOKIE_SECURE=false \
  -e TIMEREPORT_SETUP_TOKEN=dev-setup-token-1234567890 \
  -v timereport-local:/data \
  timereport-web:local
```

## Troubleshooting

- **The setup page says the token is invalid.** The generated token expires
  after 24 hours or was replaced by a restart. Restart the container with no
  users to generate a new one, or set `TIMEREPORT_SETUP_TOKEN`.
- **Login succeeds but the session is lost on every request.** The proxy is not
  trusted, `X-Forwarded-Proto` is missing, or `TIMEREPORT_COOKIE_SECURE` is true
  while you are testing over plain HTTP.
- **`The authentication database could not be opened`.** The master key does not
  match the existing `/data/auth.db`. Restore the original master key; do not
  delete the database to "fix" it.
- **`The user database could not be opened`.** The master key changed or a user
  database was copied from another deployment. Restore the matching key and
  volume.
- **Everyone is signed out after an upgrade.** Data Protection keys are
  deliberately ephemeral in `/tmp`. Accounts and data are unaffected.
- **The container cannot write `/data`.** The image runs as UID/GID `10001`. A
  fresh named volume is initialized from the image directory, but an existing
  bind mount or pre-created volume may be root-owned. Fix ownership once:

  ```bash
  docker run --rm -v timereport_timereport-data:/data alpine \
    chown -R 10001:10001 /data
  ```

  For a bind mount, use `chown -R 10001:10001 /path/to/data` on the host.

## License and reporting

TimeReport Web is MIT licensed, like the desktop application. Report security
issues through GitHub's private vulnerability reporting, not a public issue. See
[`SECURITY.md`](../SECURITY.md).
