# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH
RUN dotnet build src/HealthCheckApp/HealthCheckApp.csproj --configuration Release --property:TargetArch=$TARGETARCH


# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-noble-chiseled-composite
ARG VERSION
WORKDIR /

LABEL org.opencontainers.image.source=https://github.com/Particular/ServiceControl \
      org.opencontainers.image.authors="Particular Software" \
      org.opencontainers.image.url=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.documentation=https://docs.particular.net/servicecontrol/ \
      org.opencontainers.image.version=$VERSION \
      org.opencontainers.image.title=ServiceControl.Monitoring \
      org.opencontainers.image.description="ServiceControl monitoring instance"

EXPOSE 33633

COPY --from=build /deploy/Particular.ServiceControl.Monitoring ./app
COPY --from=build /deploy/healthcheck ./healthcheck

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:33633/connection"]

USER $APP_UID
ENTRYPOINT ["./app/ServiceControl.Monitoring"]