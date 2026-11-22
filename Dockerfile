# syntax=docker/dockerfile:1

ARG DOTNET_SDK_VERSION=10.0.403
ARG DOTNET_RUNTIME_VERSION=10.0.14

FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_SDK_VERSION}-noble AS build

ARG BUILD_NUMBER=local
ARG BUILD_COMMIT=unknown

WORKDIR /src
COPY Directory.Build.props Directory.Packages.props global.json ./
COPY src/TimeReport.Core/TimeReport.Core.csproj src/TimeReport.Core/packages.lock.json src/TimeReport.Core/
COPY src/TimeReport.Web/TimeReport.Web.csproj src/TimeReport.Web/packages.lock.json src/TimeReport.Web/

RUN dotnet restore src/TimeReport.Web/TimeReport.Web.csproj --locked-mode

COPY src/TimeReport.Core/ src/TimeReport.Core/
COPY src/TimeReport.Web/ src/TimeReport.Web/

RUN dotnet publish src/TimeReport.Web/TimeReport.Web.csproj \
      -c Release \
      -o /app/publish \
      --no-restore \
      /p:UseAppHost=false \
      /p:DebugType=None \
      /p:DebugSymbols=false \
      -p:BuildNumber="${BUILD_NUMBER}" \
      -p:BuildCommit="${BUILD_COMMIT}"

FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_RUNTIME_VERSION}-noble AS runtime

ARG DEBIAN_FRONTEND=noninteractive
ARG DEMO_MODE=false

RUN apt-get update \
 && apt-get install -y --no-install-recommends \
      ca-certificates \
      curl \
      tzdata \
 && rm -rf /var/lib/apt/lists/* \
 && groupadd --gid 10001 timereport \
 && useradd --uid 10001 --gid 10001 --home-dir /data --no-create-home \
      --shell /usr/sbin/nologin timereport \
 && install -d -o 10001 -g 10001 -m 0700 /data /data/users

WORKDIR /app
COPY --from=build /app/publish/ ./
COPY LICENSE THIRD-PARTY-NOTICES.md ./
COPY docker/entrypoint.sh /usr/local/bin/timereport-entrypoint
RUN chmod 0755 /usr/local/bin/timereport-entrypoint \
 && chown -R 10001:10001 /app

ENV ASPNETCORE_URLS=http://0.0.0.0:8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_EnableDiagnostics=0 \
    DOTNET_NOLOGO=1 \
    TIMEREPORT_DATA_DIR=/data \
    TIMEREPORT_DEMO_MODE=${DEMO_MODE} \
    TZ=UTC

LABEL org.opencontainers.image.title="TimeReport Web" \
      org.opencontainers.image.description="Multiuser TimeReport web application with TOTP and per-user SQLCipher databases" \
      org.opencontainers.image.source="https://github.com/RogerBergling/TimeReport" \
      org.opencontainers.image.licenses=MIT

USER 10001:10001
EXPOSE 8080
VOLUME ["/data"]

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
  CMD curl -fsS http://127.0.0.1:8080/healthz >/dev/null || exit 1

STOPSIGNAL SIGTERM
ENTRYPOINT ["/usr/local/bin/timereport-entrypoint", "dotnet", "/app/TimeReport.Web.dll"]
