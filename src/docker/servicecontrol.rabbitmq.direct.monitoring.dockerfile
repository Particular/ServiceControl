FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.RabbitMQ/bin/Release/net472 .
ADD /ServiceControl.Monitoring/bin/Release/net472 .

ENV "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

ENV "Monitoring/TransportType"="ServiceControl.Transports.RabbitMQ.RabbitMQDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

ENV "Monitoring/LogPath"="C:\\Data\\Logs\\"

EXPOSE 33633

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable"]