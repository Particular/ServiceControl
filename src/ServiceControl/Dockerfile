# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl/ServiceControl.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
ARG VERSION
WORKDIR /

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl \
      org.opencontainers.image.description="ServiceControl primary instance"

EXPOSE 33333
COPY --from=build /deploy/Particular.ServiceControl ./app

RUN mkdir -p /var/servicecontrol && chown -R $APP_UID /var/servicecontrol
VOLUME /var/servicecontrol

ENV PersistenceType=RavenDB \
    LogPath=/var/servicecontrol/logs \
    ForwardErrorMessages=false \
    ErrorRetentionPeriod=15

USER $APP_UID
ENTRYPOINT ["./app/ServiceControl"]