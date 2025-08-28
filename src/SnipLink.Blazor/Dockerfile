# ── Stage 1: Build ───────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first for layer caching
COPY SnipLink.sln .
COPY src/SnipLink.Api/SnipLink.Api.csproj          src/SnipLink.Api/
COPY src/SnipLink.Blazor/SnipLink.Blazor.csproj    src/SnipLink.Blazor/
COPY src/SnipLink.Shared/SnipLink.Shared.csproj    src/SnipLink.Shared/
COPY src/SnipLink.Tests/SnipLink.Tests.csproj      src/SnipLink.Tests/

RUN dotnet restore SnipLink.sln

COPY src/ src/

RUN dotnet publish src/SnipLink.Blazor/SnipLink.Blazor.csproj \
    -c Release -o /app/publish --no-restore

# ── Stage 2: Runtime ─────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0-alpine AS final
WORKDIR /app

RUN adduser -D appuser && chown -R appuser:appuser /app

COPY --from=build --chown=appuser:appuser /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

USER appuser

ENTRYPOINT ["dotnet", "SnipLink.Blazor.dll"]
