# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /

# Not copying csproj and restore as distinct layers for now, why do we need/want that?
COPY . .
RUN dotnet restore src --arch $TARGETARCH

# copy and publish app and libraries.
RUN dotnet build src/ServiceControl.Monitoring/ServiceControl.Monitoring.csproj --configuration Release -graph --no-restore --arch $TARGETARCH


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 33633
WORKDIR /
COPY --from=build /deploy/Particular.ServiceControl.Monitoring ./app

ENV MONITORING_TRANSPORTTYPE= \
    Monitoring_HttpHostName=* \
    Monitoring_HttpPort=33633 \
    Monitoring_LogPath=/logs \
    SERVICECONTROL_RUNNING_IN_DOCKER=true

USER $APP_UID
ENTRYPOINT ["./app/ServiceControl.Monitoring"]