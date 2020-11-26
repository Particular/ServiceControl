FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.RabbitMQ/bin/Release/net462 .
ADD /ServiceControl.Monitoring/bin/Release/net462 .

ENV "Monitoring/TransportType"="ServiceControl.Transports.RabbitMQ.RabbitMQConventionalRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"
ENV "Monitoring/HttpHostName"="*"

EXPOSE 33633

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable"]