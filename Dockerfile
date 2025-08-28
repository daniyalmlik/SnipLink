# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching — restore only re-runs when these change
COPY SnipLink.sln .
COPY src/SnipLink.Api/SnipLink.Api.csproj          src/SnipLink.Api/
COPY src/SnipLink.Blazor/SnipLink.Blazor.csproj    src/SnipLink.Blazor/
COPY src/SnipLink.Shared/SnipLink.Shared.csproj    src/SnipLink.Shared/
COPY src/SnipLink.Tests/SnipLink.Tests.csproj      src/SnipLink.Tests/

RUN dotnet restore SnipLink.sln

COPY src/ src/

RUN dotnet publish src/SnipLink.Api/SnipLink.Api.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

# Non-root user for security
RUN adduser -D appuser && mkdir -p /app/data && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

# SQLite database lives on a named volume so it persists across container restarts
VOLUME /app/data

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=10s --retries=3 \
    CMD wget -qO- http://localhost:8080/healthz || exit 1

USER appuser

ENTRYPOINT ["dotnet", "SnipLink.Api.dll"]
