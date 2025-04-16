# Build image
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
ARG TARGETARCH
WORKDIR /
ENV CI=true

COPY . .
RUN dotnet build src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj --configuration Release -graph --property:TargetArch=$TARGETARCH
RUN dotnet publish src/HealthCheckApp/HealthCheckApp.csproj --arch $TARGETARCH --output /healthcheck

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-chiseled-composite-extra
WORKDIR /app

EXPOSE 33633

COPY --from=build /deploy/Particular.ServiceControl.Monitoring /app
COPY --from=build /healthcheck /healthcheck

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:33633/connection"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Monitoring"]