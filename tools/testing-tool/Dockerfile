# Multi-stage build for the ServiceControl testing tool.
# Build context is the repository root so global.json + nuget.config are available.
ARG RUNTIMEVERSION=10.0

# --- build stage ---
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:10.0 AS build
ARG TARGETARCH
WORKDIR /src
ENV CI=true

# global.json pins the SDK; nuget.config adds the Particular feed for later NServiceBus deps.
COPY global.json nuget.config ./
COPY tools/testing-tool/ ./tools/testing-tool/

# Publish the runnable host; its ProjectReferences pull in Contracts + Scenarios.
RUN dotnet publish tools/testing-tool/TestingTool/TestingTool.csproj \
    --configuration Release \
    --arch $TARGETARCH \
    --output /app \
    /p:UseAppHost=false

# --- runtime stage ---
# Uses the same chiseled composite base as the rest of the ServiceControl images so that
# globalization/ICU dependencies are present.
FROM mcr.microsoft.com/dotnet/aspnet:$RUNTIMEVERSION-noble-chiseled-composite-extra
WORKDIR /app

COPY --from=build /app ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Health probes are served by the /health/live and /health/ready HTTP endpoints (added in
# Phase 6). Kubernetes uses HTTP GET probes directly; the chiseled base ships no shell/curl so
# exec-based probes are not supported.

USER $APP_UID
ENTRYPOINT ["dotnet", "TestingTool.dll"]
