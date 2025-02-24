# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl/ServiceControl.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH
RUN dotnet publish src/HealthCheckApp/HealthCheckApp.csproj --arch $TARGETARCH --output /healthcheck

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-composite-extra
ARG VERSION
ARG SHA=unknown
ARG CREATED=2000-01-01T00:00:00Z
WORKDIR /app

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl/tree/$SHA
LABEL org.opencontainers.image.authors="Particular Software"
LABEL org.opencontainers.image.vendor="Particular Software"
LABEL org.opencontainers.image.url=https://hub.docker.com/r/particular/servicecontrol
LABEL org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/
LABEL org.opencontainers.image.version=$VERSION
LABEL org.opencontainers.image.revision=$SHA
LABEL org.opencontainers.image.created=$CREATED
LABEL org.opencontainers.image.title="ServiceControl"
LABEL org.opencontainers.image.description="Gather status, performance and monitoring data for multiple endpoints from a single location."
LABEL org.opencontainers.image.base.name=mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-composite-extra


EXPOSE 33333

COPY --from=build /deploy/Particular.ServiceControl /app
COPY --from=build /healthcheck /healthcheck

ENV PersistenceType=RavenDB \
    ForwardErrorMessages=false \
    ErrorRetentionPeriod=15

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:33333/api/configuration"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl"]