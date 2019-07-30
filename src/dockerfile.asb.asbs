FROM mcr.microsoft.com/windows/servercore:1803

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.ASBS/bin/Release/net461 .
ADD /ServiceControl/bin/Release/net461 .
ADD runcontainer.ps1 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

EXPOSE 33333

SHELL ["powershell"]

ENTRYPOINT ["powershell", "./runcontainer.ps1"]