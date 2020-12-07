FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.SQS/bin/Release/net462 .
ADD /ServiceControl/bin/Release/net462 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.SQS.SQSTransportCustomization, ServiceControl.Transports.SQS"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

# Defaults
ENV "ServiceControl/ForwardErrorMessages"="False"
ENV "ServiceControl/ErrorRetentionPeriod"="15"

ENTRYPOINT ["ServiceControl.exe", "--portable", "--setup"]