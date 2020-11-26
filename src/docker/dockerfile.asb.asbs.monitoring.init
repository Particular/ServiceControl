FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol.monitoring

ADD /ServiceControl.Transports.ASBS/bin/Release/net462 .
ADD /ServiceControl.Monitoring/bin/Release/net462 .

ENV "Monitoring/TransportType"="ServiceControl.Transports.ASBS.ASBSTransportCustomization, ServiceControl.Transports.ASBS"
ENV "Monitoring/HttpHostName"="*"

ENTRYPOINT ["ServiceControl.Monitoring.exe", "--portable", "--setup"]