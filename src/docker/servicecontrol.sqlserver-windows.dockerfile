FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2-windowsservercore-ltsc2016

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.SqlServer/bin/Release/net472 .
ADD /ServiceControl/bin/Release/net472 .

ENV "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

ENV "ServiceControl/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\DB\\"
ENV "ServiceControl/LogPath"="C:\\Data\\Logs\\"

# Defaults
ENV "ServiceControl/ForwardErrorMessages"="False"
ENV "ServiceControl/ErrorRetentionPeriod"="15"

EXPOSE 33333
EXPOSE 33334

ENTRYPOINT ["ServiceControl.exe", "--portable"]