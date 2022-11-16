FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2-windowsservercore-ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /deploy/Particular.ServiceControl.Monitoring/ServiceControl.Monitoring .
ADD /deploy/Particular.ServiceControl.Monitoring/Transports/AzureStorageQueue .

ENV "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

ENV "Monitoring/TransportType"="ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

ENV "Monitoring/LogPath"="C:\\Data\\Logs\\"

EXPOSE 33633

VOLUME [ "C:/Data" ]

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable"]