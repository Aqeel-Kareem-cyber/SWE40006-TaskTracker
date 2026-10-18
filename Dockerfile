# syntax=docker/dockerfile:1
#
# TaskTracker container image (SWE40006).
# Stage 1 restores and publishes the Razor Pages app with the .NET 10 SDK.
# Stage 2 copies only the published output onto the smaller ASP.NET runtime image.
#
# Build:  docker build --build-arg GIT_SHA=$(git rev-parse HEAD) -t tasktracker:local .
# Run:    docker compose up    (see docker-compose.yml)

ARG DOTNET_VERSION=10.0

# ---------- build stage ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build
WORKDIR /src

# Restore first, using only the project file, so the restore layer is cached
# until dependencies change.
COPY TaskTracker/TaskTracker.csproj TaskTracker/
RUN dotnet restore TaskTracker/TaskTracker.csproj

COPY TaskTracker/ TaskTracker/
RUN dotnet publish TaskTracker/TaskTracker.csproj \
        --configuration Release \
        --no-restore \
        --output /app/publish \
        /p:UseAppHost=false

# ---------- runtime stage ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION} AS runtime

# Commit SHA of the build. The /version endpoint reports it so a deployment
# can be traced back to its commit.
ARG GIT_SHA=dev
ENV APP_VERSION=${GIT_SHA}

LABEL org.opencontainers.image.title="TaskTracker" \
      org.opencontainers.image.description="SWE40006 TaskTracker (ASP.NET Core Razor Pages + EF Core + SQLite)" \
      org.opencontainers.image.revision="${GIT_SHA}"

# Listen on 8080 inside the container (the non-root user cannot bind to 80).
# The SQLite file lives in /app/data, which is mounted as a volume at run time
# so task records survive container replacement.
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ConnectionStrings__DefaultConnection="Data Source=/app/data/tasktracker.db" \
    DOTNET_CLI_TELEMETRY_OPTOUT=1

WORKDIR /app
COPY --from=build /app/publish .

# The .NET runtime images ship a non-root "app" user ($APP_UID). Give it the
# data directory before switching to it.
RUN mkdir -p /app/data && chown -R ${APP_UID}:${APP_UID} /app/data
USER ${APP_UID}

VOLUME ["/app/data"]
EXPOSE 8080

ENTRYPOINT ["dotnet", "TaskTracker.dll"]
