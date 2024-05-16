# Learn about building .NET container images:
# https://github.com/dotnet/dotnet-docker/blob/main/samples/README.md
FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:8.0 AS build
ARG TARGETARCH
WORKDIR /

# Not copying csproj and restore as distinct layers for now, why do we need/want that?
COPY . .
RUN dotnet restore src --arch $TARGETARCH

# copy and publish app and libraries.
RUN dotnet build src/ServiceControl/ServiceControl.csproj --configuration Release -graph --no-restore --arch $TARGETARCH


# final stage/image
FROM mcr.microsoft.com/dotnet/aspnet:8.0
EXPOSE 33333
WORKDIR /
COPY --from=build /deploy/Particular.ServiceControl ./app

ENV "ServiceControl/PersistenceType"="RavenDB" \
    "ServiceControl/Hostname"="*" \
    "ServiceControl/DBPath"="C:\\Data\\DB\\" \
    "ServiceControl/LogPath"="/logs" \
    "ServiceControl/ForwardErrorMessages"="False" \
    "ServiceControl/ErrorRetentionPeriod"="15" \
    "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

USER $APP_UID
ENTRYPOINT ["./app/ServiceControl"]