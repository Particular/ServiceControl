FROM mcr.microsoft.com/windows/servercore:1803

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.RabbitMQ/bin/Release/net461 .
ADD /ServiceControl/bin/Release/net461 .
ADD runcontainer.ps1 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

EXPOSE 33333

SHELL ["powershell"]

ENTRYPOINT ["powershell", "./runcontainer.ps1"]