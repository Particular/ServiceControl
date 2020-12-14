FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.SQS/bin/Release/net462 .
ADD /ServiceControl.Monitoring/bin/Release/net462 .

ENV "Monitoring/TransportType"="ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS"
ENV "Monitoring/HttpHostName"="*"
ENV "Monitoring/HttpPort"="33633"

EXPOSE 33633

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable"]