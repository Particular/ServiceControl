# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl/ServiceControl.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH
RUN dotnet publish src/HealthCheckApp/HealthCheckApp.csproj --arch $TARGETARCH --output /healthcheck

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble
ARG VERSION
WORKDIR /app

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl \
      org.opencontainers.image.description="ServiceControl primary instance"

EXPOSE 33333

COPY --from=build /deploy/Particular.ServiceControl /app
COPY --from=build /healthcheck /healthcheck

ENV PersistenceType=RavenDB \
    ForwardErrorMessages=false \
    ErrorRetentionPeriod=15

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:33333/api/configuration"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl"]