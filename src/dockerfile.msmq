FROM mcr.microsoft.com/windows/servercore:1803

WORKDIR /servicecontrol

ADD /ServiceControl.Transports.Msmq/bin/Release/net461 .
ADD /ServiceControl/bin/Release/net461 .
ADD runcontainer.ps1 .

ENV "ServiceControl/TransportType"="ServiceControl.Transports.Msmq.MsmqTransportCustomization, ServiceControl.Transports.Msmq"
ENV "ServiceControl/Hostname"="*"

ENV "ServiceControl/DBPath"="C:\\Data\\"
ENV "ServiceControl/LogPath"="C:\\Logs\\"

EXPOSE 33333

SHELL ["powershell"]

RUN mkdir c:\\MessageStore
RUN mkdir c:\\TransactionLogStore
RUN mkdir c:\\MessageLogStore

RUN powershell -Command Enable-WindowsOptionalFeature -Online -FeatureName MSMQ-Server -All
RUN powershell -Command Set-MsmqQueueManager -MsgStore C:\MessageStore -TransactionLogStore C:\MessageStore -MsgLogStore C:\MessageStore