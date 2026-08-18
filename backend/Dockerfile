# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0-noble AS build
WORKDIR /src

COPY NodeReel.Domain/NodeReel.Domain.csproj NodeReel.Domain/
COPY NodeReel.Application/NodeReel.Application.csproj NodeReel.Application/
COPY NodeReel.Infrastructure/NodeReel.Infrastructure.csproj NodeReel.Infrastructure/
COPY NodeReel.Api/NodeReel.Api.csproj NodeReel.Api/

RUN dotnet restore NodeReel.Api/NodeReel.Api.csproj

COPY NodeReel.Domain/ NodeReel.Domain/
COPY NodeReel.Application/ NodeReel.Application/
COPY NodeReel.Infrastructure/ NodeReel.Infrastructure/
COPY NodeReel.Api/ NodeReel.Api/

RUN dotnet publish NodeReel.Api/NodeReel.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

# Static binaries — do not depend on apt ffmpeg or PATH inside the runtime image.
FROM mwader/static-ffmpeg:7.1 AS ffmpeg

# Pin Ubuntu (noble), not a chiseled tag: we need apt for python/curl and a real shell.
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble AS final
USER root
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates python3 \
    && curl -L https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp -o /usr/local/bin/yt-dlp \
    && chmod a+rx /usr/local/bin/yt-dlp \
    && rm -rf /var/lib/apt/lists/*

COPY --from=ffmpeg /ffmpeg /usr/local/bin/ffmpeg
COPY --from=ffmpeg /ffprobe /usr/local/bin/ffprobe
RUN chmod a+rx /usr/local/bin/ffmpeg /usr/local/bin/ffprobe

WORKDIR /app
COPY --from=build /app/publish .

# Dokploy creates .env next to this Dockerfile from "Environment Settings" before build.
# ASP.NET does not read .env by itself — Program.cs loads /app/.env at startup.
COPY .env /app/.env

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
ENV Ffmpeg__BinaryPath=/usr/local/bin/ffmpeg
ENV YtDlp__BinaryPath=/usr/local/bin/yt-dlp
ENV PATH="/usr/local/bin:/usr/bin:/bin:${PATH}"

EXPOSE 8080

HEALTHCHECK --interval=10s --timeout=5s --retries=10 --start-period=20s \
  CMD curl -fsS http://127.0.0.1:8080/health >/dev/null || exit 1

# aspnet images default to non-root "app"; keep that after installs.
USER $APP_UID

ENTRYPOINT ["dotnet", "NodeReel.Api.dll"]
