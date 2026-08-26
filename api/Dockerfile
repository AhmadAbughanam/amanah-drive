FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY src/AmanahDrive.Api/AmanahDrive.Api.csproj src/AmanahDrive.Api/
RUN dotnet restore src/AmanahDrive.Api/AmanahDrive.Api.csproj

COPY . .
RUN dotnet publish src/AmanahDrive.Api/AmanahDrive.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app

# curl is required for the container HEALTHCHECK below; the aspnet runtime image
# does not include it by default.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Pre-create the log and uploaded-file directories owned by the image's
# built-in non-root user (APP_UID, defined by the .NET 8+ base images)
# *before* they become volume mount points. Docker initializes a fresh named
# volume by copying the ownership/permissions of the directory it is mounted
# over, so this makes /var/log/amanah-drive and /var/lib/amanah-drive/storage
# writable by the non-root process once infra/docker-compose.yml mounts
# api_logs and api_storage there. Without this, the process runs as
# $APP_UID but the volumes would be created owned by root, and both file
# logging and file uploads would fail with a permissions error.
RUN mkdir -p /var/log/amanah-drive /var/lib/amanah-drive/storage \
    && chown -R $APP_UID /var/log/amanah-drive /var/lib/amanah-drive

EXPOSE 8080
COPY --from=build /app/publish .

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health/ready || exit 1

USER $APP_UID
ENTRYPOINT ["dotnet", "AmanahDrive.Api.dll"]
