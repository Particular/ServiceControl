# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
ARG VERSION
WORKDIR /

# Not copying csproj and restore as distinct layers for now, why do we need/want that?
COPY . .
RUN dotnet restore src --arch $TARGETARCH

# copy and publish app and libraries.
RUN dotnet build src/ServiceControl.Audit/ServiceControl.Audit.csproj --configuration Release -graph --no-restore --arch $TARGETARCH


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG VERSION

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.Audit \
      org.opencontainers.image.description="ServiceControl audit instance"

EXPOSE 44444
WORKDIR /
COPY --from=build /deploy/Particular.ServiceControl.Audit ./app

RUN mkdir -p /var/servicecontrol && chown -R $APP_UID /var/servicecontrol
VOLUME /var/servicecontrol

ENV "ServiceControl.Audit/PersistenceType"="RavenDB" \
    "ServiceControl.Audit/LogPath"="/var/servicecontrol/logs" \
    "ServiceControl.Audit/ForwardAuditMessages"="False" \
    "ServiceControl.Audit/AuditRetentionPeriod"="14"

USER $APP_UID
ENTRYPOINT ["./app/ServiceControl.Audit"]