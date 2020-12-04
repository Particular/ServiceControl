FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.SqlServer/bin/Release/net462 .
ADD /ServiceControl/bin/Release/net462 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

# Defaults
ENV "ServiceControl/ForwardErrorMessages"="False"
ENV "ServiceControl/ErrorRetention"="15"

EXPOSE 33333
EXPOSE 33334

ENTRYPOINT ["ServiceControl.exe", "--portable"]