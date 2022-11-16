FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2-windowsservercore-ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /deploy/Particular.ServiceControl.Monitoring/ServiceControl.Monitoring .
ADD /deploy/Particular.ServiceControl.Monitoring/Transports/SQLServer .

ENV "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

ENV "Monitoring/TransportType"="ServiceControl.Transports.SqlServer.SqlServerTransportCustomization, ServiceControl.Transports.SqlServer"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

ENV "Monitoring/LogPath"="C:\\Data\\Logs\\"

VOLUME [ "C:/Data" ]

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable", "--setup"]