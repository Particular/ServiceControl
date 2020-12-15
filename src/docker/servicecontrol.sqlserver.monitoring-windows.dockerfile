FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.SqlServer/bin/Release/net462 .
ADD /ServiceControl.Monitoring/bin/Release/net462 .

ENV "Monitoring/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

ENV "Monitoring/LogPath"="C:\\Data\\Logs\\"

EXPOSE 33633

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable"]