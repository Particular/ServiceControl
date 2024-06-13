# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl.Audit/ServiceControl.Audit.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG VERSION
WORKDIR /

RUN apt-get update && apt-get install -y wget && rm -rf /var/lib/apt/lists/*

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.Audit \
      org.opencontainers.image.description="ServiceControl audit instance"

EXPOSE 44444
COPY --from=build /deploy/Particular.ServiceControl.Audit ./app

ENV PersistenceType=RavenDB \
    AuditRetentionPeriod=7

HEALTHCHECK --start-period=60s CMD wget --tries=1 --no-verbose -O/dev/null http://localhost:44444/api/configuration || exit 1


USER $APP_UID
ENTRYPOINT ["./app/ServiceControl.Audit"]