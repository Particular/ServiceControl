FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.SqlServer/bin/Release/net462 .
ADD /ServiceControl.Monitoring/bin/Release/net462 .

ENV "SERVICECONTROL_NO_TRIAL"="true"

ENV "Monitoring/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable", "--setup"]