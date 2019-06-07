FROM mcr.microsoft.com/dotnet/framework/runtime:4.8

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.SqlServer/bin/Release/net461 .
ADD /ServiceControl/bin/Release/net461 .
ADD runcontainer.ps1 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

EXPOSE 33333

SHELL ["powershell"]

ENTRYPOINT ["powershell", "./runcontainer.ps1"]