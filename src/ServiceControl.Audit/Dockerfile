# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl.Audit/ServiceControl.Audit.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH
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
LABEL org.opencontainers.image.url=https://hub.docker.com/r/particular/servicecontrol-audit
LABEL org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/
LABEL org.opencontainers.image.version=$VERSION
LABEL org.opencontainers.image.revision=$SHA
LABEL org.opencontainers.image.created=$CREATED
LABEL org.opencontainers.image.title="ServiceControl Audit Instance"
LABEL org.opencontainers.image.description="Provide valuable information about the message flow through a system."
LABEL org.opencontainers.image.base.name=mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-composite-extra

EXPOSE 44444

COPY --from=build /deploy/Particular.ServiceControl.Audit /app
COPY --from=build /healthcheck /healthcheck

ENV PersistenceType=RavenDB \
    AuditRetentionPeriod=7

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:44444/api/configuration"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Audit"]