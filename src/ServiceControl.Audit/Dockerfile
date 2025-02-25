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
WORKDIR /app

EXPOSE 44444

COPY --from=build /deploy/Particular.ServiceControl.Audit /app
COPY --from=build /healthcheck /healthcheck

ENV PersistenceType=RavenDB \
    AuditRetentionPeriod=7

HEALTHCHECK --start-period=10s CMD ["/healthcheck/healthcheck", "http://localhost:44444/api/configuration"]

USER $APP_UID
ENTRYPOINT ["/app/ServiceControl.Audit"]