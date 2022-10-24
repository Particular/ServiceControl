FROM mcr.microsoft.com/dotnet/framework/runtime:4.7.2-windowsservercore-ltsc2016

WORKDIR /servicecontrol.audit

ADD /ServiceControl.Transports.RabbitMQ/bin/Release/net472 .
ADD /ServiceControl.Audit.Persistence.RavenDb/bin/Release/net472 .
ADD /ServiceControl.Audit/bin/Release/net472 .

ENV "SERVICECONTROL_RUNNING_IN_DOCKER"="true"

ENV "ServiceControl.Audit/TransportType"="ServiceControl.Transports.RabbitMQ.RabbitMQQuorumDirectRoutingTransportCustomization, ServiceControl.Transports.RabbitMQ"
ENV "ServiceControl.Audit/PersistenceType"="ServiceControl.Audit.Persistence.RavenDb.RavenDbPersistenceConfiguration, ServiceControl.Audit.Persistence.RavenDb"
ENV "ServiceControl.Audit/Hostname"="*"

ENV "ServiceControl.Audit/DBPath"="C:\\Data\\DB\\"
ENV "ServiceControl.Audit/LogPath"="C:\\Data\\Logs\\"

# Defaults
ENV "ServiceControl.Audit/ForwardAuditMessages"="False"
ENV "ServiceControl.Audit/AuditRetentionPeriod"="365"

EXPOSE 44444
EXPOSE 44445

VOLUME [ "C:/Data" ]

ENTRYPOINT ["ServiceControl.Audit.exe", "--portable"]
