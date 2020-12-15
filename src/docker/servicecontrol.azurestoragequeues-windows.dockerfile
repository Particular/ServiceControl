FROM mcr.microsoft.com/windows/servercore:ltsc2016

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.ASQ/bin/Release/net462 .
ADD /ServiceControl/bin/Release/net462 .

ENV "SERVICECONTROL_NO_TRIAL"="true"

ENV "ServiceControl/TransportType"="ServiceControl.Transports.ASQ.ASQTransportCustomization, ServiceControl.Transports.ASQ"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\DB\\"
ENV "ServiceControl/LogPath"="C:\\Data\\Logs\\"

# Defaults
ENV "ServiceControl/ForwardErrorMessages"="False"
ENV "ServiceControl/ErrorRetentionPeriod"="15"

EXPOSE 33333
EXPOSE 33334

ENTRYPOINT ["ServiceControl.exe", "--portable"]